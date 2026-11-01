# Scene Surface Gates Design

## Intent
Place the Gates root and all four gate centers inside the detected scene surface while preserving their formation. Support Floor polygons and Table Box3D surfaces.

## Decisions
- Keep `AlignGatesToHeadset()` as the initial formation/orientation step.
- After surface query, uniformly shrink the formation only when needed, translate it to the surface center, and set every gate's Y to the surface height. Do not enlarge a formation that already fits.
- Check actual polygon containment, shrinking further around the target center when necessary. The Gates root is moved to the same target center without changing child world positions.
- Floor height remains `PicoFloorArea.AverageHeight`.
- Table height is the highest Y of the four Box3D top-face vertices.
- Table uses `LocateAnchor` + `GetSceneBox3DData`; it does not call `GetScenePolygonData` because Table is Box3D in the official SDK.
- `PicoFloorArea.SurfaceHeight` returns the appropriate height and is also used by `TryGetRandomPoint`.

## Constraints
- XR Origin and Camera Offset remain at Position/Rotation `(0,0,0)` per official Scene Capture requirements.
- Existing fallback behavior remains when no surface is available.
- The surface label is selectable on the provider and defaults to Floor.

## Non-goals
- No independent random gate placement.
- No obstacle avoidance or arbitrary concave-polygon largest-rectangle solver.
