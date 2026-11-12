using System.Collections.Generic;
using Il2CppActiveRagdoll;
using UnityEngine;

namespace ThrowAssist
{
    /// <summary>Рагдолл, в которого имеет смысл целиться.</summary>
    internal sealed class ThrowTarget
    {
        public StickHealth Health;
        public string Name;
    }

    internal static class TargetScan
    {
        // Манежники и обломки тоже носят StickHealth, а рост у них меньше.
        const float MinHeight = 1.0f;

        /// <summary>Дорогой поиск по сцене: вызывать не чаще раза в пару секунд.</summary>
        public static List<ThrowTarget> Scan()
        {
            var result = new List<ThrowTarget>();
            StickHealth[] all;
            try { all = UnityEngine.Object.FindObjectsOfType<StickHealth>(); }
            catch { return result; }

            foreach (var h in all)
            {
                if (h == null || h.isDead || h.isBeingHeld) continue;

                // Кости есть только у настоящих персонажей, у оружия и стендов их нет.
                if (h.pelvisGo == null && h.upperChest == null && h.jawBone == null) continue;

                Transform root = h.transform.root;
                string rootName = root != null ? root.name : string.Empty;
                if (rootName.Contains("Hexa4Rig") || rootName.Contains("Player")) continue;

                if (Height(h.gameObject) < MinHeight) continue;

                result.Add(new ThrowTarget { Health = h, Name = rootName });
            }
            return result;
        }

        static float Height(GameObject go)
        {
            Collider[] cols;
            try { cols = go.GetComponentsInChildren<Collider>(); }
            catch { return 0f; }

            float min = float.MaxValue, max = float.MinValue;
            foreach (var c in cols)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                if (b.size.sqrMagnitude <= 0f) continue; // коллайдер ещё не просчитан
                if (b.min.y < min) min = b.min.y;
                if (b.max.y > max) max = b.max.y;
            }
            return max > min ? max - min : 0f;
        }
    }
}
