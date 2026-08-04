using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 物品表现层：从 ItemEntity 读取逻辑位置，同步到 Unity Transform。
    /// 由 GameSync 创建并绑定（ItemEntity 纯逻辑，不感知 Unity 生命周期）。
    /// </summary>
    public class ItemView : MonoBehaviour
    {
        private ItemEntity _entity;

        public void Bind(ItemEntity entity)
        {
            _entity = entity;
            transform.position = entity.Position.ToVector3();
            entity.SetView(this);
        }

        private void Update()
        {
            if (_entity != null)
            {
                transform.position = _entity.Position.ToVector3();
            }
        }
    }
}
