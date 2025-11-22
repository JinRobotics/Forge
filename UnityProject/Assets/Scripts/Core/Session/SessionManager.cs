using System;
using System.Collections.Generic;
using UnityEngine;
using Forge.Core.Pipeline;
using UnityEngine.SceneManagement;

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

                // 필수 필드 검증
                if (string.IsNullOrEmpty(CurrentConfig.sessionId))
                    throw new Exception("sessionId is required");
                if (CurrentConfig.totalFrames <= 0)
                    throw new Exception("totalFrames must be > 0");
                if (CurrentConfig.cameras == null || CurrentConfig.cameras.Count == 0)
                    throw new Exception("at least one camera is required");

                Debug.Log($"[SessionManager] Loaded config for session: {CurrentConfig.sessionId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SessionManager] Failed to load config: {e.Message}");
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
        }

        public void StopSession()
        {
            IsSessionRunning = false;
            Debug.Log($"[SessionManager] Session {CurrentConfig?.sessionId} STOPPED.");
            // Perception usually stops automatically after totalIterations, 
            // but we can force stop or cleanup here.
            FrameGenerator.Instance?.StopGeneration();
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
                foreach (var camCfg in CurrentConfig.cameras)
                {
                    var go = new GameObject($"AutoCam_{camCfg.id}");
                    var cam = go.AddComponent<Camera>();
                    cam.transform.position = camCfg.position;
                    cam.transform.rotation = Quaternion.Euler(camCfg.rotation);
                    cam.fieldOfView = camCfg.fov;
                    cam.tag = "MainCamera";

                    // 해상도 설정은 Perception/렌더 타겟 연동 시 추가 구현 필요
                }
            }
        }
    }
}
