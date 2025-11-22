using System;
using System.Collections.Generic;
using UnityEngine;

namespace Forge.Core.Session
{
    [Serializable]
    public class SessionConfig
    {
        public string sessionId;
        public int totalFrames;
        public int targetFps;
        public string sceneName;
        public List<CameraConfig> cameras;
    }

    [Serializable]
    public class CameraConfig
    {
        public string id;
        public Vector3 position;
        public Vector3 rotation;
        public float fov;
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
        }

        public void StopSession()
        {
            IsSessionRunning = false;
            Debug.Log($"[SessionManager] Session {CurrentConfig?.sessionId} STOPPED.");
            // Perception usually stops automatically after totalIterations, 
            // but we can force stop or cleanup here.
        }

        // InitializeScene is no longer needed as Perception handles scene setup via Randomizers
        // private void InitializeScene() { ... }
    }
}
