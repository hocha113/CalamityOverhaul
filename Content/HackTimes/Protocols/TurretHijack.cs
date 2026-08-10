using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>炮台劫持：篡改敌我识别，让它调转枪口</summary>
    internal class TurretHijack : QuickHackDef
    {
        //倒戈约 8 秒
        private const int HijackFrames = 60 * 8;

        private static readonly Color Takeover = new(120, 255, 200);

        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 5;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.Turret;
            UnlockedByDefault = false;
        }

        public override bool CanApplyTo(IHackTarget target) {
            //已经被打瘫的炮台没什么好劫持的
            return base.CanApplyTo(target)
                && target is IHackableTurret turret && !turret.IsCircuitDisabled;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableTurret turret) return false;
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                turret.ApplyHijack(HijackFrames, caster);
            }
            if (Main.netMode != NetmodeID.Server) EmitVisual(turret.WorldCenter);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is IHackableTurret turret) EmitVisual(turret.WorldCenter);
        }

        private static void EmitVisual(Vector2 center) {
            //绕塔一圈的接管环，收束到中心，读作"权限被换了主人"
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 dir = angle.ToRotationVector2();
                PRTLoader.NewParticle<PRT_Spark>(center + dir * 40f, -dir * 3.2f,
                    Takeover, 1.0f)?.Configure(false, 26);
            }
            PRTLoader.NewParticle<PRT_Spark>(center, Vector2.Zero, Color.White, 2f)
                ?.Configure(false, 14);
        }
    }
}
