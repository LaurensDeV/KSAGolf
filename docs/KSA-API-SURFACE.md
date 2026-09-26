# KSA API surface

Every external type and member `KSAGolf.dll` binds to, read out of its
metadata tables by `tools/api-surface.sh`. **Generated - do not edit.**

This is the checklist for a KSA update: anything here that changed shape in the new
build is a breaking change for this mod, and anything not here cannot be. See the
`upgrade-ksa` skill, which diffs the decompiled sources against exactly this list.

100 types and 261 members across 6 assemblies.

## Brutal.Concurrency

### Brutal.Concurrency.Jobs.JobScheduler

- `void Wait()`

## Brutal.Core.Numerics

### Brutal.Numerics.byte4

*referenced as a type only*

### Brutal.Numerics.double2

- `double X`
- `double Y`

### Brutal.Numerics.double3

- `Brutal.Numerics.double3 Cross(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `Brutal.Numerics.double3 get_Zero()`
- `Brutal.Numerics.double3 op_Addition(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `Brutal.Numerics.double3 op_Division(Brutal.Numerics.double3, double)`
- `Brutal.Numerics.double3 op_Multiply(Brutal.Numerics.double3, double)`
- `Brutal.Numerics.double3 op_Subtraction(Brutal.Numerics.double3, Brutal.Numerics.double3)`
- `Brutal.Numerics.double3 op_UnaryNegation(Brutal.Numerics.double3)`
- `bool Equals(Brutal.Numerics.double3)`
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

*referenced as a type only*

### Brutal.Numerics.doubleQuat

- `Brutal.Numerics.double3 op_Multiply(Brutal.Numerics.doubleQuat, Brutal.Numerics.double3)`
- `Brutal.Numerics.doubleQuat Concatenate(Brutal.Numerics.doubleQuat, Brutal.Numerics.doubleQuat)`
- `Brutal.Numerics.doubleQuat CreateFromAxisAngle(Brutal.Numerics.double3, double)`
- `Brutal.Numerics.doubleQuat get_Identity()`
- `Brutal.Numerics.doubleQuat op_Multiply(Brutal.Numerics.doubleQuat, Brutal.Numerics.doubleQuat)`

### Brutal.Numerics.float2

- `float X`
- `float Y`
- `void .ctor(float, float)`

### Brutal.Numerics.float3

- `Brutal.Numerics.float3 Normalize(Brutal.Numerics.float3)`
- `Brutal.Numerics.float3 get_One()`
- `Brutal.Numerics.float3 op_Addition(Brutal.Numerics.float3, Brutal.Numerics.float3)`
- `Brutal.Numerics.float3 op_Multiply(Brutal.Numerics.float3, Brutal.Numerics.float3)`
- `Brutal.Numerics.float3 op_Multiply(Brutal.Numerics.float3, float)`
- `float LengthSquared()`
- `float X`
- `float Y`
- `float Z`
- `void .ctor(float, float, float)`

### Brutal.Numerics.float4

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
- `Brutal.Numerics.float4x4 CreateRotationX(float)`
- `Brutal.Numerics.float4x4 CreateRotationY(float)`
- `Brutal.Numerics.float4x4 CreateRotationZ(float)`
- `Brutal.Numerics.float4x4 CreateScale(float)`
- `Brutal.Numerics.float4x4 CreateTranslation(Brutal.Numerics.float3)`
- `Brutal.Numerics.float4x4 op_Multiply(Brutal.Numerics.float4x4, Brutal.Numerics.float4x4)`
- `void .ctor(float, float, float, float, float, float, float, float, float, float, float, float, float, float, float, float)`

### Brutal.Numerics.floatQuat

- `Brutal.Numerics.floatQuat Concatenate(Brutal.Numerics.floatQuat, Brutal.Numerics.floatQuat)`
- `Brutal.Numerics.floatQuat CreateFromAxisAngle(Brutal.Numerics.float3, float)`

### Brutal.Numerics.int2

- `int X`
- `int Y`

## Brutal.Glfw

### Brutal.GlfwApi.GlfwButtonAction

*referenced as a type only*

### Brutal.GlfwApi.GlfwCursorMode

*referenced as a type only*

### Brutal.GlfwApi.GlfwModifier

*referenced as a type only*

### Brutal.GlfwApi.GlfwMouseButton

*referenced as a type only*

### Brutal.GlfwApi.GlfwWindow

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
- `bool IsMouseClicked(Brutal.ImGuiApi.ImGuiMouseButton, bool)`
- `bool IsMouseDown(Brutal.ImGuiApi.ImGuiMouseButton)`
- `bool MenuItem(Brutal.ImGuiApi.ImString, Brutal.ImGuiApi.ImString, ref bool, bool)`
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

- `ref bool get_KeyShift()`
- `ref bool get_WantCaptureMouse()`

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
- `RenderCore.Animation.Skeleton Skeleton`
- `System.Collections.Generic.List`1<KSA.IAnimProcessor> AnimProcessors`
- `bool ShouldUpdate`

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

### KSA.Camera

- `Brutal.Numerics.double3 EgoToEcl(Brutal.Numerics.double3)`
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
- `Brutal.Numerics.doubleQuat GetCci2Cce()`
- `double GetLatitudeFromCce(Brutal.Numerics.double3)`
- `double GetLongitudeFromCce(Brutal.Numerics.double3)`
- `double GetTerrainHeightFromDirCce(Brutal.Numerics.double3, bool)`
- `double get_MaxTerrainHeightApprox()`

### KSA.CelestialSystem

- `KSA.Astronomical GetIndex(int)`
- `KSA.LookupCollection`1<KSA.Astronomical> get_All()`
- `int get_Count()`

### KSA.CharacterAvatar

- `CharacterAttachments Attachments`
- `CharacterCore Core`

### KSA.CharacterAvatar+CharacterAttachments

- `Helmet Helmet`
- `Mmu Mmu`
- `System.Collections.Generic.List`1<CosmeticAttachment> CosmeticAttachments`

### KSA.CharacterAvatar+CharacterCore

- `KSA.AnimatedRenderable CharacterModel`

### KSA.CharacterAvatar+CosmeticAttachment

- `Brutal.Numerics.float4x4 Transform`
- `KSA.StaticMeshRenderable Mesh`
- `int SocketIndex`
- `void .ctor()`

### KSA.CharacterAvatar+Helmet

- `KSA.StaticMeshRenderable HelmetMesh`
- `bool Enabled`

### KSA.CharacterAvatar+Mmu

- `bool Enabled`

### KSA.Constants

- `string get_DocumentsFolderPath()`

### KSA.ConstraintSim

- `KSA.ShapesUnlock UnlockShapesBlocking()`

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
- `void .ctor(KSA.Camera, string)`
- `void OnFrame(KSA.IViewport, double)`

### KSA.GameSaves

- `void LoadSaveGame(string)`

### KSA.GizmosRenderer

- `void DrawLine(Brutal.Numerics.double3, Brutal.Numerics.double3, Brutal.Numerics.float4)`
- `void DrawSphere(Brutal.Numerics.double3, float, Brutal.Numerics.float4)`

### KSA.GltfPbrAssetRef

- `KSA.GpuObjectAssetRef[] Materials`

### KSA.GpuObjectAssetRef

*referenced as a type only*

### KSA.IAnimProcessor

*referenced as a type only*

### KSA.IFollowable

- `KSA.OrbitView get_OrbitView()`

### KSA.IGameViewport

- `KSA.Camera get_BaseCamera()`
- `KSA.FixedController get_FixedController()`
- `KSA.OrbitController get_OrbitController()`
- `void SetCameraMode(KSA.CameraMode)`

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

### KSA.JobSystems

- `Brutal.Concurrency.Jobs.JobScheduler VehicleSolver`

### KSA.KittenEva

- `KSA.KittenRenderable get_Renderable()`

### KSA.KittenRenderable

*referenced as a type only*

### KSA.LoadedAssetRef

*referenced as a type only*

### KSA.LookupCollection`1

*referenced as a type only*

### KSA.MeshRenderTechnique

*referenced as a type only*

### KSA.MeshViewModule

*referenced as a type only*

### KSA.Mod

- `string get_Id()`

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

- `KSA.Camera GetMainCamera()`
- `KSA.Camera GetRenderCamera()`
- `KSA.GizmosRenderer GizmosRenderer`
- `KSA.IGameViewport get_MainViewport()`
- `KSA.Program get_Instance()`
- `KSA.SuperMeshRenderSystem SuperMeshRenderSystem`
- `KSA.Vehicle get_ControlledVehicle()`
- `KSA.VehicleEditor Editor`
- `System.ReadOnlySpan`1<KSA.Vehicle> get_VehiclesInFrame()`
- `void OnGameLoaded()`
- `void set_ControlledVehicle(KSA.Vehicle)`

### KSA.QuaternionEx

- `Brutal.Numerics.doubleQuat Inverse(Brutal.Numerics.doubleQuat)`

### KSA.Ray

- `Brutal.Numerics.double3 Direction`
- `Brutal.Numerics.double3 Origin`

### KSA.Rendering.Water.Data.OceanReference

- `KSA.DensityReference Density`
- `KSA.DistanceReference Level`

### KSA.ScreenshotCapture

- `void Request(int, string)`

### KSA.ShapesUnlock

*referenced as a type only*

### KSA.SimSpeed

- `void .ctor(double)`

### KSA.SimStep

- `KSA.UniverseTime get_NextTime()`
- `double get_DeltaTime()`

### KSA.StaticMeshRenderable

- `Brutal.Numerics.float4x4 Transform`
- `KSA.GltfPbrAssetRef GltfAssetRef`
- `void Dispose()`

### KSA.StellarBody

*referenced as a type only*

### KSA.SuperMeshRenderSystem

- `KSA.MeshRenderTechnique MeshRendererStaticPbr`
- `KSA.MeshRenderTechnique MeshRendererStaticPrePass`

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
- `void SetSimulationSpeed(KSA.SimSpeed)`

### KSA.UniverseTime

- `System.Int128 get_Nanoseconds()`
- `bool Equals(KSA.UniverseTime)`

### KSA.Vehicle

- `Brutal.Numerics.byte4 get_OrbitColor()`
- `Brutal.Numerics.double4x4 GetMatrixAsmb2Ego(Brutal.Numerics.double3)`
- `Brutal.Numerics.doubleQuat get_Body2Cce()`
- `KSA.IParentBody get_Parent()`
- `KSA.PartTree get_Parts()`
- `bool get_IsDebris()`
- `bool get_IsDisposed()`
- `int get_BubbleVehicleCount()`
- `void Teleport(KSA.Orbit, System.Nullable`1<Brutal.Numerics.doubleQuat>, System.Nullable`1<Brutal.Numerics.double3>)`
- `void TeleportToLocation(KSA.Celestial, double, double)`

### KSA.VehicleDestructionCause

*referenced as a type only*

### KSA.VehicleDestructionEvent

- `KSA.VehicleDestructionCause Cause`
- `float PeakDynamicPressure`
- `float PeakGLoad`
- `void .ctor()`

### KSA.VehicleEditor

*referenced as a type only*

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
