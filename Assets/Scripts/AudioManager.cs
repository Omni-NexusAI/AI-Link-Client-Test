using UnityEngine;
using System;
using System.Collections.Generic;
using Concentus.Enums; // Requires Concentus library
using Concentus.Structs; // Requires Concentus library

#if UNITY_EDITOR
using UnityEngine.SceneManagement; // For scene unload handling
#endif

/// <summary>
/// Manages audio input (microphone), Opus encoding, and playback of received audio data.
/// Requires the Concentus library for Opus encoding/decoding.
/// </summary>
/// <remarks>
/// IMPORTANT RESOURCE MANAGEMENT NOTES:
/// 1. This class manages critical native resources that must be properly released:
///    - Microphone input (Unity's Microphone API)
///    - Opus encoder/decoder (Concentus library)
///    - AudioClips for recording and playback
/// 2. OnDisable stops recording and clears queues but doesn't dispose encoders/decoders.
/// 3. OnDestroy fully disposes all resources including Opus encoder/decoder.
/// 4. OnApplicationQuit provides extra safety for Microphone resources.
/// 5. When modifying this class, ensure all native resources are properly disposed.
/// 6. Note: Opus encoder/decoder methods are marked obsolete and should be updated
///    to use OpusCodecFactory methods in a future update.
/// </remarks>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    // Static tracking for active resources
    private static int s_ActiveMicrophoneCount = 0;
    private static int s_ActiveOpusEncoderCount = 0;
    private static int s_ActiveOpusDecoderCount = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainCleanup()
    {
        // This method is called when the domain is reloaded (e.g., when entering/exiting play mode)
        Debug.Log($"AudioManager: Domain reload cleanup. Active Microphones: {s_ActiveMicrophoneCount}, Encoders: {s_ActiveOpusEncoderCount}, Decoders: {s_ActiveOpusDecoderCount}");

        // Register with PlayModeCounter
        PlayModeCounter.RegisterResource("AudioManager.ActiveMicrophones", false);
        PlayModeCounter.RegisterResource("AudioManager.ActiveOpusEncoders", false);
        PlayModeCounter.RegisterResource("AudioManager.ActiveOpusDecoders", false);

        // Reset static state
        s_ActiveMicrophoneCount = 0;
        s_ActiveOpusEncoderCount = 0;
        s_ActiveOpusDecoderCount = 0;
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
        Debug.Log($"AudioManager: Scene '{scene.name}' unloaded. Active Microphones: {s_ActiveMicrophoneCount}, Encoders: {s_ActiveOpusEncoderCount}, Decoders: {s_ActiveOpusDecoderCount}");

        // Register with PlayModeCounter
        PlayModeCounter.RegisterResource("AudioManager.ActiveMicrophones", s_ActiveMicrophoneCount > 0);
        PlayModeCounter.RegisterResource("AudioManager.ActiveOpusEncoders", s_ActiveOpusEncoderCount > 0);
        PlayModeCounter.RegisterResource("AudioManager.ActiveOpusDecoders", s_ActiveOpusDecoderCount > 0);
    }
