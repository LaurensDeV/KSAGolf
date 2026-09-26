using System.Reflection;
using Brutal.Numerics;
using KSA;
using RenderCore;
using RenderCore.Animation;

namespace KSAGolf;

/// <summary>
/// Whether a mod can pose a kitten: turns a list of bones on every kitten out on EVA through the engine's
/// own pose-processor list, the one Core's eyes, ears and expressions use. Diagnostic only, driven
/// from the bridge's <c>kitten_pose</c>; see <c>docs/KITTEN-ARMS.md</c>.
/// </summary>
internal static class KittenPoseProbe
{
    // KittenEva.Renderable is public; the avatar inside it is not.
    private static readonly FieldInfo? AvatarField =
        typeof(KittenRenderable).GetField("_characterAvatar", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly List<(AnimatedRenderable Model, Bend Processor)> Attached = [];

    /// <summary>The avatar a kitten is drawn through, or null when the private field has moved.</summary>
    public static CharacterAvatar? AvatarOf(KittenEva kitten) => AvatarField?.GetValue(kitten.Renderable) as CharacterAvatar;

    /// <summary>How a turn varies over time.</summary>
    public enum Shape { Swing, Bounce, Spin }

    /// <summary>
    /// One bone turned about one of its own axes — or, when <see cref="Move"/>, shifted along one in its
    /// parent's frame, by <see cref="Degrees"/> read as model units — on top of whatever KSA animated.
    /// Held, or varied by <see cref="Swing"/> at <see cref="Hz"/>: either side (<see cref="Shape.Swing"/>),
    /// one side only as a hop (<see cref="Shape.Bounce"/>), or round and round (<see cref="Shape.Spin"/>).
    /// Active from <see cref="From"/> to <see cref="To"/> seconds into the routine, eased in and out.
    /// <see cref="Stretch"/> scales the bone along the axis by one plus the amount instead, which carries
    /// its children with it.
    /// </summary>
    public readonly record struct Turn(string Bone, float3 Axis, float Degrees, float Swing = 0f, float Hz = 0f,
                                       float PhaseDeg = 0f, bool Move = false, Shape Shape = Shape.Swing,
                                       float From = 0f, float To = float.PositiveInfinity, bool Stretch = false);

    /// <summary>Attaches or retunes the pose on every kitten in the world, and says what it reached.</summary>
    public static Dictionary<string, object?> Apply(IReadOnlyList<Turn> turns, bool fly, bool? mmu = null,
                                                   bool? helmet = null, float loopSeconds = 0f,
                                                   float rate = 1f, bool fromRest = false)
    {
        int kittens = 0, attached = 0;
        string? flying = null;
        List<string> trouble = [];

        if (AvatarField is null) trouble.Add("KittenRenderable._characterAvatar not found");

        foreach (Vehicle vehicle in KsaWorld.Vehicles)
        {
            if (vehicle is not KittenEva kitten) continue;
            kittens++;

            // A kitten nobody is looking at is never rendered, so its processors are never called.
            if (fly && flying is null && KsaWorld.GoTo(kitten)) flying = KsaWorld.DisplayName(kitten);
            if (AvatarField?.GetValue(kitten.Renderable) is not CharacterAvatar avatar) continue;

            // The MMU's hand controllers ride the forearms and hide the hands.
            if (mmu is { } showMmu) avatar.Attachments.Mmu.Enabled = showMmu;
            if (helmet is { } showHelmet) avatar.Attachments.Helmet.Enabled = showHelmet;

            AnimatedRenderable model = avatar.Core.CharacterModel;
            List<(int Bone, Turn Turn)> resolved = [];
            foreach (Turn turn in turns)
            {
                int index = model.Skeleton.BoneNames?.IndexOf(turn.Bone) ?? -1;
                if (index < 0)
                {
                    trouble.Add($"no bone '{turn.Bone}' on {KsaWorld.DisplayName(vehicle)}");
                    continue;
                }

                resolved.Add((index, turn.Move || turn.Stretch ? turn : turn with { Axis = float3.Normalize(turn.Axis) }));
            }

            Bend? existing = Attached.Find(a => ReferenceEquals(a.Model, model)).Processor;
            Bend processor = existing ?? new Bend();
            processor.Turns = resolved.ToArray();
            processor.LoopSeconds = loopSeconds;
            processor.Rate = rate;
            processor.FromRest = fromRest;
            processor.Restart();
            if (existing is null)
            {
                model.AnimProcessors.Add(processor);
                Attached.Add((model, processor));
            }

            attached++;
        }

        Dictionary<string, object?> report = Report(kittens, attached, trouble);
        report["flying"] = flying;
        return report;
    }

    /// <summary>
    /// Zooms the orbit camera on what it follows. Written to the followed object's own orbit view as the
    /// azimuth is, because the controller springs back towards that every frame.
    /// </summary>
    public static bool Zoom(double power)
    {
        try
        {
            if (!double.IsFinite(power) || Program.GetMainCamera()?.Following?.OrbitView is not { } view) return false;

            view.DistancePower = power;
            return KsaWorld.SetOrbitZoomPower(power);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Takes every bend back off, leaving the kittens as KSA animates them.</summary>
    public static Dictionary<string, object?> Remove()
    {
        int removed = 0;
        foreach ((AnimatedRenderable model, Bend processor) in Attached)
        {
            if (model.AnimProcessors.Remove(processor)) removed++;
        }

        Attached.Clear();
        return new() { ["removed"] = removed };
    }

    /// <summary>What is attached, and whether the engine has been calling it.</summary>
    public static Dictionary<string, object?> Report(int kittens = -1, int attached = -1, List<string>? trouble = null)
    {
        Attached.RemoveAll(a => !a.Model.AnimProcessors.Contains(a.Processor));

        return new()
        {
            ["kittens"] = kittens < 0 ? null : kittens,
            ["attached_now"] = attached < 0 ? null : attached,
            ["processors"] = Attached.Select(a => new Dictionary<string, object?>
            {
                ["turns"] = a.Processor.Turns.Length,
                ["clock"] = a.Processor.Clock,
                ["calls"] = a.Processor.Calls,
                ["should_update"] = a.Processor.ShouldUpdate,
                ["model_updating"] = a.Model.ShouldUpdate,
                ["processors_on_model"] = a.Model.AnimProcessors.Count,
                ["bones"] = a.Processor.Snapshot(),
            }).ToList(),
            ["trouble"] = trouble ?? [],
        };
    }

    private sealed class Bend : IAnimProcessor
    {
        public (int Bone, Turn Turn)[] Turns = [];
        public float LoopSeconds;
        public float Rate = 1f;
        public bool FromRest;
        private TransformTRS[]? _rest;
        public long Calls;
        private double _clock;

        private const double FadeSeconds = 0.25;

        public double Clock => _clock;

        public void Restart() => _clock = 0.0;

        public float Priority => 1f;
        public bool ShouldUpdate { get; set; } = true;
        public bool CanCacheAnimation => true;

        public void UpdateLocalPose(float4x4 transform, Skeleton skeleton, Span<TransformTRS> localPose, float dt)
        {
            // Inside the engine's render: nothing here may throw.
            Calls++;
            _clock += dt * Rate;

            // KSA's own clip is dropped for every bone the pose names, so walking plays the pose's gait
            // rather than the pose added to a two-legged walk.
            if (FromRest)
            {
                // The engine rewrites the local transforms from the pose once processors are done, so
                // borrowing them to read the rest pose once leaves nothing behind.
                if (_rest is null)
                {
                    skeleton.SetDefaultPose();
                    Span<float4x4> rest = skeleton.LocalTransforms;
                    _rest = new TransformTRS[rest.Length];
                    for (int i = 0; i < rest.Length; i++) _rest[i] = TransformTRS.FromMatrix(rest[i]);
                }

                foreach ((int bone, Turn _) in Turns)
                {
                    if ((uint)bone < (uint)localPose.Length && bone < _rest.Length) localPose[bone] = _rest[bone];
                }
            }
            double t = LoopSeconds > 0f ? _clock % LoopSeconds : _clock;

            foreach ((int bone, Turn turn) in Turns)
            {
                if ((uint)bone >= (uint)localPose.Length) continue;

                // A move from the top of the routine starts at full weight, so a paused world still shows it.
                double rise = turn.From > 0f ? (t - turn.From) / FadeSeconds : 1.0;
                double weight = Math.Clamp(Math.Min(rise, (turn.To - t) / FadeSeconds), 0.0, 1.0);

                // Faded, a spin would unwind the whole of it in a quarter second; whole turns join seamlessly.
                if (turn.Shape == Shape.Spin) weight = t >= turn.From && t < turn.To ? 1.0 : 0.0;
                if (weight <= 0.0) continue;

                double phase = (2.0 * Math.PI * turn.Hz * t) + double.DegreesToRadians(turn.PhaseDeg);
                double amount = turn.Shape switch
                {
                    Shape.Bounce => turn.Degrees + (turn.Swing * Math.Abs(Math.Sin(phase))),
                    Shape.Spin => turn.Degrees + (360.0 * ((turn.Hz * (t - turn.From)) % 1.0)),
                    _ => turn.Degrees + (turn.Swing * Math.Sin(phase)),
                };
                amount *= weight;

                if (turn.Stretch)
                {
                    localPose[bone].Scale *= float3.One + (turn.Axis * (float)amount);
                    continue;
                }

                if (turn.Move)
                {
                    localPose[bone].Translation += turn.Axis * (float)amount;
                    continue;
                }

                floatQuat rotation = floatQuat.CreateFromAxisAngle(turn.Axis, float.DegreesToRadians((float)amount));
                localPose[bone].Rotation = floatQuat.Concatenate(rotation, localPose[bone].Rotation);
            }
        }

        // The skeleton's frame is the model's, so the kitten's own up, right and forward are read off
        // these rather than assumed.
        private static readonly string[] Watched =
        [
            "Root_M", "Chest_M", "Head_M", "NoseJoint_M", "Shoulder_R", "Elbow_R", "Wrist_R", "MiddleFinger2_R",
            "Shoulder_L", "Elbow_L", "Wrist_L", "MiddleFinger2_L", "Tail0_M", "Tail07_M", "Hip_R", "Ankle_R",
            "MiddleFinger1_R", "IndexFinger1_R", "IndexFinger2_R", "PinkyFinger1_R", "PinkyFinger2_R",
            "ThumbFinger1_R", "ThumbFinger2_R", "ThumbFinger3_R", "Cup_R",
        ];

        private readonly Dictionary<string, float[]> _seen = [];

        public Dictionary<string, float[]> Snapshot()
        {
            lock (_seen) return new(_seen);
        }

        public void UpdateSkeleton(float4x4 transform, Skeleton skeleton, float dt)
        {
            if (skeleton.BoneNames is not { } names) return;

            Span<float4x4> world = skeleton.WorldTransforms;
            lock (_seen)
            {
                foreach (string name in Watched)
                {
                    int i = names.IndexOf(name);
                    if ((uint)i >= (uint)world.Length) continue;

                    float4x4 m = world[i];
                    _seen[name] = [m.W.X, m.W.Y, m.W.Z, m.X.X, m.X.Y, m.X.Z, m.Y.X, m.Y.Y, m.Y.Z, m.Z.X, m.Z.Y, m.Z.Z];
                }
            }
        }
    }
}
