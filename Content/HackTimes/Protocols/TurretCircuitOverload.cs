using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>炮台过载，长时间失效</summary>
    internal class TurretCircuitOverload : QuickHackDef
    {
        //失效约 12 秒
        private const int DisableFrames = 60 * 12;

        public override void SetDefaults() {
            UploadTime = 180;
            RamCost = 6;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Turret;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableTurret turret) return false;
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                turret.ApplyCircuitOverload(DisableFrames, caster);
            }
            Vector2 center = turret.WorldCenter;

            if (Main.netMode != NetmodeID.Server) EmitVisual(center);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is IHackableTurret turret) EmitVisual(turret.WorldCenter);
        }

        private static void EmitVisual(Vector2 center) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 34; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                    Color c = Color.Lerp(new Color(255, 120, 200), new Color(255, 240, 255), Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Spark>(center, vel, c, 1.4f).Configure(false, 32);
                }
                for (int i = 0; i < 16; i++) {
                    float angle = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(-0.1f, 0.1f);
                    Vector2 dir = angle.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_Spark>(center + dir * 32f, dir * 5.5f, new Color(220, 60, 140, 180), 0.9f).Configure(false, 30);
                }
            }
        }
    }
}
