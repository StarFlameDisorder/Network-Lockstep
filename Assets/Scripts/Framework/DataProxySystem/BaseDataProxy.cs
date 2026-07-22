namespace Framework
{
    public abstract class BaseDataProxy : IDataProxy
    {
        public abstract string DataName { get; }
        public abstract bool IsNeedSaveToLocal { get; }
        public bool IsInitialized { get; private set; }

        public virtual void Init()
        {
        }

        public void _Init()
        {
            if (IsInitialized) return;

            Load();
            Init();
            IsInitialized = true;
        }

        public virtual void Load()
        {
        }

        public virtual void Save()
        {
        }

        public virtual void Clear()
        {
        }

        public void _Clear()
        {
            if (!IsInitialized) return;

            if (IsNeedSaveToLocal) Save();
            Clear();
            IsInitialized = false;
        }
    }
}
