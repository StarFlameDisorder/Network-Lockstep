using UnityEngine;

namespace Network.Server
{
    /// <summary>
    /// 服务器配置（与 GameConstants 互补，管理网络相关配置）
    /// </summary>
    [CreateAssetMenu(fileName = "ServerConfig", menuName = "Factory/ServerConfig")]
    public class ServerConfig : ScriptableObject
    {
        [Header("网络端口")]
        public int TcpPort = 1975;
        public int KcpPort = 1975;

        [Header("帧同步")]
        public int GameFrameRate = 30;
        public int SnapshotSpacing = 10;

        [Header("心跳")]
        public float HeartbeatTimeoutSec = 4f;

        [Header("KCP")] // 原 UDP 已替换为 KCP，以下字段暂保留兼容
        public float KeepAliveInterval = 0.5f;
        public int MaxPacketBuffer = 600;
    }
}
