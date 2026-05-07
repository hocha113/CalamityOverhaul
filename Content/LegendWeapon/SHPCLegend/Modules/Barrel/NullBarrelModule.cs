using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 归零枪管：伤害极低但穿透无限，命中施加剧毒数据侵蚀
    /// 定位扫射清怪流，配合 Homing 构建全屏覆盖
    /// </summary>
    internal sealed class NullBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //数据侵蚀灰绿
        public override Color TintColor => new(100, 255, 140);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.5f;
            ctx.BeamExtraPierce += 99;
            ctx.AttackSpeedMul += 0.7f;
            ctx.HomingMul += 0.3f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Venom, 240);
        }
    }
}
