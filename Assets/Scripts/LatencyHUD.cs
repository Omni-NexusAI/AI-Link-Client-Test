using UnityEngine;
using UnityEngine.UI; // Required for basic UI Text
// using TMPro; // Use this namespace if using TextMeshPro

/// <summary>
/// Displays latency information from LatencyMonitor on UI Text elements.
/// </summary>
public class LatencyHUD : MonoBehaviour
{
    // --- Inspector References ---
    // Rationale: Assign these Text components in the Unity Inspector.
    [Tooltip("Text element to display Network (WebSocket Ping/Pong) latency.")]
    [SerializeField] private Text networkLatencyText; // Or use TextMeshProUGUI

    [Tooltip("Text element to display Audio round-trip latency.")]
    [SerializeField] private Text audioLatencyText; // Or use TextMeshProUGUI

    [Tooltip("Text element to display Video round-trip latency.")]
    [SerializeField] private Text videoLatencyText; // Or use TextMeshProUGUI

    // --- Private References ---
    private LatencyMonitor _latencyMonitor;

    // --- Update Frequency ---
    [Tooltip("How often to update the HUD display (in seconds).")]
    [SerializeField] private float updateInterval = 0.5f;
    private float _timer = 0f;

    // --- Counter variables ---
    private int _networkPacketCount = 0;
    private int _audioPacketCount = 0;
    private int _videoFrameCount = 0;

    void Start()
    {
        // Rationale: Find the LatencyMonitor instance in the scene. Assumes it exists.
        // Use FindAnyObjectByType as FindObjectOfType is obsolete.
        _latencyMonitor = FindAnyObjectByType<LatencyMonitor>();

        if (_latencyMonitor == null)
        {
            Debug.LogError("LatencyMonitor instance not found in scene. LatencyHUD cannot function.");
            enabled = false; // Disable HUD if monitor is missing
            return;
        }

        // Initial UI update
        UpdateHUD();
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= updateInterval)
        {
            _timer -= updateInterval; // Reset timer relative to interval
            UpdateHUD();
        }
    }

    private void UpdateHUD()
    {
        if (_latencyMonitor == null) return;

        // Update counters first
        UpdateCounters();

        // Rationale: Fetch latest average latency values and update UI text with both latency and counter.
        // Using average provides a smoother display than last RTT.
        UpdateTextElementWithCounter(networkLatencyText, "Network", _latencyMonitor.GetAverageLatency(LatencyMonitor.NetworkChannel), _networkPacketCount);
        UpdateTextElementWithCounter(audioLatencyText, "Audio", _latencyMonitor.GetAverageLatency(LatencyMonitor.AudioChannel), _audioPacketCount);
        UpdateTextElementWithCounter(videoLatencyText, "Video", _latencyMonitor.GetAverageLatency(LatencyMonitor.VideoChannel), _videoFrameCount);
    }

    private void UpdateTextElementWithCounter(Text textElement, string prefix, float value, int counter) // Change Text to TextMeshProUGUI if using TMP
    {
        if (textElement != null)
        {
            if (value < 0) // Check if monitor returned valid data (-1 indicates no data)
            {
                textElement.text = $"{prefix}: --- ms ({counter})";
            }
            else
            {
                // Rationale: Format latency to one decimal place for readability and include counter.
                textElement.text = $"{prefix}: {value:F1} ms ({counter})";
            }
        }
        // else { Debug.LogWarning($"Text element for '{prefix}' is not assigned in the Inspector."); }
    }

    private void UpdateCounters()
    {
        // Simulate counter updates - replace with actual data sources
        // These should be connected to your actual network, audio, and video systems
        
        // Increment network packet counter (example)
        _networkPacketCount++;
        
        // Increment audio packet counter (example)
        _audioPacketCount++;
        
        // Increment video frame counter (example)
        _videoFrameCount++;
        
        // Reset counters periodically to prevent overflow
        if (_networkPacketCount > 99999) _networkPacketCount = 0;
        if (_audioPacketCount > 99999) _audioPacketCount = 0;
        if (_videoFrameCount > 99999) _videoFrameCount = 0;
    }

    /// <summary>
    /// Call this method to increment the network packet counter from external systems
    /// </summary>
    public void IncrementNetworkPacketCount()
    {
        _networkPacketCount++;
    }

    /// <summary>
    /// Call this method to increment the audio packet counter from external systems
    /// </summary>
    public void IncrementAudioPacketCount()
    {
        _audioPacketCount++;
    }

    /// <summary>
    /// Call this method to increment the video frame counter from external systems
    /// </summary>
    public void IncrementVideoFrameCount()
    {
        _videoFrameCount++;
    }

    /// <summary>
    /// Reset all counters to zero
    /// </summary>
    public void ResetCounters()
    {
        _networkPacketCount = 0;
        _audioPacketCount = 0;
        _videoFrameCount = 0;
    }
}
