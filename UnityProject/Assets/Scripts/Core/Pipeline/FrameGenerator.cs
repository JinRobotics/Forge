using System.Collections;
using System.IO;
using UnityEngine;
using Forge.Core.Session;

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
        public float QueueDepthSummary => Backpressure;
        public float CaptureTimeMs { get; private set; }

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
            _captureCameras = FindObjectsOfType<Camera>();
            if (_captureCameras.Length == 0)
            {
                Debug.LogWarning("[FrameGenerator] No cameras found. Captures will be skipped.");
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

            var outputDir = Path.Combine(Application.persistentDataPath, "Sessions", config.sessionId);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            for (int i = 0; i < config.totalFrames && SessionManager.Instance != null && SessionManager.Instance.IsSessionRunning; i++)
            {
                CurrentFrame = i + 1;

                // TODO: Annotation/Encode/Storage 단계로 교체
                var labelPath = Path.Combine(outputDir, $"frame_{CurrentFrame:D6}.json");
                File.WriteAllText(labelPath, $"{{\"frame\":{CurrentFrame},\"session\":\"{config.sessionId}\"}}");

                var imagePath = Path.Combine(outputDir, $"frame_{CurrentFrame:D6}.jpg");
                var start = Time.realtimeSinceStartup;
                CaptureJpg(imagePath, config);
                CaptureTimeMs = (Time.realtimeSinceStartup - start) * 1000f;

                // 간단한 백프레셔 계산: (렌더+저장) 시간 측정 → 목표 FPS 대비 비율
                float targetFrameTime = config.targetFps > 0 ? 1f / config.targetFps : 0.033f;
                Backpressure = Mathf.Clamp01((CaptureTimeMs / 1000f) / targetFrameTime);
                if (Backpressure > 0.7f)
                {
                    Warnings = new[] { $"BACKPRESSURE_HIGH ({Backpressure:0.00})" };
                }
                else
                {
                    Warnings = Array.Empty<string>();
                }

                // 프레임당 한 번씩 넘겨서 Unity 메인 루프를 쉬게 함
                yield return null;
            }

            IsRunning = false;
            SessionManager.Instance?.StopSession();
        }

        private void CaptureJpg(string path, SessionConfig config)
        {
            if (_captureCameras.Length == 0) return;

            int camCount = Mathf.Min(config.cameras != null ? config.cameras.Count : 0, _captureCameras.Length);
            if (camCount == 0) camCount = _captureCameras.Length;

            EnsureBuffers(camCount, config);

            for (int i = 0; i < camCount; i++)
            {
                var cam = _captureCameras[i];
                if (cam == null) continue;
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
