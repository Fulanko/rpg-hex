using Godot;
using System;

namespace RPG.Utils;

public static class Vector3Extensions
{
    private static readonly RandomNumberGenerator rng = new();

    static Vector3Extensions()
    {
        rng.Randomize();
    }

    /// <summary>
    /// Returns a random unit vector pointing in the upper hemisphere (y >= 0).
    /// Ignores the value of the 'this' Vector3; it's just an extension for convenience.
    /// </summary>
    public static Vector3 RandomUpperHemisphere(this Vector3 _)
    {
        double u = rng.Randf();                 // cos(theta) in [0,1]
        double phi = 2.0 * Math.PI * rng.Randf();

        double y = u;
        double r = Math.Sqrt(1.0 - y * y);

        double x = r * Math.Cos(phi);
        double z = r * Math.Sin(phi);

        return new Vector3((float)x, (float)y, (float)z);
    }
}
