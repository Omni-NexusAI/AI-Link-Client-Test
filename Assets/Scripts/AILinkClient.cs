using UnityEngine;
using UnityEngine.UI; // Required for RawImage
using System;
using System.IO; // For BitConverter

/// <summary>
/// Central orchestrator for the AI-Link client.
/// Manages connections, configuration, and data flow between managers.
/// Provides public API for controlling the streaming state.
/// </summary>
[RequireComponent(typeof(ConfigManager))] // Ensure ConfigManager is present
[RequireComponent(typeof(WebSocketClient))]
[RequireComponent(typeof(AudioManager))]
[RequireComponent(typeof(VideoManager))]
[RequireComponent(typeof(LatencyMonitor))]
public class AILinkClient : MonoBehaviour
{
    // --- Events ---
    // Optional: Expose higher-level status events if needed by external UI/logic
    public event Action OnStreamingStarted;
    public event Action OnStreamingStopped;
    public event Action<bool> OnConnectionStatusChanged; // True if connected

    // --- Public State ---
    public bool IsClientConnected => _webSocketClient != null && _webSocketClient.IsConnected;
    public bool IsClientStreaming { get; private set; } = false;

    // --- Private References to Managers ---
    // Rationale: Get references to required manager components attached to the same GameObject.
    private ConfigManager _configManager;
    private WebSocketClient _webSocketClient;
    private AudioManager _audioManager;
    private VideoManager _videoManager;
    private LatencyMonitor _latencyMonitor;

    // --- Inspector References ---
    [Tooltip("UI RawImage element to display the received video feed.")]
    [SerializeField] private RawImage videoDisplayImage;

    // --- Private State ---
    // private bool _echoMode = false; // Removed: Echo mode logic handled within managers for now.

