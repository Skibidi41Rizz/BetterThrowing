using System;
using System.Collections.Generic;
using Il2CppActiveRagdoll;
using Il2CppHurricaneVR.Framework.Core;
using Il2CppHurricaneVR.Framework.Core.Grabbers;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ThrowAssist.ThrowAssistMod), "Throw Assist", "1.7.0", "mister67")]
[assembly: MelonGame("Kubunautilus", "BrutalistickVR")]

namespace ThrowAssist
{
    /// <summary>
    /// Помощь в бросках: после отпускания подправляет скорость так, чтобы предмет попал
    /// в рагдолла в стороне броска. Один раз, дальше физика игры.
    /// </summary>
    public class ThrowAssistMod : MelonMod
    {
        // Тело после отпускания несколько кадров может оставаться kinematic.
        const float MaxReleaseWait = 0.5f;
        const float AimHeightOffset = 0.9f; // запасной вариант — живот/грудь, но не таз и не ноги
        const float ScanInterval = 2f;
        const float MaxFlightTime = 3f;
        // Сколько не даём брошенному предмету биться о руку/предплечье игрока.
        const float HandIgnoreTime = 0.3f;
        // Сколько кадров держим скорость, если игра перезапишет её после нас.
        const int ReassertFrames = 3;
        // Сколько секунд удерживаем остриё по траектории (длинные броски летят дольше секунды).
        const float TipLockTime = 2.5f;

        sealed class PendingThrow
        {
            public Rigidbody Body;
            public HVRGrabbable Grabbable;
            public HVRGrabberBase Grabber;
            public Vector3 HandPos;
            public Vector3 HandVel;
            public float HandSpeed;
            public float Age;
            public int Frames;
            public bool Applied;
            public Vector3 AppliedVelocity;
            public int Reassert;
            public bool TipDone;
        }

        sealed class IgnorePair
        {
            public Collider A, B;
            public float Left;
        }

        sealed class HandSample
        {
            public Vector3 Pos;
            public Vector3 Velocity;
            public float Time;
        }

        sealed class TipLock
        {
            public Rigidbody Body;
            public Vector3 LocalDir;
            public float Left;
            // Насколько резко доворачивать и какой предел угловой скорости.
            public float Gain;
            public float MaxRate;
        }

        MelonPreferences_Entry<bool> _enabled, _aimAtHead, _ignoreUnconscious, _bladeFirst, _log, _assistRagdolls, _knifeAssist;
        MelonPreferences_Entry<float> _strength, _maxAngle, _maxDistance, _minThrowSpeed, _minAimSpeed, _maxSpeed, _handThrowSpeed, _arc;
        MelonPreferences_Entry<int> _assistLevel;

        readonly List<PendingThrow> _pending = new List<PendingThrow>();
        readonly List<IgnorePair> _ignores = new List<IgnorePair>();
        readonly List<TipLock> _locks = new List<TipLock>();
        readonly Dictionary<int, HVRGrabbable> _hooked = new Dictionary<int, HVRGrabbable>();
        readonly Dictionary<int, HandSample> _hands = new Dictionary<int, HandSample>();
        List<ThrowTarget> _targets = new List<ThrowTarget>();
        Action<HVRGrabberBase, HVRGrabbable> _releasedHandler;
        float _scanTimer;
        bool _errorLogged;

