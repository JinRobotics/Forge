using System.Collections;
using System.IO;
using UnityEngine;
using Forge.Core.Session;

namespace Forge.Core.Pipeline
{
    /// <summary>
    /// Phase 1 간단한 프레임 생성 스텁.
    /// PerceptionCamera 기반 캡처 대신 더미 파일을 기록하여 파이프라인 흐름을 검증한다.
    /// 추후 Capture/Annotation/Encode/Storage 단계로 교체한다.
    /// </summary>
    public class FrameGenerator : MonoBehaviour
    {
        public static FrameGenerator Instance { get; private set; }

        public int CurrentFrame { get; private set; }
        public bool IsRunning { get; private set; }

        private Coroutine _loop;

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
            _loop = StartCoroutine(GenerateFrames(config));
        }

        public void StopGeneration()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
            IsRunning = false;
        }

        private IEnumerator GenerateFrames(SessionConfig config)
        {
            IsRunning = true;
            CurrentFrame = 0;

            var outputDir = Path.Combine(Application.persistentDataPath, "Sessions", config.sessionId);
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            for (int i = 0; i < config.totalFrames && SessionManager.Instance != null && SessionManager.Instance.IsSessionRunning; i++)
            {
                CurrentFrame = i + 1;

                // TODO: 실제 Capture/Annotation/Encode/Storage로 교체
                var dummyLabelPath = Path.Combine(outputDir, $"frame_{CurrentFrame:D6}.json");
                File.WriteAllText(dummyLabelPath, $"{{\"frame\":{CurrentFrame},\"session\":\"{config.sessionId}\"}}");

                // 프레임당 한 번씩 넘겨서 Unity 메인 루프를 쉬게 함
                yield return null;
            }

            IsRunning = false;
            SessionManager.Instance?.StopSession();
        }
    }
}
