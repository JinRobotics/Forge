using UnityEngine;

namespace Forge.Core.Pipeline
{
    /// <summary>
    /// Phase 1 더미 라벨러: 중심 bbox를 생성해 JSON으로 저장.
    /// 향후 GT 기반 Annotation 단계로 교체.
    /// </summary>
    public static class SimpleLabeler
    {
        public static string GenerateLabelJson(int frameId, string sessionId, int width, int height)
        {
            int bboxW = Mathf.Max(32, width / 4);
            int bboxH = Mathf.Max(32, height / 4);
            int x = (width - bboxW) / 2;
            int y = (height - bboxH) / 2;
            return $"{{\"frame\":{frameId},\"session\":\"{sessionId}\",\"bbox\":[{x},{y},{bboxW},{bboxH}]}}";
        }
    }
}
