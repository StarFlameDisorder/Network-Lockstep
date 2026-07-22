using Framework;
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
        #endregion

        #region 属性

        public bool IsPaused;

        #endregion
        
        private void InitializeGameCore()
        {
            Application.targetFrameRate = 60;
            Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
            
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