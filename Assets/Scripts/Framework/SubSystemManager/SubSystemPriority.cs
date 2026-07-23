namespace Framework
{
    /// <summary>
    /// 子系统优先级 越低越优先
    /// </summary>
    public enum SubSystemPriority
    {
        SystemManager = int.MinValue,
        GameServer = -200000,
        GameClient = -190000,
        TimerManager = -120,
        DataProxyManager = -100,

    }
}
