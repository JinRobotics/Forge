using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Forge.Core.Session;

namespace Forge.Core.Network
{
    public class SimulationServer : MonoBehaviour
    {
        [SerializeField] private int _port = 8080;
        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning = false;

        private void Start()
        {
            StartServer();
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void StartServer()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _isRunning = true;
                
                _listenerThread = new Thread(ListenLoop);
                _listenerThread.Start();
                
                Debug.Log($"[SimulationServer] Listening on port {_port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimulationServer] Failed to start: {e.Message}");
            }
        }

        private void StopServer()
        {
            _isRunning = false;
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Close();
            }
            if (_listenerThread != null)
            {
                _listenerThread.Abort(); // Not ideal, but simple for Phase 1
            }
        }

        private void ListenLoop()
        {
            while (_isRunning && _listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Listener closed
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SimulationServer] Error: {e.Message}");
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string responseString = "{}";
            int statusCode = 200;

            if (request.Url.AbsolutePath == "/status")
            {
                // Get status from SessionManager and ForgeScenario
                var manager = SessionManager.Instance;
                var scenario = ForgeScenario.Instance;
                
                // Get current iteration (exposed via public property in ForgeScenario)
                int currentFrame = scenario != null ? scenario.CurrentIteration : 0;
                int totalFrames = scenario != null ? scenario.constants.iterationCount : 1;
                
                var status = new
                {
                    isRunning = manager != null && manager.IsSessionRunning,
                    sessionId = manager?.CurrentConfig?.sessionId ?? "none",
                    fps = 1.0f / Time.smoothDeltaTime,
                    progress = (float)currentFrame / totalFrames,
                    currentFrame = currentFrame,
                    totalFrames = totalFrames
                };
                responseString = JsonUtility.ToJson(status);
                response.ContentType = "application/json";
            }
            else
            {
                statusCode = 404;
                responseString = "Not Found";
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.StatusCode = statusCode;
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
