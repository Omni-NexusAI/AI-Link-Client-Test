using UnityEngine;
using System;
using System.IO;

// Define classes to match the JSON structure
[Serializable]
public class AudioConfig
{
    public int sampleRate = 16000;
    public bool recordingEnabled = true;
    public bool playbackEnabled = true;
    public string codec = "Opus";
    public int opusBitrate = 64000;
}

[Serializable]
public class VideoConfig
{
    public int captureFps = 30;
    public int captureWidth = 1280;
    public int captureHeight = 720;
    public bool captureEnabled = true;
    public bool displayEnabled = true;
    public string codec = "H264";
    public int jpegQuality = 80; // Note: Used for initial testing, H264 is target
    public int h264Bitrate = 25000000;
}

[Serializable]
public class LatencyConfig
{
    public bool logToFile = true;
    public string logFileName = "LatencyMonitor.log";
    public int historySize = 30;
}

[Serializable]
public class AppConfiguration
{
    public string serverUrl = "ws://localhost:8080";
    public string authToken = "";
    public float reconnectDelaySeconds = 1.0f;
    public int maxReconnectAttempts = 5;
    public AudioConfig audio = new AudioConfig();
    public VideoConfig video = new VideoConfig();
    public LatencyConfig latency = new LatencyConfig();
}

/// <summary>
/// Loads and provides access to application configuration from AppConfig.json.
/// Uses a Singleton pattern for easy access.
/// </summary>
public class ConfigManager : MonoBehaviour
{
    public static ConfigManager Instance { get; private set; }

    public AppConfiguration Config { get; private set; }

    private string configFilePath;

    /// <summary>
    /// Cleans up static references when the domain is reloaded.
    /// This is critical for preventing crashes on domain reload (e.g., when entering/exiting play mode).
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method is automatically called by Unity when the domain is reloaded.
    /// It ensures that static references are cleared to prevent issues with stale references.
    ///
    /// The [RuntimeInitializeOnLoadMethod] attribute with SubsystemRegistration timing ensures
    /// this runs at the right time during domain reload.
    ///
    /// IMPORTANT: All singleton classes with static references should implement this pattern
    /// to prevent crashes on domain reload. This is especially important for ConfigManager
    /// since it's often referenced by other managers during initialization.
    /// </remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainCleanup()
    {
        // This method is called when the domain is reloaded (e.g., when entering/exiting play mode)
        Debug.Log("ConfigManager: Domain reload cleanup");

        // Register with PlayModeCounter before cleanup
        if (Instance != null)
        {
            PlayModeCounter.RegisterResource("ConfigManager.Instance", false);
        }

        // Clean up static instance to prevent issues on domain reload
        Instance = null;
    }

    void Awake()
    {
        // --- Singleton Pattern Implementation ---
        if (Instance != null && Instance != this)
        {
            // If an instance already exists and it's not this one, destroy this one.
            Debug.LogWarning($"Duplicate ConfigManager instance detected. Destroying the new one (existing: {Instance.gameObject.name}, this: {gameObject.name})");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // Keep this object alive across scene changes, if necessary.
        // DontDestroyOnLoad(gameObject);
        // --- End Singleton Pattern ---

        // Register with PlayModeCounter
        PlayModeCounter.RegisterResource("ConfigManager.Instance", true);

        // Construct the full path to the config file
        configFilePath = Path.Combine(Application.streamingAssetsPath, "AppConfig.json");

        LoadConfig();
    }

    void OnDisable()
    {
        // Don't clear the static instance here as it might be needed by other components
        // during their own OnDisable/OnDestroy calls
        Debug.Log("ConfigManager: OnDisable");
    }

    void OnDestroy()
    {
        Debug.Log("ConfigManager: OnDestroy");

        // Only clear the static instance if this is the current instance
        if (Instance == this)
        {
            Debug.Log("ConfigManager: Clearing static Instance reference");
            Instance = null;

            // Register with PlayModeCounter that this instance has been cleaned up
            PlayModeCounter.UnregisterResource("ConfigManager.Instance");
        }
    }

    public void LoadConfig()
    {
        // Rationale: Load configuration from StreamingAssets for read-only access after build.
        Debug.Log($"Attempting to load configuration from: {configFilePath}");

        if (!File.Exists(configFilePath))
        {
            Debug.LogError($"Configuration file not found at: {configFilePath}. Using default values.");
            Config = new AppConfiguration(); // Use default values if file doesn't exist
            // Attempt to create a default config file if it doesn't exist
            // Note: This might fail on platforms where StreamingAssets is read-only
            try
            {
                string directoryPath = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                string defaultConfigJson = JsonUtility.ToJson(Config, true); // Pretty print
                File.WriteAllText(configFilePath, defaultConfigJson);
                Debug.Log($"Created default configuration file at: {configFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not create default config file: {e.Message}");
            }
            return;
        }

        try
        {
            string jsonContent = File.ReadAllText(configFilePath);
            Config = JsonUtility.FromJson<AppConfiguration>(jsonContent);

            if (Config == null)
            {
                Debug.LogError($"Failed to parse configuration file: {configFilePath}. Using default values.");
                Config = new AppConfiguration(); // Fallback to defaults on parsing error
            }
            else
            {
                Debug.Log("Configuration loaded successfully.");
                // Optional: Log loaded values for verification
                // Debug.Log($"Server URL: {Config.serverUrl}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading configuration file: {e.Message}. Using default values.");
            Config = new AppConfiguration(); // Fallback to defaults on exception
        }
    }

    // Optional: Add methods to save configuration if needed in the future,
    // but note that StreamingAssets is read-only on many platforms after build.
    // Saving runtime changes would typically go to Application.persistentDataPath.
}
