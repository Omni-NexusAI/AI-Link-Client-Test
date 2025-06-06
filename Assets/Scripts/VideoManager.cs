using UnityEngine;
using System;
using System.Collections;
using System.Linq; // For finding WebCamDevice
using UnityEngine.Rendering; // For Graphics.Blit
using UnityEngine.Android; // Potentially needed for runtime permissions

#if UNITY_EDITOR
using UnityEngine.SceneManagement; // For scene unload handling
#endif

/// <summary>
/// Manages video capture (Meta Quest passthrough), encoding (initially JPEG),
/// and display of received video frames.
/// Requires Meta XR SDK for actual passthrough functionality.
/// </summary>
/// <remarks>
/// IMPORTANT RESOURCE MANAGEMENT NOTES:
/// 1. This class manages critical texture resources that must be properly released:
///    - WebCamTexture for passthrough/camera feed
///    - RenderTexture for efficient copying
///    - Texture2D objects for frame processing and display
/// 2. OnDisable calls CleanupResources(false) to pause operations without destroying textures
/// 3. OnDestroy and OnApplicationQuit call CleanupResources(true) for full cleanup
/// 4. The CleanupResources method handles different cleanup levels based on the fullCleanup parameter
/// 5. When modifying this class, ensure all texture resources are properly released
///    to prevent memory leaks and crashes on domain reload
/// 6. Coroutines are properly stopped in CleanupResources to prevent orphaned operations
/// </remarks>
public class VideoManager : MonoBehaviour
{
    // Static tracking for active resources
    private static int s_ActiveWebCamTextureCount = 0;
    private static int s_ActiveRenderTextureCount = 0;
    private static int s_ActiveTexture2DCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainCleanup()
    {
        // This method is called when the domain is reloaded (e.g., when entering/exiting play mode)
        Debug.Log($"VideoManager: Domain reload cleanup. Active WebCamTextures: {s_ActiveWebCamTextureCount}, RenderTextures: {s_ActiveRenderTextureCount}, Texture2Ds: {s_ActiveTexture2DCount}");

        // Register with PlayModeCounter
        PlayModeCounter.RegisterResource("VideoManager.ActiveWebCamTextures", false);
        PlayModeCounter.RegisterResource("VideoManager.ActiveRenderTextures", false);
        PlayModeCounter.RegisterResource("VideoManager.ActiveTexture2Ds", false);

        // Reset static state
        s_ActiveWebCamTextureCount = 0;
        s_ActiveRenderTextureCount = 0;
        s_ActiveTexture2DCount = 0;
    }

#if UNITY_EDITOR
    // Register for scene unload events to ensure cleanup
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneEvents()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        // Log active resources when a scene is unloaded
        Debug.Log($"VideoManager: Scene '{scene.name}' unloaded. Active WebCamTextures: {s_ActiveWebCamTextureCount}, RenderTextures: {s_ActiveRenderTextureCount}, Texture2Ds: {s_ActiveTexture2DCount}");

        // Register with PlayModeCounter
        PlayModeCounter.RegisterResource("VideoManager.ActiveWebCamTextures", s_ActiveWebCamTextureCount > 0);
        PlayModeCounter.RegisterResource("VideoManager.ActiveRenderTextures", s_ActiveRenderTextureCount > 0);
        PlayModeCounter.RegisterResource("VideoManager.ActiveTexture2Ds", s_ActiveTexture2DCount > 0);
    }
