namespace JungleBooze.Core
{
    /// <summary>
    /// Platform-independent trigonometry for simulation code (spec 004 section 4.3 and T401 section 15). Only
    /// <c>+ - * /</c> are used, so the result is the same on every device and runtime; <c>System.Math.Sin/Cos</c>
    /// must not appear in the swing. Polynomials are the Taylor series in Horner form in <c>x^2</c>, the same ones
    /// as <c>tools/sim/pendulum_model.py</c> (<c>det_sin</c>, <c>det_cos</c>, <c>det_asin</c>).
    /// </summary>
    public static class DeterministicMath
    {
        /// <summary>Largest |x| (rad) for which <see cref="Sin"/> and <see cref="Cos"/> are within their stated error.</summary>
        public const double MaxArgumentRad = 1.3;

        /// <summary>Largest |a| for which <see cref="AsinSmall"/> is within its stated error (sin of 10 degrees plus margin).</summary>
        public const double MaxAsinArgument = 0.2;

        /// <summary>Sine, odd Taylor polynomial to x^11. Max error 5e-9 for |x| &lt;= 1.3 in double.</summary>
        public static double Sin(double x)
        {
            double x2 = x * x;
            double p = -1.0 / 39916800.0;
            p = (p * x2) + (1.0 / 362880.0);
            p = (p * x2) - (1.0 / 5040.0);
            p = (p * x2) + (1.0 / 120.0);
            p = (p * x2) - (1.0 / 6.0);
            p = (p * x2) + 1.0;
            return x * p;
        }

        /// <summary>Cosine, even Taylor polynomial to x^12. Max error 4e-10 for |x| &lt;= 1.3 in double.</summary>
        public static double Cos(double x)
        {
            double x2 = x * x;
            double p = 1.0 / 479001600.0;
            p = (p * x2) - (1.0 / 3628800.0);
            p = (p * x2) + (1.0 / 40320.0);
            p = (p * x2) - (1.0 / 720.0);
            p = (p * x2) + (1.0 / 24.0);
            p = (p * x2) - 0.5;
            p = (p * x2) + 1.0;
            return p;
        }

        /// <summary>
        /// Arcsine for small arguments: <c>a (1 + a^2 (1/6 + a^2 (3/40 + a^2 15/336)))</c>. Max error 4.5e-9 rad for
        /// |a| &lt;= sin(10 degrees); not valid beyond <see cref="MaxAsinArgument"/>.
        /// </summary>
        public static double AsinSmall(double a)
        {
            double a2 = a * a;
            double p = 15.0 / 336.0;
            p = (p * a2) + (3.0 / 40.0);
            p = (p * a2) + (1.0 / 6.0);
            p = (p * a2) + 1.0;
            return a * p;
        }
    }
}
