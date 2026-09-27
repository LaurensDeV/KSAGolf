using System.Numerics;
using System.Reflection;
using BepuPhysics;
using BepuPhysics.Collidables;
using HarmonyLib;
using KSA;
using double3 = Brutal.Numerics.double3;
using double4x4 = Brutal.Numerics.double4x4;
using doubleQuat = Brutal.Numerics.doubleQuat;

namespace KSAGolf;

/// <summary>
/// The top of a launch site under a point: a ray cast straight down into the collider the engine stands
/// craft and kittens on, and into the ground the site draws, which runs on well past that collider.
///
/// <para>The height field knows only the terrain, and a launch site is laid over it -- an apron a few
/// centimetres proud, a pad a metre and a half. The engine builds each site's collider as a
/// <c>BigCompound</c> of convex pieces and places it in <c>ConstraintSim.UpdateStaticObjectCollider</c>:
/// body-fixed, at the landmark's direction on the terrain, lifted by <c>GroundOffset</c>, turned onto its
/// up, east and north. The same placement is rebuilt here.</para>
///
/// <para><b>The pieces are copied once, and the copy is what the ray meets.</b> The shapes registry
/// is locked while the engine steps its vehicles, which it does on worker threads through most of the
/// frame -- including the GUI hook the ball steps in -- and the vehicle step refuses to start at all while
/// anyone else holds it. So the registry is only read in the one window nothing can be running: a prefix
/// on <c>Universe.ExecuteNextVehicleSolvers</c>, after the last step has been applied and before the next
/// is queued. Each convex piece's own <c>RayTest</c> needs no registry and no pool, so the copy can be
/// asked from anywhere, any time. A launch site's collider does not change once built.</para>
/// </summary>
internal static class PadSurface
{
    private interface IPiece
    {
        bool Ray(in RigidPose pose, Vector3 origin, Vector3 direction, out float t);
    }

    private sealed class Piece<TShape>(TShape shape) : IPiece where TShape : unmanaged, IConvexShape
    {
        public bool Ray(in RigidPose pose, Vector3 origin, Vector3 direction, out float t) => shape.RayTest(pose, origin, direction, out t, out _);
    }

    private sealed record Pad(double3 PositionCcf, Quaternion Orientation, double Reach, (RigidPose Pose, IPiece Piece)[] Pieces, DrawnGround Drawn);

    // The site's drawn ground, in its own frame: the models it tags as terrain, which reach past the
    // collider -- a road drawn on for metres beyond where anything stands on it.
    private sealed class DrawnGround
    {
        private const double Cell = 4.0;

        private readonly Dictionary<(int, int), List<int>> _cells = [];
        private readonly List<double3> _corners = [];

        public int Triangles => _corners.Count / 3;

        // Site frame: X up, Y east, Z north.
        public void Add(double3 a, double3 b, double3 c)
        {
            int i = _corners.Count;
            _corners.Add(a);
            _corners.Add(b);
            _corners.Add(c);

            int y0 = Key(Math.Min(a.Y, Math.Min(b.Y, c.Y))), y1 = Key(Math.Max(a.Y, Math.Max(b.Y, c.Y)));
            int z0 = Key(Math.Min(a.Z, Math.Min(b.Z, c.Z))), z1 = Key(Math.Max(a.Z, Math.Max(b.Z, c.Z)));
            for (int y = y0; y <= y1; y++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    if (!_cells.TryGetValue((y, z), out List<int>? list)) _cells[(y, z)] = list = [];
                    list.Add(i);
                }
            }
        }

        // The nearest hit along a ray that runs close to straight down the site's X.
        public bool Ray(double3 origin, double3 direction, out double nearest)
        {
            nearest = double.MaxValue;
            if (!_cells.TryGetValue((Key(origin.Y), Key(origin.Z)), out List<int>? list)) return false;

            foreach (int i in list)
            {
                if (Hit(origin, direction, _corners[i], _corners[i + 1], _corners[i + 2], out double t) && t < nearest) nearest = t;
            }

            return nearest < double.MaxValue;
        }

        private static int Key(double v) => (int)Math.Floor(v / Cell);

