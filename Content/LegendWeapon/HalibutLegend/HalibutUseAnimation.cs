using InnoVault.GameSystem;
using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>
    /// 大比目鱼的使用动画：朝鼠标方向持握 + 复合前臂跟随 + 轻微起手摆动<br/>
    /// 远程玩家的瞄准朝向由框架默认的玩家网络同步（InnoVault PlayerNetwork）驱动，无需本武器自行联网
    /// </summary>
    internal class HalibutUseAnimation : AimedHoldAnimation
    {
        public override int TargetID => HalibutOverride.ID;
        /// <summary>武器中心沿持握方向距玩家稳定中心的距离</summary>
        public override float HoldDistance => 7f;
        /// <summary>持握精灵的原点偏移，使握把对准手部</summary>
        public override Vector2 HoldOrigin => new Vector2(-40, 6);
        /// <summary>起手时手臂的轻微摆动幅度（弧度）</summary>
        public override float SwingStrength => 0.06f;
        /// <summary>摆动发生在使用动画的前 40%</summary>
        public override float SwingPhase => 0.4f;
        /// <summary>与 Terraria Overhaul 的持握样式冲突时让位，由本模组判断而非框架内置</summary>
        public override bool Active(Item item, Player player) => CWRMod.Instance.terrariaOverhaul == null;
    }
}
