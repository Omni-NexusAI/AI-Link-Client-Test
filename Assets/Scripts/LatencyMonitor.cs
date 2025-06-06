using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // For Average calculation

/// <summary>
/// Tracks audio and video round-trip latency, calculates averages, and logs to a file.
/// </summary>
public class LatencyMonitor : MonoBehaviour
{
    // --- Events ---
    // Optional: Event to notify UI or other systems about updated average latency
    // public event Action<string, float> OnAverageLatencyUpdated; // Channel name, Average latency ms

    // --- Configuration ---
    private bool _logToFile = true;
    private string _logFileName = "LatencyMonitor.log";
    private int _historySize = 30;
    private string _logFilePath;

    // --- Private State ---
    private readonly Dictionary<string, List<float>> _latencyHistory = new Dictionary<string, List<float>>();
    private StreamWriter _logWriter;
    private readonly object _logLock = new object(); // Lock for thread-safe file writing

    // Channel names constants
    public const string AudioChannel = "Audio";
    public const string VideoChannel = "Video";
    public const string NetworkChannel = "Network"; // For WebSocket Ping/Pong

    void Start()
    {
        if (ConfigManager.Instance == null)
        {
            Debug.LogError("ConfigManager instance not found. LatencyMonitor cannot initialize.");
            enabled = false;
            return;
        }
        LoadConfig();

        InitializeChannel(AudioChannel);
        InitializeChannel(VideoChannel);
        InitializeChannel(NetworkChannel);

        if (_logToFile)
        {
            InitializeLogFile();
        }
    }

    void OnDestroy()
    {
        // Rationale: Ensure the log file stream is properly closed on exit.
        lock (_logLock)
        {
            _logWriter?.Close();
            _logWriter = null;
        }
    }

    private void LoadConfig()
    {
        var config = ConfigManager.Instance.Config.latency;
        _logToFile = config.logToFile;
        _logFileName = config.logFileName;
        _historySize = Mathf.Max(1, config.historySize); // Ensure history size is at least 1

        // Rationale: Log file path should be in a writable location like persistentDataPath.
        _logFilePath = Path.Combine(Application.persistentDataPath, _logFileName);

        Debug.Log($"LatencyMonitor configured: LogToFile={_logToFile}, LogFile={_logFilePath}, HistorySize={_historySize}");
    }

    private void InitializeChannel(string channelName)
    {
        // Rationale: Prepare history list for each channel.
        if (!_latencyHistory.ContainsKey(channelName))
        {
            _latencyHistory[channelName] = new List<float>(_historySize);
        }
    }

    private void InitializeLogFile()
    {
        // Rationale: Open log file for appending, create if it doesn't exist.
        try
        {
            // Ensure directory exists
            string directory = Path.GetDirectoryName(_logFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Open file stream for appending text
            // Use a buffer size for better performance
            _logWriter = new StreamWriter(_logFilePath, append: true, System.Text.Encoding.UTF8, 4096);
            _logWriter.AutoFlush = true; // Ensure data is written promptly
            Log($"--- Latency Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
            Debug.Log($"Latency log file initialized at: {_logFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize latency log file '{_logFilePath}': {e.Message}");
            _logToFile = false; // Disable logging if file cannot be opened
        }
    }

    /// <summary>
    /// Updates the latency for a specific channel (e.g., "Audio", "Video", "Network").
    /// Called by AudioManager, VideoManager, WebSocketClient.
    /// </summary>
    public void UpdateLatency(string channel, float latencyMs)
    {
        if (!_latencyHistory.ContainsKey(channel))
        {
            Debug.LogWarning($"Latency update received for unknown channel: {channel}");
            InitializeChannel(channel); // Initialize on the fly if needed
        }

        var history = _latencyHistory[channel];

        // Rationale: Maintain a rolling window of latency values.
        if (history.Count >= _historySize)
        {
            history.RemoveAt(0); // Remove oldest entry
        }
        history.Add(latencyMs);

        // Log the raw RTT value
        Log($"{DateTime.Now:HH:mm:ss.fff} | {channel} RTT: {latencyMs:F1} ms");

        // Optional: Calculate and emit average
        // float average = history.Average();
        // OnAverageLatencyUpdated?.Invoke(channel, average);
    }

    /// <summary>
    /// Gets the most recent latency value for a channel.
    /// </summary>
    public float GetLastLatency(string channel)
    {
        if (_latencyHistory.TryGetValue(channel, out var history) && history.Count > 0)
        {
            return history[history.Count - 1];
        }
        return -1f; // Indicate no data
    }

    /// <summary>
    /// Gets the average latency over the history window for a channel.
    /// </summary>
    public float GetAverageLatency(string channel)
    {
        if (_latencyHistory.TryGetValue(channel, out var history) && history.Count > 0)
        {
            return history.Average();
        }
        return -1f; // Indicate no data
    }


    private void Log(string message)
    {
        if (!_logToFile || _logWriter == null) return;

        // Rationale: Use a lock to prevent race conditions when writing from different threads/callbacks.
        lock (_logLock)
        {
            try
            {
                _logWriter.WriteLine(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error writing to latency log: {e.Message}");
                // Optionally disable further logging attempts if errors persist
                // _logToFile = false;
                // _logWriter.Close();
                // _logWriter = null;
            }
        }
    }
}