    void Awake()
    {
        // Rationale: Get component references in Awake to ensure they are available in Start.
        _configManager = GetComponent<ConfigManager>();
        _webSocketClient = GetComponent<WebSocketClient>();
        _audioManager = GetComponent<AudioManager>();
        _videoManager = GetComponent<VideoManager>();
        _latencyMonitor = GetComponent<LatencyMonitor>();

        // Basic validation
        if (_configManager == null || _webSocketClient == null || _audioManager == null || _videoManager == null || _latencyMonitor == null)
        {
            Debug.LogError("AILinkClient is missing one or more required manager components!");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // Rationale: Subscribe to events from managers in Start after they have initialized.
        SubscribeToEvents();

        // Optionally auto-connect on start based on config or other logic
        // ConnectToServer();
        // Optionally auto-start streaming
        // StartStreaming(); // Call this after successful connection?
    }

    void OnDisable()
    {
        Debug.Log("AILinkClient: OnDisable - Pausing operations");

        // Stop streaming but don't disconnect yet
        if (IsClientStreaming)
        {
            StopStreaming();
        }
    }

    void OnDestroy()
    {
        Debug.Log("AILinkClient: OnDestroy - Cleaning up resources");

        // Rationale: Unsubscribe from events to prevent memory leaks.
        UnsubscribeFromEvents();

        // Ensure connection is closed and streaming stopped
        StopStreaming();
        DisconnectFromServer();
    }

    void OnApplicationQuit()
    {
        Debug.Log("AILinkClient: OnApplicationQuit - Ensuring all connections are closed");

        // Extra safety to ensure everything is properly shut down
        StopStreaming();
        DisconnectFromServer();
    }

    private void SubscribeToEvents()
    {
        // WebSocket Events
        _webSocketClient.OnConnected += HandleWebSocketConnected;
        _webSocketClient.OnDisconnected += HandleWebSocketDisconnected;
        _webSocketClient.OnError += HandleWebSocketError;
        _webSocketClient.OnTextMessageReceived += HandleWebSocketTextMessage;
        _webSocketClient.OnBinaryMessageReceived += HandleWebSocketBinaryMessage;
        _webSocketClient.OnLatencyUpdated += HandleNetworkLatencyUpdate; // Network RTT

        // Audio Events
        _audioManager.OnOpusPacketEncoded += HandleAudioPacketEncoded;
        _audioManager.OnPlaybackLatencyUpdated += HandleAudioLatencyUpdate; // Audio RTT

        // Video Events
        _videoManager.OnFrameCaptured += HandleVideoFrameCaptured; // Encoded video frame
        _videoManager.OnLatencyUpdated += HandleVideoLatencyUpdate; // Video RTT
        _videoManager.OnFrameReceived += HandleVideoFrameReceived; // Received video texture for display
    }

    private void UnsubscribeFromEvents()
    {
        // Important to prevent errors if objects are destroyed in different orders
        if (_webSocketClient != null)
        {
            _webSocketClient.OnConnected -= HandleWebSocketConnected;
            _webSocketClient.OnDisconnected -= HandleWebSocketDisconnected;
            _webSocketClient.OnError -= HandleWebSocketError;
            _webSocketClient.OnTextMessageReceived -= HandleWebSocketTextMessage;
            _webSocketClient.OnBinaryMessageReceived -= HandleWebSocketBinaryMessage;
            _webSocketClient.OnLatencyUpdated -= HandleNetworkLatencyUpdate;
        }
        if (_audioManager != null)
        {
            _audioManager.OnOpusPacketEncoded -= HandleAudioPacketEncoded;
            _audioManager.OnPlaybackLatencyUpdated -= HandleAudioLatencyUpdate;
        }
        if (_videoManager != null)
        {
            _videoManager.OnFrameCaptured -= HandleVideoFrameCaptured;
            _videoManager.OnLatencyUpdated -= HandleVideoLatencyUpdate;
            _videoManager.OnFrameReceived -= HandleVideoFrameReceived;
        }
    }

    // --- Public Control API ---

    public void ConnectToServer()
    {
        Debug.Log("AILinkClient: Connect requested.");
        _webSocketClient.Connect();
    }

    public void DisconnectFromServer()
    {
        Debug.Log("AILinkClient: Disconnect requested.");
        _webSocketClient.CloseConnection();
        // Stop streaming if disconnecting
        // StopStreaming(); // Already called in OnDestroy if object is destroyed
    }

    public void StartStreaming()
    {
        if (!IsClientConnected)
        {
            Debug.LogWarning("Cannot start streaming: Not connected to server.");
            return;
        }
        if (IsClientStreaming)
        {
            Debug.LogWarning("Already streaming.");
            return;
        }

        Debug.Log("AILinkClient: Starting streams...");
        // Rationale: Start individual capture components.
        _audioManager.StartRecording();
        _videoManager.StartCapture(); // VideoManager handles passthrough availability check internally

        IsClientStreaming = true;
        OnStreamingStarted?.Invoke();
    }

    public void StopStreaming()
    {
        if (!IsClientStreaming) return;

        Debug.Log("AILinkClient: Stopping streams...");
        // Rationale: Stop individual capture components.
        _audioManager.StopRecording();
        _videoManager.StopCapture();

        IsClientStreaming = false;
        OnStreamingStopped?.Invoke();
    }

    // --- Event Handlers ---

    private void HandleWebSocketConnected()
    {
        Debug.Log("AILinkClient: WebSocket Connected.");
        OnConnectionStatusChanged?.Invoke(true);
        // Automatically start streaming upon connection if desired
        // StartStreaming();
    }

    private void HandleWebSocketDisconnected()
    {
        Debug.Log("AILinkClient: WebSocket Disconnected.");
        OnConnectionStatusChanged?.Invoke(false);
        // Ensure streaming stops if connection drops
        StopStreaming();
    }

    private void HandleWebSocketError(string errorMessage)
    {
        Debug.LogError($"AILinkClient: WebSocket Error: {errorMessage}");
        // Optionally update UI or state based on error
    }

    private void HandleNetworkLatencyUpdate(float latencyMs)
    {
        _latencyMonitor.UpdateLatency(LatencyMonitor.NetworkChannel, latencyMs);
    }

    private void HandleAudioLatencyUpdate(float latencyMs)
    {
        _latencyMonitor.UpdateLatency(LatencyMonitor.AudioChannel, latencyMs);
    }

     private void HandleVideoLatencyUpdate(float latencyMs)
    {
        _latencyMonitor.UpdateLatency(LatencyMonitor.VideoChannel, latencyMs);
    }

    private void HandleAudioPacketEncoded(byte[] opusData)
    {
        if (!IsClientConnected || !IsClientStreaming) return;

        // Rationale: Prepend message type (0=audio) and timestamp before sending.
        // Timestamp is crucial for latency calculation on the receiving end (echo).
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        byte[] timestampBytes = BitConverter.GetBytes(timestamp); // 8 bytes

        // Combine type, timestamp, and data
        // Rationale: Create a single byte array for efficient WebSocket binary transfer.
        byte[] message = new byte[1 + timestampBytes.Length + opusData.Length];
        message[0] = 0; // Type 0 for Audio
        Buffer.BlockCopy(timestampBytes, 0, message, 1, timestampBytes.Length);
        Buffer.BlockCopy(opusData, 0, message, 1 + timestampBytes.Length, opusData.Length);

        _webSocketClient.SendBinary(message);
    }

    private void HandleVideoFrameCaptured(byte[] frameData) // Initially JPEG, later H264
    {
        if (!IsClientConnected || !IsClientStreaming) return;

        // Rationale: Prepend message type (1=video) and timestamp.
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        byte[] timestampBytes = BitConverter.GetBytes(timestamp); // 8 bytes

        // Combine type, timestamp, and data
        byte[] message = new byte[1 + timestampBytes.Length + frameData.Length];
        message[0] = 1; // Type 1 for Video
        Buffer.BlockCopy(timestampBytes, 0, message, 1, timestampBytes.Length);
        Buffer.BlockCopy(frameData, 0, message, 1 + timestampBytes.Length, frameData.Length);

        _webSocketClient.SendBinary(message);
    }

    private void HandleWebSocketTextMessage(string message)
    {
        // Rationale: Handle server-sent text messages (e.g., control commands, status updates).
        // Currently, only handles basic logging. Expand based on server protocol.
        Debug.Log($"AILinkClient: Received Text: {message}");
        // Example: Parse JSON messages if server sends them
        // try {
        //     var json = JsonUtility.FromJson<ServerMessage>(message); // Define ServerMessage class
        //     if (json != null && json.type == "configUpdate") { HandleConfigUpdate(json); }
        // } catch (Exception e) { Debug.LogWarning($"Failed to parse text message as JSON: {e.Message}"); }
    }

    private void HandleWebSocketBinaryMessage(byte[] data)
    {
        // Rationale: Process incoming binary data based on the prepended type byte.
        if (data == null || data.Length < 1 + 8) // Must have type byte + 8 byte timestamp
        {
            Debug.LogWarning("Received invalid binary message (too short).");
            return;
        }

        byte messageType = data[0];
        long timestamp = BitConverter.ToInt64(data, 1); // Read 8-byte timestamp starting at index 1
        int payloadOffset = 1 + 8;
        int payloadLength = data.Length - payloadOffset;

        if (payloadLength <= 0)
        {
             Debug.LogWarning($"Received binary message type {messageType} with no payload.");
             return;
        }

        // Rationale: Create a separate byte array for the actual payload.
        byte[] payload = new byte[payloadLength];
        Buffer.BlockCopy(data, payloadOffset, payload, 0, payloadLength);

        // Route payload to the appropriate manager
        switch (messageType)
        {
            case 0: // Audio
                _audioManager.HandleReceivedOpusPacket(payload, timestamp);
                break;
            case 1: // Video
                _videoManager.HandleReceivedVideoData(payload, timestamp);
                break;
            default:
                Debug.LogWarning($"Received unknown binary message type: {messageType}");
                break;
        }
    }

    private void HandleVideoFrameReceived(Texture2D receivedTexture)
    {
        // Rationale: Update the assigned RawImage component with the received texture.
        if (videoDisplayImage != null)
        {
            videoDisplayImage.texture = receivedTexture;
            // Optional: Match RawImage aspect ratio to texture aspect ratio
            // videoDisplayImage.rectTransform.sizeDelta = new Vector2(receivedTexture.width, receivedTexture.height); // Adjust sizing as needed
        }
        // else { Debug.LogWarning("VideoDisplayImage RawImage is not assigned in the Inspector."); }
    }
}
