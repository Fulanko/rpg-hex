using Godot;
using System;

namespace RPG.Utils;

public static class Geometry
{
    private static readonly RandomNumberGenerator rng = new();

    static Geometry()
    {
        rng.Randomize();
    }

    public static Vector3 RandomUnitVector()
    {
        double u = 2.0 * GD.Randf() - 1.0;
        double phi = 2.0 * Math.PI * GD.Randf();

        double y = u;
        double r = Math.Sqrt(1.0 - y * y);

        double x = r * Math.Cos(phi);
        double z = r * Math.Sin(phi);

        return new Vector3((float)x, (float)y, (float)z);
    }
}
