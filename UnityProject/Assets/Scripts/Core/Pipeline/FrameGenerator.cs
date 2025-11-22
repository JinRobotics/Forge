using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Forge.Core.Session;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.Perception.GroundTruth;

namespace Forge.Core.Pipeline
{
    /// <summary>
    /// Phase 1 간단한 프레임 생성 스텁.
    /// PerceptionCamera 기반 캡처 대신 실제 카메라 렌더 타겟을 JPG/JSON으로 저장한다.
    /// 추후 Annotation/Encode 파이프라인으로 교체한다.
    /// </summary>
    public class FrameGenerator : MonoBehaviour
    {
        public static FrameGenerator Instance { get; private set; }

        public int CurrentFrame { get; private set; }
        public bool IsRunning { get; private set; }
        public float Backpressure { get; private set; }
        public string[] Warnings { get; private set; } = Array.Empty<string>();
        public float QueueDepthSummary { get; private set; }
        public float CaptureTimeMs { get; private set; }
        private readonly System.Collections.Generic.Queue<float> _frameTimes = new System.Collections.Generic.Queue<float>();
        private const int WindowSize = 30;

        // 간단한 큐 시뮬레이션 (Capture 단계 큐 길이)
        private int _queueDepth = 0;
        private const int QueueLimit = 64;
        private int _errorCount = 0;
        private int _skipDueToErrors = 0;
        private int _skipDueToBackpressure = 0;
        private readonly System.Collections.Generic.Queue<int> _queueDepthHistory = new System.Collections.Generic.Queue<int>();
        private const int QueueDepthWindow = 30;
        private const int MaxRetryPerFrame = 3;
        private bool _noCameraWarning = false;
        private string _outputDir;
        private float _captureMsSum = 0f;
        private int _captureCount = 0;
        private float _backpressureSum = 0f;

        private Coroutine _loop;
        private Camera[] _captureCameras = Array.Empty<Camera>();
        private RenderTexture[] _rts = Array.Empty<RenderTexture>();
        private Texture2D[] _texes = Array.Empty<Texture2D>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void StartGeneration(SessionConfig config)
        {
            StopGeneration();
            _captureCameras = FindObjectsOfType<Camera>()
                .Where(c => c != null && c.enabled && (c.GetComponent<PerceptionCamera>() != null || c.CompareTag("MainCamera")))
                .ToArray();
            if (_captureCameras.Length == 0)
            {
                Debug.LogWarning("[FrameGenerator] No cameras found (Perception or MainCamera). Captures will be skipped.");
                _noCameraWarning = true;
            }
            _loop = StartCoroutine(GenerateFrames(config));
        }

        public void StopGeneration()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
            CleanupCapture();
            IsRunning = false;
        }

