namespace Core
{
    /// <summary>
    /// 全局游戏常量（代码内可调参数统一入口）。
    /// 服务端网络设置（端口/帧率/心跳超时等资产可编辑项）另见 ServerConfig（ScriptableObject）。
    /// </summary>
    public class GameConstants
    {
        public const bool VERBOSE_INFO = true;

        // ── 帧同步 ──
        /// <summary>逻辑帧率（服务端/客户端一致）</summary>
        public const int GAME_FRAME_RATE = 30;
        /// <summary>快照上报间隔（秒 × 帧率：30fps × 10 = 每 10 秒上报一次）</summary>
        public const int SNAPSHOT_SPACING = 10;
        /// <summary>客户端接收缓冲帧数（吸收网络抖动；也是执行滞后的下限）</summary>
        public const int BUFFER_SIZE = 3;
        /// <summary>每帧最大追帧数（缓冲超阈值时快速追平服务端帧号）</summary>
        public const int MAX_CATCHUP = 5;
        /// <summary>
        /// 客户端输入管线深度（帧）：允许提前发送的输入数上限。
        /// 在途输入 = 已发送帧 - 服务端已消费帧；超过此值暂停发送（延迟有界，防 tick 漂移无界积压）。
        /// 权衡：值 ≥ (RTT+服务端门控)/帧间隔 才喂得饱服务器；越大输入延迟越大。本机/LAN 3~4，高 RTT 网络 6~8。
        /// </summary>
        public const int MAX_INPUT_PIPELINE = 10;
        /// <summary>哈希上报间隔（已执行帧数，30 ≈ 1 秒；按 30 的倍数里程碑上报，保证各端同帧可比）</summary>
        public const ulong HASH_REPORT_INTERVAL = 30;

        // ── 心跳 / 保活 ──
        /// <summary>客户端心跳发送间隔（秒）</summary>
        public const float CLIENT_HEARTBEAT_INTERVAL = 1f;
        /// <summary>服务端心跳超时检测节流（秒）</summary>
        public const float SERVER_HEARTBEAT_CHECK_INTERVAL = 1f;
        /// <summary>服务端保活包广播间隔（秒；客户端据此做"收包超时"断线检测）</summary>
        public const float SERVER_KEEPALIVE_INTERVAL = 1f;

        // ── 队列 / 缓存上限 ──
        /// <summary>服务端输入队列积压告警阈值（超过即提示 tick 漂移）</summary>
        public const int INPUT_QUEUE_WARN_THRESHOLD = 4;
        /// <summary>服务端广播积压排空阈值：任一玩家输入队列 ≥ 此值时提前广播（降延迟，防延迟涨到管线深度）</summary>
        public const int SERVER_BROADCAST_DRAIN_QUEUE = 2;
        /// <summary>每玩家历史帧缓存上限（防内存无限增长）</summary>
        public const int MAX_CACHED_FRAMES = 600;
        /// <summary>KcpServer 待发缓存上限（conv 会话未创建时暂存消息）</summary>
        public const int MAX_PENDING_SENDS = 64;
    }
}
