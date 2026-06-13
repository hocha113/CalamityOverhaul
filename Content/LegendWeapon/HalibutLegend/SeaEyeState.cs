using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>
    /// 单只领域之眼数据（持久化于 <see cref="HalibutSave"/>）
    /// 悬停、辉光、眨眼由 UI 层维护，本类无绘制状态
    /// </summary>
    public class SeaEyeState
    {
        /// <summary>
        /// 眼睛在外圈的固定序号（0-8），决定其轨道角度
        /// </summary>
        public int Index;
        /// <summary>
        /// 是否处于激活状态
        /// </summary>
        public bool IsActive;
        /// <summary>
        /// 激活层数（按激活顺序分配，1起始）；未激活时为 null
        /// </summary>
        public int? LayerNumber;

        public SeaEyeState(int index) {
            Index = index;
        }

        /// <summary>
        /// 该眼在外圈上的轨道角度（屏幕坐标系，首只位于正上方）
        /// </summary>
        public float Angle => Index / (float)HalibutSave.MaxEyes * MathHelper.TwoPi - MathHelper.PiOver2;

        /// <summary>
        /// 判断该眼对指定玩家是否处于死机状态（层数不高于死机等级即死机）
        /// </summary>
        public bool IsCrashedState(Player player) {
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return false;
            }
            return (LayerNumber ?? 1) <= halibutPlayer.CrashesLevel();
        }
    }
}