        private IEnumerator GenerateFrames(SessionConfig config)
        {
            IsRunning = true;
            CurrentFrame = 0;
            Backpressure = 0f;
            Warnings = Array.Empty<string>();
            CaptureTimeMs = 0f;

            _outputDir = Path.Combine(Application.persistentDataPath, "Sessions", config.sessionId);
            if (!Directory.Exists(_outputDir))
            {
                Directory.CreateDirectory(_outputDir);
            }
            _captureMsSum = 0f;
            _captureCount = 0;
            _backpressureSum = 0f;
            _skipDueToBackpressure = 0;

            for (int i = 0; i < config.totalFrames && SessionManager.Instance != null && SessionManager.Instance.IsSessionRunning; i++)
            {
                CurrentFrame = i + 1;

                // TODO: Annotation/Encode/Storage 단계로 교체
                // 더미 라벨 JSON 생성 (GT Annotation 대체)
                var labelPath = Path.Combine(_outputDir, $"frame_{CurrentFrame:D6}.json");
                var labelJson = SimpleLabeler.GenerateLabelJson(CurrentFrame, config.sessionId, config.cameras?[0].width ?? Screen.width, config.cameras?[0].height ?? Screen.height);
                File.WriteAllText(labelPath, labelJson);

                var imagePath = Path.Combine(_outputDir, $"frame_{CurrentFrame:D6}.jpg");
                var start = Time.realtimeSinceStartup;
                int attempts = 0;
                bool success = false;
                while (attempts < MaxRetryPerFrame && !success)
                {
                    try
                    {
                        attempts++;
                        CaptureJpg(imagePath, config);
                        success = true;
                    }
                    catch (System.Exception e)
                    {
                        _errorCount++;
                        if (attempts >= MaxRetryPerFrame)
                        {
                            _skipDueToErrors++;
                            Debug.LogError($"[FrameGenerator] Capture failed after retries: {e.Message}");
                        }
                        else
                        {
                            Debug.LogWarning($"[FrameGenerator] Capture retry {attempts}: {e.Message}");
                        }
                    }
                }
                CaptureTimeMs = (Time.realtimeSinceStartup - start) * 1000f;
                _captureMsSum += CaptureTimeMs;
                _captureCount++;
                _queueDepth = Mathf.Max(0, _queueDepth - 1);

                // 간단한 백프레셔 계산: 최근 WindowSize 평균 프레임시간 + 큐 평균
                _frameTimes.Enqueue(CaptureTimeMs);
                while (_frameTimes.Count > WindowSize) _frameTimes.Dequeue();
                float avgMs = 0f;
                foreach (var t in _frameTimes) avgMs += t;
                avgMs /= _frameTimes.Count;
                float targetFrameTime = config.targetFps > 0 ? 1000f / config.targetFps : 33f;
                float timeBackpressure = Mathf.Clamp01(avgMs / targetFrameTime);
                _queueDepthHistory.Enqueue(_queueDepth);
                while (_queueDepthHistory.Count > QueueDepthWindow) _queueDepthHistory.Dequeue();
                float avgQueue = 0f;
                foreach (var q in _queueDepthHistory) avgQueue += q;
                avgQueue = _queueDepthHistory.Count > 0 ? avgQueue / _queueDepthHistory.Count : 0f;
                float queueBackpressure = QueueMetrics.NormalizeQueue((int)avgQueue, QueueLimit);
                Backpressure = Mathf.Clamp01(Mathf.Max(timeBackpressure, queueBackpressure));
                _backpressureSum += Backpressure;
                QueueDepthSummary = queueBackpressure;
                var warns = new List<string>();
                if (Backpressure > 0.7f) warns.Add($"BACKPRESSURE_HIGH ({Backpressure:0.00})");
                if (_skipDueToBackpressure > 0) warns.Add($"SKIP_BACKPRESSURE ({_skipDueToBackpressure})");
                if (_errorCount > 0) warns.Add($"CAPTURE_ERRORS ({_errorCount})");
                if (_skipDueToErrors > 0) warns.Add($"CAPTURE_SKIPPED ({_skipDueToErrors})");
                if (_noCameraWarning) warns.Add("NO_CAPTURE_CAMERA");
                Warnings = warns.ToArray();

                // 진행률/추정 FPS 업데이트 (세션 스냅샷)
                SessionManager.Instance.UpdateProgress(
                    (float)CurrentFrame / config.totalFrames,
                    CaptureTimeMs > 0 ? 1000f / CaptureTimeMs : 0f,
                    Backpressure,
                    Warnings);

                // 프레임당 한 번씩 넘겨서 Unity 메인 루프를 쉬게 함
                yield return null;
            }

            IsRunning = false;
            SessionManager.Instance?.StopSession();

            // 보고서 생성 (manifest/validation/statistics 간이 버전)
            float avgCaptureMs = _captureCount > 0 ? _captureMsSum / _captureCount : 0f;
            float avgBackpressure = _captureCount > 0 ? _backpressureSum / _captureCount : 0f;
            ReportGenerator.WriteReports(_outputDir, config, CurrentFrame, avgCaptureMs, avgBackpressure, Warnings);
        }

        private void CaptureJpg(string path, SessionConfig config)
        {
            if (_captureCameras.Length == 0) return;

            int camCount = Mathf.Min(config.cameras != null ? config.cameras.Count : 0, _captureCameras.Length);
            if (camCount == 0) camCount = _captureCameras.Length;

            EnsureBuffers(camCount, config);

            _queueDepth = Mathf.Min(QueueLimit, _queueDepth + camCount);

            for (int i = 0; i < camCount; i++)
            {
                var cam = _captureCameras[i];
                if (cam == null) continue;
                if (_errorCount > 50) continue; // 오류가 과도하면 캡처 스킵
                var rt = _rts[i];
                var tex = _texes[i];
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                var bytes = tex.EncodeToJPG(75);
                var camId = (config.cameras != null && i < config.cameras.Count) ? config.cameras[i].id : $"cam{i}";
                var camPath = path.Replace(".jpg", $"_{camId}.jpg");
                File.WriteAllBytes(camPath, bytes);
                cam.targetTexture = null;
            }

            RenderTexture.active = null;
        }

        private void EnsureBuffers(int count, SessionConfig config)
        {
            if (_rts.Length != count)
            {
                CleanupCapture();
                _rts = new RenderTexture[count];
                _texes = new Texture2D[count];
            }

            for (int i = 0; i < count; i++)
            {
                int w = (config.cameras != null && i < config.cameras.Count) ? config.cameras[i].width : Screen.width;
                int h = (config.cameras != null && i < config.cameras.Count) ? config.cameras[i].height : Screen.height;
                if (_rts[i] == null || _rts[i].width != w || _rts[i].height != h)
                {
                    if (_rts[i] != null) { _rts[i].Release(); UnityEngine.Object.Destroy(_rts[i]); }
                    if (_texes[i] != null) { UnityEngine.Object.Destroy(_texes[i]); }
                    _rts[i] = new RenderTexture(w, h, 24);
                    _texes[i] = new Texture2D(w, h, TextureFormat.RGB24, false);
                }
            }
        }

        private void CleanupCapture()
        {
            if (_rts != null)
            {
                foreach (var rt in _rts)
                {
                    if (rt == null) continue;
                    rt.Release();
                    UnityEngine.Object.Destroy(rt);
                }
            }
            if (_texes != null)
            {
                foreach (var tex in _texes)
                {
                    if (tex == null) continue;
                    UnityEngine.Object.Destroy(tex);
                }
            }
            _rts = Array.Empty<RenderTexture>();
            _texes = Array.Empty<Texture2D>();
        }
    }
}
