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
        public int UdpPort = 1975;

        [Header("帧同步")]
        public int GameFrameRate = 30;
        public int SnapshotSpacing = 10;

        [Header("心跳")]
        public float HeartbeatTimeoutSec = 4f;

        [Header("UDP")]
        public float KeepAliveInterval = 0.5f;  // UDP 重传定时器间隔
        public int MaxPacketBuffer = 600;       // 最大接收缓冲包数

        [Header("运行模式")]
        public bool RunServerAutomatically = true; // 开发模式下是否自动启动
    }
}
