# KSA API surface

Every external type and member `KSAGolf.dll` binds to, read out of its
metadata tables by `tools/api-surface.sh`. **Generated - do not edit.**

This is the checklist for a KSA update: anything here that changed shape in the new
build is a breaking change for this mod, and anything not here cannot be. See the
`upgrade-ksa` skill, which diffs the decompiled sources against exactly this list.

156 types and 390 members across 8 assemblies.

## BepuPhysics

### BepuPhysics.BodyHandle

*referenced as a type only*

### BepuPhysics.Collidables.BigCompound

- `BepuUtilities.Memory.Buffer`1<BepuPhysics.Collidables.CompoundChild> Children`

### BepuPhysics.Collidables.Box

- `void .ctor(float, float, float)`

### BepuPhysics.Collidables.Capsule

*referenced as a type only*

### BepuPhysics.Collidables.CompoundChild

- `BepuPhysics.Collidables.TypedIndex ShapeIndex`
- `System.Numerics.Quaternion LocalOrientation`
- `System.Numerics.Vector3 LocalPosition`

### BepuPhysics.Collidables.ConvexHull

*referenced as a type only*

### BepuPhysics.Collidables.Cylinder

*referenced as a type only*

### BepuPhysics.Collidables.IConvexShape

- `bool RayTest(ref BepuPhysics.RigidPose, System.Numerics.Vector3, System.Numerics.Vector3, ref float, ref System.Numerics.Vector3)`

### BepuPhysics.Collidables.Shapes

- `BepuPhysics.Collidables.TypedIndex Add<1>(ref !!0)`
- `ref !!0 GetShape<1>(int)`

### BepuPhysics.Collidables.Sphere

*referenced as a type only*

### BepuPhysics.Collidables.Triangle

*referenced as a type only*

### BepuPhysics.Collidables.TypedIndex

- `bool get_Exists()`
- `int get_Index()`
- `int get_Type()`

### BepuPhysics.RigidPose

- `void .ctor(System.Numerics.Vector3, System.Numerics.Quaternion)`

### BepuPhysics.Simulation

- `BepuPhysics.Statics get_Statics()`

### BepuPhysics.StaticDescription

- `BepuPhysics.Collidables.TypedIndex Shape`
- `BepuPhysics.RigidPose Pose`

### BepuPhysics.StaticHandle

*referenced as a type only*

### BepuPhysics.Statics

- `BepuPhysics.StaticHandle Add<1>(ref BepuPhysics.StaticDescription, ref !!0)`
- `void ApplyDescription<1>(BepuPhysics.StaticHandle, ref BepuPhysics.StaticDescription, ref !!0)`
- `void Remove(BepuPhysics.StaticHandle)`

## BepuUtilities

### BepuUtilities.Memory.Buffer`1

*referenced as a type only*

## Brutal.Concurrency

### Brutal.Concurrency.Jobs.JobScheduler

- `void Wait()`

## Brutal.Core.Numerics

### Brutal.Numerics.Pack

*referenced as a type only*

### Brutal.Numerics.Pack+Float

*referenced as a type only*

### Brutal.Numerics.byte4

*referenced as a type only*

### Brutal.Numerics.double2

- `Brutal.Numerics.double2 Lerp(Brutal.Numerics.double2, Brutal.Numerics.double2, double)`
- `Brutal.Numerics.double2 op_Addition(Brutal.Numerics.double2, Brutal.Numerics.double2)`
- `Brutal.Numerics.double2 op_Division(Brutal.Numerics.double2, double)`
- `Brutal.Numerics.double2 op_Multiply(Brutal.Numerics.double2, double)`
- `Brutal.Numerics.double2 op_Subtraction(Brutal.Numerics.double2, Brutal.Numerics.double2)`
- `double Distance(Brutal.Numerics.double2, Brutal.Numerics.double2)`
- `double Dot(Brutal.Numerics.double2, Brutal.Numerics.double2)`
- `double X`
- `double Y`
- `void .ctor(double, double)`

### Brutal.Numerics.double3

