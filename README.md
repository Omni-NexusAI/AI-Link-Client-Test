# AI-Link Unity Client (Quest Prototype)

This project is the Unity prototype for AI-Link, a client designed to run on Meta Quest headsets. It acts as a real-time sensor node for the AI Nexus system, streaming passthrough video and microphone audio, while rendering responses (video textures, audio) received from the server back in the headset.

The primary goal is to achieve low-latency (< 250ms) multimodal streaming and provide a clean code scaffold for comparison with a parallel Godot implementation.

## Prerequisites

*   **Unity Hub**
*   **Unity Editor:** Version 6.x.x (Developed with Unity 6)
*   **Meta XR SDK:** Installed via Unity Package Manager and configured for Quest development.
*   **Required Libraries:**
    *   **WebSocketSharp:** Download `websocket-sharp.dll` (e.g., from NuGet or GitHub releases) and place it in `Assets/Plugins`.
    *   **Concentus:** Download `Concentus.dll` and `Concentus.OggFile.dll` (e.g., from NuGet) and place them in `Assets/Plugins`.
*   **Android Build Support:** Installed for Unity Editor (including SDK, NDK, JDK).
*   **Meta Quest Headset:** Enabled for Developer Mode.
*   **Meta Quest Developer Hub (MQDH)** or **Android Debug Bridge (adb):** For sideloading builds.

## Setup Instructions

1.  **Clone/Obtain Project:** Get the project files (this directory).
2.  **Open in Unity:** Open this project folder using Unity Hub (select Unity 6.x.x).
3.  **Install Libraries:** Place the required `.dll` files (WebSocketSharp, Concentus) into an `Assets/Plugins` folder (create it if it doesn't exist).
4.  **Configure Meta XR SDK:** Ensure the Meta XR Plugin is enabled in Project Settings -> XR Plug-in Management for the Android tab. Configure settings as needed (e.g., permissions, Passthrough feature enabled in OVRManager).
5.  **Configure `AppConfig.json`:** Edit the `Assets/StreamingAssets/AppConfig.json` file. At minimum, set the correct `serverUrl` for your AI Nexus WebSocket server.
6.  **Scene Setup:**
    *   Open the main scene (e.g., `Assets/Scenes/SampleScene.unity` or create a new one).
    *   Ensure an `XROrigin` prefab (configured for Quest controllers/headset) is present.
    *   Create an empty GameObject named `AILinkSystem`.
    *   Attach the following scripts (from `Assets/Scripts`) as components to `AILinkSystem`:
        *   `ConfigManager`
        *   `WebSocketClient`
        *   `AudioManager` (requires an `AudioSource` component - add one)
        *   `VideoManager`
        *   `LatencyMonitor`
        *   `AILinkClient`
    *   Create a UI Canvas in the scene (World Space recommended for VR).
    *   Add three UI Text elements (or TextMeshProUGUI) to the Canvas for the Latency HUD.
    *   Create an empty GameObject named `LatencyHUDManager`. Attach the `LatencyHUD.cs` script. Drag the three Text elements onto the corresponding fields in the `LatencyHUD` component in the Inspector.
    *   Create a 3D Quad or UI RawImage in the scene to display the received video. Drag this object onto the `Video Display Image` field in the `AILinkClient` component in the Inspector.
    *   Ensure Camera permissions are handled (logic commented in `VideoManager.cs` or via Manifest).

## Build Instructions (Quest APK)

1.  Go to `File -> Build Settings...`.
2.  Select `Android` as the platform. Click `Switch Platform`.
3.  Ensure your main scene is added to the `Scenes In Build`.
4.  Under `Player Settings... -> Other Settings`:
    *   Set `Minimum API Level` (check Meta Quest requirements, often API Level 29+).
    *   Set `Target API Level` to Automatic or a specific required level.
    *   Configure `Scripting Backend` (IL2CPP recommended).
    *   Configure `Target Architectures` (ensure ARM64 is checked).
5.  Under `Player Settings... -> XR Plug-in Management -> Android tab`:
    *   Ensure `Meta Quest` is checked.
6.  Click `Build`. Choose a location and name for your APK file.

## Sideloading Instructions

Use **Meta Quest Developer Hub (MQDH)**:
1.  Connect your Quest headset via USB.
2.  Open MQDH.
3.  Ensure your device is connected and authorized.
4.  Drag and drop the built `.apk` file onto the device in MQDH, or use the `Install APK` button.

Use **adb**:
1.  Connect your Quest headset via USB.
2.  Ensure `adb` is in your system's PATH.
3.  Open a terminal or command prompt.
4.  Verify connection: `adb devices` (ensure device shows up and is authorized).
5.  Install the APK: `adb install path/to/your/AI-Link.apk`

After installation, find and launch `AI-Link` from the `Unknown Sources` section in your Quest's app library.