        public override void OnInitializeMelon()
        {
            const string cat = "ThrowAssist";
            _enabled = MelonPreferences.CreateEntry(cat, "Enabled", true,
                "Включить Throw Assist", "Подправлять броски предметов в сторону цели.");
            _assistLevel = MelonPreferences.CreateEntry(cat, "AssistLevel", 2,
                "Уровень ассиста (1..5)", "1=слабый, 2=средний, 3=сильный, 4=максимальный, 5=экстрим, 0=ручные MaxAngle/Strength/MinAimSpeed.");
            _strength = MelonPreferences.CreateEntry(cat, "Strength", 1.0f,
                "Сила помощи (0..1)", "Только для уровня 0. 1 = бросок точно в цель, 0.5 = наполовину.");
            _maxAngle = MelonPreferences.CreateEntry(cat, "MaxAngle", 35f,
                "Угол захвата, градусы", "Только для уровня 0. Цель не дальше этого угла от направления броска.");
            _maxDistance = MelonPreferences.CreateEntry(cat, "MaxDistance", 20f,
                "Максимальная дистанция, м", "Дальше этого цель не выбирается.");
            _minThrowSpeed = MelonPreferences.CreateEntry(cat, "MinThrowSpeed", 3f,
                "Минимальная скорость броска, м/с", "Медленнее тело — но если рука двигалась быстрее, это всё равно бросок.");
            _handThrowSpeed = MelonPreferences.CreateEntry(cat, "HandThrowSpeed", 2.5f,
                "Скорость руки для броска, м/с", "Если тело получило меньше MinThrowSpeed, но рука шла быстрее — считаем броском.");
            _minAimSpeed = MelonPreferences.CreateEntry(cat, "MinAimSpeed", 8f,
                "Минимальная скорость полёта, м/с", "Только для уровня 0. Слабый бросок всё равно полетит с этой скоростью.");
            _maxSpeed = MelonPreferences.CreateEntry(cat, "MaxSpeed", 30f,
                "Предел скорости, м/с", "Если для попадания нужна скорость выше, бросок не меняется.");
            _aimAtHead = MelonPreferences.CreateEntry(cat, "AimAtHead", true,
                "Целиться в голову", "Иначе — в грудь, а если груди нет — в живот. В ноги и руки мод не целится.");
            _bladeFirst = MelonPreferences.CreateEntry(cat, "BladeFirst", true,
                "Остриём по траектории", "Разворачивать и удерживать ножи, копья и прочее острое оружие наконечником вперёд.");
            _arc = MelonPreferences.CreateEntry(cat, "Arc", 8f,
                "Дуга броска, °", "Насколько бросок может подниматься над прямой до цели. Меньше — прямее и быстрее. 0 = максимально прямо.");
            _ignoreUnconscious = MelonPreferences.CreateEntry(cat, "IgnoreUnconscious", false,
                "Не помогать по лежачим", "Игнорировать оглушённых и без сознания.");
            _log = MelonPreferences.CreateEntry(cat, "Log", false,
                "Подробный лог", "Писать в консоль MelonLoader строку на каждый подправленный бросок.");
            _assistRagdolls = MelonPreferences.CreateEntry(cat, "AssistRagdolls", true,
                "Помогать броскам врагов", "Давать импульс брошенным врагам и манекенам. Выключи, чтобы они летели обычной физикой.");
            _knifeAssist = MelonPreferences.CreateEntry(cat, "KnifeAssist", true,
                "Стабилизировать ножи", "Разворачивать ножи и кинжалы остриём вперёд и гасить кувырок в полёте.");

            _releasedHandler = OnReleased;
            LoggerInstance.Msg("Throw Assist 1.7.0 загружен.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _targets.Clear();
            _pending.Clear();
            _locks.Clear();
        }

        public override void OnUpdate()
        {
            // Даже при выключенном моде возвращаем столкновения, иначе предмет навсегда пролетает сквозь руку.
            TickIgnores(Time.deltaTime);
            HoldTips(Time.deltaTime);
            if (!_enabled.Value) return;

            TrackHands();

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = ScanInterval;
                _targets = TargetScan.Scan();
                HookGrabbables();
            }

            ProcessPending(Time.deltaTime);
        }

        void ProcessPending(float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingThrow p = _pending[i];
                p.Age += dt;
                p.Frames++;

                if (p.Body == null) { _pending.RemoveAt(i); continue; }

                if (p.Applied)
                {
                    Reassert(p);
                    if (--p.Reassert <= 0) _pending.RemoveAt(i);
                    continue;
                }

                bool held;
                try { held = p.Grabbable != null && p.Grabbable.IsBeingHeld; } catch { held = false; }
                if (held) { _pending.RemoveAt(i); continue; }
                if (p.Age > MaxReleaseWait) { _pending.RemoveAt(i); continue; }
                if (p.Body.isKinematic) continue; // рука ещё держит тело
                if (p.Frames < 2) continue;      // даём игре выставить собственный бросок

                bool done;
                Vector3 applied;
                try { done = TryAssist(p, out applied); }
                catch (Exception e) { LogErrorOnce(e); done = true; applied = default; }
                if (!done)
                {
                    // Цели нет — но разворот остриём всё равно нужен: иначе нож летит кувырком.
                    if (!p.TipDone) p.TipDone = AlignTipFirst(p, p.Body.velocity);
                    continue;
                }

                p.Applied = true;
                p.AppliedVelocity = applied;
                p.Reassert = ReassertFrames;
            }
        }

