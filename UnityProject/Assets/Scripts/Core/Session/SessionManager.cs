using System;
using System.Collections.Generic;
using UnityEngine;
using Forge.Core.Pipeline;
using UnityEngine.SceneManagement;
using System.IO;

namespace Forge.Core.Session
{
    [Serializable]
    public class SessionConfig
    {
        public string sessionId;
        public int totalFrames;
        public int targetFps;
        public string sceneName;
        public string qualityMode; // strict | relaxed
        public string frameRatePolicy; // quality_first | throughput_first | balanced
        public List<CameraConfig> cameras;
    }

    [Serializable]
    public class CameraConfig
    {
        public string id;
        public Vector3 position;
        public Vector3 rotation;
        public float fov;
        public int width = 1920;
        public int height = 1080;
    }

    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }
        public SessionConfig CurrentConfig { get; private set; }
        public bool IsSessionRunning { get; private set; }
        private readonly List<SessionSnapshot> _sessions = new List<SessionSnapshot>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void LoadConfig(string jsonConfig)
        {
            try
            {
                CurrentConfig = JsonUtility.FromJson<SessionConfig>(jsonConfig);

                SessionConfigValidator.Validate(CurrentConfig);

                Debug.Log($"[SessionManager] Loaded config for session: {CurrentConfig.sessionId}");
                UpsertSessionSnapshot("ready", 0f, 0f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionManager] Failed to load config: {e.Message}");
                throw;
            }
        }

        public void StartSession()
        {
            if (CurrentConfig == null)
            {
                Debug.LogError("[SessionManager] Cannot start session: No config loaded.");
                return;
            }

            if (CurrentConfig.targetFps > 0)
            {
                Application.targetFrameRate = CurrentConfig.targetFps;
            }

            SetupSceneAndCameras();

            IsSessionRunning = true;
            Debug.Log($"[SessionManager] Session {CurrentConfig.sessionId} STARTED.");
            
            // Configure Perception Scenario
            if (ForgeScenario.Instance != null)
            {
                ForgeScenario.Instance.Configure(CurrentConfig.totalFrames, CurrentConfig.sessionId.GetHashCode());
            }
            else
            {
                Debug.LogError("[SessionManager] ForgeScenario instance not found!");
            }

            // Frame generation stub
            if (FrameGenerator.Instance != null)
            {
                FrameGenerator.Instance.StartGeneration(CurrentConfig);
            }
            else
            {
                Debug.LogWarning("[SessionManager] FrameGenerator not found in scene.");
            }

            UpsertSessionSnapshot("running", 0f, CurrentConfig.targetFps);
        }

        public void StopSession()
        {
            IsSessionRunning = false;
            Debug.Log($"[SessionManager] Session {CurrentConfig?.sessionId} STOPPED.");
            // Perception usually stops automatically after totalIterations, 
            // but we can force stop or cleanup here.
            FrameGenerator.Instance?.StopGeneration();

            UpsertSessionSnapshot("stopped", 1f, 0f);
        }

        // InitializeScene is no longer needed as Perception handles scene setup via Randomizers
        // private void InitializeScene() { ... }

        private void SetupSceneAndCameras()
        {
            // Scene 로드 (동일 이름이면 스킵)
            if (!string.IsNullOrEmpty(CurrentConfig.sceneName))
            {
                var active = SceneManager.GetActiveScene().name;
                if (!string.Equals(active, CurrentConfig.sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        SceneManager.LoadScene(CurrentConfig.sceneName);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[SessionManager] Scene load failed ({CurrentConfig.sceneName}): {e.Message}");
                    }
                }
            }

            // 기존 자동 생성 카메라 정리 후 Config 기반 카메라 생성
            foreach (var autoCam in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                Destroy(autoCam);
            }

            if (CurrentConfig.cameras != null)
            {
                SetupPerceptionCameras(CurrentConfig.cameras);
            }
        }

        private void SetupPerceptionCameras(List<CameraConfig> cameras)
        {
            foreach (var camCfg in cameras)
            {
                var go = new GameObject($"AutoCam_{camCfg.id}");
                var cam = go.AddComponent<Camera>();
                cam.transform.position = camCfg.position;
                cam.transform.rotation = Quaternion.Euler(camCfg.rotation);
                cam.fieldOfView = camCfg.fov;
                cam.tag = "MainCamera";

                // PerceptionCamera 부착 (필요 패키지 존재 시)
                var perceptionType = Type.GetType("UnityEngine.Perception.GroundTruth.PerceptionCamera, Unity.Perception");
                if (perceptionType != null)
                {
                    go.AddComponent(perceptionType);
                }

                // 해상도 설정 (렌더 타겟 기준)
                if (cam.targetTexture == null && camCfg.width > 0 && camCfg.height > 0)
                {
                    cam.targetTexture = new RenderTexture(camCfg.width, camCfg.height, 24);
                }
            }
        }

        public IReadOnlyList<SessionSnapshot> GetSessions() => _sessions.AsReadOnly();

        public void UpdateProgress(float progress, float fps, float backpressure, string[] warnings)
        {
            if (CurrentConfig == null) return;
            UpsertSessionSnapshot(IsSessionRunning ? "running" : "stopped", progress, fps, backpressure, warnings);
        }

        private void UpsertSessionSnapshot(string status, float progress, float fps, float backpressure = 0f, string[] warnings = null)
        {
            if (CurrentConfig == null) return;
            var snap = _sessions.Find(s => s.sessionId == CurrentConfig.sessionId);
            if (snap == null)
            {
                snap = new SessionSnapshot { sessionId = CurrentConfig.sessionId };
                _sessions.Add(snap);
            }
            snap.status = status;
            snap.progress = progress;
            snap.qualityMode = CurrentConfig.qualityMode;
            snap.frameRatePolicy = CurrentConfig.frameRatePolicy;
            snap.updatedAt = DateTime.UtcNow.ToString("o");
            snap.fps = fps;
            snap.totalFrames = CurrentConfig.totalFrames;
            snap.backpressure = backpressure;
            snap.warnings = warnings ?? Array.Empty<string>();
        }
    }

    [Serializable]
    public class SessionSnapshot
    {
        public string sessionId;
        public string status;
        public float progress;
        public string qualityMode;
        public string frameRatePolicy;
        public string updatedAt;
        public float fps;
        public int totalFrames;
        public float backpressure;
        public string[] warnings;
    }
}
