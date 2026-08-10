using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>电网瘫痪：借信号塔把整片区域的机械一起断电</summary>
    internal class GridBlackout : QuickHackDef
    {
        private const float BlackoutRadius = 6400f;
        private const int DisableFrames = 60 * 15;

        private static readonly Color Dead = new(90, 110, 140);

        public override void SetDefaults() {
            UploadTime = 200;
            RamCost = 7;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.SignalTower;
            UnlockedByDefault = false;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not IHackableSignalTower tower) return false;
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                tower.BeginGridBlackout(BlackoutRadius, DisableFrames, caster);
            }
            if (Main.netMode != NetmodeID.Server) EmitVisual(tower.WorldCenter);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is IHackableSignalTower tower) EmitVisual(tower.WorldCenter);
        }

        private static void EmitVisual(Vector2 center) {
            //一圈扩散的灰环，读作"灯一片片灭下去"
            for (int ring = 0; ring < 3; ring++) {
                float radius = 34f + ring * 26f;
                int count = 12 + ring * 6;
                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count;
                    Vector2 dir = angle.ToRotationVector2();
                    PRTLoader.NewParticle<PRT_Spark>(center + dir * radius,
                        dir * (1.4f + ring * 0.9f), Dead, 0.9f - ring * 0.15f)
                        ?.Configure(false, 28 + ring * 6);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.7f }, center);
            }
        }
    }
}