        /// <summary>Игра (HVR) иногда пишет скорость на кадр позже нас — держим её пару кадров.</summary>
        void Reassert(PendingThrow p)
        {
            if (p.Body == null || p.Body.isKinematic) return;
            Vector3 now;
            try { now = p.Body.velocity; } catch { return; }
            bool lost = now.sqrMagnitude < p.AppliedVelocity.sqrMagnitude * 0.64f;
            if (!lost && now.sqrMagnitude > 0.01f && p.AppliedVelocity.sqrMagnitude > 0.01f)
                lost = Vector3.Angle(now, p.AppliedVelocity) > 12f;
            if (!lost) return;
            try { p.Body.velocity = p.AppliedVelocity; } catch { }
        }

        void TickIgnores(float dt)
        {
            for (int i = _ignores.Count - 1; i >= 0; i--)
            {
                IgnorePair pair = _ignores[i];
                pair.Left -= dt;
                if (pair.Left > 0f) continue;

                try
                {
                    if (pair.A != null && pair.B != null) Physics.IgnoreCollision(pair.A, pair.B, false);
                }
                catch { }
                _ignores.RemoveAt(i);
            }
        }

        /// <summary>Скорость кисти по кадрам: в обычном времени тело получает слабый бросок, а рука — реальный.</summary>
        void TrackHands()
        {
            float now = Time.time;
            List<int> dead = null;

            foreach (var kv in _hooked)
            {
                HVRGrabbable g = kv.Value;
                bool alive;
                try { alive = g != null; } catch { alive = false; }
                if (!alive) { (dead ?? (dead = new List<int>())).Add(kv.Key); continue; }

                bool held;
                try { held = g.IsBeingHeld; } catch { continue; }
                if (!held) continue;

                HVRGrabberBase grabber;
                try { grabber = g.PrimaryGrabber; } catch { continue; }
                if (grabber == null) continue;

                Vector3 pos = HandPosition(grabber);
                if (_hands.TryGetValue(kv.Key, out var s))
                {
                    float dt = now - s.Time;
                    if (dt > 0.0001f) s.Velocity = (pos - s.Pos) / dt;
                }
                else
                {
                    s = new HandSample();
                    _hands[kv.Key] = s;
                }
                s.Pos = pos;
                s.Time = now;
            }

            if (dead == null) return;
            foreach (int id in dead) { _hooked.Remove(id); _hands.Remove(id); }
        }

        /// <summary>Удерживает остриё острого оружия по направлению полёта, пока оно летит.</summary>
        void HoldTips(float dt)
        {
            for (int i = _locks.Count - 1; i >= 0; i--)
            {
                TipLock l = _locks[i];
                l.Left -= dt;
                if (l.Body == null || l.Left <= 0f) { _locks.RemoveAt(i); continue; }

                Vector3 v;
                bool kinematic;
                try { v = l.Body.velocity; kinematic = l.Body.isKinematic; }
                catch { _locks.RemoveAt(i); continue; }
                if (kinematic || v.sqrMagnitude < 4f) { _locks.RemoveAt(i); continue; }

                Vector3 want = v.normalized;
                Vector3 tip = l.Body.transform.TransformDirection(l.LocalDir);
                float angle = Vector3.Angle(tip, want);
                Vector3 axis = Vector3.Cross(tip, want);
                if (axis.sqrMagnitude < 0.000001f)
                {
                    try { l.Body.angularVelocity = Vector3.Project(l.Body.angularVelocity, want); } catch { }
                    continue;
                }
                axis.Normalize();

                float rate = Mathf.Clamp(angle * l.Gain * Mathf.Deg2Rad, -l.MaxRate, l.MaxRate);
                try { l.Body.angularVelocity = axis * rate; } catch { }
            }
        }

