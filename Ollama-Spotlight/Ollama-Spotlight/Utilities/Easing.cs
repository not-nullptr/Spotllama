using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ironically, llama3:8b wrote this code

namespace Ollama_Spotlight.Utilities
{
    public class BezierCurve
    {
        public double X { get; set; }
        public double Y { get; set; }

        public BezierCurve(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    public class Easing
    {
        public BezierCurve Bezier1 { get; set; }
        public BezierCurve Bezier2 { get; set; }

        public Easing(BezierCurve bezier1, BezierCurve bezier2)
        {
            Bezier1 = bezier1 ?? throw new ArgumentNullException(nameof(bezier1));
            Bezier2 = bezier2 ?? throw new ArgumentNullException(nameof(bezier2));
        }
    }

    public static class EasingFunctions
    {
        public static double Ease(double t, Easing easing)
        {
            if (t < 0 || t > 1)
                throw new ArgumentOutOfRangeException(nameof(t), "t must be between 0 and 1.");

            // Cubic Bezier formula: B(t) = (1 - t)^3 * P0 + 3 * (1 - t)^2 * t * P1 + 3 * (1 - t) * t^2 * P2 + t^3 * P3
            double CalculateBezier(double t, double p0, double p1, double p2, double p3)
            {
                double u = 1 - t;
                double tt = t * t;
                double uu = u * u;
                double uuu = uu * u;
                double ttt = tt * t;

                return (uuu * p0) + (3 * uu * t * p1) + (3 * u * tt * p2) + (ttt * p3);
            }

            // P0 and P3 are always (0, 0) and (1, 1)
            double x = CalculateBezier(t, 0, easing.Bezier1.X, easing.Bezier2.X, 1);
            double y = CalculateBezier(t, 0, easing.Bezier1.Y, easing.Bezier2.Y, 1);

            return y;
        }

        public static double EaseBetween(double startValue, double endValue, double t, Easing easing)
        {
            double easedT = Ease(t, easing);
            return startValue + (endValue - startValue) * easedT;
        }
    }

}
