using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 环形爆发：头颅悬停定点，充能 → 全向弹幕脉冲，重复三次。
    /// 每次脉冲前都有明确的充能火花与音效预警，节奏"静—爆—静—爆"。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.RadialBurst, typeof(PrimeStateContext))]
    internal class PrimeRadialBurstState : PrimeStateBase
    {
        public override string StateName => "RadialBurst";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.RadialBurst;

        private const int ChargeFrames = 35;
        private const int CycleFrames = 55;
        private const int MaxPulses = 3;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 2;

            //缓停在玩家上方，给玩家清晰的走位参照
            Vector2 anchor = context.Target.Center + new Vector2(0, -280);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.04f, 0.2f);
            LeanByVelocity(npc);

            int cycleTimer = Timer % CycleFrames;

            if (cycleTimer < ChargeFrames) {
                context.SetChargeState(3, cycleTimer / (float)ChargeFrames);

                if (!VaultUtils.isServer) {
                    if (cycleTimer == 10) {
                        SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.5f, Volume = 0.8f }, npc.Center);
                    }
                    if (cycleTimer % 3 == 0) {
                        Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                        Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.FireworkFountain_Red,
                            0, 0, 100, Color.OrangeRed, Main.rand.NextFloat(1f, 1.6f));
                        dust.velocity = (npc.Center - pos) * 0.1f;
                        dust.noGravity = true;
                    }
                }
            }
            else if (cycleTimer == ChargeFrames) {
                context.ResetChargeState();
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                    FireRing(context);
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.1f, Pitch = -0.2f }, npc.Center);
                }
                Counter++;
            }

            Timer++;
            if (Counter >= MaxPulses && cycleTimer >= CycleFrames - 1) {
                npc.damage = npc.defDamage * 2;
                if (!VaultUtils.isClient) {
                    return new PrimeRageHoverState();
                }
            }
            return null;
        }

        private void FireRing(PrimeStateContext context) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            //制导炮弹全难度统一使用，难度只影响环密度
            float ringCount = context.BossRush ? 15 : (context.DeathMode ? 13 : (Main.masterMode ? 11 : 9));

            Vector2 baseVelocity = (context.Target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 10f;
            //奇偶脉冲错相半个间隔，叠出交错的弹幕网
            float phaseOffset = Counter % 2 == 0 ? 0f : MathHelper.TwoPi / ringCount * 0.5f;

            for (int i = 0; i < ringCount; i++) {
                float rotOffset = MathHelper.TwoPi / ringCount * i + phaseOffset;
                Vector2 perturbedSpeed = baseVelocity.RotatedBy(rotOffset);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, npc.whoAmI, npc.target, rotOffset);
            }
        }
    }
}