        void HookGrabbables()
        {
            HVRGrabbable[] all;
            try { all = UnityEngine.Object.FindObjectsOfType<HVRGrabbable>(); }
            catch { return; }

            foreach (var g in all)
            {
                if (g == null) continue;
                int id = g.GetInstanceID();
                // Не вешаем второй слушатель на объект, переживший смену сцены.
                if (_hooked.TryGetValue(id, out var known) && known != null) continue;
                try
                {
                    g.Released.AddListener(_releasedHandler);
                    _hooked[id] = g;
                }
                catch { /* объект уже уничтожен в этом кадре — попробуем в следующий раз */ }
            }
        }

        void OnReleased(HVRGrabberBase grabber, HVRGrabbable grabbable)
        {
            if (grabbable == null) return;

            Rigidbody rb = null;
            try { rb = grabbable.Rigidbody; } catch { }
            if (rb == null)
            {
                try { rb = grabbable.GetComponentInChildren<Rigidbody>(); } catch { return; }
            }
            if (rb == null) return;

            // Две руки отпускают один предмет — assist нужен один.
            foreach (var q in _pending)
                if (q.Body == rb) return;

            Vector3 handPos = HandPosition(grabber);
            IgnoreHandCollisions(grabbable, grabber, handPos);

            // Тело врага — не предмет: импульс только если это разрешено.
            if (!_assistRagdolls.Value && rb.GetComponentInParent<Il2CppActiveRagdoll.StickHealth>() != null) return;

            Vector3 handVel = default;
            float handSpeed = 0f;
            int id = grabbable.GetInstanceID();
            if (_hands.TryGetValue(id, out var sample))
            {
                handVel = sample.Velocity;
                handSpeed = handVel.magnitude;
            }

            _pending.Add(new PendingThrow
            {
                Body = rb, Grabbable = grabbable, Grabber = grabber, HandPos = handPos,
                HandVel = handVel, HandSpeed = handSpeed
            });
        }

        /// <summary>True — бросок подправлен, ждать больше нечего.</summary>
        bool TryAssist(PendingThrow p, out Vector3 applied)
        {
            applied = default;
            Rigidbody rb = p.Body;
            Vector3 velocity = rb.velocity;
            float speed = velocity.magnitude;

            // В обычном времени HVR отдаёт телу слабую скорость, хотя рука шла нормально.
            Vector3 throwVelocity = velocity;
            if (speed < _minThrowSpeed.Value)
            {
                if (p.HandSpeed < _handThrowSpeed.Value) return false;
                throwVelocity = p.HandVel;
            }
            if (throwVelocity.sqrMagnitude < 0.25f) return false;

            float angleDeg, strength, minAimSpeed;
            AssistPreset(out angleDeg, out strength, out minAimSpeed);

            // Оружие не целится в руки/ноги: голова (по настройке) или корпус.
            bool head = _aimAtHead.Value;

            Vector3 origin = rb.position;
            ThrowTarget best = null;
            Vector3 bestAim = default;
            float bestAngle = angleDeg;
            float bestDistance = 0f;

            foreach (var t in _targets)
            {
                if (t.Health == null || (_ignoreUnconscious.Value && t.Health.isUnconscious)) continue;

                Vector3 aim;
                if (!AimPoint(t.Health, head, out aim)) continue;

                Vector3 to = aim - origin;
                float distance = to.magnitude;
                if (distance < 1f || distance > _maxDistance.Value) continue;

                float angle = Vector3.Angle(throwVelocity, to);
                if (angle >= bestAngle) continue;

                best = t;
                bestAim = aim;
                bestAngle = angle;
                bestDistance = distance;
            }

            if (best == null) return false;

            Vector3 solved;
            float flightTime;
            float drag = DragOf(rb);
            if (!SolveAim(origin, bestAim, minAimSpeed, drag, out solved, out flightTime)) return false;

            // Упреждение: цель может идти. Одной поправки достаточно.
            Rigidbody targetBody = best.Health.pelvisRb;
            if (targetBody != null)
            {
                Vector3 lead = targetBody.velocity * flightTime;
                if (lead.sqrMagnitude > 0.0001f &&
                    !SolveAim(origin, bestAim + lead, minAimSpeed, drag, out solved, out flightTime))
                    return false;
            }

            applied = Vector3.Lerp(velocity, solved, Mathf.Clamp01(strength));
            rb.velocity = applied;
            if (AlignTipFirst(p, applied)) p.TipDone = true;

            if (_log.Value)
                LoggerInstance.Msg($"[Throw] ур.{_assistLevel.Value} {best.Name}: d={bestDistance:F1} м, угол={bestAngle:F1}°, " +
                                   $"скорость {speed:F1} -> {applied.magnitude:F1} м/с, рука {p.HandSpeed:F1}, " +
                                   $"timeScale={Time.timeScale:F2}");
            return true;
        }

