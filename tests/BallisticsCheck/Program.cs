using System;
using ThrowAssist;

static class Program
{
    static int _failed;
    const float G = 9.81f;

    static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "OK   " : "FAIL ") + name);
        if (!ok) _failed++;
    }

    /// <summary>Решаем задачу и проверяем, что по этой скорости тело реально приходит в цель.</summary>
    static bool Hits(float flat, float dy, float naturalSpeed, float g, float drag, float maxT, float maxSpeed,
                     out float tHit, out float missBy)
    {
        tHit = 0f;
        missBy = 0f;
        float vx, vy, t;
        if (!Ballistics.Solve(flat, dy, naturalSpeed, g, drag, maxT, maxSpeed, out vx, out vy, out t)) return false;

        tHit = t;
        float x = 0f, y = 0f;
        const float dt = 0.0002f;
        for (float time = 0f; time < t; time += dt)
        {
            float damp = (float)Math.Exp(-drag * dt);
            x += vx * dt;
            y += vy * dt;
            vx *= damp;
            vy = vy * damp - g * dt;
        }

        float dx = x - flat, dyy = y - dy;
        missBy = (float)Math.Sqrt(dx * dx + dyy * dyy);
        return missBy < 0.01f;
    }

    /// <summary>Прямой бросок: проверяем дугу над прямой до цели и само попадание.</summary>
    static bool HitsStraight(float flat, float dy, float speed, float maxExcess, out float excessDeg, out float missBy)
    {
        excessDeg = 0f;
        missBy = 0f;
        float vx, vy, t;
        if (!Ballistics.SolveStraight(flat, dy, speed, G, 0f, 3f, 30f, maxExcess, out vx, out vy, out t))
            return false;

        excessDeg = (float)((Math.Atan2(vy, vx) - Math.Atan2(dy, flat)) * 180.0 / Math.PI);

        float x = 0f, y = 0f;
        const float dt = 0.0002f;
        for (float time = 0f; time < t; time += dt)
        {
            x += vx * dt;
            y += vy * dt;
            vy -= G * dt;
        }

        float dx = x - flat, dyy = y - dy;
        missBy = (float)Math.Sqrt(dx * dx + dyy * dyy);
        return true;
    }

    static void Main()
    {
        float t, miss, vx, vy, ft;

        Check("ровная цель 10 м: попадание", Hits(10f, 0f, 10f, G, 0f, 3f, 30f, out t, out miss));
        Console.WriteLine($"     время={t:F3} c, промах={miss:F4} м");

        Check("цель выше на 2 м: попадание", Hits(8f, 2f, 7f, G, 0f, 3f, 30f, out t, out miss));
        Check("цель ниже на 3 м: попадание", Hits(6f, -3f, 6f, G, 0f, 3f, 30f, out t, out miss));

        Check("сопротивление 0.6: попадание", Hits(10f, 0f, 8f, G, 0.6f, 3f, 30f, out t, out miss));
        Console.WriteLine($"     время={t:F3} c, промах={miss:F4} м");
        Check("сопротивление 1.0, цель выше: попадание", Hits(9f, 1.5f, 6f, G, 1.0f, 3f, 30f, out t, out miss));

        Check("дальняя цель: время полёта ограничено",
              Ballistics.Solve(30f, 0f, 5f, G, 0f, 3f, 60f, out vx, out vy, out ft) &&
              Math.Abs(ft - 3f) < 0.001 && vx > 5f);

        Check("недостижимо при малом пределе скорости: отказ",
              !Ballistics.Solve(10f, 0f, 10f, G, 0f, 3f, 5f, out vx, out vy, out ft));

        Check("нулевая дистанция: отказ",
              !Ballistics.Solve(0.01f, 0f, 10f, G, 0f, 3f, 30f, out vx, out vy, out ft));

        Check("нулевая скорость броска: отказ",
              !Ballistics.Solve(5f, 0f, 0f, G, 0f, 3f, 30f, out vx, out vy, out ft));

        Ballistics.Solve(5f, 0f, 5f, G, 0f, 3f, 30f, out vx, out vy, out ft);
        Check("вертикаль согласована со временем полёта", Math.Abs(vy - 0.5f * G * ft) < 0.001);

        // Прямой бросок: дуга не больше заданного угла, попадание сохраняется.
        float excess, miss2;
        Check("прямой бросок 10 м: дуга <= 8°, попадание",
              HitsStraight(10f, 0f, 8f, 8f, out excess, out miss2) && excess <= 8.01f && miss2 < 0.01f);
        Console.WriteLine($"     дуга={excess:F2}°, промах={miss2:F4} м");
        Check("прямой бросок 20 м: скорость у предела, попадание",
              HitsStraight(20f, 0f, 8f, 8f, out excess, out miss2) && miss2 < 0.01f);
        Console.WriteLine($"     дуга={excess:F2}°, промах={miss2:F4} м");
        Check("прямой бросок вниз: попадание",
              HitsStraight(8f, -2f, 8f, 8f, out excess, out miss2) && miss2 < 0.01f);

        Console.WriteLine(_failed == 0 ? "Все проверки прошли." : $"Провалено: {_failed}");
        Environment.Exit(_failed == 0 ? 0 : 1);
    }
}
