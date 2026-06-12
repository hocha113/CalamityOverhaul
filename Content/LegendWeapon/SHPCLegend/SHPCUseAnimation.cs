using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    /// <summary>
    /// SHPC 左键使用动画：朝鼠标方向持握 + 复合前臂跟随 + 开火后坐力<br/>
    /// 右键蓄力由 <see cref="Cyberspaces.SHPCChargeHeldProj"/> 接管手臂与绘制（此处跳过）；
    /// 激光持续模式下保持枪口稳定、不产生回退<br/>
    /// 远程玩家的瞄准朝向由框架默认的玩家网络同步（InnoVault PlayerNetwork）驱动
    /// </summary>
    internal class SHPCUseAnimation : AimedHoldAnimation
    {
        public override int TargetID => SHPCOverride.ID;
        /// <summary>持握精灵原点偏移（随武器缩放）</summary>
        public override Vector2 HoldOrigin => new Vector2(-56, 10) * SHPCOverride.ItemScale;
        /// <summary>开火后坐力最大回退距离（随武器缩放）</summary>
        public override float RecoilStrength => 8f * SHPCOverride.ItemScale;
        /// <summary>右键蓄力分支不走本持握动画</summary>
        public override bool ShouldAnimate(Item item, Player player) => player.altFunctionUse != 2;
        /// <summary>激光持续模式下不产生后坐力，保持照射稳定</summary>
        public override bool RecoilActive(Item item, Player player) => !SHPCModificationSystem.Resolve(player).LaserMode;
    }
}
