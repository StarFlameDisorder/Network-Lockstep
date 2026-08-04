using FrameSync;
using GamePlay;
using UnityEngine;

namespace UI.View
{
    /// <summary>
    /// 协作搬运 demo 简易 HUD（OnGUI，零场景/UI 资源依赖）：
    /// 计分（已送达/目标）+ 操作提示 + 完成提示。
    /// 由 GameSync 创建；仅游戏开始后显示。
    /// </summary>
    public class CargoHud : MonoBehaviour
    {
        private void OnGUI()
        {
            var sync = GameSync.Instance;
            if (sync == null) return;

            // 仅游戏中显示（大厅/暂停不显示）
            if (sync.GetStatus() != GameStatus.Started) return;

            var items = sync.Items;
            if (items.Count == 0) return;

            int delivered = sync.DeliveredTotal;
            int target = CargoConfig.DeliverTarget;

            GUILayout.BeginArea(new Rect(10, 10, 280, 90));
            GUILayout.Label($"<size=22><b>搬运: {delivered}/{target}</b></size>");
            if (delivered >= target)
                GUILayout.Label("<size=16><color=green><b>任务完成！</b></color></size>");
            else
                GUILayout.Label("提示: WASD 移动 | E 拾取/放置 | 火车处送达");
            GUILayout.EndArea();
        }
    }
}
