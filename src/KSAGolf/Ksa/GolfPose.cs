using Brutal.Numerics;
using KSA;
using RenderCore;
using RenderCore.Animation;

namespace KSAGolf;

/// <summary>What the kitten holding the putter is doing: where it is drawn standing, how far into that it is, and the swing.</summary>
/// <param name="Theta">The swing angle while addressing the ball, null while carrying the club.</param>
/// <param name="FirstPerson">Whether the main view looks out through this kitten's eyes.</param>
internal readonly record struct Posture(Stance Stance, double Blend, double? Theta, bool FirstPerson = false);

/// <summary>
/// A kitten holding the putter: carried in the right paw while it walks, and at address drawn standing
/// over the ball, bent at the waist, both paws reaching for the grip while the club swings.
///
/// <para>Everything is aimed in model space, off the pose the engine has just animated, so nothing
/// depends on which way a bone's own axes point. The club is placed from <see cref="UpdateSkeleton"/>,
/// which the engine calls for each view after that view's skeleton is built and before the attachments
/// are drawn: the attachment is solved against that view's own wrist, so the club lands where the rig
/// puts it with no frame of lag.</para>
/// </summary>
internal sealed class GolfPose(KittenEva kitten, CharacterAvatar avatar, StaticMeshRenderable club, PutterRig rig, Func<Posture> posture)
    : IAnimProcessor, IFirstPersonBody
{
    private const float LeanDeg = 24f;

    // Where the left paw closes on the grip, above the right.
    private static readonly double3 LeftHandAbove = new(0, 4.0, 0);

    // The attachment's own turn, applied before the socket's bone by KittenRenderable.
    private static readonly float4x4 SocketTurn = float4x4.CreateRotationZ(-MathF.PI / 2f) * float4x4.CreateRotationX(-MathF.PI / 2f);

    private static readonly string[] Names =
        ["Spine1_M", "Shoulder_R", "Wrist_R", "Shoulder_L", "Wrist_L", "Head_M", "IndexFinger1_R", "PinkyFinger1_R", "MiddleFinger2_R"];

    private int[] _bones = [];
    private float4x4[] _world = [];
    private bool[] _rightPaw = [];

    public KittenEva Kitten => kitten;
    public CharacterAvatar Avatar => avatar;
    public bool ViewmodelApplied { get; private set; }
    public float4x4[]? BodyWorld { get; private set; }

    /// <summary>Where the head was last drawn, in model space, stepped and bent as it is: where a first-person eye goes.</summary>
    public double3? Head { get; private set; }

    public float Priority => 1f;
    public bool ShouldUpdate { get; set; } = true;
    public bool CanCacheAnimation => false;

    public void UpdateLocalPose(float4x4 transform, Skeleton skeleton, Span<TransformTRS> localPose, float dt)
    {
        try
        {
            Posture now = posture();
            if (!Resolve(skeleton) || !(now.Blend > 0.0)) return;

            float4x4 stance = StanceMatrix(now);
            Span<TransformNodeInfo> nodes = skeleton.NodeInfo;
            for (int i = 0; i < Math.Min(nodes.Length, localPose.Length); i++)
            {
                if (nodes[i].ParentIndex < 0) localPose[i] = TransformTRS.FromMatrix(localPose[i].CreateMatrix() * stance);
            }

            if (now.Theta is not { } theta) return;

            World(skeleton, localPose);
            double3 across = double3.Transform(new double3(1, 0, 0), now.Stance.Rotation(now.Blend));
            TurnAbout(skeleton, localPose, _bones[0], floatQuat.CreateFromAxisAngle(float3.Pack(across), float.DegreesToRadians(LeanDeg) * (float)now.Blend));

            double3 grip = now.Stance.Place(rig.Grip(theta), now.Blend);
            Reach(skeleton, localPose, _bones[1], _bones[2], grip);
            Reach(skeleton, localPose, _bones[3], _bones[4], grip + LeftHandAbove);
        }
        catch
        {
            // Inside the engine's render: an unposed kitten is better than a thrown frame.
        }
    }

    public void UpdateSkeleton(float4x4 transform, Skeleton skeleton, float dt)
    {
        ViewmodelApplied = false;
        try
        {
            if (!Resolve(skeleton)) return;

            float3 head = skeleton.WorldTransforms[_bones[5]].Translation;
            Head = new double3(head.X, head.Y, head.Z);

            Posture now = posture();
            float4x4 placed = now.Theta is { } theta && now.Blend > 0.0 ? ClubInModel(rig, now, theta) : InTheFist(skeleton.WorldTransforms);
            if (!float4x4.Invert(SocketTurn * skeleton.WorldTransforms[_bones[2]], out float4x4 fromSocket)) return;
            float4x4 attachment = placed * fromSocket;

            List<CharacterAvatar.CosmeticAttachment> list = avatar.Attachments.CosmeticAttachments;
            int i = list.FindIndex(a => ReferenceEquals(a.Mesh, club));
            if (i < 0) return;

            CharacterAvatar.CosmeticAttachment held = list[i];
            held.Transform = attachment;
            list[i] = held;

            if (now.FirstPerson && FirstPersonHook.DrawingMainViewOf(kitten)) StripForTheEyes(skeleton, keepPaw: now.Theta is null);
        }
        catch
        {
            // As above.
        }
    }

    // What the kitten's own eyes see of it: the paw holding the club while it walks, and nothing at address,
    // where the paws on the grip sit between the eyes and the ball -- Golf draws the club itself there.
    // Every other bone is folded to nothing at the right wrist, so the skin between a paw and its arm has
    // nowhere far to stretch; the helmet and the fur fold with the head and the body they hang from. The
    // whole kitten is kept for the shadow, which FirstPersonHook puts back.
    private void StripForTheEyes(Skeleton skeleton, bool keepPaw)
    {
        Span<float4x4> world = skeleton.WorldTransforms;
        BodyWorld = BodyWorld is { } kept && kept.Length == world.Length ? kept : new float4x4[world.Length];
        world.CopyTo(BodyWorld);

        if (_rightPaw.Length != world.Length) _rightPaw = Below(skeleton, _bones[2]);

        float3 wrist = world[_bones[2]].Translation;
        float4x4 gone = new(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, wrist.X, wrist.Y, wrist.Z, 1f);
        for (int i = 0; i < world.Length; i++)
        {
            if (!(keepPaw && _rightPaw[i])) world[i] = gone;
        }

        ViewmodelApplied = true;
    }

    // A bone and everything hanging from it.
    private static bool[] Below(Skeleton skeleton, int top)
    {
        Span<TransformNodeInfo> nodes = skeleton.NodeInfo;
        bool[] below = new bool[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            for (int b = i, hops = 0; b >= 0 && hops < nodes.Length; b = nodes[b].ParentIndex, hops++)
            {
                if (b != top) continue;
                below[i] = true;
                break;
            }
        }

        return below;
    }

    // The club carried: its grip through the closed paw along the knuckles, index to little finger, so the
    // shaft falls out of the bottom of the fist, and its face turned the way the kitten faces.
    private float4x4 InTheFist(Span<float4x4> world)
    {
        double3 index = Position(world[_bones[6]]), pinky = Position(world[_bones[7]]), middle = Position(world[_bones[8]]);
        double3 fist = (index + pinky + middle) / 3.0;
        double3 down = Vec.Unit(pinky - index);

        double3 face = Vec.Unit(Vec.RejectFrom(new double3(0, 0, 1), down));
        if (!Vec.IsFinite(face) || Vec.Len2(face) < 0.5) face = Vec.Unit(Vec.AnyPerpendicular(down));
        double3 shaftUp = -down;
        double3 toe = Vec.Cross(face, shaftUp);

        float4x4 turn = new((float)toe.X, (float)toe.Y, (float)toe.Z, 0f,
                            (float)face.X, (float)face.Y, (float)face.Z, 0f,
                            (float)shaftUp.X, (float)shaftUp.Y, (float)shaftUp.Z, 0f,
                            0f, 0f, 0f, 1f);
        return float4x4.CreateScale((float)rig.Scale) * turn * float4x4.CreateTranslation(float3.Pack(fist));
    }

    /// <summary>Where the club is at a swing angle, in model space, the kitten stepped up as it is drawn.</summary>
    public static float4x4 ClubInModel(PutterRig rig, Posture now, double theta)
    {
        rig.Pose(theta, out doubleQuat rotation, out double3 translation);
        return float4x4.CreateScale((float)rig.Scale)
               * float4x4.CreateFromQuaternion(floatQuat.Pack(rotation))
               * float4x4.CreateTranslation(float3.Pack(translation))
               * StanceMatrix(now);
    }

    private static float4x4 StanceMatrix(Posture now)
        => float4x4.CreateFromQuaternion(floatQuat.Pack(now.Stance.Rotation(now.Blend)))
           * float4x4.CreateTranslation(float3.Pack(now.Stance.Offset * now.Blend));

    private bool Resolve(Skeleton skeleton)
    {
        if (_bones.Length == Names.Length) return true;
        if (skeleton.BoneNames is not { } names) return false;

        int[] found = Names.Select(n => names.IndexOf(n)).ToArray();
        if (found.Any(i => i < 0)) return false;

        _bones = found;
        return true;
    }

    // The model-space transform of every bone, from the pose as it stands now.
    private void World(Skeleton skeleton, Span<TransformTRS> localPose)
    {
        int count = Math.Min(skeleton.BoneCount, localPose.Length);
        if (_world.Length != count) _world = new float4x4[count];

        Span<TransformNodeInfo> nodes = skeleton.NodeInfo;
        bool[] done = new bool[count];
        for (int i = 0; i < count; i++) Build(i, nodes, localPose, done);
    }

    private void Build(int i, Span<TransformNodeInfo> nodes, Span<TransformTRS> localPose, bool[] done)
    {
        if (done[i]) return;

        int parent = nodes[i].ParentIndex;
        float4x4 local = localPose[i].CreateMatrix();
        if (parent >= 0 && parent < done.Length)
        {
            Build(parent, nodes, localPose, done);
            local *= _world[parent];
        }

        _world[i] = local;
        done[i] = true;
    }

    // Turns one bone about its own position by a model-space rotation, and rebuilds everything below it.
    private void TurnAbout(Skeleton skeleton, Span<TransformTRS> localPose, int bone, floatQuat turn)
    {
        int parent = skeleton.NodeInfo[bone].ParentIndex;
        floatQuat above = parent >= 0 ? Rotation(_world[parent]) : floatQuat.Identity;
        floatQuat wanted = floatQuat.Concatenate(floatQuat.Concatenate(localPose[bone].Rotation, above), turn);

        localPose[bone].Rotation = floatQuat.Normalize(floatQuat.Concatenate(wanted, floatQuat.Inverse(above)));
        World(skeleton, localPose);
    }

    // Turns an arm at the shoulder so the wrist points at a model-space target.
    private void Reach(Skeleton skeleton, Span<TransformTRS> localPose, int shoulder, int wrist, double3 target)
    {
        double3 from = Position(_world[shoulder]);
        double3 hand = Position(_world[wrist]) - from;
        double3 want = target - from;
        if (Vec.Len2(hand) < 1e-6 || Vec.Len2(want) < 1e-6) return;

        TurnAbout(skeleton, localPose, shoulder, floatQuat.Pack(Vec.RotationFromTo(hand, want)));
    }

    private static floatQuat Rotation(float4x4 m) => TransformTRS.FromMatrix(m).Rotation;

    private static double3 Position(float4x4 m) => new(m.Translation.X, m.Translation.Y, m.Translation.Z);
}
