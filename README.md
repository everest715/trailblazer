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
- Coins and power ups prefer PICO Scene Capture floor anchors and fall back to configurable fixed safe areas when room data is unavailable.
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

## Spawn areas
`SpawnPowerUps` and `SpawnCoinsAtStart` use `PicoSceneSpawnPositions` with `PicoSceneCaptureFloorAreaProvider`. If Scene Capture cannot provide a floor, `PicoSpawnPositions` is used as the fallback. Adjust the fallback component's `spawnBounds` in the Inspector so generated objects stay inside the intended safe play area and outside the player's starting position.

Do not use the old Quest boundary-disabling workaround. PICO safety, passthrough and room-calibration behavior must follow the PICO XR documentation.
