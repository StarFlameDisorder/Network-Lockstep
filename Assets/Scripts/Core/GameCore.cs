using Framework;
using GamePlay;
using Network.Client;
using Network.Server;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 游戏核心索引
    /// </summary>
    
    public class GameCore: MonoSingleton<GameCore>
    {
        #region 组件
        public SystemManager SystemMgr { get; private set; }
        public DataProxyManager DataProxyMgr { get; private set; }
        public GameServer GameServer { get; private set; }
        public GameClient GameClient { get; private set; }
        public GameSync GameSync { get; private set; }
        #endregion

        #region 属性

        public bool IsPaused;
        
        [SerializeField] private GameObject _playerPrefab;

        #endregion
        
        private void InitializeGameCore()
        {
            Application.targetFrameRate = 60;
            Screen.SetResolution(1920, 1080,FullScreenMode.Windowed);
            Application.runInBackground = true;
            
            InitSubSystems();
            InitDataProxy();
        }
        
        /// <summary>
        /// 初始化所有子系统
        /// </summary>
        private void InitSubSystems()
        {
            // 初始化子系统管理模块
            SystemMgr = new SystemManager();
            SystemMgr._Init();
            
            // 框架模块
            DataProxyMgr = SystemMgr.RegisterSystem<DataProxyManager>();
            
            // 服务端子系统中立（独立线程运行网络监听）
            GameServer = SystemMgr.RegisterSystem<GameServer>();
            GameClient = SystemMgr.RegisterSystem<GameClient>();
            
            // 帧同步主控
            GameSync = SystemMgr.RegisterSystem<GameSync>();
            GameSync.InjectPrefab(_playerPrefab);
            GameSync.PostInit();
        }
        
        /// <summary>
        /// 初始化全局游戏数据
        /// </summary>
        private void InitDataProxy()
        {
            // DataProxyMgr.RegisterDataProxy<GameSettingsProxy>();
            // DataProxyMgr.RegisterDataProxy<GameModelProxy>();
        }
        
        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        protected override void Init()
        {
            InitializeGameCore();
        }

        private void Update()
        {
            if (IsPaused) return;
            
            SystemMgr.Update(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (IsPaused) return;
            
            SystemMgr.LateUpdate();
        }

        private void FixedUpdate()
        {
            if (IsPaused) return;
            
            SystemMgr.FixedUpdate(Time.fixedDeltaTime);
        }

        protected override void Destroy()
        {
            SystemMgr.Destroy();
            Global.Clear();
        }

        /// <summary>
        /// 程序退出清理
        /// </summary>
        private void OnApplicationQuit()
        {
            // 先清理所有的子模块
            ShutDown();
        }
    }
}