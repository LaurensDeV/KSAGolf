using Brutal.Numerics;

namespace KSAGolf;

/// <summary>
/// Where the putter and the ball sit around a kitten addressing a putt, in the kitten's model space.
///
/// <para>Model space is the skeleton's: centimetres, +Y up, +Z the way the kitten faces and +X its
/// left, which is where a right-handed putt goes. The club swings as a pendulum about
/// <see cref="Pivot"/>, turning about +Z, so a positive <c>theta</c> carries the head towards the
/// target and a negative one is the backswing.</para>
///
/// <para>The club's own numbers are the ones <c>Meshes/KSAGolf_Putter.glb</c> was built with, at the
/// kitten's own size: its origin at the grip, the shaft down −Z, a head 11 cm toe to heel and 3.2 cm
/// tall whose faces sit 1.8 cm either side of the shaft's plane, and a lie of 18° leaning the shaft
/// towards the heel.
/// The head was modelled in its own frame, sole on z = 0, toe +X and face +Y, and moved into the
/// club file by <see cref="AddressToFile"/>.</para>
/// </summary>
internal sealed class PutterRig
{
    public const double LieDeg = 18.0;
    public const double HoselZ = -34.5;
    public const double HeadHeight = 3.2;
    public const double FaceHalfDepth = 1.8;
    public const double HeadHalfLength = 5.5;

    public const double BallRadiusCm = GolfBall.RadiusM * 100.0;

    /// <summary>How big the club is drawn. It is modelled at the kitten's size.</summary>
    public double Scale = 1.0;

    /// <summary>The ground's height in model space.</summary>
    public double GroundY;

    /// <summary>How far in front of the kitten's origin the ball is addressed.</summary>
    public double AddressZ = 22.0;

    /// <summary>What the club swings about: the middle of the chest. The head hangs under it across the target line.</summary>
    public double3 Pivot = new(0.0, 44.0, 8.0);

    /// <summary>The gap left between the face and the ball at address: room for a hand that is not quite still.</summary>
    public double AddressGap = 1.0;

    private static readonly doubleQuat HeadToFile = doubleQuat.CreateFromAxisAngle(new double3(0, 1, 0), double.DegreesToRadians(LieDeg));

    // The head's frame to the kitten's: toe away from the kitten, face at the target, shaft up --
    // a third of a turn about the diagonal.
    private static readonly doubleQuat HeadToModel = doubleQuat.CreateFromAxisAngle(Vec.Unit(new double3(1, 1, 1)), -2.0 * Math.PI / 3.0);

    private static readonly doubleQuat FileToModel = doubleQuat.Concatenate(doubleQuat.Inverse(HeadToFile), HeadToModel);

    /// <summary>A point of the head's own frame, in the club file.</summary>
    public static double3 AddressToFile(double3 head) => double3.Transform(head - new double3(0, 0, HeadHeight), HeadToFile) + new double3(0, 0, HoselZ);

    /// <summary>Where the head's centre is at address.</summary>
    public double3 HeadAtAddress => new(Pivot.X, GroundY + (Scale * HeadHeight / 2.0), AddressZ);

    /// <summary>Where the ball's centre is addressed.</summary>
    public double3 Ball => new(Pivot.X + (Scale * FaceHalfDepth) + BallRadiusCm + AddressGap, GroundY + BallRadiusCm, AddressZ);

    /// <summary>The head's distance from the pivot across the swing: what turns a swing rate into a head speed.</summary>
    public double SwingRadiusCm
    {
        get
        {
            double3 arm = HeadAtAddress - Pivot;
            return Math.Sqrt((arm.X * arm.X) + (arm.Y * arm.Y));
        }
    }

    /// <summary>
    /// The club's pose at a swing angle: a club-file point <c>p</c> lands at
    /// <c>Transform(p * Scale, rotation) + translation</c> in model space.
    /// </summary>
    public void Pose(double theta, out doubleQuat rotation, out double3 translation)
    {
        doubleQuat swing = Swing(theta);
        double3 headInFile = AddressToFile(new double3(0, 0, HeadHeight / 2.0));
        double3 atAddress = HeadAtAddress - double3.Transform(headInFile * Scale, FileToModel);

        rotation = doubleQuat.Concatenate(FileToModel, swing);
        translation = double3.Transform(atAddress - Pivot, swing) + Pivot;
    }

    /// <summary>A point of the club file, in model space, at a swing angle.</summary>
    public double3 Place(double3 file, double theta)
    {
        Pose(theta, out doubleQuat rotation, out double3 translation);
        return double3.Transform(file * Scale, rotation) + translation;
    }

    /// <summary>Where the grip is: the club file's origin.</summary>
    public double3 Grip(double theta) => Place(Vec.Zero, theta);

    public double3 Head(double theta) => double3.Transform(HeadAtAddress - Pivot, Swing(theta)) + Pivot;

    /// <summary>How far towards the target the striking face has come.</summary>
    public double LeadingFaceX(double theta) => Head(theta).X + (Scale * FaceHalfDepth * Math.Cos(theta));

    /// <summary>The bottom of the head, which has to be below the ball's middle to strike it.</summary>
    public double SoleY(double theta) => Head(theta).Y - (Scale * HeadHeight / 2.0);

    private static doubleQuat Swing(double theta) => doubleQuat.CreateFromAxisAngle(new double3(0, 0, 1), theta);
}
