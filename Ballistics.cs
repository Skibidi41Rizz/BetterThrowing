using System;

namespace ThrowAssist
{
    /// <summary>
    /// Чистая математика броска, без UnityEngine: можно проверять обычным консольным тестом.
    /// Гравитация постоянная, сопротивление воздуха учитывается коэффициентом drag (1/с).
    /// </summary>
    public static class Ballistics
    {
        /// <summary>
        /// Подбирает скорость, при которой тело из точки броска попадёт в цель.
        /// Возвращает false, если нужная скорость больше maxSpeed (цель слишком далеко/высоко).
        /// </summary>
        public static bool Solve(float flatDist, float heightDiff, float naturalHorizontalSpeed,
                                 float gravity, float drag, float maxFlightTime, float maxSpeed,
                                 out float horizontalSpeed, out float verticalSpeed, out float flightTime)
        {
            horizontalSpeed = 0f;
            verticalSpeed = 0f;
            flightTime = 0f;

            if (flatDist < 0.05f || naturalHorizontalSpeed <= 0.01f || gravity <= 0f)
                return false;

            // Держим горизонтальную скорость игрока, но не даём броску лететь слишком долго.
            float t = flatDist / naturalHorizontalSpeed;
            if (t < 0.1f) t = 0.1f;
            if (t > maxFlightTime) t = maxFlightTime;

            flightTime = t;
            if (drag < 0.001f)
            {
                horizontalSpeed = flatDist / t;
                verticalSpeed = heightDiff / t + 0.5f * gravity * t;
            }
            else
            {
                // v' = -drag*v (+ гравитация). x(t) = (v/drag)(1-e^-drag*t).
                double e = Math.Exp(-drag * t);
                double oneMinusE = 1.0 - e;
                if (oneMinusE < 1e-4) return false;

                double k = drag;
                horizontalSpeed = (float)(flatDist * k / oneMinusE);
                verticalSpeed = (float)((k * heightDiff + gravity * t) / oneMinusE - gravity / k);
            }

            float speed = (float)Math.Sqrt(horizontalSpeed * horizontalSpeed + verticalSpeed * verticalSpeed);
            return speed <= maxSpeed;
        }

        /// <summary>
        /// Как Solve, но для острого оружия разгоняет бросок, пока угол подъёма над прямой
        /// до цели не станет не больше maxExcessDeg. Если прямее не выходит — отдаёт самый
        /// плоский вариант в пределах maxSpeed. Гравитация никуда не девается, но дуга
        /// становится почти незаметной.
        /// </summary>
        public static bool SolveStraight(float flatDist, float heightDiff, float minHorizontalSpeed,
                                         float gravity, float drag, float maxFlightTime, float maxSpeed,
                                         float maxExcessDeg,
                                         out float horizontalSpeed, out float verticalSpeed, out float flightTime)
        {
            horizontalSpeed = 0f;
            verticalSpeed = 0f;
            flightTime = 0f;

            if (flatDist < 0.05f || gravity <= 0f) return false;

            float straight = (float)Math.Atan2(heightDiff, flatDist);
            float limit = maxExcessDeg * (float)Math.PI / 180f;
            float speed = Math.Max(minHorizontalSpeed, 0.1f);

            float bestVx = 0f, bestVy = 0f, bestT = 0f;
            bool haveBest = false;

            for (int i = 0; i < 14; i++)
            {
                float vx, vy, t;
                if (!Solve(flatDist, heightDiff, speed, gravity, drag, maxFlightTime, maxSpeed,
                           out vx, out vy, out t))
                    break; // дальше нужная скорость уже выше maxSpeed

                bestVx = vx; bestVy = vy; bestT = t; haveBest = true;

                float excess = (float)Math.Atan2(vy, vx) - straight;
                if (excess <= limit) break;
                speed *= 1.25f;
            }

            if (!haveBest) return false;

            horizontalSpeed = bestVx;
            verticalSpeed = bestVy;
            flightTime = bestT;
            return true;
        }
    }
}
