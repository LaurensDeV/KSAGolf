using Brutal.Numerics;
using KSA;

namespace KSAGolf;

/// <summary>
/// Plays one of the sounds declared in <c>KSAGolfSounds.xml</c> at a point in the world, heard from the
/// audio camera. Anchored on a vehicle nearby so the engine gives it that vehicle's velocity, and with it
/// no false Doppler, and the air pressure at the listener.
/// </summary>
internal static class GolfSounds
{
    private static bool _warned;

    /// <summary>The last sound asked for, and whether the engine gave it a channel to play on.</summary>
    public static string LastPlayed { get; private set; } = "";

    public static void Play((string Id, float Volume) sound, Vehicle near, double3 atEcl)
    {
        try
        {
            if (!KsaWorld.IsAlive(near) || ModLibrary.Get<SoundBehavior>(sound.Id) is not { } behavior) return;

            GameAudio.PlaySound(behavior, new SpatialAudio(near, atEcl - KsaWorld.PositionEcl(near)), out IChannel? channel, sound.Volume);
            LastPlayed = $"{sound.Id} at {sound.Volume:F2}{(channel is null ? ", no channel" : "")}";
        }
        catch (Exception e)
        {
            if (_warned) return;
            _warned = true;
            Log.Warn($"could not play {sound.Id}, so golf is silent this session: {e.Message}");
        }
    }
}
