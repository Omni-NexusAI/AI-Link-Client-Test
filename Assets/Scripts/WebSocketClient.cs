using UnityEngine;
using WebSocketSharp; // Requires WebSocketSharp library (websocket-sharp.dll)
using System;
using System.Collections.Concurrent; // For thread-safe queue
using System.Threading;

#if UNITY_EDITOR
using UnityEngine.SceneManagement; // For scene unload handling
#endif

/// <summary>
/// Manages WebSocket connection, data transfer, reconnection, and latency checks.
/// Relies on the WebSocketSharp library.
/// </summary>
/// <remarks>
/// IMPORTANT RESOURCE MANAGEMENT NOTES:
/// 1. This class manages native WebSocket connections that must be properly closed
///    to prevent memory leaks and crashes on domain reload.
/// 2. OnDisable pauses connections but doesn't fully dispose resources to allow resuming.
/// 3. OnDestroy fully cleans up all resources including WebSocket and timers.
/// 4. When modifying this class, ensure all resources are properly disposed in OnDestroy.
/// 5. Any new timers or native resources must be added to the cleanup methods.
/// 6. Thread-safe queues are cleared in OnDestroy to prevent memory leaks.
/// Updated: Fixed latency tracking implementation.
/// </remarks>
public class WebSocketClient : MonoBehaviour
{
    // Static reference to track active WebSocket instances
    private static int s_ActiveWebSocketCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainCleanup()
    {
        // This method is called when the domain is reloaded (e.g., when entering/exiting play mode)
        Debug.Log($"WebSocketClient: Domain reload cleanup. Active WebSockets: {s_ActiveWebSocketCount}");

        // Register with PlayModeCounter
        PlayModeCounter.RegisterResource("WebSocketClient.ActiveConnections", false);

        // Reset static state
        s_ActiveWebSocketCount = 0;
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
        // Log active WebSockets when a scene is unloaded
        Debug.Log($"WebSocketClient: Scene '{scene.name}' unloaded. Active WebSockets: {s_ActiveWebSocketCount}");

        // Register with PlayModeCounter
        PlayModeCounter.RegisterResource("WebSocketClient.ActiveConnections", s_ActiveWebSocketCount > 0);
    }
#endif
    // --- Events ---
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;
    public event Action<string> OnTextMessageReceived;
    public event Action<byte[]> OnBinaryMessageReceived;
    public event Action<float> OnLatencyUpdated; // Latency in milliseconds

    // --- Public State ---
    public bool IsConnected => _ws != null && _ws.ReadyState == WebSocketState.Open;

    // --- Private State ---
    private WebSocket _ws;
    private string _serverUrl;
    private string _authToken;
    private float _reconnectDelaySeconds;
    private int _maxReconnectAttempts;
    private int _reconnectAttempts = 0;
    private Timer _reconnectTimer;
    private Timer _pingTimer;
    private long _lastPingTimestampMs = 0; // Using long for milliseconds timestamp
    private bool _isClosing = false; // Flag to prevent reconnect attempts when explicitly closing
    private bool _attemptingConnection = false;

    // Thread-safe queues for messages received on WebSocket thread
    private readonly ConcurrentQueue<string> _textMessageQueue = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<byte[]> _binaryMessageQueue = new ConcurrentQueue<byte[]>();
    private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();


    void Awake()
    {
        // Track active WebSocket instances
        s_ActiveWebSocketCount++;
        PlayModeCounter.RegisterResource($"WebSocketClient.Instance_{GetInstanceID()}", true);
    }

    void Start()
    {
        // Ensure ConfigManager is ready
        if (ConfigManager.Instance == null)
        {
            Debug.LogError("ConfigManager instance not found. WebSocketClient cannot initialize.");
            enabled = false; // Disable this component
            return;
        }
        LoadConfig();
    }

    void Update()
    {
        // Process queued actions on the main thread
        while (_mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }

        // Process queued messages on the main thread
        while (_textMessageQueue.TryDequeue(out var message))
        {
            HandleTextMessage(message);
        }
        while (_binaryMessageQueue.TryDequeue(out var data))
        {
            OnBinaryMessageReceived?.Invoke(data);
        }
    }

