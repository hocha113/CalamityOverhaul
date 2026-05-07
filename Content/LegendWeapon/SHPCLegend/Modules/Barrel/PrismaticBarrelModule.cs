using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 棱光枪管：光束飞行时持续在两侧生成 RGB 三色色散粒子拖尾
    /// 命中时溅出彩色光屑，纯视觉强化加少量爆击与额外光束
    /// </summary>
    internal sealed class PrismaticBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //彩虹流光，整体偏品红
        public override Color TintColor => new(255, 90, 200);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamCountAdd += 1;
            ctx.SpreadMul += 0.15f;
            ctx.DamageMul += -0.15f;
            ctx.CritAdd += 6;
        }

        private static readonly Color RChannel = new(255, 60, 80);
        private static readonly Color GChannel = new(60, 255, 130);
        private static readonly Color BChannel = new(80, 130, 255);

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            if (Main.netMode == NetmodeID.Server) return;
            //每 3 帧生成 RGB 三道色散尾迹
            if (Main.GameUpdateCount % 3 != 0) return;

            Vector2 forward = beam.Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);
            Color[] channels = { RChannel, GChannel, BChannel };
            for (int i = 0; i < 3; i++) {
                float side = i - 1f; //-1, 0, 1
                Vector2 offset = perp * side * 6f;
                Vector2 vel = -forward * 1.5f + perp * side * 0.6f;
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    beam.Projectile.Center + offset, vel,
                    channels[i], Color.White,
                    Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(10, 18)));
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 9; i++) {
                Color c = (i % 3) switch { 0 => RChannel, 1 => GChannel, _ => BChannel };
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    target.Center, vel,
                    c, Color.White,
                    Main.rand.NextFloat(0.7f, 1.5f), Main.rand.Next(15, 28)));
            }
        }
    }
}
