using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 玩家表现层：从 PlayerEntity 读取逻辑位置，同步到 Unity Transform
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        private PlayerEntity _entity;

        public void Bind(PlayerEntity entity)
        {
            _entity = entity;
            transform.position = entity.Position.ToVector3();
            _entity.SetView(this);
        }

        private void Update()
        {
            if (_entity != null)
            {
                transform.position = _entity.Position.ToVector3();
                // Debug.Log("[Debug][PlayerView]Position: " + transform.position);
            }
        }
    }
}
