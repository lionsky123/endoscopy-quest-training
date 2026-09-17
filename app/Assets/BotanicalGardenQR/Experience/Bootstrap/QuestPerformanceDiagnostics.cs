#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BotanicalGardenQR.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class QuestPerformanceDiagnostics : MonoBehaviour
    {
        const int RecorderCapacity = 512;
        const int MaximumFileBytes = 4 * 1024 * 1024;
        const double InitialDelaySeconds = 5d;
        const double SampleIntervalSeconds = 1d;
        static readonly Encoding Utf8 = new UTF8Encoding(false);
        static QuestPerformanceDiagnostics s_instance;

        Metric _totalUsed;
        Metric _totalReserved;
        Metric _gcUsed;
        Metric _gcReserved;
        Metric _videoUsed;
        Metric _videoReserved;
        Metric _audioUsed;
        Metric _audioReserved;
        Metric _gcAllocated;
        Metric _mainThread;
        Metric _renderThread;
        Metric _gpuFrame;
        Metric _canvasBuildBatch;
        Metric _uiRebuild;
        string _path;
        string _session;
        long _writtenBytes;
        double _nextSample;

#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var root = new GameObject("QuestPerformanceDiagnostics") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(root);
            s_instance = root.AddComponent<QuestPerformanceDiagnostics>();
        }
