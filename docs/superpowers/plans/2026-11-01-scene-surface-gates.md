# Scene Surface Gates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fit the existing gate formation inside Floor or Table scene surfaces and use the correct surface height.

**Architecture:** `PicoFloorArea` owns surface height, planar bounds, and containment. The scene provider creates it from Floor polygons or Table Box3D top faces. `Game` performs one bounded uniform-scale/translate fit after headset alignment.

**Tech Stack:** Unity 2022.3, C#, PICO XR Scene Capture, NUnit PlayMode tests.

**Spec:** `docs/superpowers/specs/2026-11-01-scene-surface-gates-design.md`

## Global Constraints
- Camera Offset and XR Origin stay at Position/Rotation `(0,0,0)`.
- Table uses Box3D, not `GetScenePolygonData`.
- Existing gate formation is preserved; scaling is uniform and never increased above 1.

## Review Focus
- Zero-width gate formations: no division by zero; test in Task 3.
- Concave Floor polygons: all gate centers must pass `ContainsXZ`; test bounded shrinking in Task 3.
- Table top height: use maximum top-vertex Y, not average; test in Task 1.
- Empty query: retain current fallback height/positions; existing gate tests cover fallback in Task 3.
- Box3D rotation is officially unsupported: only anchor rotation is applied; test anchor rotation in Task 1.

---

### Task 1: Surface Data Model

**Files:**
- Modify: `Assets/Scripts/Pico/PicoFloorArea.cs`
- Create: `Assets/Tests/PlayMode/PicoFloorAreaTests.cs`

**Interfaces:**
- Produces: `PicoFloorArea.SurfaceHeight`, `PicoFloorArea.PlanarBounds`, `PicoFloorArea.ContainsXZ(Vector3)`, `PicoFloorArea.FromBox3D(...)`, and an optional highest-vertex constructor flag.

- [ ] Write failing tests for Floor average height, Table highest-top-vertex height, top-face bounds, anchor rotation, containment, and random points using `SurfaceHeight`.
- [ ] Run PlayMode tests and confirm the new API is missing.
- [ ] Implement the model while keeping the existing constructor compatible.
- [ ] Run the tests and confirm they pass.

### Task 2: Floor/Table Scene Provider

**Files:**
- Modify: `Assets/Scripts/Pico/Scene/PicoSceneCaptureFloorAreaProvider.cs`
- Modify: `Assets/Scenes/BikeScene.unity`
- Test: `Assets/Tests/PlayMode/PicoFloorAreaTests.cs`

**Interfaces:**
- Consumes: `PicoFloorArea.FromBox3D(...)`.
- Produces: provider `surfaceLabel`, default `PxrSemanticLabel.Floor`; Table creates top-face areas with highest-vertex height.

- [ ] Add a failing model test that defines the expected Table top-face conversion.
- [ ] Branch query/data extraction by `surfaceLabel`: Floor uses Polygon, Table uses Box3D.
- [ ] Keep debug status accurate for the selected label and leave scene defaults at Floor.
- [ ] Run surface tests and inspect compiler output.

### Task 3: Fit Gate Formation

**Files:**
- Modify: `Assets/Scripts/Game.cs`
- Test: `Assets/Tests/PlayMode/GateGroundAlignmentTests.cs`

**Interfaces:**
- Consumes: `SurfaceHeight`, `PlanarBounds`, `ContainsXZ(Vector3)`.
- Produces: `AlignGatesToArea(PicoFloorArea area)` called after a successful query; `EnableGate` keeps the fitted height.

- [ ] Write failing tests proving all gate centers and the Gates root fit inside a smaller rectangle, uniform scale never exceeds 1, relative formation is preserved, zero-width formations do not divide by zero, and all Y values equal `SurfaceHeight`.
- [ ] Implement bounded center/scale fitting, including containment checks and a safe fallback for degenerate formations.
- [ ] Call the fitting method from `AlignGatesToGround()` instead of only assigning Y.
- [ ] Run the full PlayMode suite and verify no existing gate behavior regresses.
