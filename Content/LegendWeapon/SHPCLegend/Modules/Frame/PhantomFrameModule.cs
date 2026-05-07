using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>
    /// 相位机匣：追踪光束每90次AI调用（约30游戏帧）将自身传送到最近敌人附近
    /// 通过 OnBeamAI 钩子修改 Projectile.Center 与 velocity 方向，彻底绕开障碍物追踪
    /// </summary>
    internal sealed class PhantomFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //相位紫
        public override Color TintColor => new(180, 80, 255);

        private int _phantomTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += 0.30f;
            ctx.DamageMul += 0.10f;
            ctx.ManaCostMul += 0.30f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            _phantomTimer++;
            if (_phantomTimer < 90) return;
            _phantomTimer = 0;
            if (beam.Projectile.owner != Main.myPlayer) return;
            NPC target = beam.Projectile.Center.FindClosestNPC(300f, false, true);
            if (target == null) return;
            Vector2 dir = (target.Center - beam.Projectile.Center).SafeNormalize(Vector2.UnitX);
            beam.Projectile.Center = target.Center - dir * 80f;
            float speed = beam.Projectile.velocity.Length();
            beam.Projectile.velocity = dir * speed;
            beam.Projectile.netUpdate = true;
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                    PRTLoader.AddParticle(new PRT_CyberSquare(
                        beam.Projectile.Center, vel,
                        new Color(200, 100, 255), new Color(100, 40, 200),
                        Main.rand.NextFloat(0.8f, 1.8f), Main.rand.Next(15, 30)));
                }
            }
        }
    }
}