#endif

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            _session = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
            CreateRecorders();
            try
            {
                var directory = Path.Combine(Application.persistentDataPath, "BotanicalGardenQR", "Diagnostics");
                Directory.CreateDirectory(directory);
                _path = Path.Combine(directory, $"quest-performance-{_session}.csv");
                const string header = "utc,session,scene,app_version,unity_version,device_model,meta_available,meta_time,app_fps,display_refresh_rate,app_gpu_time_us,timewarp_gpu_time_us,cpu_util_pct,gpu_util_pct,cpu_level,gpu_level,app_pss_mb,app_rss_mb,app_uss_mb,available_memory_mb,battery_pct,battery_temp_c,sensor_temp_c,stale_frame_count,stale_frames_consecutive,screen_tear_count,foveation_level,eye_buffer_width,eye_buffer_height,unity_total_used_bytes,unity_total_reserved_bytes,unity_gc_used_bytes,unity_gc_reserved_bytes,unity_video_used_bytes,unity_video_reserved_bytes,unity_audio_used_bytes,unity_audio_reserved_bytes,gc_alloc_p95_bytes,main_thread_p95_ms,render_thread_p95_ms,gpu_frame_p95_ms,canvas_build_batch_p95_ms,ui_rebuild_p95_ms\n";
                File.WriteAllText(_path, header, Utf8);
                _writtenBytes = Utf8.GetByteCount(header);
                _nextSample = Time.realtimeSinceStartupAsDouble + InitialDelaySeconds;
            }
            catch
            {
                enabled = false;
            }
        }

        void Update()
        {
            if (string.IsNullOrWhiteSpace(_path) || Time.realtimeSinceStartupAsDouble < _nextSample) return;
            _nextSample = Time.realtimeSinceStartupAsDouble + SampleIntervalSeconds;
            Capture();
        }

        void OnDestroy()
        {
            DisposeRecorders();
            if (s_instance == this) s_instance = null;
        }

        void Capture()
        {
            OVRMetricsToolSDK.MetricsSnapshot? meta = null;
            try
            {
                meta = OVRMetricsToolSDK.Instance.GetLatestMetricsSnapshot();
            }
            catch
            {
                // Missing device metrics must not affect the visitor experience.
            }

            try
            {
                var line = new StringBuilder(768);
                Add(line, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                Add(line, _session);
                Add(line, SceneManager.GetActiveScene().name);
                Add(line, Application.version);
                Add(line, Application.unityVersion);
                Add(line, SystemInfo.deviceModel);
                Add(line, meta.HasValue ? 1 : 0);
                Add(line, meta?.time);
                Add(line, meta?.average_frame_rate);
                Add(line, meta?.display_refresh_rate);
                Add(line, meta?.app_gpu_time_microseconds);
                Add(line, meta?.timewarp_gpu_time_microseconds);
                Add(line, meta?.cpu_utilization_percentage);
                Add(line, meta?.gpu_utilization_percentage);
                Add(line, meta?.cpu_level);
                Add(line, meta?.gpu_level);
                Add(line, meta?.app_pss_MB);
                Add(line, meta?.app_rss_MB);
                Add(line, meta?.app_uss_MB);
                Add(line, meta?.available_memory_MB);
                Add(line, meta?.battery_level_percentage);
                Add(line, meta?.battery_temperature_celcius);
                Add(line, meta?.sensor_temperature_celcius);
                Add(line, meta?.stale_frame_count);
                Add(line, meta?.stale_frames_consecutive);
                Add(line, meta?.screen_tear_count);
                Add(line, meta?.foveation_level);
                Add(line, meta?.eye_buffer_width);
                Add(line, meta?.eye_buffer_height);
                Add(line, _totalUsed.Last);
                Add(line, _totalReserved.Last);
                Add(line, _gcUsed.Last);
                Add(line, _gcReserved.Last);
                Add(line, _videoUsed.Last);
                Add(line, _videoReserved.Last);
                Add(line, _audioUsed.Last);
                Add(line, _audioReserved.Last);
                Add(line, _gcAllocated.P95());
                Add(line, _mainThread.P95(1e-6));
                Add(line, _renderThread.P95(1e-6));
                Add(line, _gpuFrame.P95(1e-6));
                Add(line, _canvasBuildBatch.P95(1e-6));
                Add(line, _uiRebuild.P95(1e-6));
                line.Append('\n');

                var text = line.ToString();
                var bytes = Utf8.GetByteCount(text);
                if (_writtenBytes + bytes > MaximumFileBytes)
                {
                    enabled = false;
                    return;
                }

                File.AppendAllText(_path, text, Utf8);
                _writtenBytes += bytes;
            }
            catch
            {
                enabled = false;
            }
        }

        void CreateRecorders()
        {
            _totalUsed = new Metric("Total Used Memory");
            _totalReserved = new Metric("Total Reserved Memory");
            _gcUsed = new Metric("GC Used Memory");
            _gcReserved = new Metric("GC Reserved Memory");
            _videoUsed = new Metric("Video Used Memory");
            _videoReserved = new Metric("Video Reserved Memory");
            _audioUsed = new Metric("Audio Used Memory");
            _audioReserved = new Metric("Audio Reserved Memory");
            _gcAllocated = new Metric("GC Allocated In Frame");
            _mainThread = new Metric("CPU Main Thread Frame Time");
            _renderThread = new Metric("CPU Render Thread Frame Time");
            _gpuFrame = new Metric("GPU Frame Time");
            _canvasBuildBatch = new Metric("Canvas.BuildBatch");
            _uiRebuild = new Metric("UI.Rebuild");
        }

        void DisposeRecorders()
        {
            foreach (var metric in new[] { _totalUsed, _totalReserved, _gcUsed, _gcReserved, _videoUsed,
                         _videoReserved, _audioUsed, _audioReserved, _gcAllocated, _mainThread, _renderThread,
                         _gpuFrame, _canvasBuildBatch, _uiRebuild })
                metric?.Dispose();
        }

        static void Add(StringBuilder line, string value)
        {
            if (line.Length > 0) line.Append(',');
            if (string.IsNullOrEmpty(value)) return;
            line.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
        }

        static void Add(StringBuilder line, int? value) => Add(line, value?.ToString(CultureInfo.InvariantCulture));
        static void Add(StringBuilder line, long? value) => Add(line, value?.ToString(CultureInfo.InvariantCulture));
        static void Add(StringBuilder line, double? value) => Add(line, value?.ToString("0.###", CultureInfo.InvariantCulture));

        sealed class Metric : IDisposable
        {
            static readonly Comparison<ProfilerRecorderSample> ByValue = (left, right) => left.Value.CompareTo(right.Value);
            readonly List<ProfilerRecorderSample> _samples = new List<ProfilerRecorderSample>(RecorderCapacity);
            ProfilerRecorder _recorder;

            public Metric(string name)
            {
                _recorder = new ProfilerRecorder(
                    name,
                    RecorderCapacity,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
            }

            public long? Last => _recorder.Valid ? _recorder.LastValue : null;

            public double? P95(double multiplier = 1d)
            {
                if (!_recorder.Valid) return null;
                _samples.Clear();
                _recorder.CopyTo(_samples, false);
                if (_samples.Count == 0) return null;
                _samples.Sort(ByValue);
                var index = Math.Max(0, (int)Math.Ceiling(_samples.Count * 0.95d) - 1);
                return _samples[index].Value * multiplier;
            }

            public void Dispose() => _recorder.Dispose();
        }
    }
}
#endif
