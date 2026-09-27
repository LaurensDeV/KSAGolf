namespace KSAGolf;

/// <summary>
/// Session-wide settings: what cannot sensibly differ between two things in one world, because there
/// is one screen, one pair of ears and one clock.
///
/// <para>Fields rather than properties, because the panel binds them by <c>ref</c>.</para>
/// </summary>
public sealed class Config
{
    /// <summary>
    /// Show the little floating button that reopens the panel. The menu-bar entry works by appending
    /// to KSA's own bar, which is ImGui behaviour rather than a supported hook, and the control that
    /// would switch this back on lives inside the panel it reopens.
    /// </summary>
    public bool FloatingPanelButton = true;

    /// <summary>Log at debug level. Off by default: a bug report wants it, a session does not.</summary>
    public bool VerboseLog;

    /// <summary>Click a craft to lift it, click the ground to set it down.</summary>
    public bool MoveCraftWithMouse;

    /// <summary>The flown kitten carries a putter, and G steps it up to the ball.</summary>
    public bool PlayGolf = true;

    /// <summary>See through the kitten's eyes while playing golf; the mouse looks around.</summary>
    public bool FirstPerson;
}
