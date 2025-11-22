namespace Forge.Core.Pipeline
{
    public static class QueueMetrics
    {
        public static float NormalizeQueue(int depth, int limit)
        {
            if (limit <= 0) return 0f;
            return depth <= 0 ? 0f : UnityEngine.Mathf.Clamp01((float)depth / limit);
        }
    }
}
