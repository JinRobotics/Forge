using System;
using System.Net;
using System.IO;
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
        private CancellationTokenSource _cts;
        private Task _listenTask;

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

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));

                Debug.Log($"[SimulationServer] Listening on port {_port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SimulationServer] Failed to start: {e.Message}");
            }
        }

        private void StopServer()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                _listenTask?.Wait(500);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SimulationServer] StopServer warning: {e.Message}");
            }
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (_listener != null && _listener.IsListening && !token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
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

            try
            {
                switch (request.Url.AbsolutePath)
                {
                    case "/session/init":
                        if (request.HttpMethod != "POST") { statusCode = 405; break; }
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            var body = reader.ReadToEnd();
                            SessionManager.Instance?.LoadConfig(body);
                            responseString = "{\"ok\":true}";
                        }
                        break;
                    case "/session/start":
                        SessionManager.Instance?.StartSession();
                        responseString = "{\"ok\":true}";
                        break;
                    case "/session/stop":
                        SessionManager.Instance?.StopSession();
                        responseString = "{\"ok\":true}";
                        break;
                    case "/status":
                        BuildStatus(out responseString);
                        response.ContentType = "application/json";
                        break;
                    default:
                        statusCode = 404;
                        responseString = "Not Found";
                        break;
                }
            }
            catch (Exception e)
            {
                statusCode = 500;
                responseString = JsonUtility.ToJson(new { error = e.Message });
                Debug.LogError($"[SimulationServer] Request error: {e.Message}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.StatusCode = statusCode;
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private void BuildStatus(out string json)
        {
            var manager = SessionManager.Instance;
            var scenario = ForgeScenario.Instance;

            var frameGen = Forge.Core.Pipeline.FrameGenerator.Instance;

            int currentFrame = frameGen != null ? frameGen.CurrentFrame : (scenario != null ? scenario.CurrentIteration : 0);
            int totalFrames = scenario != null ? scenario.TotalIterations : Math.Max(manager?.CurrentConfig?.totalFrames ?? 1, 1);

            var status = new
            {
                isRunning = manager != null && manager.IsSessionRunning,
                sessionId = manager?.CurrentConfig?.sessionId ?? "none",
                fps = Time.smoothDeltaTime > 0 ? 1.0f / Time.smoothDeltaTime : 0f,
                progress = totalFrames > 0 ? (float)currentFrame / totalFrames : 0f,
                currentFrame = currentFrame,
                totalFrames = totalFrames,
                simulationTick = scenario?.CurrentIteration ?? 0,
                backpressure = 0f, // placeholder for Phase 1
                qualityMode = manager?.CurrentConfig?.qualityMode ?? "strict",
                frameRatePolicy = manager?.CurrentConfig?.frameRatePolicy ?? "quality_first"
            };

            json = JsonUtility.ToJson(status);
        }
    }
}
