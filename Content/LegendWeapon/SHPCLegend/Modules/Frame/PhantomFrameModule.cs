using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>相位机匣，约 30 帧传送到最近敌附近</summary>
    internal sealed class PhantomFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //相位紫
        public override Color TintColor => new(180, 80, 255);

        private int _phantomTimer;

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamLifeMul += 0.24f;
            ctx.DamageMul += 0.08f;
            ctx.ManaCostMul += 0.36f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.numUpdates != -1) return;
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
                    PRTLoader.NewParticle<PRT_CyberSquare>(beam.Projectile.Center, vel, new Color(200, 100, 255), Main.rand.NextFloat(0.8f, 1.8f)).Configure(new Color(100, 40, 200), Main.rand.Next(15, 30));
                }
            }
        }
    }
}