- `Brutal.Numerics.double3 Cross(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `Brutal.Numerics.double3 Transform(Brutal.Numerics.double3, Brutal.Numerics.double4x4)`
- `Brutal.Numerics.double3 Transform(Brutal.Numerics.double3, Brutal.Numerics.doubleQuat)`
- `Brutal.Numerics.double3 get_Zero()`
- `Brutal.Numerics.double3 op_Addition(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `Brutal.Numerics.double3 op_Division(Brutal.Numerics.double3, double)`
- `Brutal.Numerics.double3 op_Multiply(Brutal.Numerics.double3, double)`
- `Brutal.Numerics.double3 op_Subtraction(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `Brutal.Numerics.double3 op_UnaryNegation(Brutal.Numerics.double3)`
- `bool Equals(Brutal.Numerics.double3)`
- `bool op_Inequality(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `double Dot(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `double Length()`
- `double LengthSquared()`
- `double X`
- `double Y`
- `double Z`
- `void .ctor(double, double, double)`

### Brutal.Numerics.double4

- `double W`
- `double X`
- `double Y`

### Brutal.Numerics.double4x4

- `void .ctor(double, double, double, double, double, double, double, double, double, double, double, double, double, double, double, double)`

### Brutal.Numerics.doubleQuat

- `Brutal.Numerics.double3 op_Multiply(Brutal.Numerics.doubleQuat, Brutal.Numerics.double3)`
- `Brutal.Numerics.doubleQuat Concatenate(Brutal.Numerics.doubleQuat, Brutal.Numerics.doubleQuat)`
- `Brutal.Numerics.doubleQuat CreateFromAxisAngle(Brutal.Numerics.double3, double)`
- `Brutal.Numerics.doubleQuat CreateFromRotationMatrix(Brutal.Numerics.double4x4)`
- `Brutal.Numerics.doubleQuat Inverse(Brutal.Numerics.doubleQuat)`
- `Brutal.Numerics.doubleQuat Normalize(Brutal.Numerics.doubleQuat)`
- `Brutal.Numerics.doubleQuat get_Identity()`
- `Brutal.Numerics.doubleQuat op_Multiply(Brutal.Numerics.doubleQuat, Brutal.Numerics.doubleQuat)`
- `double W`
- `double X`
- `double Y`
- `double Z`

### Brutal.Numerics.float2

- `float X`
- `float Y`
- `void .ctor(float, float)`

### Brutal.Numerics.float3

- `Brutal.Numerics.float3 Normalize(Brutal.Numerics.float3)`
- `Brutal.Numerics.float3 Pack(ref Brutal.Numerics.double3, Float)`
- `Brutal.Numerics.float3 Transform(Brutal.Numerics.float3, Brutal.Numerics.floatQuat)`
- `Brutal.Numerics.float3 get_One()`
- `Brutal.Numerics.float3 op_Addition(Brutal.Numerics.float3, Brutal.Numerics.float3)`
- `Brutal.Numerics.float3 op_Multiply(Brutal.Numerics.float3, Brutal.Numerics.float3)`
- `Brutal.Numerics.float3 op_Multiply(Brutal.Numerics.float3, float)`
- `Brutal.Numerics.float3 op_Subtraction(Brutal.Numerics.float3, Brutal.Numerics.float3)`
- `float Dot(Brutal.Numerics.float3, Brutal.Numerics.float3)`
- `float Length()`
- `float LengthSquared()`
- `float X`
- `float Y`
- `float Z`
- `void .ctor(float, float, float)`

### Brutal.Numerics.float4

- `Brutal.Numerics.float3 get_XYZ()`
- `float W`
- `float X`
- `float Y`
- `float Z`
- `void .ctor(float, float, float, float)`

### Brutal.Numerics.float4x4

- `Brutal.Numerics.float3 get_Translation()`
- `Brutal.Numerics.float4 W`
- `Brutal.Numerics.float4 X`
- `Brutal.Numerics.float4 Y`
- `Brutal.Numerics.float4 Z`
- `Brutal.Numerics.float4x4 CreateFromQuaternion(Brutal.Numerics.floatQuat)`
- `Brutal.Numerics.float4x4 CreateRotationX(float)`
- `Brutal.Numerics.float4x4 CreateRotationY(float)`
- `Brutal.Numerics.float4x4 CreateRotationZ(float)`
- `Brutal.Numerics.float4x4 CreateScale(float)`
- `Brutal.Numerics.float4x4 CreateTranslation(Brutal.Numerics.float3)`
- `Brutal.Numerics.float4x4 op_Multiply(Brutal.Numerics.float4x4, Brutal.Numerics.float4x4)`
- `bool Invert(Brutal.Numerics.float4x4, ref Brutal.Numerics.float4x4)`
- `void .ctor(float, float, float, float, float, float, float, float, float, float, float, float, float, float, float, float)`

### Brutal.Numerics.floatQuat

- `Brutal.Numerics.floatQuat Concatenate(Brutal.Numerics.floatQuat, Brutal.Numerics.floatQuat)`
- `Brutal.Numerics.floatQuat CreateFromAxisAngle(Brutal.Numerics.float3, float)`
- `Brutal.Numerics.floatQuat Inverse(Brutal.Numerics.floatQuat)`
- `Brutal.Numerics.floatQuat Normalize(Brutal.Numerics.floatQuat)`
- `Brutal.Numerics.floatQuat Pack(ref Brutal.Numerics.doubleQuat, Float)`
- `Brutal.Numerics.floatQuat get_Identity()`

### Brutal.Numerics.int2

- `int X`
- `int Y`

## Brutal.Glfw

### Brutal.GlfwApi.GlfwButtonAction

*referenced as a type only*

### Brutal.GlfwApi.GlfwCursorMode

*referenced as a type only*

### Brutal.GlfwApi.GlfwKey

*referenced as a type only*

### Brutal.GlfwApi.GlfwKeyAction

*referenced as a type only*

### Brutal.GlfwApi.GlfwModifier

*referenced as a type only*

### Brutal.GlfwApi.GlfwMouseButton

*referenced as a type only*

### Brutal.GlfwApi.GlfwWindow

- `int GetAttribute(Brutal.GlfwApi.GlfwWindowAttribute)`

### Brutal.GlfwApi.GlfwWindowAttribute

*referenced as a type only*

## Brutal.ImGui

### Brutal.ImGuiApi.ImGui

- `Brutal.ImGuiApi.ImGuiIOPtr GetIO()`
- `Brutal.ImGuiApi.ImGuiViewportPtr GetMainViewport()`
- `Brutal.Numerics.float2 GetMousePos()`
- `bool Begin(Brutal.ImGuiApi.ImString, Brutal.ImGuiApi.ImGuiWindowFlags)`
- `bool Begin(Brutal.ImGuiApi.ImString, ref bool, Brutal.ImGuiApi.ImGuiWindowFlags)`
- `bool BeginItemTooltip()`
- `bool BeginMainMenuBar()`
- `bool BeginMenu(Brutal.ImGuiApi.ImString, bool)`
- `bool Button(Brutal.ImGuiApi.ImString, ref System.Nullable`1<Brutal.Numerics.float2>)`
- `bool Checkbox(Brutal.ImGuiApi.ImString, ref bool)`
- `bool IsKeyDown(Brutal.ImGuiApi.ImGuiKey)`
- `bool IsKeyPressed(Brutal.ImGuiApi.ImGuiKey, bool)`
- `bool IsMouseClicked(Brutal.ImGuiApi.ImGuiMouseButton, bool)`
- `bool IsMouseDown(Brutal.ImGuiApi.ImGuiMouseButton)`
- `bool MenuItem(Brutal.ImGuiApi.ImString, Brutal.ImGuiApi.ImString, ref bool, bool)`
- `bool RadioButton(Brutal.ImGuiApi.ImString, bool)`
- `bool SmallButton(Brutal.ImGuiApi.ImString)`
- `float GetFontSize()`
- `void End()`
- `void EndMainMenuBar()`
- `void EndMenu()`
- `void EndTooltip()`
- `void PopTextWrapPos()`
- `void PushTextWrapPos(float)`
- `void SameLine(float, float)`
- `void Separator()`
- `void Text(Brutal.ImGuiApi.ImString)`
- `void TextColored(ref Brutal.Numerics.float4, Brutal.ImGuiApi.ImString)`
- `void TextDisabled(Brutal.ImGuiApi.ImString)`
- `void TextWrapped(Brutal.ImGuiApi.ImString)`

### Brutal.ImGuiApi.ImGuiIOPtr

- `ref Brutal.Numerics.float2 get_DisplaySize()`
- `ref Brutal.Numerics.float2 get_MouseDelta()`
- `ref bool get_KeyAlt()`
- `ref bool get_KeyCtrl()`
- `ref bool get_KeyShift()`
- `ref bool get_WantCaptureMouse()`
- `ref bool get_WantTextInput()`
- `ref float get_MouseWheel()`

### Brutal.ImGuiApi.ImGuiKey

*referenced as a type only*

### Brutal.ImGuiApi.ImGuiMouseButton

*referenced as a type only*

### Brutal.ImGuiApi.ImGuiViewportPtr

- `ref Brutal.Numerics.float2 get_Pos()`
- `ref Brutal.Numerics.float2 get_Size()`

### Brutal.ImGuiApi.ImGuiWindowFlags

*referenced as a type only*

### Brutal.ImGuiApi.ImString

- `Brutal.ImGuiApi.ImString op_Implicit(string)`
- `void .ctor(int, int)`
- `void AppendFormatted(string, int, string)`
- `void AppendFormatted<1>(!!0, int, string)`
- `void AppendLiteral(System.ReadOnlySpan`1<char>)`

## KSA

### KSA.AnimatedRenderable

- `Brutal.Numerics.float4x4 GetBoneTransform(int)`
- `Brutal.Numerics.float4x4 Transform`
- `KSA.GltfPbrAssetRef GltfAssetRef`
- `RenderCore.Animation.Skeleton Skeleton`
- `RenderCore.Systems.GlobalMeshBucketHandle[] DepthMeshBucketHandles`
- `System.Collections.Generic.List`1<KSA.IAnimProcessor> AnimProcessors`
- `bool ShouldUpdate`
- `void Draw(RenderCore.Systems.ViewHandle)`

### KSA.Astronomical

- `Brutal.Numerics.double3 GetPositionEcl()`
- `Brutal.Numerics.double3 GetVelocityEcl()`
- `KSA.AtmosphereReference GetAtmosphereReference()`
- `KSA.OrbitView OrbitView`
- `KSA.Rendering.Water.Data.OceanReference GetOceanReference()`
- `double get_MaxTerrainRadius()`
- `double get_MeanRadius()`
- `string get_Id()`

### KSA.AtmosphereReference

- `KSA.PhysicalAtmosphereReference Physical`

### KSA.BubbleClutterStatics

*referenced as a type only*

### KSA.BubbleFrame

*referenced as a type only*

### KSA.BubbleFrameEx

- `bool IsCcf(KSA.BubbleFrame)`

### KSA.BubbleOrigin

- `Brutal.Numerics.double3 PositionBub`
- `KSA.BubbleFrame BubFrame`
- `KSA.IParentBody Parent`

### KSA.Camera

- `Brutal.Numerics.double3 EgoToEcl(Brutal.Numerics.double3)`
- `Brutal.Numerics.double3 GetForwardEcl()`
- `Brutal.Numerics.double3 GetPositionEgo(KSA.IPosition)`
- `Brutal.Numerics.double3 GetRightEcl()`
- `Brutal.Numerics.double4 EgoToClipDouble(Brutal.Numerics.double3)`
- `Brutal.Numerics.doubleQuat LookAtRotation(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `Brutal.Numerics.float2 EclToScreen(Brutal.Numerics.double3, bool)`
- `Brutal.Numerics.int2 FramebufferSize`
- `KSA.IFollowable get_Following()`
- `KSA.Ray ScreenToEgoRay(Brutal.Numerics.float2)`
- `float GetFieldOfView()`
- `void SetFieldOfView(float)`
- `void SetFollow(KSA.IFollowable, bool, bool, bool)`

### KSA.CameraMode

*referenced as a type only*

### KSA.Celestial

- `Brutal.Numerics.double3 GetSurfacePositionEclFromCce(Brutal.Numerics.double3, bool)`
- `Brutal.Numerics.doubleQuat GetCce2Ccf()`
- `Brutal.Numerics.doubleQuat GetCce2Cci()`
- `Brutal.Numerics.doubleQuat GetCcf2Cce()`
- `Brutal.Numerics.doubleQuat GetCci2Cce()`
- `KSA.CelestialTemplate get_BodyTemplate()`
- `double GetLatitudeFromCce(Brutal.Numerics.double3)`
- `double GetLongitudeFromCce(Brutal.Numerics.double3)`
- `double GetTerrainHeightFromDirCce(Brutal.Numerics.double3, bool)`
- `double GetTerrainHeightFromDirCcf(Brutal.Numerics.double3, bool)`
- `double get_MaxTerrainHeightApprox()`

### KSA.CelestialSystem

- `KSA.Astronomical GetIndex(int)`
- `KSA.LookupCollection`1<KSA.Astronomical> get_All()`
- `int get_Count()`

### KSA.CelestialTemplate

- `System.Collections.Generic.List`1<KSA.LocationReference> Locations`

### KSA.CharacterAvatar

- `CharacterAttachments Attachments`
- `CharacterCore Core`

### KSA.CharacterAvatar+CharacterAttachments

- `Helmet Helmet`
- `Mmu Mmu`
- `System.Collections.Generic.List`1<CosmeticAttachment> CosmeticAttachments`

### KSA.CharacterAvatar+CharacterCore

- `KSA.AnimatedRenderable CharacterModel`
- `float Scale`

### KSA.CharacterAvatar+CosmeticAttachment

- `Brutal.Numerics.float4x4 Transform`
- `KSA.StaticMeshRenderable Mesh`
- `int SocketIndex`
- `void .ctor()`

### KSA.CharacterAvatar+Helmet

- `Brutal.Numerics.float4x4 HelmetTransform`
- `Brutal.Numerics.float4x4 VisorTransform`
- `KSA.StaticMeshRenderable HelmetMesh`
- `KSA.StaticMeshRenderable VisorMesh`
- `bool Enabled`
- `int SocketIndex`

### KSA.CharacterAvatar+Mmu

- `bool Enabled`

### KSA.CharacterControlInputs

- `Brutal.Numerics.float3 CameraForwardCce`

### KSA.Constants

- `string get_DocumentsFolderPath()`

### KSA.ConstraintSim

- `BepuPhysics.Simulation Simulation`
- `KSA.BubbleClutterStatics get_ClutterStatics()`
- `KSA.ShapesUnlock UnlockShapes()`
- `KSA.ShapesUnlock UnlockShapesBlocking()`
- `System.Collections.Generic.Dictionary`2<BepuPhysics.BodyHandle, KSA.VehicleUpdateState> HandleToState`
- `bool TryResetForPool()`
- `void DetectCollisions(double)`
- `void Simulate(double, ref KSA.SimStep)`

### KSA.Controller

- `Brutal.GlfwApi.GlfwCursorMode GetCursorMode()`
- `KSA.Camera Camera`
- `bool IsMouseDrag()`
- `bool OnCursorPos(Brutal.GlfwApi.GlfwWindow, Brutal.Numerics.double2)`
- `bool OnMouseButton(Brutal.GlfwApi.GlfwWindow, Brutal.GlfwApi.GlfwMouseButton, Brutal.GlfwApi.GlfwButtonAction, Brutal.GlfwApi.GlfwModifier)`
- `bool OnScroll(Brutal.GlfwApi.GlfwWindow, Brutal.Numerics.double2)`

### KSA.CrewDisposition

*referenced as a type only*

### KSA.DensityReference

- `double op_Implicit(KSA.DensityReference)`

### KSA.DistanceReference

- `double InMeters()`
- `double op_Implicit(KSA.DistanceReference)`

### KSA.Double3Ex

- `Brutal.Numerics.double3 Transform(Brutal.Numerics.double3, Brutal.Numerics.doubleQuat)`

### KSA.FixedController

- `Brutal.Numerics.double3 CameraOffset`
- `Brutal.Numerics.double3 CameraRotation`
- `bool OnKey(RenderCore.Input.GlfwKeyEvent)`
- `void .ctor(KSA.Camera, string)`
- `void OnFrame(KSA.IViewport, double)`

### KSA.Float3Ex

- `Brutal.Numerics.float3 Normalized(Brutal.Numerics.float3)`

### KSA.GameAudio

- `void PlaySound(KSA.SoundBehavior, KSA.SpatialAudio, ref KSA.IChannel, float, bool, bool)`

### KSA.GameSaves

- `void LoadSaveGame(string)`

### KSA.GizmosRenderer

- `void DrawLine(Brutal.Numerics.double3, Brutal.Numerics.double3, Brutal.Numerics.float4)`
- `void DrawSphere(Brutal.Numerics.double3, float, Brutal.Numerics.float4)`

### KSA.GltfPbrAssetRef

- `KSA.GpuObjectAssetRef[] Materials`
- `KSA.IMeshAsset[] Meshes`

### KSA.GpuObjectAssetRef

*referenced as a type only*

### KSA.IAnimProcessor

*referenced as a type only*

### KSA.IChannel

*referenced as a type only*

### KSA.IFollowable

- `KSA.OrbitView get_OrbitView()`

### KSA.IGameViewport

- `KSA.Camera get_BaseCamera()`
- `KSA.FixedController get_FixedController()`
- `KSA.OrbitController get_OrbitController()`
- `void SetCameraMode(KSA.CameraMode)`

### KSA.IMeshAsset

- `float get_BoundingRadius()`

### KSA.IParentBody

- `Brutal.Numerics.double3 GetAngularVelocityCce()`
- `double get_Mu()`

### KSA.IPosition

- `Brutal.Numerics.double3 GetPositionEcl()`

### KSA.IViewport

- `Brutal.Numerics.float2 get_Position()`
- `KSA.Camera GetCamera()`
- `KSA.CameraMode get_Mode()`
- `bool get_Visible()`
- `int get_Height()`
- `int get_Width()`

### KSA.InstanceData

- `Brutal.Numerics.float4 data`
- `Brutal.Numerics.float4x4 model`

### KSA.JobSystems

- `Brutal.Concurrency.Jobs.JobScheduler VehicleSolver`

### KSA.KittenEva

- `KSA.KittenRenderable get_Renderable()`

### KSA.KittenLocomotion

- `KSA.LocomotionCommand StepGrounded(ref KSA.CharacterControlInputs, ref KSA.ManualControlInputs, ref KSA.LocomotionFacts, ref KSA.KittenLocomotionTuning, ref KSA.LocomotionState)`

### KSA.KittenLocomotionTuning

*referenced as a type only*

### KSA.KittenRenderable

*referenced as a type only*

### KSA.LandmarkReference

- `bool IsLaunchPad`

### KSA.LoadedAssetRef

*referenced as a type only*

### KSA.LocationReference

- `Brutal.Numerics.double3 get_ForwardCcf()`
- `KSA.StaticObject GetStaticObject()`
- `void GetAxesCcf(ref Brutal.Numerics.double3, ref Brutal.Numerics.double3, ref Brutal.Numerics.double3)`

### KSA.LocomotionCommand

- `Brutal.Numerics.float3 FacingDirPhys`

### KSA.LocomotionFacts

- `Brutal.Numerics.float3 UpDirPhys`
- `Brutal.Numerics.floatQuat Cce2Phys`

### KSA.LocomotionState

- `Brutal.Numerics.float3 FacingDirPhys`

### KSA.LookupCollection`1

*referenced as a type only*

### KSA.ManualControlInputs

*referenced as a type only*

### KSA.MeshReference

- `Brutal.Numerics.double3[][] PositionsCompare`

### KSA.MeshRenderTechnique

*referenced as a type only*

### KSA.MeshViewModule

*referenced as a type only*

### KSA.Mod

- `string get_Id()`

### KSA.ModLibrary

- `!!0 Get<1>(string)`

### KSA.ModuleList

- `bool HasAny<1>()`

### KSA.Orbit

- `KSA.Orbit CreateFromStateCci(KSA.IParentBody, KSA.UniverseTime, Brutal.Numerics.double3, Brutal.Numerics.double3, Brutal.Numerics.byte4)`

### KSA.OrbitController

- `double DistancePower`

### KSA.OrbitView

- `double Azimuth`
- `double DistancePower`
- `double Elevation`

### KSA.Part

- `KSA.ModuleList Modules`
- `KSA.PartTemplate Template`
- `System.ReadOnlySpan`1<KSA.Part> get_SubParts()`
- `bool RayCastEgo(ref Brutal.Numerics.double4x4, KSA.Ray, ref double, ref double, ref Brutal.Numerics.double3, ref Brutal.Numerics.double3, ref Brutal.Numerics.double3, ref Brutal.Numerics.double3, ref KSA.Part, ref KSA.Part)`

### KSA.Part+Connector

*referenced as a type only*

### KSA.Part+Connector+Flag

*referenced as a type only*

### KSA.Part+Connector+TemplateBase

- `Flag Flags`

### KSA.PartModelModule

*referenced as a type only*

### KSA.PartModelModule+Template

- `KSA.MeshReference Mesh`
- `bool Terrain`

### KSA.PartTemplate

- `System.Collections.Generic.List`1<TemplateBase> Connectors`

### KSA.PartTree

- `System.ReadOnlySpan`1<KSA.Part> get_Parts()`

### KSA.PhysicalAtmosphereReference

- `KSA.DensityReference SeaLevelDensity`
- `KSA.DistanceReference ScaleHeight`
- `KSA.DistanceReference get_Height()`
- `double GetAtmosphericDensityAtAltitude(double)`

### KSA.Program

- `Brutal.GlfwApi.GlfwWindow GetWindow()`
- `KSA.Camera GetMainCamera()`
- `KSA.Camera GetRenderCamera()`
- `KSA.GizmosRenderer GizmosRenderer`
- `KSA.IGameViewport get_MainViewport()`
- `KSA.Program get_Instance()`
- `KSA.SuperMeshRenderSystem SuperMeshRenderSystem`
- `KSA.Vehicle get_ControlledVehicle()`
- `KSA.VehicleEditor Editor`
- `System.ReadOnlySpan`1<KSA.Vehicle> get_VehiclesInFrame()`
- `bool IsWindowOpen`
- `void OnGameLoaded()`
- `void set_ControlledVehicle(KSA.Vehicle)`

### KSA.QuaternionEx

- `Brutal.Numerics.doubleQuat Inverse(Brutal.Numerics.doubleQuat)`

### KSA.Ray

- `Brutal.Numerics.double3 Direction`
- `Brutal.Numerics.double3 Origin`

### KSA.ReadOnlyPhysicsStates

- `ref KSA.BubbleOrigin Origin`

### KSA.Rendering.Water.Data.OceanReference

- `KSA.DensityReference Density`
- `KSA.DistanceReference Level`

### KSA.ScreenshotCapture

- `void Request(int, string)`

### KSA.SerializedId

- `string get_Id()`

### KSA.ShadowRenderable

- `KSA.InstanceData InstanceData`
- `RenderCore.Systems.GlobalMeshBucketHandle BucketHandle`
- `float Radius`

### KSA.ShapesUnlock

- `BepuPhysics.Collidables.Shapes get_Shapes()`

### KSA.SimSpeed

- `void .ctor(double)`

### KSA.SimStep

- `KSA.UniverseTime get_NextTime()`
- `double get_DeltaTime()`

### KSA.SoundBehavior

*referenced as a type only*

### KSA.SpatialAudio

- `void .ctor(KSA.Astronomical, Brutal.Numerics.double3)`

### KSA.StaticMeshRenderable

- `Brutal.Numerics.float4x4 Transform`
- `KSA.GltfPbrAssetRef GltfAssetRef`
- `void Dispose()`
- `void Draw(RenderCore.Systems.ViewHandle)`

### KSA.StaticObject

- `BepuPhysics.Collidables.TypedIndex get_CollisionShape()`
- `double get_FootprintRadius()`
- `double get_GroundOffset()`
- `double get_SurfaceHeight()`

### KSA.StaticObjectModel

- `Template Template`

### KSA.StaticsShouldntAwakenBodies

- `ref KSA.StaticsShouldntAwakenBodies get_Shared()`

### KSA.StellarBody

*referenced as a type only*

### KSA.SuperMeshRenderSystem

- `KSA.MeshRenderTechnique MeshRendererStaticPbr`
- `KSA.MeshRenderTechnique MeshRendererStaticPrePass`
- `RenderCore.Systems.ViewHandle ViewForViewport(KSA.IViewport)`
- `System.Collections.Generic.List`1<KSA.ShadowRenderable> ShadowRenderablePool`
- `System.Span`1<Brutal.Numerics.float4x4> AllocateSkinningMatrices(int, ref int)`

### KSA.Transform3D

- `Brutal.Numerics.double3 get_PositionEcl()`
- `Brutal.Numerics.doubleQuat LocalRotation`
- `void set_PositionEcl(Brutal.Numerics.double3)`

### KSA.Universe

- `KSA.CelestialSystem get_CurrentSystem()`
- `KSA.SimStep GetLastSimStep()`
- `KSA.UniverseTime GetElapsedTime()`
- `bool IsPaused()`
- `bool get_IsAutoWarpActive()`
- `double get_SimulationSpeed()`
- `void DestroyVehicle(KSA.Vehicle, KSA.CrewDisposition)`
- `void DestroyVehicleFromEvent(KSA.Vehicle, KSA.VehicleDestructionEvent)`
- `void ExecuteNextVehicleSolvers(double, KSA.SimStep)`
- `void SetSimulationSpeed(KSA.SimSpeed)`

### KSA.UniverseTime

- `System.Int128 get_Nanoseconds()`
- `bool Equals(KSA.UniverseTime)`

### KSA.Vehicle

- `Brutal.Numerics.byte4 get_OrbitColor()`
- `Brutal.Numerics.double3 get_BodyRates()`
- `Brutal.Numerics.double4x4 GetMatrixAsmb2Ego(Brutal.Numerics.double3)`
- `Brutal.Numerics.doubleQuat get_Body2Cce()`
- `Brutal.Numerics.float3 get_CenterOfMassAsmbF()`
- `KSA.IParentBody get_Parent()`
- `KSA.PartTree get_Parts()`
- `bool get_IsDebris()`
- `bool get_IsDisposed()`
- `int get_BubbleVehicleCount()`
- `void ClearHeldPlayerInput()`
- `void Teleport(KSA.Orbit, System.Nullable`1<Brutal.Numerics.doubleQuat>, System.Nullable`1<Brutal.Numerics.double3>)`
- `void TeleportToLocation(KSA.Celestial, double, double)`
- `void UpdateRenderData(KSA.IViewport, int)`

### KSA.VehicleDestructionCause

*referenced as a type only*

### KSA.VehicleDestructionEvent

- `KSA.VehicleDestructionCause Cause`
- `float PeakDynamicPressure`
- `float PeakGLoad`
- `void .ctor()`

### KSA.VehicleEditor

*referenced as a type only*

### KSA.VehicleUpdateState

- `KSA.ReadOnlyPhysicsStates GetReadOnlyStates()`

### KSA.ViewportRegistry

- `System.ReadOnlySpan`1<KSA.IGameViewport> get_GameViews()`

## StarMap.API

### StarMap.API.StarMapAfterGuiAttribute

- `void .ctor()`

### StarMap.API.StarMapAfterOnFrameAttribute

- `void .ctor()`

### StarMap.API.StarMapAllModsLoadedAttribute

- `void .ctor()`

### StarMap.API.StarMapBeforeGuiAttribute

- `void .ctor()`

### StarMap.API.StarMapImmediateLoadAttribute

- `void .ctor()`

### StarMap.API.StarMapModAttribute

- `void .ctor()`

### StarMap.API.StarMapUnloadAttribute

- `void .ctor()`