        /// <summary>Уровень ассиста -> угол захвата, сила, минимальная скорость полёта.</summary>
        void AssistPreset(out float angle, out float strength, out float minSpeed)
        {
            switch (Mathf.Clamp(_assistLevel.Value, 0, 5))
            {
                case 1: angle = 25f; strength = 0.75f; minSpeed = 10f; return;
                case 2: angle = 35f; strength = 1.0f; minSpeed = 13f; return;
                case 3: angle = 50f; strength = 1.0f; minSpeed = 17f; return;
                case 4: angle = 70f; strength = 1.0f; minSpeed = 22f; return;
                case 5: angle = 85f; strength = 1.0f; minSpeed = 27f; return;
                default: angle = _maxAngle.Value; strength = _strength.Value; minSpeed = _minAimSpeed.Value; return;
            }
        }

        /// <summary>
        /// Разворачивает наконечник по траектории и запоминает ось наконечника
        /// в локальных координатах, чтобы удерживать её в полёте.
        /// </summary>
        bool AlignTipFirst(PendingThrow p, Vector3 velocity)
        {
            if (!_bladeFirst.Value || p.Grabbable == null || velocity.sqrMagnitude < 0.01f) return false;

            try
            {
                Vector3 tipDir;
                bool blade;
                if (!FindTip(p.Grabbable, p.Body.transform, p.HandPos, out tipDir, out blade)) return false;
                if (blade && !_knifeAssist.Value) return false;
                if (tipDir.sqrMagnitude < 0.0001f) return false;

                Vector3 flightDir = velocity.normalized;
                Rigidbody rb = p.Body;
                rb.rotation = Quaternion.FromToRotation(tipDir.normalized, flightDir) * rb.rotation;
                // Любой кувырок уводит остриё с траектории; оставляем только вращение вокруг неё.
                rb.angularVelocity = Vector3.Project(rb.angularVelocity, flightDir);

                _locks.RemoveAll(l => l.Body == rb);
                _locks.Add(new TipLock
                {
                    Body = rb,
                    // Ось «рукоять -> наконечник» после разворота, в локальных координатах тела.
                    LocalDir = rb.transform.InverseTransformDirection(tipDir),
                    Left = TipLockTime,
                    // Нож меньше и вертлявее копья: доводим резче и сильнее гасим вращение.
                    Gain = blade ? 12f : 8f,
                    MaxRate = blade ? 30f : 12f
                });
                if (_log.Value)
                    LoggerInstance.Msg($"[Tip] {p.Body.name}: {(blade ? "нож" : "древко")}");
                return true;
            }
            catch (Exception e) { LogErrorOnce(e); return false; }
        }

