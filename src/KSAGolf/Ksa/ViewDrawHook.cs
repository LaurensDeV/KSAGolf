using System.Reflection;
using HarmonyLib;
using KSA;

namespace KSAGolf;

/// <summary>
/// Where the mod draws a mesh of its own into the world: a postfix on
/// <c>SuperMeshRenderSystem.ClearBuckets</c>, which <c>Program.RenderViewport</c> calls for each viewport
/// it renders, straight before it draws the kittens into it.
///
/// <para>That is the one moment a static mesh's draw survives. A draw submitted any earlier in the frame,
/// from a StarMap hook or from <c>Universe.UpdateRenderData</c>, is collected and then cleared by this
/// very call before anything is rendered.</para>
///
/// <para>Public, so its signature is pinned in <c>docs/KSA-API-SURFACE.md</c>. If the patch cannot apply
/// it says so once, and the ball is not drawn; nothing else depends on it. Nothing in the postfix may
/// throw: it runs inside the engine's render.</para>
/// </summary>
internal static class ViewDrawHook
{
    private const string HarmonyId = "com.ksagolf.viewdraw";

    private static Action<IViewport>? _draw;
    private static Harmony? _harmony;
    private static bool _complained;

    public static bool Installed { get; private set; }

    public static void Install(Action<IViewport> draw)
    {
        _draw = draw;
        if (Installed) return;

        try
        {
            MethodInfo? target = typeof(SuperMeshRenderSystem).GetMethod(nameof(SuperMeshRenderSystem.ClearBuckets),
                                                                         BindingFlags.Public | BindingFlags.Instance, [typeof(IViewport)]);
            if (target is null)
            {
                Log.Warn("the ball cannot be drawn: KSA has no SuperMeshRenderSystem.ClearBuckets(IViewport)");
                return;
            }

            MethodInfo postfix = typeof(ViewDrawHook).GetMethod(nameof(AfterClearBuckets), BindingFlags.NonPublic | BindingFlags.Static)!;
            _harmony = new Harmony(HarmonyId);
            _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            Installed = true;
            Log.Info("meshes of the mod's own are drawn, via SuperMeshRenderSystem.ClearBuckets");
        }
        catch (Exception e)
        {
            Log.Warn($"could not hook the viewport render, so the ball is not drawn: {e.Message}");
        }
    }

    public static void Remove()
    {
        _draw = null;

        try
        {
            _harmony?.UnpatchAll(HarmonyId);
        }
        catch (Exception e)
        {
            Log.Warn($"could not remove the viewport draw hook: {e.Message}");
        }

        _harmony = null;
        Installed = false;
    }

    private static void AfterClearBuckets(IViewport viewport)
    {
        try
        {
            _draw?.Invoke(viewport);
        }
        catch (Exception e)
        {
            _draw = null;

            if (_complained) return;
            _complained = true;
            Log.Error("drawing into the world failed; the ball is no longer drawn this session", e);
        }
    }
}
