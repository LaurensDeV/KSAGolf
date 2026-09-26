using Brutal.Numerics;
using KSA;

namespace KSAGolf;

/// <summary>
/// Whether a mod can put something in a kitten's hand: a mesh added to the avatar's cosmetic
/// attachments at runtime, socketed to a bone, and moved by writing the attachment's own transform.
/// Diagnostic only, driven from the bridge's <c>kitten_prop</c>; see <c>docs/KITTEN-ARMS.md</c>.
/// </summary>
internal static class KittenPropProbe
{
    private static readonly List<(CharacterAvatar Avatar, StaticMeshRenderable Mesh, string Kind)> Props = [];

    /// <summary>
    /// Adds the prop to every kitten on EVA, or moves the one already there. The transform is in the
    /// attachment's own centimetre space, applied before the bone: scale, then turns about x, y and z,
    /// then the offset.
    /// </summary>
    public static Dictionary<string, object?> Apply(string kind, string bone, float scale, float3 turnDeg, float3 offset,
                                                   float[]? matrix = null)
    {
        List<string> trouble = [];
        List<Dictionary<string, object?>> props = [];

        float4x4 local = float4x4.CreateScale(scale)
                         * float4x4.CreateRotationX(float.DegreesToRadians(turnDeg.X))
                         * float4x4.CreateRotationY(float.DegreesToRadians(turnDeg.Y))
                         * float4x4.CreateRotationZ(float.DegreesToRadians(turnDeg.Z))
                         * float4x4.CreateTranslation(offset);
        if (matrix is { Length: 16 }) local = Rows(matrix);

        foreach (Vehicle vehicle in KsaWorld.Vehicles)
        {
            if (vehicle is not KittenEva kitten) continue;
            if (KittenPoseProbe.AvatarOf(kitten) is not { } avatar)
            {
                trouble.Add("KittenRenderable._characterAvatar not found");
                continue;
            }

            int socket = avatar.Core.CharacterModel.Skeleton.BoneNames?.IndexOf(bone) ?? -1;
            if (socket < 0)
            {
                trouble.Add($"no bone '{bone}' on {KsaWorld.DisplayName(vehicle)}");
                continue;
            }

            List<CharacterAvatar.CosmeticAttachment> list = avatar.Attachments.CosmeticAttachments;
            var held = Props.Find(p => ReferenceEquals(p.Avatar, avatar));
            if (held.Mesh is not null && held.Kind != kind)
            {
                list.RemoveAll(a => ReferenceEquals(a.Mesh, held.Mesh));
                Props.Remove(held);
                held = default;
            }

            StaticMeshRenderable? mesh = held.Mesh;
            if (mesh is null)
            {
                // Anything but the helmet names a declared pair, <kind>_Glb and <kind>_Material.
                bool built = kind == "helmet"
                    ? TryCloneHelmet(avatar, out mesh, out string w1) || Fail(w1, out mesh)
                    : AttachmentMesh.TryBuild(kind + "_Glb", kind + "_Material", out mesh, out string w2)
                      || Fail(w2, out mesh);
                bool Fail(string reason, out StaticMeshRenderable? none)
                {
                    trouble.Add(reason);
                    none = null;
                    return false;
                }

                if (!built)
                {
                    continue;
                }

                list.Add(new CharacterAvatar.CosmeticAttachment { Mesh = mesh, SocketIndex = socket, Transform = local });
                Props.Add((avatar, mesh!, kind));
            }
            else
            {
                // A struct in a list: read it, change it, write it back.
                int i = list.FindIndex(a => ReferenceEquals(a.Mesh, mesh));
                if (i < 0) continue;
                CharacterAvatar.CosmeticAttachment a = list[i];
                a.SocketIndex = socket;
                a.Transform = local;
                list[i] = a;
            }

            props.Add(new()
            {
                ["kitten"] = KsaWorld.DisplayName(vehicle),
                ["socket"] = socket,
                ["drawn_at"] = new[] { mesh!.Transform.W.X, mesh.Transform.W.Y, mesh.Transform.W.Z },
                // The drawn matrix and the bones where they were drawn come from the same render, so a
                // seat can be solved from them without either being a frame out.
                ["drawn"] = Flat(mesh.Transform),
                ["attachment"] = Flat(list.Find(a => ReferenceEquals(a.Mesh, mesh)).Transform),
                ["bones_drawn"] = BonesDrawn(avatar),
                ["attachments"] = list.Count,
            });
        }

        return new() { ["props"] = props, ["trouble"] = trouble };
    }

    private static readonly string[] Watched =
        ["Root_M", "Chest_M", "Head_M", "NoseJoint_M", "Shoulder_L", "Shoulder_R", "Elbow_R", "Wrist_R", "MiddleFinger2_R", "IndexFinger1_R"];

    private static Dictionary<string, float[]> BonesDrawn(CharacterAvatar avatar)
    {
        AnimatedRenderable model = avatar.Core.CharacterModel;
        Dictionary<string, float[]> into = [];
        foreach (string name in Watched)
        {
            int i = model.Skeleton.BoneNames?.IndexOf(name) ?? -1;
            if (i < 0) continue;
            float4x4 m = model.GetBoneTransform(i);
            into[name] = [m.W.X, m.W.Y, m.W.Z];
        }

        return into;
    }

    private static float[] Flat(float4x4 m) =>
        [m.X.X, m.X.Y, m.X.Z, m.X.W, m.Y.X, m.Y.Y, m.Y.Z, m.Y.W, m.Z.X, m.Z.Y, m.Z.Z, m.Z.W, m.W.X, m.W.Y, m.W.Z, m.W.W];

    private static float4x4 Rows(float[] v) =>
        new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);

    /// <summary>Takes every prop back off.</summary>
    public static Dictionary<string, object?> Remove()
    {
        int removed = 0;
        foreach ((CharacterAvatar avatar, StaticMeshRenderable mesh, _) in Props)
        {
            removed += avatar.Attachments.CosmeticAttachments.RemoveAll(a => ReferenceEquals(a.Mesh, mesh));
            mesh.Dispose();
        }

        Props.Clear();
        return new() { ["removed"] = removed };
    }

    // Core's helmet is already loaded with its materials resolved, which makes it a stand-in that can
    // fail only on the attachment path under test.
    private static bool TryCloneHelmet(CharacterAvatar avatar, out StaticMeshRenderable? mesh, out string why)
    {
        mesh = null;
        why = string.Empty;
        try
        {
            if (avatar.Attachments.Helmet.HelmetMesh is not { } helmet)
            {
                why = "this kitten has no helmet mesh to copy";
                return false;
            }

            object? id = AttachmentMesh.NameOf(helmet);
            if (id is null)
            {
                why = "LoadedAssetRef.Id not found";
                return false;
            }

            SuperMeshRenderSystem system = Program.Instance.SuperMeshRenderSystem;
            mesh = (StaticMeshRenderable?)Activator.CreateInstance(
                typeof(StaticMeshRenderable), system.MeshRendererStaticPbr, id, system.MeshRendererStaticPrePass, true);
            if (mesh is null) why = "the mesh could not be built";
            return mesh is not null;
        }
        catch (Exception e)
        {
            why = $"building the mesh threw: {e.GetBaseException().Message}";
            return false;
        }
    }
}