        /// <summary>
        /// Ищет наконечник. Сначала острые коллайдеры оружия, иначе дальний конец
        /// вытянутого предмета — копья-пропы без MeleeWeaponWrapper иначе кувыркаются.
        /// </summary>
        bool FindTip(HVRGrabbable g, Transform body, Vector3 handPos, out Vector3 tipDir, out bool blade)
        {
            tipDir = Vector3.zero;
            blade = false;
            Il2Cpp.MeleeWeaponWrapper wrapper = null;
            try { wrapper = g.GetComponentInChildren<Il2Cpp.MeleeWeaponWrapper>(); } catch { }

            if (wrapper != null && wrapper.meleeWeaponSO != null &&
                wrapper.meleeWeaponSO.meleeWeaponType == Il2Cpp.MeleeWeaponType.Sharp)
            {
                Collider[] sharp = SharpColliders(wrapper, g);
                Vector3 far = FarthestPoint(sharp, handPos);
                tipDir = far - handPos;
                if (tipDir.sqrMagnitude > 0.0001f) return true;
            }

            Collider[] all = CollectColliders(g);
            if (Elongated(all, body, handPos, out Vector3 tip, out blade))
            {
                tipDir = tip - handPos;
                return tipDir.sqrMagnitude > 0.0001f;
            }

            // Геометрия не распознала (широкая лопата, вилы, нестандартный коллайдер) —
            // для названных предметов берём самую дальнюю от руки точку.
            string n = (g.name + " " + body.name).ToLowerInvariant();
            if (n.Contains("knife") || n.Contains("dagger") || n.Contains("spear") ||
                n.Contains("naginata") || n.Contains("shovel") || n.Contains("pitchfork") ||
                n.Contains("fork") || n.Contains("нож") || n.Contains("копь") || n.Contains("вил"))
            {
                blade = n.Contains("knife") || n.Contains("dagger") || n.Contains("нож");
                Vector3 far = FarthestPoint(all, handPos);
                tipDir = far - handPos;
                return tipDir.sqrMagnitude > 0.0001f;
            }
            return false;
        }

