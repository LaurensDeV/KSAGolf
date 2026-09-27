namespace KSAGolf;

/// <summary>
/// The player's putting stroke: the mouse moved left and right swings the club, and the face
/// crossing the ball on the way through is the strike.
///
/// <para>On player time, because it is the player's hand: a stroke made while the world is slowed
/// is still made at the speed the mouse moved. The ball it launches runs on simulated time.</para>
/// </summary>
internal sealed class PuttingStroke
{
    /// <summary>How far the club turns per pixel the mouse moves.</summary>
    public const double RadiansPerPixel = 0.0035;

    public const double MaxTheta = 1.3;


    /// <summary>
    /// Ball speed over head speed: (1 + e) M / (M + m) for a rubber face with e ≈ 0.6 and a head
    /// eight times the ball's mass.
    /// </summary>
    public const double Transfer = 1.4;

    // A head faster than this is a mouse jump rather than a stroke.
    private const double MaxRate = 40.0;

    // Mouse movement arrives in bursts, so one frame's rate can be twice or half the hand's.
    private const double RateWindow = 0.06;

    private readonly Queue<(double Dt, double Turn)> _recent = new();

    public double Theta { get; private set; }

    /// <summary>The swing rate last update, radians per second.</summary>
    public double Rate { get; private set; }

    public bool Struck { get; private set; }

    public void Reset()
    {
        Theta = 0.0;
        Rate = 0.0;
        Struck = false;
        _recent.Clear();
    }

    /// <summary>
    /// Moves the club by a mouse movement and says whether that struck the ball, and how fast it
    /// sends it, in metres per second. <paramref name="towardsTarget"/> is +1 when moving the mouse
    /// right carries the head towards the target on screen, −1 when it carries it away.
    /// </summary>
    public bool Update(double dxPixels, double dtPlayer, double towardsTarget, PutterRig rig, out double launchSpeed)
    {
        launchSpeed = 0.0;
        double before = Theta;
        Theta = Math.Clamp(Theta + (dxPixels * RadiansPerPixel * Math.Sign(towardsTarget)), -MaxTheta, MaxTheta);
        Rate = Smoothed(Math.Max(dtPlayer, 0.0), Theta - before);

        if (Struck || Theta <= before) return false;

        double contact = rig.Ball.X - PutterRig.BallRadiusCm;
        if (rig.LeadingFaceX(before) >= contact || rig.LeadingFaceX(Theta) < contact) return false;
        if (rig.SoleY(Theta) > rig.Ball.Y) return false;

        Struck = true;
        launchSpeed = Transfer * Rate * rig.SwingRadiusCm / 100.0;
        return launchSpeed > 0.0;
    }

    private double Smoothed(double dt, double turn)
    {
        _recent.Enqueue((dt, turn));

        double span = 0.0, turned = 0.0;
        foreach ((double d, double t) in _recent)
        {
            span += d;
            turned += t;
        }

        while (_recent.Count > 1 && span - _recent.Peek().Dt >= RateWindow)
        {
            (double d, double t) = _recent.Dequeue();
            span -= d;
            turned -= t;
        }

        return span > 0.0 ? Math.Clamp(turned / span, -MaxRate, MaxRate) : 0.0;
    }
}
