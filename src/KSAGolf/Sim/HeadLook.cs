namespace KSAGolf;

/// <summary>
/// Looking around from a kitten's eyes: the captured mouse turns the head (<see cref="Turn"/>), or a
/// right-button drag does while the cursor is free, and it stays where it is put.
///
/// <para>Measured from the world rather than from the kitten: KSA turns a walking kitten to face the way
/// the camera looks, so a look measured from the kitten would chase its own tail.</para>
/// </summary>
public sealed class HeadLook : IMouseDrag
{
    public const double RadiansPerPixel = ChaseOrbit.RadiansPerPixel;
    public static readonly double MaxPitchRad = double.DegreesToRadians(85.0);

    private bool _pressed;
    private bool _haveCursor;
    private double _x, _y, _startX, _startY;

    /// <summary>Round from north towards east, in radians.</summary>
    public double Yaw { get; private set; }

    /// <summary>Above the horizon, in radians.</summary>
    public double Pitch { get; private set; }

    public bool Dragging { get; private set; }

    public void LookTowards(double yaw, double pitch)
    {
        Yaw = yaw;
        Pitch = Math.Clamp(pitch, -MaxPitchRad, MaxPitchRad);
    }

    /// <summary>How far the captured mouse turns the view: close to a shooter's default.</summary>
    public static readonly double CapturedRadiansPerPixel = double.DegreesToRadians(0.08);

    /// <summary>The mouse moved with the cursor captured: right looks right, up looks up.</summary>
    public void Turn(double dx, double dy)
    {
        if (!double.IsFinite(dx) || !double.IsFinite(dy)) return;

        Yaw += dx * CapturedRadiansPerPixel;
        Pitch = Math.Clamp(Pitch - (dy * CapturedRadiansPerPixel), -MaxPitchRad, MaxPitchRad);
    }

    public void Press()
    {
        _pressed = true;
        Dragging = false;
        (_startX, _startY) = (_x, _y);
    }

    public void Release()
    {
        _pressed = false;
        Dragging = false;
    }

    public void Move(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;

        double dx = _haveCursor ? x - _x : 0.0;
        double dy = _haveCursor ? y - _y : 0.0;
        (_x, _y) = (x, y);
        _haveCursor = true;

        double fromX = x - _startX, fromY = y - _startY;
        if (_pressed && !Dragging && (fromX * fromX) + (fromY * fromY) > ChaseOrbit.DragStartsPastSquared) Dragging = true;
        if (!Dragging) return;

        // Drag right to look right, drag up to look up.
        Yaw += dx * RadiansPerPixel;
        Pitch = Math.Clamp(Pitch - (dy * RadiansPerPixel), -MaxPitchRad, MaxPitchRad);
    }

    public void Scroll(double notches)
    {
    }
}
