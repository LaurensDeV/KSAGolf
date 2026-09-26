using System.Reflection;
using KSA;

namespace KSAGolf;

/// <summary>
/// A mesh of this mod's own, built to hang on a kitten as a cosmetic attachment — the way
/// <c>CharacterAvatar</c> builds Core's, with the material slot filled before the renderable is made.
///
/// <para>Everything is reached by reflection because the asset name type, <c>AssetName</c>, lives in
/// <c>Planet.Core.dll</c>, which this mod does not reference. The type is read off
/// <see cref="LoadedAssetRef.Id"/>, so it follows the engine rather than a guess.</para>
/// </summary>
internal static class AttachmentMesh
{
    private static readonly Type? AssetNameType = typeof(LoadedAssetRef).GetProperty("Id")?.PropertyType;

    /// <summary>
    /// Builds a renderable from a declared <c>&lt;GltfFile&gt;</c> with a declared <c>&lt;PbrMaterial&gt;</c> in
    /// every slot, or says why it could not.
    /// </summary>
    public static bool TryBuild(string gltfId, string materialId, out StaticMeshRenderable? mesh, out string why)
    {
        mesh = null;
        why = string.Empty;
        try
        {
            if (AssetNameType is null)
            {
                why = "LoadedAssetRef.Id not found";
                return false;
            }

            SuperMeshRenderSystem system = Program.Instance.SuperMeshRenderSystem;
            object gltfName = Activator.CreateInstance(AssetNameType, gltfId)!;
            object materialName = Activator.CreateInstance(AssetNameType, materialId)!;

            if (Load(system, "GltfSystem", gltfName) is not GltfPbrAssetRef gltf)
            {
                why = $"glTF '{gltfId}' did not load";
                return false;
            }

            if (Load(system, "MaterialSystem", materialName) is not GpuObjectAssetRef material)
            {
                why = $"material '{materialId}' did not load";
                return false;
            }

            // An exported .glb names a material slot, which is what gives this array a length.
            for (int i = 0; i < gltf.Materials.Length; i++) gltf.Materials[i] = material;

            mesh = (StaticMeshRenderable?)Activator.CreateInstance(
                typeof(StaticMeshRenderable), system.MeshRendererStaticPbr, gltfName, system.MeshRendererStaticPrePass, true);
            if (mesh is null) why = "the renderable could not be built";
            return mesh is not null;
        }
        catch (Exception e)
        {
            why = $"building '{gltfId}' threw: {e.GetBaseException().Message}";
            return false;
        }
    }

    /// <summary>The asset name a loaded mesh was built from, as the engine's own type.</summary>
    public static object? NameOf(StaticMeshRenderable mesh) =>
        typeof(LoadedAssetRef).GetProperty("Id")?.GetValue(mesh.GltfAssetRef);

    private static object? Load(SuperMeshRenderSystem system, string managerField, object name)
    {
        object? manager = system.GetType().GetField(managerField, BindingFlags.Instance | BindingFlags.Public)?.GetValue(system);
        MethodInfo? load = manager?.GetType().GetMethod("GetOrLoad", [AssetNameType!]);
        return load?.Invoke(manager, [name]);
    }
}
