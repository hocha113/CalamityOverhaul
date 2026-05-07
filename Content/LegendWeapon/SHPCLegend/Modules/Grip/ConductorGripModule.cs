using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 导体握把：增加链式跳跃次数与范围，命中时在击中点生成电弧粒子爆发
    /// OnBeamHitNPC 钩子仅做视觉增强，无额外伤害，逻辑彻底去耦合
    /// </summary>
    internal sealed class ConductorGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //导体电蓝
        public override Color TintColor => new(80, 200, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamChainCount += 1;
            ctx.BeamChainRange = System.MathF.Max(ctx.BeamChainRange, 240f) + 60f;
            ctx.DamageMul += 0.05f;
            ctx.ManaCostMul += 0.4f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    target.Center, vel,
                    new Color(180, 240, 255), new Color(80, 180, 255),
                    Main.rand.NextFloat(1.0f, 2.2f), Main.rand.Next(8, 18)));
            }
        }
    }
}
