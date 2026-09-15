namespace MCPForUnity.Editor.Constants
{
    /// <summary>
    /// Constants for health check status values.
    /// Used for coordinating health state between Connection and Advanced sections.
    /// </summary>
    public static class HealthStatus
    {
        public const string Unknown = "未知";
        public const string Healthy = "健康";
        public const string PingFailed = "Ping 失败";
        public const string Unhealthy = "不健康";
    }
}
