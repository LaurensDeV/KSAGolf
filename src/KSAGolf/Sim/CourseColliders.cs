using Brutal.Numerics;

namespace KSAGolf;

/// <summary>A box in a course's own metres: its centre, its three axes, and its size along each.</summary>
internal readonly record struct ColliderBox(double3 Centre, double3 X, double3 Y, double3 Z, double3 Size)
{
    /// <summary>The height of the box's top face over a point of the course.</summary>
    public double TopAt(double2 p)
    {
        double3 top = Centre + (Z * (Size.Z / 2.0));
        return top.Z - (((Z.X * (p.X - top.X)) + (Z.Y * (p.Y - top.Y))) / Z.Z);
    }
}

/// <summary>
/// What a kitten stands on: each piece's slab as one box, its top the felt and its bottom sunk into the ground,
/// tilted along a ramp. Rails are left out, so a kitten steps across the lanes rather than catching on them.
/// </summary>
internal static class CourseColliders
{
    /// <summary>How thick a ramp's tilted slab is, under its felt.</summary>
    public const double RampThicknessM = 0.12;

    public static List<ColliderBox> Of(Course course)
    {
        List<ColliderBox> boxes = [];
        double c = CourseSize.CellM;
        foreach (Placed piece in course.Pieces)
        {
            (int w, int h) = piece.Footprint;
            Port[] ports = [.. piece.Ports()];
            var middle = new double2((piece.Origin.I + (w / 2.0)) * c, (piece.Origin.J + (h / 2.0)) * c);

            if (ports.Length < 2 || ports[0].Level == ports[1].Level)
            {
                double top = piece.Level * CourseSize.LevelM, bottom = -PieceShape.BaseDepthM;
                boxes.Add(new ColliderBox(new double3(middle.X, middle.Y, (top + bottom) / 2.0),
                                          new double3(1, 0, 0), new double3(0, 1, 0), new double3(0, 0, 1),
                                          new double3(w * c, h * c, top - bottom)));
                continue;
            }

            double3 a = Edge(ports[0]), b = Edge(ports[1]);
            double3 along = b - a;
            double3 y = Vec.Unit(along);
            double3 x = Vec.Unit(new double3(-along.Y, along.X, 0.0));
            double3 z = Vec.Cross(x, y);
            if (z.Z < 0.0)
            {
                x = -x;
                z = -z;
            }

            double width = (Math.Abs(x.X) * w * c) + (Math.Abs(x.Y) * h * c);
            double3 centre = ((a + b) / 2.0) - (z * (RampThicknessM / 2.0));
            boxes.Add(new ColliderBox(centre, x, y, z, new double3(width, Vec.Len(along), RampThicknessM)));
        }

        return boxes;
    }

    // A port's edge middle on the course, at its level's height.
    private static double3 Edge(Port port)
    {
        double2 o = port.Side switch { Side.North => new(0, 1), Side.East => new(1, 0), Side.South => new(0, -1), _ => new(-1, 0) };
        double2 p = (new double2(port.Cell.I + 0.5, port.Cell.J + 0.5) + (o * 0.5)) * CourseSize.CellM;
        return new double3(p.X, p.Y, port.Level * CourseSize.LevelM);
    }
}