        private static bool Hit(double3 o, double3 d, double3 a, double3 b, double3 c, out double t)
        {
            t = 0.0;
            double3 e1 = b - a, e2 = c - a;
            double3 p = Vec.Cross(d, e2);
            double det = double3.Dot(e1, p);
            if (Math.Abs(det) < 1e-12) return false;

            double inv = 1.0 / det;
            double3 s = o - a;
            double u = double3.Dot(s, p) * inv;
            if (u < 0.0 || u > 1.0) return false;

            double3 q = Vec.Cross(s, e1);
            double v = double3.Dot(d, q) * inv;
            if (v < 0.0 || u + v > 1.0) return false;

            t = double3.Dot(e2, q) * inv;
            return t >= 0.0;
        }
    }

    // How far above the terrain the ray starts, and so the tallest structure it can find.
    private const double RayAbove = 30.0;

    private const string HarmonyId = "com.ksagolf.padsurface";

    private static Harmony? _harmony;
    private static volatile Celestial? _wanted;
    private static Celestial? _body;
    private static Pad[] _pads = [];
    private static bool _complained;

    /// <summary>Whether the copy can be taken: without the hook, balls rest on terrain alone.</summary>
    public static bool Installed { get; private set; }

    public static void Install()
    {
        if (Installed) return;

        try
        {
            MethodInfo? target = AccessTools.Method(typeof(Universe), nameof(Universe.ExecuteNextVehicleSolvers));
            if (target is null)
            {
                Log.Warn("balls rest on terrain alone: KSA has no Universe.ExecuteNextVehicleSolvers to copy launch sites in");
                return;
            }

            _harmony = new Harmony(HarmonyId);
            _harmony.Patch(target, prefix: new HarmonyMethod(typeof(PadSurface).GetMethod(nameof(BetweenVehicleSteps), BindingFlags.NonPublic | BindingFlags.Static)));
            Installed = true;
            Log.Info("launch sites are copied between vehicle steps, via Universe.ExecuteNextVehicleSolvers");
        }
        catch (Exception e)
        {
            Log.Warn($"balls rest on terrain alone: could not hook the vehicle step ({e.GetBaseException().Message})");
        }
    }

    public static void Remove()
    {
        try
        {
            _harmony?.UnpatchAll(HarmonyId);
        }
        catch
        {
            // Unloading anyway.
        }

        _harmony = null;
        Installed = false;
        Forget();
    }

    /// <summary>Asks for a body's launch sites to be copied at the next safe moment.</summary>
    public static void Want(Celestial body)
    {
        if (!ReferenceEquals(body, _body)) _wanted = body;
    }

    /// <summary>Forgets the sites, for a world a save load has replaced.</summary>
    public static void Forget()
    {
        _body = null;
        _wanted = null;
        _pads = [];
    }

    /// <summary>The distance from the body's centre to a launch site's top along a direction, if one is there.</summary>
    public static bool TryRadius(Celestial body, double3 dirCcf, double terrainRadius, out double radius)
    {
        radius = 0.0;
        if (!ReferenceEquals(body, _body))
        {
            Want(body);
            return false;
        }

        double3 point = dirCcf * terrainRadius;
        foreach (Pad pad in _pads)
        {
            if ((point - pad.PositionCcf).Length() > pad.Reach) continue;

            // Into the site's own frame, where each piece sits at its pose within the compound.
            Quaternion toLocal = Quaternion.Conjugate(pad.Orientation);
            double3 from = (dirCcf * (terrainRadius + RayAbove)) - pad.PositionCcf;
            Vector3 origin = Vector3.Transform(new Vector3((float)from.X, (float)from.Y, (float)from.Z), toLocal);
            Vector3 direction = Vector3.Transform(new Vector3((float)-dirCcf.X, (float)-dirCcf.Y, (float)-dirCcf.Z), toLocal);

            float nearest = float.MaxValue;
            foreach ((RigidPose pose, IPiece piece) in pad.Pieces)
            {
                if (piece.Ray(pose, origin, direction, out float t) && t >= 0f && t < nearest) nearest = t;
            }

            double hit = nearest;
            double3 originD = new(origin.X, origin.Y, origin.Z), directionD = new(direction.X, direction.Y, direction.Z);
            if (pad.Drawn.Ray(originD, directionD, out double drawn) && drawn < hit) hit = drawn;

            if (hit == float.MaxValue || hit > RayAbove * 2.0) continue;

            double top = terrainRadius + RayAbove - hit;
            if (top > radius) radius = top;
        }

        return radius > 0.0;
    }

    // Nothing is stepping here and nothing can start: the last step has been applied and the next is
    // queued after this returns. A refusal all the same leaves the copy for the next frame.
    private static void BetweenVehicleSteps()
    {
        if (_wanted is not { } body) return;

        try
        {
            using ShapesUnlock unlocked = ConstraintSim.UnlockShapes();
            _pads = Copy(body, unlocked.Shapes);
            _body = body;
            _wanted = null;
            Log.Info($"copied {_pads.Length} launch site(s) on {body.Id} for the ball to rest on, "
                     + $"{_pads.Sum(p => p.Pieces.Length)} collider pieces and {_pads.Sum(p => p.Drawn.Triangles)} drawn ground triangles");
        }
        catch (Exception e)
        {
            if (_complained) return;
            _complained = true;
            Log.Warn($"could not copy the launch sites yet, will try again: {e.GetBaseException().Message}");
        }
    }

    private static Pad[] Copy(Celestial body, Shapes shapes)
    {
        List<Pad> pads = [];
        if (body.BodyTemplate is not { } template) return [];

        foreach (LocationReference location in template.Locations)
        {
            if (location is not LandmarkReference { IsLaunchPad: true } landmark) continue;
            if (landmark.GetStaticObject() is not { CollisionShape.Exists: true } site) continue;
            if (site.CollisionShape.Type != BigCompound.Id) continue;

            double3 forward = landmark.ForwardCcf;
            landmark.GetAxesCcf(out double3 up, out double3 east, out double3 north);
            double3 at = (forward * (body.MeanRadius + body.GetTerrainHeightFromDirCcf(forward))) + (up * site.GroundOffset);
            doubleQuat turn = doubleQuat.CreateFromRotationMatrix(new double4x4(up.X, up.Y, up.Z, 0.0, east.X, east.Y, east.Z, 0.0,
                                                                                north.X, north.Y, north.Z, 0.0, 0.0, 0.0, 0.0, 1.0));

            ref BigCompound compound = ref shapes.GetShape<BigCompound>(site.CollisionShape.Index);
            List<(RigidPose, IPiece)> pieces = [];
            for (int i = 0; i < compound.Children.Length; i++)
            {
                ref CompoundChild child = ref compound.Children[i];
                if (PieceOf(shapes, child.ShapeIndex) is { } piece) pieces.Add((new RigidPose(child.LocalPosition, child.LocalOrientation), piece));
            }

            pads.Add(new Pad(at, new Quaternion((float)turn.X, (float)turn.Y, (float)turn.Z, (float)turn.W),
                             Math.Max(site.FootprintRadius, 50.0) * 1.5, [.. pieces], DrawnGroundOf(site)));
        }

        return [.. pads];
    }

    private static readonly FieldInfo? ModelsField = AccessTools.Field(typeof(StaticObject), "_models");

    // Every triangle of the site's terrain-tagged models, placed as StaticObject.UpdateRenderData places them.
    private static DrawnGround DrawnGroundOf(StaticObject site)
    {
        var ground = new DrawnGround();
        if (ModelsField?.GetValue(site) is not System.Collections.IEnumerable models) return ground;

        foreach (object instance in models)
        {
            Type type = instance.GetType();
            if (type.GetProperty("Model")?.GetValue(instance) is not StaticObjectModel { Template: { Terrain: true, Mesh: { } mesh } }) continue;
            if (type.GetProperty("Model2Asmb")?.GetValue(instance) is not double4x4 toSite) continue;
            if (mesh.PositionsCompare is not { } primitives) continue;

            foreach (double3[] soup in primitives)
            {
                for (int i = 0; i + 2 < soup.Length; i += 3)
                {
                    ground.Add(double3.Transform(soup[i], toSite), double3.Transform(soup[i + 1], toSite), double3.Transform(soup[i + 2], toSite));
                }
            }
        }

        return ground;
    }

    private static IPiece? PieceOf(Shapes shapes, TypedIndex index) => index.Type switch
    {
        Box.Id => new Piece<Box>(shapes.GetShape<Box>(index.Index)),
        Sphere.Id => new Piece<Sphere>(shapes.GetShape<Sphere>(index.Index)),
        Capsule.Id => new Piece<Capsule>(shapes.GetShape<Capsule>(index.Index)),
        Cylinder.Id => new Piece<Cylinder>(shapes.GetShape<Cylinder>(index.Index)),
        ConvexHull.Id => new Piece<ConvexHull>(shapes.GetShape<ConvexHull>(index.Index)),
        Triangle.Id => new Piece<Triangle>(shapes.GetShape<Triangle>(index.Index)),
        _ => null,
    };

    // Never called. Puts the patched method in this assembly's metadata for the API record.
    private static void PinTheStep(double dtPlayer, SimStep step) => Universe.ExecuteNextVehicleSolvers(dtPlayer, step);
}