#endif
    // --- Events ---
    public event Action<byte[]> OnFrameCaptured; // Sends encoded frame data (JPEG initially)
    public event Action<Texture2D> OnFrameReceived; // Sends Texture2D for display
    public event Action<float> OnLatencyUpdated; // Reports latency for received packets

    // --- Public State ---
    public bool IsCapturing { get; private set; } = false;

    // --- Configuration (Loaded from ConfigManager) ---
    private int _captureFps = 30;
    private int _captureWidth = 1280; // Target width, actual may vary based on passthrough
    private int _captureHeight = 720; // Target height, actual may vary based on passthrough
    private bool _captureEnabled = true;
    private bool _displayEnabled = true;
    private int _jpegQuality = 80; // For initial JPEG encoding
    // private string _targetCodec = "H264"; // Target, not implemented initially
    // private int _h264Bitrate = 25000000; // Target, not implemented initially

    // --- Private State ---
    private Coroutine _captureCoroutine;
    private Texture2D _receivedTexture;     // Texture for displaying received frames
    private WebCamTexture _passthroughWebcamTexture; // Input texture from Passthrough camera feed
    private RenderTexture _copyRenderTexture;   // Intermediate texture for efficient copying
    private Texture2D _readableTexture;     // CPU-readable texture holding the copied frame

    // --- Meta XR SDK Specific ---
    // While OVRPassthroughLayer handles rendering, WebCamTexture is used for accessing the feed data.
    private bool _isPassthroughInitialized = false;
    private string _passthroughWebcamName = null; // Name of the passthrough webcam device

    void Awake()
    {
        // Register this instance with PlayModeCounter
        PlayModeCounter.RegisterResource($"VideoManager.Instance_{GetInstanceID()}", true);
    }

    void Start()
    {
        if (ConfigManager.Instance == null)
        {
            Debug.LogError("ConfigManager instance not found. VideoManager cannot initialize.");
            enabled = false;
            return;
        }
        LoadConfig();

        // Allocate texture for received frames
        // Rationale: Pre-allocate texture to avoid repeated allocations when receiving frames.
        // Use a format suitable for LoadImage (e.g., RGB24 or RGBA32)
        _receivedTexture = new Texture2D(2, 2, TextureFormat.RGB24, false); // Start small, LoadImage resizes

        // Track texture creation
        s_ActiveTexture2DCount++;
        PlayModeCounter.RegisterResource($"VideoManager.ReceivedTexture_{GetInstanceID()}", true);

        // Attempt to initialize Passthrough and start capture if enabled
        StartCoroutine(InitializeAndStartCapture());
    }

    /// <summary>
    /// Pauses video operations when the component is disabled.
    /// Calls CleanupResources(false) to stop capture but preserve textures.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method stops active operations but doesn't fully destroy textures.
    /// This allows the component to resume operations when re-enabled.
    /// For full cleanup, see OnDestroy.
    /// </remarks>
    void OnDisable()
    {
        Debug.Log("VideoManager: OnDisable - Pausing resources");
        CleanupResources(false);
    }

    /// <summary>
    /// Fully cleans up all resources when the component is destroyed.
    /// Calls CleanupResources(true) to destroy all textures.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method performs complete cleanup of all resources
    /// by calling CleanupResources(true) which destroys all textures.
    ///
    /// IMPORTANT: When adding new resources to this class, ensure they are properly
    /// released in CleanupResources to prevent memory leaks and crashes.
    /// </remarks>
    void OnDestroy()
    {
        Debug.Log("VideoManager: OnDestroy - Fully cleaning up resources");
        CleanupResources(true);
    }

    /// <summary>
    /// Extra safety measure to ensure all resources are released on application quit.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This provides an additional safety net to ensure
    /// all resources are released even if OnDestroy is not called properly.
    /// Unity sometimes has issues with cleanup order during application quit.
    /// </remarks>
    void OnApplicationQuit()
    {
        Debug.Log("VideoManager: OnApplicationQuit - Ensuring all resources are released");
        CleanupResources(true);
    }

    /// <summary>
    /// Cleans up video resources with different levels of cleanup based on the fullCleanup parameter.
    /// </summary>
    /// <param name="fullCleanup">
    /// If true, performs full cleanup including destroying textures.
    /// If false, pauses operations but preserves textures for potential resuming.
    /// </param>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This is the central method for resource cleanup in VideoManager.
    /// It handles:
    /// 1. Stopping capture coroutines
    /// 2. Stopping WebCamTexture
    /// 3. Releasing RenderTexture
    /// 4. Destroying Texture2D objects (if fullCleanup=true)
    ///
    /// IMPORTANT: When adding new resources to this class, ensure they are properly
    /// released in this method to prevent memory leaks and crashes.
    /// </remarks>
    private void CleanupResources(bool fullCleanup)
    {
        // Stop capture first to ensure coroutine is stopped
        StopCapture();

        // Clean up WebCamTexture
        if (_passthroughWebcamTexture != null)
        {
            if (_passthroughWebcamTexture.isPlaying)
            {
                _passthroughWebcamTexture.Stop();
                Debug.Log("VideoManager: Stopped passthrough webcam texture");
            }

            if (fullCleanup)
            {
                Destroy(_passthroughWebcamTexture);
                _passthroughWebcamTexture = null;
                Debug.Log("VideoManager: Destroyed passthrough webcam texture");

                // Update tracking
                s_ActiveWebCamTextureCount = Math.Max(0, s_ActiveWebCamTextureCount - 1);
                PlayModeCounter.RegisterResource($"VideoManager.WebCamTexture_{GetInstanceID()}", false);
            }
        }

        // Clean up other textures
        if (_receivedTexture != null)
        {
            if (fullCleanup)
            {
                Destroy(_receivedTexture);
                _receivedTexture = null;
                Debug.Log("VideoManager: Destroyed received texture");

                // Update tracking
                s_ActiveTexture2DCount = Math.Max(0, s_ActiveTexture2DCount - 1);
                PlayModeCounter.RegisterResource($"VideoManager.ReceivedTexture_{GetInstanceID()}", false);
            }
        }

        if (_copyRenderTexture != null)
        {
            RenderTexture.ReleaseTemporary(_copyRenderTexture);
            _copyRenderTexture = null;
            Debug.Log("VideoManager: Released copy render texture");

            // Update tracking
            s_ActiveRenderTextureCount = Math.Max(0, s_ActiveRenderTextureCount - 1);
            PlayModeCounter.RegisterResource($"VideoManager.RenderTexture_{GetInstanceID()}", false);
        }

        if (_readableTexture != null)
        {
            if (fullCleanup)
            {
                Destroy(_readableTexture);
                _readableTexture = null;
                Debug.Log("VideoManager: Destroyed readable texture");

                // Update tracking
                s_ActiveTexture2DCount = Math.Max(0, s_ActiveTexture2DCount - 1);
                PlayModeCounter.RegisterResource($"VideoManager.ReadableTexture_{GetInstanceID()}", false);
            }
        }

        // Reset state
        _isPassthroughInitialized = false;

        if (fullCleanup)
        {
            // Unregister this instance if fully cleaning up
            PlayModeCounter.UnregisterResource($"VideoManager.Instance_{GetInstanceID()}");

            // Update global resource tracking
            PlayModeCounter.RegisterResource("VideoManager.ActiveWebCamTextures", s_ActiveWebCamTextureCount > 0);
            PlayModeCounter.RegisterResource("VideoManager.ActiveRenderTextures", s_ActiveRenderTextureCount > 0);
            PlayModeCounter.RegisterResource("VideoManager.ActiveTexture2Ds", s_ActiveTexture2DCount > 0);
        }
    }

    private void LoadConfig()
    {
        var config = ConfigManager.Instance.Config.video;
        _captureFps = config.captureFps;
        _captureWidth = config.captureWidth;
        _captureHeight = config.captureHeight;
        _captureEnabled = config.captureEnabled;
        _displayEnabled = config.displayEnabled;
        _jpegQuality = config.jpegQuality;
        // _targetCodec = config.codec; // Store if needed for future H264 impl
        // _h264Bitrate = config.h264Bitrate; // Store if needed for future H264 impl

        Debug.Log($"VideoManager configured: FPS={_captureFps}, Size={_captureWidth}x{_captureHeight}, JPEGQuality={_jpegQuality}");
    }

    private IEnumerator InitializeAndStartCapture()
    {
        // Rationale: Initialization requires checking permissions and finding the device.
        // This might take time or require user interaction, hence the coroutine.

        // --- Request Camera Permission (Android) ---
        // IMPORTANT: This requires the UnityEngine.Android namespace and runtime handling.
        // Add `using UnityEngine.Android;` at the top if implementing this.

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.Log("Requesting camera permission...");
            Permission.RequestUserPermission(Permission.Camera);
            // Wait a bit for the dialog
            yield return new WaitForSeconds(0.5f);
            float timeWaited = 0.5f;
            // Wait longer for user response
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && timeWaited < 10f)
            {
                 yield return new WaitForSeconds(0.5f);
                 timeWaited += 0.5f;
            }
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Debug.LogError("Camera permission denied. Cannot start passthrough capture.");
            _captureEnabled = false; // Disable capture if permission denied
            yield break; // Exit coroutine
        }
        Debug.Log("Camera permission granted.");

        // Debug.LogWarning("Camera permission request logic is commented out. Ensure permissions are handled."); // Remove warning now that it's uncommented
        yield return null; // Allow a frame for potential permission UI

        // --- Find Passthrough Webcam Device ---
        // Rationale: Identify the specific WebCamDevice used for Passthrough.
        // Names might vary; check Meta documentation or log device names.
        // Common names might include "OVR Tracked Camera" or similar.
        // Use FirstOrDefault to handle case where no matching device is found
        var passthroughDevice = WebCamTexture.devices.FirstOrDefault(device =>
            device.name.Contains("Tracked Camera") || // Example name fragment
            device.name.Contains("Passthrough")       // Another example
            // Add more specific identifiers if known
        );
        _passthroughWebcamName = passthroughDevice.name; // Get the name property (will be null if not found)


        if (string.IsNullOrEmpty(_passthroughWebcamName))
        {
            Debug.LogError("Meta Quest Passthrough camera device not found. Available devices:");
            foreach (var device in WebCamTexture.devices) { Debug.Log($"- {device.name}"); }
            _captureEnabled = false;
            yield break;
        }
        Debug.Log($"Found Passthrough camera device: {_passthroughWebcamName}");

        // --- Initialize WebCamTexture ---
        _passthroughWebcamTexture = new WebCamTexture(_passthroughWebcamName, _captureWidth, _captureHeight, _captureFps);

        // Track WebCamTexture creation
        s_ActiveWebCamTextureCount++;
        PlayModeCounter.RegisterResource($"VideoManager.WebCamTexture_{GetInstanceID()}", true);

        _passthroughWebcamTexture.Play(); // Start the camera feed

        // Wait for the webcam to initialize
        float initTimeout = 5f; // 5 second timeout
        float timeElapsed = 0f;
        while (!_passthroughWebcamTexture.isPlaying && timeElapsed < initTimeout)
        {
             if (_passthroughWebcamTexture.didUpdateThisFrame) // Check if it started quickly
             {
                 Debug.Log("WebCamTexture started playing.");
                 break;
             }
             yield return null; // Wait for the next frame
             timeElapsed += Time.deltaTime;
        }

        if (!_passthroughWebcamTexture.isPlaying)
        {
            Debug.LogError($"Failed to start Passthrough WebCamTexture '{_passthroughWebcamName}' within {initTimeout} seconds.");
            _passthroughWebcamTexture.Stop(); // Ensure it's stopped
            _passthroughWebcamTexture = null;
            _captureEnabled = false;
            yield break;
        }

        Debug.Log($"Passthrough WebCamTexture started: {_passthroughWebcamTexture.width}x{_passthroughWebcamTexture.height} @ {_passthroughWebcamTexture.requestedFPS}fps (Actual: {_passthroughWebcamTexture.deviceName})");

        // --- Initialize Buffers for Copying ---
        // Rationale: Create textures needed for the efficient copy process.
        // Use the actual webcam texture dimensions.
        _copyRenderTexture = RenderTexture.GetTemporary(
            _passthroughWebcamTexture.width,
            _passthroughWebcamTexture.height,
            0, // No depth buffer needed
            RenderTextureFormat.Default, // Match format if possible, or use default
            RenderTextureReadWrite.Linear); // Use Linear color space

        // Track RenderTexture creation
        s_ActiveRenderTextureCount++;
        PlayModeCounter.RegisterResource($"VideoManager.RenderTexture_{GetInstanceID()}", true);

        _readableTexture = new Texture2D(
            _passthroughWebcamTexture.width,
            _passthroughWebcamTexture.height,
            TextureFormat.RGB24, // Format for JPEG encoding
            false, // No mipmaps
            true); // Linear color space

        // Track Texture2D creation
        s_ActiveTexture2DCount++;
        PlayModeCounter.RegisterResource($"VideoManager.ReadableTexture_{GetInstanceID()}", true);

        _isPassthroughInitialized = true;

        // --- Start Capture Loop if Enabled ---
        if (_captureEnabled)
        {
            StartCapture();
        }
    }


    public void StartCapture()
    {
        if (IsCapturing || !_captureEnabled) return;

        if (!_isPassthroughInitialized || _passthroughWebcamTexture == null || !_passthroughWebcamTexture.isPlaying)
        {
             Debug.LogWarning("Cannot start capture: Passthrough WebCamTexture is not initialized or running.");
             return;
        }

        // Rationale: Use a coroutine for periodic frame capture.
        if (_captureCoroutine != null) StopCoroutine(_captureCoroutine);
        _captureCoroutine = StartCoroutine(CaptureLoop());
        IsCapturing = true;
        // Debug.Log("Video capture loop started."); // Moved inside loop start
    }

    public void StopCapture()
    {
        if (!IsCapturing) return;

        Debug.Log("VideoManager: Stopping capture...");

        // Stop the capture coroutine
        if (_captureCoroutine != null)
        {
            StopCoroutine(_captureCoroutine);
            _captureCoroutine = null;
            Debug.Log("VideoManager: Stopped capture coroutine");
        }

        // Update state before stopping webcam to prevent race conditions
        IsCapturing = false;

        // Note: We don't stop the WebCamTexture here as it might be reused
        // The actual WebCamTexture cleanup happens in CleanupResources

        Debug.Log("VideoManager: Capture stopped");
    }

    private IEnumerator CaptureLoop()
    {
        // Rationale: Continuously check for new frames and process them.
        // No fixed wait needed if we check didUpdateThisFrame.
        Debug.Log("Entering capture loop...");
        while (IsCapturing)
        {
            // Rationale: Only process if the webcam texture has updated since the last frame.
            if (_passthroughWebcamTexture != null && _passthroughWebcamTexture.didUpdateThisFrame)
            {
                CopyAndProcessFrame();
            }
            // Yield null waits for the next frame, preventing blocking the main thread.
            yield return null;
        }
        Debug.Log("Exited capture loop.");
    }

    private void CopyAndProcessFrame()
    {
        if (_passthroughWebcamTexture == null || _copyRenderTexture == null || _readableTexture == null)
        {
            Debug.LogError("Cannot copy frame: Textures not initialized.");
            return;
        }

        // Rationale: Use Graphics.Blit for efficient GPU-side copy to RenderTexture.
        Graphics.Blit(_passthroughWebcamTexture, _copyRenderTexture);

        // Rationale: ReadPixels copies from the *active* RenderTexture to the CPU-readable Texture2D.
        RenderTexture previousActive = RenderTexture.active; // Store previous active RT
        RenderTexture.active = _copyRenderTexture;
        _readableTexture.ReadPixels(new Rect(0, 0, _copyRenderTexture.width, _copyRenderTexture.height), 0, 0);
        _readableTexture.Apply(); // Apply the pixel changes
        RenderTexture.active = previousActive; // Restore previous active RT

        // Now _readableTexture contains the frame data accessible by the CPU.
        ProcessAndSendFrame(_readableTexture);
    }


    private void ProcessAndSendFrame(Texture2D frameTexture)
    {
        // Rationale: Encode the captured frame (initially JPEG) and emit it.
        if (frameTexture == null) return;
        try
        {
            // --- Initial JPEG Encoding ---
            // Rationale: Use built-in JPEG encoding for simplicity in the first pass.
            // Mark for replacement with H.264.
            // TODO: Replace JPEG with H.264 encoding.
            byte[] encodedData = frameTexture.EncodeToJPG(_jpegQuality);
            // --- End JPEG Encoding ---

            if (encodedData != null && encodedData.Length > 0)
            {
                OnFrameCaptured?.Invoke(encodedData);
            }
            else
            {
                 Debug.LogWarning("Frame encoding produced null or empty data.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Frame processing/encoding error: {e.Message}");
        }
    }


    /// <summary>
    /// Handles received video data packets from the network. Called by AILinkClient.
    /// </summary>
    public void HandleReceivedVideoData(byte[] videoData, long timestamp)
    {
        if (!_displayEnabled || videoData == null || videoData.Length == 0) return;

        // Rationale: Decode the received data (assumed JPEG initially) and update the display texture.
        try
        {
            // --- Initial JPEG Decoding ---
            // TODO: Replace with H.264 decoding when implemented.
            bool loaded = _receivedTexture.LoadImage(videoData); // LoadImage resizes texture if needed
            // --- End JPEG Decoding ---

            if (loaded)
            {
                // Emit the texture for display components (e.g., UI RawImage)
                OnFrameReceived?.Invoke(_receivedTexture);

                // Calculate and report latency
                if (timestamp > 0)
                {
                    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    float latencyMs = nowMs - timestamp;
                    OnLatencyUpdated?.Invoke(latencyMs);
                }
            }
            else
            {
                Debug.LogWarning("Failed to load received image data into texture.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error decoding/displaying received video data: {e.Message}");
        }
    }
}