#endif
    // --- Events ---
    public event Action<byte[]> OnOpusPacketEncoded; // Sends encoded Opus data
    public event Action<float> OnPlaybackLatencyUpdated; // Reports latency for received packets

    // --- Public State ---
    public bool IsRecording { get; private set; } = false;
    public bool IsPlaying { get; private set; } = false;

    // --- Configuration (Loaded from ConfigManager) ---
    private int _sampleRate = 16000;
    private bool _recordingEnabled = true;
    private bool _playbackEnabled = true;
    private int _opusBitrate = 64000;
    private readonly int _opusFrameSizeMs = 20; // Common Opus frame size (20ms)
    private int _opusSamplesPerFrame; // Calculated based on sample rate and frame size
    private readonly int _channels = 1; // Mono

    // --- Private State ---
    private AudioSource _audioSource;
    private string _microphoneDevice = null;
    private AudioClip _recordingClip;
    private int _lastRecordingPosition = 0;
    private OpusEncoder _opusEncoder;
    private OpusDecoder _opusDecoder;
    private List<float> _pcmBuffer = new List<float>(); // Buffer for PCM data before encoding
    private readonly Queue<Tuple<byte[], long>> _playbackQueue = new Queue<Tuple<byte[], long>>(); // Queue for received Opus packets (data, timestamp)
    private float[] _playbackPcmBuffer; // Buffer for decoded PCM data for playback

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        // Rationale: Ensure AudioSource is configured for optimal streaming playback.
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f; // Ensure non-spatialized audio for direct playback

        // Register this instance with PlayModeCounter
        PlayModeCounter.RegisterResource($"AudioManager.Instance_{GetInstanceID()}", true);
    }

    void Start()
    {
        if (ConfigManager.Instance == null)
        {
            Debug.LogError("ConfigManager instance not found. AudioManager cannot initialize.");
            enabled = false;
            return;
        }
        LoadConfig();
        InitializeOpus();

        // Select the default microphone device
        if (Microphone.devices.Length > 0)
        {
            _microphoneDevice = Microphone.devices[0];
            Debug.Log($"Using microphone: {_microphoneDevice}");
        }
        else
        {
            Debug.LogError("No microphone devices found!");
            _recordingEnabled = false; // Disable recording if no mic
        }

        // Note: Recording is started by AILinkClient via StartStreaming()
        // if (_recordingEnabled)
        // {
        //     StartRecording();
        // }
    }

    void Update()
    {
        if (IsRecording && _recordingEnabled)
        {
            ProcessMicrophoneInput();
        }

        // Process playback queue on main thread
        ProcessPlaybackQueue();
    }

    /// <summary>
    /// Pauses audio operations when the component is disabled.
    /// Stops recording and playback but doesn't dispose encoders/decoders.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method stops active operations but doesn't fully dispose resources.
    /// This allows the component to resume operations when re-enabled.
    /// For full cleanup, see OnDestroy.
    /// </remarks>
    void OnDisable()
    {
        Debug.Log("AudioManager: OnDisable - Stopping recording and cleaning up resources");
        StopRecording();

        // Clear playback queue but don't dispose encoders/decoders yet
        lock (_playbackQueue)
        {
            _playbackQueue.Clear();
        }

        // Stop any current playback
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
            IsPlaying = false;
        }
    }

    /// <summary>
    /// Fully disposes all resources when the component is destroyed.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This method performs complete cleanup of all resources:
    /// 1. Stops recording and releases microphone
    /// 2. Disposes Opus encoder/decoder
    /// 3. Clears all buffers and queues
    ///
    /// IMPORTANT: When adding new resources to this class, ensure they are properly
    /// disposed here to prevent memory leaks and crashes on domain reload.
    /// </remarks>
    void OnDestroy()
    {
        Debug.Log("AudioManager: OnDestroy - Disposing resources");

        // Ensure recording is stopped
        StopRecording();

        // Dispose Opus encoder/decoder
        if (_opusEncoder != null)
        {
            _opusEncoder.Dispose();
            _opusEncoder = null;

            // Update tracking
            s_ActiveOpusEncoderCount = Math.Max(0, s_ActiveOpusEncoderCount - 1);
            PlayModeCounter.RegisterResource($"AudioManager.OpusEncoder_{GetInstanceID()}", false);
        }

        if (_opusDecoder != null)
        {
            _opusDecoder.Dispose();
            _opusDecoder = null;

            // Update tracking
            s_ActiveOpusDecoderCount = Math.Max(0, s_ActiveOpusDecoderCount - 1);
            PlayModeCounter.RegisterResource($"AudioManager.OpusDecoder_{GetInstanceID()}", false);
        }

        // Clear buffers
        _pcmBuffer.Clear();
        lock (_playbackQueue)
        {
            _playbackQueue.Clear();
        }

        // Ensure microphone is released
        if (!string.IsNullOrEmpty(_microphoneDevice) && Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);

            // Update tracking
            s_ActiveMicrophoneCount = Math.Max(0, s_ActiveMicrophoneCount - 1);
            PlayModeCounter.RegisterResource($"AudioManager.Microphone_{GetInstanceID()}", false);
        }

        // Clear playback buffer
        _playbackPcmBuffer = null;

        // Unregister this instance
        PlayModeCounter.UnregisterResource($"AudioManager.Instance_{GetInstanceID()}");

        // Update global resource tracking
        PlayModeCounter.RegisterResource("AudioManager.ActiveMicrophones", s_ActiveMicrophoneCount > 0);
        PlayModeCounter.RegisterResource("AudioManager.ActiveOpusEncoders", s_ActiveOpusEncoderCount > 0);
        PlayModeCounter.RegisterResource("AudioManager.ActiveOpusDecoders", s_ActiveOpusDecoderCount > 0);
    }

    /// <summary>
    /// Extra safety measure to ensure microphone is released on application quit.
    /// </summary>
    /// <remarks>
    /// RESOURCE MANAGEMENT: This provides an additional safety net to ensure
    /// the microphone is released even if OnDestroy is not called properly.
    /// Unity sometimes has issues with cleanup order during application quit.
    /// </remarks>
    void OnApplicationQuit()
    {
        Debug.Log("AudioManager: OnApplicationQuit - Ensuring microphone is released");

        // Extra safety to ensure microphone is released on application quit
        if (!string.IsNullOrEmpty(_microphoneDevice) && Microphone.IsRecording(_microphoneDevice))
        {
            Microphone.End(_microphoneDevice);
        }
    }

    private void LoadConfig()
    {
        var config = ConfigManager.Instance.Config.audio;
        _sampleRate = config.sampleRate;
        _recordingEnabled = config.recordingEnabled;
        _playbackEnabled = config.playbackEnabled;
        _opusBitrate = config.opusBitrate;

        // Rationale: Calculate Opus frame size in samples based on config.
        _opusSamplesPerFrame = _sampleRate * _opusFrameSizeMs / 1000;

        Debug.Log($"AudioManager configured: SampleRate={_sampleRate}, OpusBitrate={_opusBitrate}, FrameSamples={_opusSamplesPerFrame}");
    }

    private void InitializeOpus()
    {
        // Rationale: Initialize Opus encoder and decoder with configured settings.
        try
        {
            _opusEncoder = new OpusEncoder(_sampleRate, _channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _opusEncoder.Bitrate = _opusBitrate;
            // Consider setting other encoder options like complexity, VBR, etc. if needed

            // Track encoder creation
            s_ActiveOpusEncoderCount++;
            PlayModeCounter.RegisterResource($"AudioManager.OpusEncoder_{GetInstanceID()}", true);

            _opusDecoder = new OpusDecoder(_sampleRate, _channels);

            // Track decoder creation
            s_ActiveOpusDecoderCount++;
            PlayModeCounter.RegisterResource($"AudioManager.OpusDecoder_{GetInstanceID()}", true);

            // Allocate buffer for decoded playback data
             _playbackPcmBuffer = new float[_opusSamplesPerFrame * _channels]; // Buffer for one frame

            Debug.Log("Opus encoder and decoder initialized.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Opus: {e.Message}. Disabling audio processing.");
            _recordingEnabled = false;
            _playbackEnabled = false;
            enabled = false;
        }
    }

    public void StartRecording()
    {
        if (IsRecording || !_recordingEnabled || string.IsNullOrEmpty(_microphoneDevice)) return;

        // Rationale: Start microphone capture into a looping AudioClip.
        // Use a longer buffer (e.g., 1 second) to avoid frequent GetData calls missing data.
        _recordingClip = Microphone.Start(_microphoneDevice, true, 1, _sampleRate);
        _lastRecordingPosition = 0;
        _pcmBuffer.Clear(); // Clear any residual PCM data

        // Track microphone usage
        s_ActiveMicrophoneCount++;
        PlayModeCounter.RegisterResource($"AudioManager.Microphone_{GetInstanceID()}", true);

        // Wait briefly for microphone to start (optional but can help)
        // System.Threading.Thread.Sleep(50); // Small delay

        if (Microphone.IsRecording(_microphoneDevice))
        {
            IsRecording = true;
            Debug.Log("Audio recording started.");
        }
        else
        {
             Debug.LogError($"Failed to start microphone: {_microphoneDevice}");
             if (_recordingClip != null) Destroy(_recordingClip);
             _recordingClip = null;

             // Update tracking since microphone failed to start
             s_ActiveMicrophoneCount = Math.Max(0, s_ActiveMicrophoneCount - 1);
             PlayModeCounter.RegisterResource($"AudioManager.Microphone_{GetInstanceID()}", false);
        }
    }

    public void StopRecording()
    {
        if (!IsRecording || string.IsNullOrEmpty(_microphoneDevice)) return;

        if (Microphone.IsRecording(_microphoneDevice))
        {
             Microphone.End(_microphoneDevice);

             // Update tracking since microphone is stopped
             s_ActiveMicrophoneCount = Math.Max(0, s_ActiveMicrophoneCount - 1);
             PlayModeCounter.RegisterResource($"AudioManager.Microphone_{GetInstanceID()}", false);
        }
        if (_recordingClip != null)
        {
            // DestroyImmediate might be needed if called from OnDestroy
            if (Application.isEditor) DestroyImmediate(_recordingClip); else Destroy(_recordingClip);
            _recordingClip = null;
        }
        IsRecording = false;
        _pcmBuffer.Clear(); // Clear buffer on stop
        Debug.Log("Audio recording stopped.");
    }

    private void ProcessMicrophoneInput()
    {
        if (_recordingClip == null || !Microphone.IsRecording(_microphoneDevice)) return;

        int currentPosition = Microphone.GetPosition(_microphoneDevice);
        int samplesAvailable;

        // Rationale: Handle buffer wrap-around correctly.
        if (currentPosition < _lastRecordingPosition)
        {
            // Wrapped around: process data from last position to end, then start to current position
            samplesAvailable = (_recordingClip.samples - _lastRecordingPosition) + currentPosition;
        }
        else
        {
            samplesAvailable = currentPosition - _lastRecordingPosition;
        }

        if (samplesAvailable > 0)
        {
            float[] sampleData = new float[samplesAvailable * _recordingClip.channels];
            _recordingClip.GetData(sampleData, _lastRecordingPosition);
            _lastRecordingPosition = currentPosition;

            // Add new PCM data to our buffer
            // Rationale: Assuming mono (_channels = 1), directly add samples. If stereo, interleave/select channel.
            _pcmBuffer.AddRange(sampleData);

            // Encode available full frames
            EncodeBufferedPcm();
        }
    }

    private void EncodeBufferedPcm()
    {
        // Rationale: Encode PCM data in Opus frame size chunks.
        while (_pcmBuffer.Count >= _opusSamplesPerFrame)
        {
            float[] framePcm = _pcmBuffer.GetRange(0, _opusSamplesPerFrame).ToArray();
            _pcmBuffer.RemoveRange(0, _opusSamplesPerFrame);

            try
            {
                // Rationale: Opus expects short samples, convert float PCM (-1 to 1) to short.
                // Concentus Encode method actually takes floats directly.
                // short[] framePcmShort = new short[_opusSamplesPerFrame];
                // for (int i = 0; i < _opusSamplesPerFrame; i++)
                // {
                //     framePcmShort[i] = (short)(framePcm[i] * short.MaxValue);
                // }

                // Rationale: Allocate buffer for encoded data. Max size can be estimated.
                byte[] encodedData = new byte[1275]; // Max Opus packet size for 120ms at highest bitrate
                int encodedLength = _opusEncoder.Encode(framePcm, 0, _opusSamplesPerFrame, encodedData, 0, encodedData.Length);

                if (encodedLength > 0)
                {
                    // Create final packet with actual length
                    byte[] opusPacket = new byte[encodedLength];
                    Array.Copy(encodedData, 0, opusPacket, 0, encodedLength);

                    // Emit the encoded packet
                    OnOpusPacketEncoded?.Invoke(opusPacket);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Opus encoding error: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Handles received Opus packets from the network. Called by AILinkClient.
    /// </summary>
    public void HandleReceivedOpusPacket(byte[] opusData, long timestamp)
    {
        if (!_playbackEnabled || opusData == null || opusData.Length == 0) return;

        // Rationale: Queue received packets for processing in Update to avoid blocking network thread.
        lock (_playbackQueue) // Ensure thread safety when accessing queue
        {
            _playbackQueue.Enqueue(Tuple.Create(opusData, timestamp));
        }
    }

    private void ProcessPlaybackQueue()
    {
        // Rationale: Play only one queued packet per Update call if not already playing,
        // to avoid potential stuttering if processing takes time.
        if (!_playbackEnabled || _audioSource.isPlaying || _playbackQueue.Count == 0) return;

        Tuple<byte[], long> receivedPacket = null;
        lock (_playbackQueue)
        {
            receivedPacket = _playbackQueue.Dequeue();
        }

        if (receivedPacket != null)
        {
            byte[] opusData = receivedPacket.Item1;
            long timestamp = receivedPacket.Item2;

            try
            {
                // Rationale: Decode Opus packet back to PCM floats.
                int decodedSamples = _opusDecoder.Decode(opusData, 0, opusData.Length, _playbackPcmBuffer, 0, _opusSamplesPerFrame, false);

                if (decodedSamples > 0)
                {
                    // Rationale: Create a new AudioClip and play it immediately.
                    // Ensure buffer matches decoded sample count.
                    float[] playbackFloats = new float[decodedSamples * _channels];
                    Array.Copy(_playbackPcmBuffer, 0, playbackFloats, 0, decodedSamples * _channels);

                    AudioClip playbackClip = AudioClip.Create("Playback", decodedSamples, _channels, _sampleRate, false);
                    playbackClip.SetData(playbackFloats, 0);

                    _audioSource.clip = playbackClip;
                    _audioSource.Play();
                    IsPlaying = true; // Set playing flag

                    // Destroy the clip after it finishes playing to avoid memory leaks
                    // Rationale: Use clip length + buffer time for destruction delay.
                    Destroy(playbackClip, playbackClip.length + 0.1f);

                    // Calculate and report latency
                    if (timestamp > 0)
                    {
                        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        float latencyMs = nowMs - timestamp;
                        OnPlaybackLatencyUpdated?.Invoke(latencyMs);
                    }
                }
                else
                {
                     Debug.LogWarning("Opus decoding produced 0 samples.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Opus decoding/playback error: {e.Message}");
            }
        }
         // Check IsPlaying status after attempting playback
         if (!_audioSource.isPlaying)
         {
             IsPlaying = false;
         }
    }

     // Optional: Could add a callback via AudioSource.finished if needed,
     // but checking isPlaying in Update is often sufficient.
    // void OnAudioPlaybackFinished()
    // {
    //     IsPlaying = false;
    // }
}
