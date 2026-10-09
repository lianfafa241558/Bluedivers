namespace FPSGame.Net
{
    /// <summary>
    /// 【传输层自有命令号】只放"与游戏内容无关"的控制报文（心跳等）。
    /// <para>游戏业务命令号见 <c>07_NetGame</c> 的 <c>CmdId</c>。分段：1xxx = 传输控制。</para>
    /// </summary>
    public static class NetCmdId
    {
        /// <summary>成员 -> 房主：心跳</summary>
        public const int PingReq = 1001;
        /// <summary>房主 -> 成员：心跳应答</summary>
        public const int PingRsp = 1002;
    }
}
