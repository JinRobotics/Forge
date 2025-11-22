using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Forge.Core.Session;

namespace Forge.Editor
{
    public class SceneEditorWindow : EditorWindow
    {
        private string _sessionId = "session_001";
        private int _totalFrames = 1000;
        private int _targetFps = 30;

        [MenuItem("Forge/Scene Editor")]
        public static void ShowWindow()
        {
            GetWindow<SceneEditorWindow>("Forge Scene Editor");
        }

        private void OnGUI()
        {
            GUILayout.Label("Session Configuration", EditorStyles.boldLabel);
            _sessionId = EditorGUILayout.TextField("Session ID", _sessionId);
            _totalFrames = EditorGUILayout.IntField("Total Frames", _totalFrames);
            _targetFps = EditorGUILayout.IntField("Target FPS", _targetFps);

            GUILayout.Space(20);
            GUILayout.Label("Cameras", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Add Camera at View"))
            {
                AddCameraAtView();
            }

            if (GUILayout.Button("Export Session Config"))
            {
                ExportConfig();
            }
        }

        private void AddCameraAtView()
        {
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                var camObj = new GameObject($"Camera_{Random.Range(100, 999)}");
                camObj.tag = "MainCamera"; // Or custom tag
                camObj.transform.position = view.camera.transform.position;
                camObj.transform.rotation = view.camera.transform.rotation;
                var cam = camObj.AddComponent<Camera>();
                
                // Add Perception Camera
                camObj.AddComponent<UnityEngine.Perception.GroundTruth.PerceptionCamera>();
                // Add Randomizer Tag
                camObj.AddComponent<Forge.Core.Randomizers.CameraPlacementRandomizerTag>();

                Debug.Log($"[SceneEditor] Added Perception Camera at {camObj.transform.position}");
            }
        }

        private void ExportConfig()
        {
            var config = new SessionConfig
            {
                sessionId = _sessionId,
                totalFrames = _totalFrames,
                targetFps = _targetFps,
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                cameras = new List<CameraConfig>()
            };

            var cameras = FindObjectsOfType<Camera>();
            foreach (var cam in cameras)
            {
                config.cameras.Add(new CameraConfig
                {
                    id = cam.name,
                    position = cam.transform.position,
                    rotation = cam.transform.eulerAngles,
                    fov = cam.fieldOfView
                });
            }

            string json = JsonUtility.ToJson(config, true);
            string path = Path.Combine(Application.streamingAssetsPath, $"{_sessionId}.json");
            
            // Ensure StreamingAssets exists
            if (!Directory.Exists(Application.streamingAssetsPath))
                Directory.CreateDirectory(Application.streamingAssetsPath);

            File.WriteAllText(path, json);
            Debug.Log($"[SceneEditor] Exported config to {path}");
            EditorUtility.RevealInFinder(path);
        }
    }
}