    /// <summary>
    /// Pauses WebSocket operations when the component is disabled.
    /// Does NOT fully dispose resources to allow resuming when re-enabled.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method pauses but doesn't fully dispose resources.
    /// This allows the component to resume operations when re-enabled.
    /// For full cleanup, see OnDestroy.
    /// </remarks>
    void OnDisable()
    {
        // Pause WebSocket connection when component is disabled
        if (IsConnected)
        {
            Debug.Log("WebSocketClient: OnDisable - Closing connection");
            CloseConnection(false); // Don't mark as explicit close to allow reconnect if re-enabled
        }

        // Pause timers but don't dispose them
        _reconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _pingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Fully cleans up all resources when the component is destroyed.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method performs complete cleanup of all resources:
    /// 1. Closes WebSocket connections
    /// 2. Disposes all timers
    /// 3. Clears all message queues
    ///
    /// IMPORTANT: When adding new resources to this class, ensure they are properly
    /// disposed here to prevent memory leaks and crashes on domain reload.
    /// </remarks>
    void OnDestroy()
    {
        Debug.Log("WebSocketClient: OnDestroy - Cleaning up resources");
        CloseConnection(true); // Mark as explicit close

        // Dispose timers
        if (_reconnectTimer != null)
        {
            _reconnectTimer.Dispose();
            _reconnectTimer = null;
        }

        if (_pingTimer != null)
        {
            _pingTimer.Dispose();
            _pingTimer = null;
        }

        // Clear message queues
        while (_textMessageQueue.TryDequeue(out _)) { }
        while (_binaryMessageQueue.TryDequeue(out _)) { }
        while (_mainThreadActions.TryDequeue(out _)) { }

        // Update active WebSocket count
        s_ActiveWebSocketCount = Math.Max(0, s_ActiveWebSocketCount - 1);
        PlayModeCounter.UnregisterResource($"WebSocketClient.Instance_{GetInstanceID()}");
        PlayModeCounter.RegisterResource("WebSocketClient.ActiveConnections", s_ActiveWebSocketCount > 0);
    }

    private void LoadConfig()
    {
        // Rationale: Load settings from ConfigManager singleton.
        _serverUrl = ConfigManager.Instance.Config.serverUrl;
        _authToken = ConfigManager.Instance.Config.authToken; // Store for potential re-auth
        _reconnectDelaySeconds = ConfigManager.Instance.Config.reconnectDelaySeconds;
        _maxReconnectAttempts = ConfigManager.Instance.Config.maxReconnectAttempts;

        // Validate URL (WebSocketSharp requires ws:// or wss://)
        if (string.IsNullOrEmpty(_serverUrl) || (!_serverUrl.StartsWith("ws://") && !_serverUrl.StartsWith("wss://")))
        {
            Debug.LogError($"Invalid WebSocket URL: {_serverUrl}. Must start with ws:// or wss://");
            OnError?.Invoke($"Invalid WebSocket URL: {_serverUrl}");
            enabled = false;
            return;
        }

        Debug.Log($"WebSocketClient configured for URL: {_serverUrl}");
    }

    public void Connect()
    {
        if (IsConnected || _attemptingConnection)
        {
            Debug.LogWarning("WebSocket is already connected or connecting.");
            return;
        }

        if (string.IsNullOrEmpty(_serverUrl))
        {
            Debug.LogError("Server URL is not configured.");
            OnError?.Invoke("Server URL not configured.");
            return;
        }

        _isClosing = false; // Reset closing flag on new connect attempt
        _attemptingConnection = true;
        Debug.Log($"Attempting to connect to {_serverUrl}...");

        // Rationale: WebSocket operations should run on a background thread.
        // WebSocketSharp handles its own threading internally.
        try
        {
            _ws = new WebSocket(_serverUrl);

            // --- Event Handlers ---
            _ws.OnOpen += Ws_OnOpen;
            _ws.OnMessage += Ws_OnMessage;
            _ws.OnError += Ws_OnError;
            _ws.OnClose += Ws_OnClose;
            // --- End Event Handlers ---

            // Track this connection
            PlayModeCounter.RegisterResource($"WebSocketClient.Connection_{GetInstanceID()}", true);

            _ws.ConnectAsync(); // Connect asynchronously
        }
        catch (Exception e)
        {
            _attemptingConnection = false;
            string errorMsg = $"WebSocket connection initiation failed: {e.Message}";
            Debug.LogError(errorMsg);
            // Queue error event to be raised on main thread
            _mainThreadActions.Enqueue(() => OnError?.Invoke(errorMsg));
            AttemptReconnect(); // Try reconnecting if initial connection fails
        }
    }

    public void CloseConnection(bool explicitClose = true)
    {
        if (_ws == null) return;

        _isClosing = explicitClose; // Set flag if this is an intentional close
        _attemptingConnection = false; // Stop connection attempts

        // Stop timers
        _reconnectTimer?.Change(Timeout.Infinite, Timeout.Infinite); // Stop reconnect timer
        _pingTimer?.Dispose(); // Dispose ping timer
        _pingTimer = null;

        if (_ws.ReadyState == WebSocketState.Open || _ws.ReadyState == WebSocketState.Connecting)
        {
            Debug.Log("Closing WebSocket connection...");
            _ws.CloseAsync(CloseStatusCode.Normal, "Client requested disconnect");
        }
        else
        {
            // If already closed or closing, just clean up
            CleanupWebSocket();
        }
    }

    private void CleanupWebSocket()
    {
        if (_ws != null)
        {
            // Unsubscribe events to prevent memory leaks
            _ws.OnOpen -= Ws_OnOpen;
            _ws.OnMessage -= Ws_OnMessage;
            _ws.OnError -= Ws_OnError;
            _ws.OnClose -= Ws_OnClose;

            // Log cleanup for tracking
            PlayModeCounter.RegisterResource($"WebSocketClient.Connection_{GetInstanceID()}", false);

            _ws = null; // Release the WebSocket object
        }
        _attemptingConnection = false;
        Debug.Log("WebSocket resources cleaned up.");
    }

    // --- WebSocket Event Handlers (Called on WebSocket Thread) ---

    private void Ws_OnOpen(object sender, EventArgs e)
    {
        Debug.Log("WebSocket connection opened.");
        _attemptingConnection = false;
        _reconnectAttempts = 0; // Reset attempts on successful connection

        // Queue actions for main thread
        _mainThreadActions.Enqueue(() =>
        {
            OnConnected?.Invoke();
            StartPingTimer(); // Start pinging once connected
            // Send auth token if available
            if (!string.IsNullOrEmpty(_authToken))
            {
                 SendAuthToken();
            }
        });
    }

    private void Ws_OnMessage(object sender, MessageEventArgs e)
    {
        if (e.IsText)
        {
            // Queue text message for processing on main thread
            _textMessageQueue.Enqueue(e.Data);
        }
        else if (e.IsBinary)
        {
            // Queue binary message for processing on main thread
            _binaryMessageQueue.Enqueue(e.RawData);
        }
        else if (e.IsPing)
        {
            // WebSocketSharp handles responding to Pings automatically.
            // Debug.Log("Ping received");
        }
        // else if (e.IsPong) // Removed: IsPong property doesn't exist in this WebSocketSharp version.
        // {
        //     // Network Latency is now implicitly measured via Audio/Video round trips.
        // }
    }

     private void Ws_OnError(object sender, ErrorEventArgs e)
    {
        string errorMsg = $"WebSocket error: {e.Message}";
        Debug.LogError(errorMsg);
        _attemptingConnection = false; // Stop connection attempts on error
        // Queue error event to be raised on main thread
        _mainThreadActions.Enqueue(() => OnError?.Invoke(errorMsg));
        // Note: OnClose will likely be called after OnError by WebSocketSharp
    }

    private void Ws_OnClose(object sender, CloseEventArgs e)
    {
        string reason = $"WebSocket closed. Code: {e.Code}, Reason: {e.Reason}";
        Debug.LogWarning(reason);
        _attemptingConnection = false; // Ensure flag is reset

        // Stop ping timer on disconnect
        _pingTimer?.Dispose();
        _pingTimer = null;

        // Queue disconnect event and cleanup for main thread
        _mainThreadActions.Enqueue(() =>
        {
            OnDisconnected?.Invoke();
            CleanupWebSocket(); // Clean up resources
            if (!_isClosing) // Only attempt reconnect if not explicitly closed
            {
                AttemptReconnect();
            }
        });
    }

    // --- Message Handling (Called on Main Thread) ---

    private void HandleTextMessage(string message)
    {
        // Check for pong response to calculate latency
        if (message.Contains("\"type\":\"pong\""))
        {
            try
            {
                // Simple JSON parsing to extract timestamp
                // Format: {"type":"pong","timestamp":123456789}
                int timestampStart = message.IndexOf("\"timestamp\":") + 12;
                int timestampEnd = message.IndexOf("}", timestampStart);
                if (timestampStart > 11 && timestampEnd > timestampStart)
                {
                    string timestampStr = message.Substring(timestampStart, timestampEnd - timestampStart);
                    if (long.TryParse(timestampStr, out long originalTimestamp))
                    {
                        long currentTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        float latencyMs = currentTimeMs - originalTimestamp;
                        OnLatencyUpdated?.Invoke(latencyMs);
                        return; // Don't pass pong messages to other handlers
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error parsing pong message: {e.Message}");
            }
        }
        
        // Pass other messages to the normal handler
            OnTextMessageReceived?.Invoke(message);
    }

    // --- Sending Data ---

    public void SendText(string message)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("Cannot send text: WebSocket not connected.");
            return;
        }
        // Rationale: Send operations are generally safe to call directly with WebSocketSharp.
        try
        {
             _ws?.SendAsync(message, null); // Send asynchronously
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending text message: {e.Message}");
            OnError?.Invoke($"Error sending text: {e.Message}");
        }
    }

    public void SendBinary(byte[] data)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("Cannot send binary: WebSocket not connected.");
            return;
        }
         try
        {
            _ws?.SendAsync(data, null); // Send asynchronously
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending binary message: {e.Message}");
            OnError?.Invoke($"Error sending binary: {e.Message}");
        }
    }

     private void SendAuthToken()
    {
        // Rationale: Encapsulate auth token sending logic.
        // Modify JSON structure as needed by the server.
        string authJson = $"{{\"type\":\"auth\",\"token\":\"{_authToken}\"}}";
        Debug.Log("Sending authentication token...");
        SendText(authJson);
    }


    // --- Latency Check ---

    private void StartPingTimer()
    {
        // Rationale: Periodically send pings to measure latency and keep connection alive.
        _pingTimer?.Dispose(); // Dispose previous timer if any
        _pingTimer = new Timer(SendPing, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)); // Send ping every 5 seconds
        Debug.Log("Ping timer started.");
    }

    private void SendPing(object state)
    {
        if (!IsConnected) return;

        try
        {
            _lastPingTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // Send a custom ping message to measure latency
            if (_ws != null && _ws.ReadyState == WebSocketState.Open)
            {
                // Send custom ping message with timestamp
                string pingMessage = $"{{\"type\":\"ping\",\"timestamp\":{_lastPingTimestampMs}}}";
                _ws.SendAsync(pingMessage, null);
                 // Debug.Log("Ping sent.");
            }
        }
        catch (Exception e)
        {
             // Queue error for main thread
            _mainThreadActions.Enqueue(() => Debug.LogError($"Error sending ping: {e.Message}"));
        }
    }

    // --- Reconnection Logic ---

    private void AttemptReconnect()
    {
        if (_isClosing || _attemptingConnection) return; // Don't reconnect if closing or already trying

        if (_reconnectAttempts < _maxReconnectAttempts)
        {
            _reconnectAttempts++;
            float delayMs = _reconnectDelaySeconds * 1000;
            Debug.LogWarning($"Attempting reconnect ({_reconnectAttempts}/{_maxReconnectAttempts}) in {_reconnectDelaySeconds} seconds...");

            // Dispose previous timer before creating a new one
            _reconnectTimer?.Dispose();
            _reconnectTimer = new Timer(ReconnectTimerCallback, null, (int)delayMs, Timeout.Infinite); // One-shot timer
        }
        else
        {
            Debug.LogError("Max reconnection attempts reached. Giving up.");
            OnError?.Invoke("Max reconnection attempts reached.");
        }
    }

    private void ReconnectTimerCallback(object state)
    {
        // Queue connection attempt for main thread? No, Connect() handles its own async logic.
        // Directly call Connect() but ensure it handles potential threading issues if necessary.
        // For WebSocketSharp, ConnectAsync is generally safe.
        // We need to ensure this callback doesn't run if we are already trying to connect.
        if (!_isClosing && !IsConnected && !_attemptingConnection)
        {
             // Queue the Connect call to be executed from the main thread Update loop
             // to avoid potential issues with Unity API calls from a timer thread.
             _mainThreadActions.Enqueue(Connect);
        }
         _reconnectTimer?.Dispose(); // Dispose timer after execution
         _reconnectTimer = null;
    }
}
