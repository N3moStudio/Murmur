namespace Murmur.Core;

// Port of Thinking Orbs 0.3.2 frameGlobe / searching-20, MIT © 2026 Jakub Antalik.
// Keep these numeric vectors aligned with the pinned upstream engine.
public readonly record struct OrbDot(double X, double Y, double Z, double R, double White, double A);
public static class ThinkingOrb
{
    public static OrbDot[] Frame(double seconds)
    {
        double t = seconds * 2.665, yaw = t * .5, tilt = .4 + .06 * Math.Sin(t * .35);
        double st = Math.Sin(tilt), ct = Math.Cos(tilt), sy = Math.Sin(yaw), cy = Math.Cos(yaw);
        double scan = t * (.5 + 1.2 * 4.335), rs = Math.Pow(20d / 300, .6);
        var dots = new List<OrbDot>(60);
        for (int li = 0; li <= 6; li++)
        {
            double lat = -Math.PI / 2 + li / 6d * Math.PI, cosLat = Math.Cos(lat), sinLat = Math.Sin(lat);
            int count = Math.Max(1, (int)Math.Floor(Math.Abs(cosLat) * 14 + .5));
            for (int lj = 0; lj < count; lj++)
            {
                double lon = lj / (double)count * 2 * Math.PI, x = cosLat * Math.Cos(lon), z = cosLat * Math.Sin(lon);
                double x1 = x * cy + z * sy, z1 = -x * sy + z * cy;
                double y1 = sinLat * ct - z1 * st, depthZ = sinLat * st + z1 * ct, depth = (depthZ + 1) / 2;
                double angle = lon + t * .5 - scan, delta = Math.Atan2(Math.Sin(angle), Math.Cos(angle));
                double boost = Math.Exp(-delta * delta / .18) * Math.Max(0, depthZ);
                dots.Add(new(10 + x1 * 8.2, 10 - y1 * 8.2, depthZ, Math.Max(.3, (1.05 + 2.975 * depth + boost) * rs), .62 - .54 * depth, .45 + .55 * Math.Min(1, boost)));
            }
        }
        return dots.OrderBy(d => d.Z).ToArray();
    }
}
