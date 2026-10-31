# Trailblazer

### An Interactive Mixed Reality Cycling Experience

https://github.com/user-attachments/assets/be3d7d73-94d8-47ef-af68-68419d090d60

## Aim of the game
- Collect coins and power ups to increase your score.
- Avoid the trail drawn by your traced path.
- Collide with your trail and lose 20 points.

## Current PICO version

The project targets **PICO OS 6** using **PICO XR**. Quest 3 is no longer a supported target.

Current status:
- PICO XR camera, controller tracking and video passthrough are configured.
- The headset is a free-moving MR observer. `TrackedCar` is the only player collider and is driven by a PICO Motion Tracker.
- Coins and power ups prefer PICO Scene Capture floor anchors and fall back to configurable fixed safe areas when room data is unavailable.
- Car collisions keep the existing game interactions and create a local effect.
- Device verification has been skipped for now; PICO OS 6 behavior is not yet confirmed on hardware.

## Requirements
- Unity **2022.3.62f3** with Android Build Support, Android SDK/NDK and OpenJDK.
- **PICO Unity SDK 6.1.1**.
- Android target: IL2CPP, ARM64, minimum SDK 32.

PICO SDK source:
```text
https://github.com/Pico-Developer/PICO-Unity-SDK.git?path=/XR#main
```

## Open and build
1. Clone and open the project in Unity 2022.3.62f3.
2. Open `Assets/Scenes/BikeScene.unity`.
3. Open `PICO > Portal`, select **PICO XR**, and check **Project Validation**.
4. Select `File > Build Settings > Android`.
5. Ensure `BikeScene` is the only enabled scene, then build the APK.

The current development build output is:
```text
Build/PICO/Trailblazer.apk
```

Install with ADB when a PICO device is connected:
```bash
adb install -r Build/PICO/Trailblazer.apk
```

## Observer and tracked car

The headset only renders the MR scene and does not drive the player, trail or gates. `MotionTrackerDriver` moves `TrackedCar` using valid poses from `PicoMotionTrackerPoseSource`; invalid poses leave the car stationary.

Before using the car:

1. Enable **Body Tracking** in `PXR_Manager` (already enabled in project settings).
2. Pair one PICO Motion Tracker in the PICO tracker app and use motion tracking mode.
3. Keep the tracker within about one meter and visible to the headset.
4. Adjust `MotionTrackerDriver.positionOffset` and `rotationOffset` for the tracker's mounting orientation.

The original Quest `FollowCamera` behavior has been removed. `DrawTrail.player` now references `TrackedCar`.

Input is configured as **Hands Only**: the APK manifest has no `controller` requirement. On Swan, also enable **Settings > Lab > Hand Tracking** and use **Hands and controllers auto-switch**. The two controller objects in `BikeScene` are disabled.

## Spawn areas
`SpawnPowerUps` and `SpawnCoinsAtStart` use `PicoSceneSpawnPositions` with `PicoSceneCaptureFloorAreaProvider`. If Scene Capture cannot provide a floor, `PicoSpawnPositions` is used as the fallback. Adjust the fallback component's `spawnBounds` in the Inspector so generated objects stay inside the intended safe play area and outside the player's starting position.

The current miniature layout uses a **1.5m x 1.5m** play area centered **1.5m ahead of the initial headset position**. The car is configured as **15cm long**, while gates, pickups, collision effects and trail width use **25%** scale. `playAreaBounds` filters both Scene Capture and fallback candidates.

Do not use the old Quest boundary-disabling workaround. PICO safety, passthrough and room-calibration behavior must follow the PICO XR documentation.
