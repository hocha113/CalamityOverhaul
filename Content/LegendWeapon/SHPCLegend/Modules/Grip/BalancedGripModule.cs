using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>平衡握把，左右键攒阳/阴衡，均衡进天平态</summary>
    internal sealed class BalancedGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //平衡青铜
        public override Color TintColor => new(220, 180, 120);

        //阳=猩红，阴=幽紫
        private static readonly Color YangColor = new(255, 90, 100);
        private static readonly Color YangEdge = new(160, 25, 45);
        private static readonly Color YinColor = new(190, 110, 255);
        private static readonly Color YinEdge = new(80, 30, 160);

        private const float ChargeCap = 100f;
        /// <summary>阳衡，左键侧</summary>
        private float yangCharge;
        /// <summary>阴衡，右键侧</summary>
        private float yinCharge;
        /// <summary>天平态，上一帧结算</summary>
        private bool balanced;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.12f;
            if (balanced) {
                ctx.DamageMul += 0.12f;
                ctx.AttackSpeedMul += 0.10f;
                ctx.ManaCostMul += -0.10f;
            }
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) return;
            yangCharge = Math.Min(yangCharge + 6f, ChargeCap);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            yangCharge = Math.Min(yangCharge + 2.5f, ChargeCap);
        }

        public override void OnOrbLaunched(CyberChargeOrbProj orb) {
            yinCharge = Math.Min(yinCharge + 35f, ChargeCap);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            yinCharge = Math.Min(yinCharge + 25f, ChargeCap);
        }

        public override void OnPlayerUpdate(Player player) {
            //双侧匀速流失
            yangCharge = Math.Max(yangCharge - 0.14f, 0f);
            yinCharge = Math.Max(yinCharge - 0.14f, 0f);

            bool nowBalanced = EvaluateBalance();
            if (nowBalanced && !balanced && Main.netMode != NetmodeID.Server) {
                //入态音+双色爆发
                SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.5f, Pitch = 0.5f }, player.Center);
                for (int i = 0; i < 12; i++) {
                    bool crimson = i % 2 == 0;
                    Vector2 vel = (MathHelper.TwoPi * i / 12f).ToRotationVector2() * Main.rand.NextFloat(3f, 5f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(player.Center, vel,
                        crimson ? YangColor : YinColor, Main.rand.NextFloat(0.7f, 1.2f))
                        .Configure(crimson ? YangEdge : YinEdge, Main.rand.Next(16, 28));
                }
            }
            balanced = nowBalanced;

            //天平态，双星环绕
            if (balanced && Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 3 == 0) {
                float orbitAngle = (float)Main.timeForVisualEffects * 0.07f;
                Vector2 offset = orbitAngle.ToRotationVector2() * 42f;
                PRTLoader.NewParticle<PRT_CyberSquare>(player.Center + offset, -Vector2.UnitY * 0.3f,
                    YangColor, 0.55f).Configure(YangEdge, 10);
                PRTLoader.NewParticle<PRT_CyberSquare>(player.Center - offset, -Vector2.UnitY * 0.3f,
                    YinColor, 0.55f).Configure(YinEdge, 10);
            }
        }

        /// <summary>双衡≥门槛且比值≥0.45</summary>
        private bool EvaluateBalance() {
            const float threshold = 15f;
            if (yangCharge < threshold || yinCharge < threshold) return false;
            float lo = Math.Min(yangCharge, yinCharge);
            float hi = Math.Max(yangCharge, yinCharge);
            return lo / hi >= 0.45f;
        }
    }
}