        /// <summary>
        /// Вытянутый предмет: длинный (>= 0.6 м и >= 4 толщин) — копьё/шест,
        /// короткий (0.12..0.6 м и >= 2.2 толщин) — нож, кинжал, тесак.
        /// Наконечник — дальний от руки конец длинной оси.
        /// </summary>
        static bool Elongated(Collider[] colliders, Transform t, Vector3 handPos, out Vector3 tipWorld, out bool blade)
        {
            tipWorld = Vector3.zero;
            blade = false;
            if (colliders == null || colliders.Length == 0 || t == null) return false;

            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var c in colliders)
            {
                if (c == null) continue;
                Bounds b;
                try { b = c.bounds; } catch { continue; }
                Vector3 e = b.extents, ctr = b.center;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 w = ctr + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);
                    Vector3 l = t.InverseTransformPoint(w);
                    if (!any) { min = l; max = l; any = true; }
                    else { min = Vector3.Min(min, l); max = Vector3.Max(max, l); }
                }
            }
            if (!any) return false;

            Vector3 size = max - min;
            int k = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
            int a = (k + 1) % 3, b2 = (k + 2) % 3;
            float len = size[k];
            float thick = Mathf.Max(size[a], size[b2]);
            if (thick <= 0.0001f || len < 0.12f) return false;

            blade = len < 0.6f;
            // Нож: вытянутый, но заметно короче копья. Древко: длинное и вытянутое;
            // порог 3 (а не 4) — чтобы лопата и вилы с широкой рабочей частью тоже считались.
            if (len / thick < (blade ? 2.2f : 3f)) return false;
            if (!blade && len < 0.8f) return false;

            Vector3 handLocal = t.InverseTransformPoint(handPos);
            float lo = min[k], hi = max[k];
            float tipK = Mathf.Abs(hi - handLocal[k]) >= Mathf.Abs(handLocal[k] - lo) ? hi : lo;
            Vector3 tipLocal = (min + max) * 0.5f;
            tipLocal[k] = tipK;
            tipWorld = t.TransformPoint(tipLocal);
            return true;
        }

        static Collider[] SharpColliders(Il2Cpp.MeleeWeaponWrapper wrapper, HVRGrabbable g)
        {
            try
            {
                var c = FromColliders(wrapper.stabColliders);
                if (c.Length > 0) return c;
            }
            catch { }
            try
            {
                var c = FromObjects(wrapper.bladeColliders);
                if (c.Length > 0) return c;
            }
            catch { }
            return CollectColliders(g);
        }

        /// <summary>bladeColliders/stabColliders — массив GameObject'ов, коллайдер висит на них.</summary>
        static Collider[] FromObjects(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameObject> objs)
        {
            if (objs == null) return new Collider[0];
            var list = new List<Collider>();
            for (int i = 0; i < objs.Length; i++)
            {
                GameObject o = objs[i];
                if (o == null) continue;
                Collider c = o.GetComponent<Collider>();
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        static Collider[] FromColliders(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> objs)
        {
            if (objs == null) return new Collider[0];
            var list = new List<Collider>();
            for (int i = 0; i < objs.Length; i++)
            {
                Collider c = objs[i];
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }

        void IgnoreHandCollisions(HVRGrabbable g, HVRGrabberBase grabber, Vector3 handPos)
        {
            if (grabber == null) return;
            try
            {
                Collider[] mine = CollectColliders(g);
                if (mine.Length == 0) return;

                Transform root = grabber.transform.root;
                string rootName = root != null ? root.name : string.Empty;
                bool playerRoot = rootName.Contains("Hexa4Rig") || rootName.Contains("Player");
                Transform handRoot = playerRoot ? root : grabber.transform;
                Collider[] around = handRoot.GetComponentsInChildren<Collider>();

                foreach (var a in mine)
                {
                    if (a == null) continue;
                    foreach (var b in around)
                    {
                        if (b == null || b == a) continue;
                        if (b.bounds.SqrDistance(handPos) > 0.04f) continue; // ближе 20 см к кисти
                        Physics.IgnoreCollision(a, b, true);
                        _ignores.Add(new IgnorePair { A = a, B = b, Left = HandIgnoreTime });
                    }
                }
            }
            catch (Exception e) { LogErrorOnce(e); }
        }

        static Collider[] CollectColliders(HVRGrabbable g)
        {
            try
            {
                var list = g.Colliders;
                if (list != null && list.Count > 0)
                {
                    var arr = new Collider[list.Count];
                    for (int i = 0; i < arr.Length; i++) arr[i] = list[i];
                    return arr;
                }
            }
            catch { }

            try { return g.GetComponentsInChildren<Collider>(); }
            catch { return new Collider[0]; }
        }

        static Vector3 HandPosition(HVRGrabberBase grabber)
        {
            if (grabber == null) return Vector3.zero;
            try
            {
                if (grabber is HVRHandGrabber hand) return hand.HandWorldPosition;
            }
            catch { }
            try { return grabber.transform.position; } catch { return Vector3.zero; }
        }

        /// <summary>Самая дальняя от руки точка коллайдеров — это остриё/голова оружия.</summary>
        static Vector3 FarthestPoint(Collider[] colliders, Vector3 from)
        {
            Vector3 best = from;
            float bestSq = 0f;
            foreach (var c in colliders)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    float d = (corner - from).sqrMagnitude;
                    if (d > bestSq) { bestSq = d; best = corner; }
                }
            }
            return best;
        }

        bool SolveAim(Vector3 origin, Vector3 aim, float horizontalSpeed, float drag,
                      out Vector3 velocity, out float flightTime)
        {
            velocity = default;
            Vector3 to = aim - origin;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float flatDist = flat.magnitude;

            float vx, vy;
            if (!Ballistics.SolveStraight(flatDist, to.y, horizontalSpeed, Mathf.Abs(Physics.gravity.y),
                                          drag, MaxFlightTime, _maxSpeed.Value, _arc.Value,
                                          out vx, out vy, out flightTime))
                return false;

            Vector3 dir = flatDist > 0.001f ? flat / flatDist : new Vector3(0f, 0f, 1f);
            velocity = dir * vx + Vector3.up * vy;
            return true;
        }

        /// <summary>Сопротивление среды тела (Unity drag), но в разумных пределах.</summary>
        static float DragOf(Rigidbody rb)
        {
            try { return Mathf.Clamp(rb.drag, 0f, 1.5f); } catch { return 0f; }
        }

        bool AimPoint(StickHealth h, bool head, out Vector3 aim)
        {
            if (head && h.jawBone != null) { aim = h.jawBone.transform.position; return true; }
            if (h.upperChest != null) { aim = h.upperChest.transform.position; return true; }
            if (h.pelvisGo != null) { aim = h.pelvisGo.transform.position + Vector3.up * AimHeightOffset; return true; }
            aim = default;
            return false;
        }

        void LogErrorOnce(Exception e)
        {
            if (_errorLogged) return;
            _errorLogged = true;
            LoggerInstance.Error("Throw Assist: ошибка при обработке броска (дальше молчу): " + e);
        }
    }
}
