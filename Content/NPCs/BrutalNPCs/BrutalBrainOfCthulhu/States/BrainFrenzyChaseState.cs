using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 二阶段狂化追猎：加速弧线咬尾+周期侧翼闪现（有预兆），整拍吐珠
    /// 压迫型呼吸招，全程接触判定（速度门控）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.FrenzyChase, typeof(BrainStateContext))]
    internal class BrainFrenzyChaseState : BrainStateBase
    {
        public override string StateName => "FrenzyChase";
        public override BrainStateIndex StateIndex => BrainStateIndex.FrenzyChase;

        #region 节奏常量
        private const int ChaseTime = 176;
        private const int BlinkInterval = 62;
        private const int BlinkTelegraph = 18;
        internal const int ShardDamage = 12;
        #endregion

        private long lastSpitBeat = -1;
        private Vector2 blinkDest;
        private bool blinkPending;

        public BrainFrenzyChaseState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            lastSpitBeat = -1;
            blinkPending = false;
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            context.BeatIntensity = 0.75f;

            //速度门控接触判定
            float speed = npc.velocity.Length();
            npc.damage = speed > 8f ? npc.defDamage : 0;

            //加速弧线咬尾
            if (!VaultUtils.isClient && !blinkPending) {
                float ramp = MathHelper.Clamp(Timer / 70f, 0f, 1f);
                float chaseSpeed = MathHelper.Lerp(11f, context.IsLowLife ? 25f : 22f, ramp);
                BrainMotion.CurveChase(npc, player.Center, chaseSpeed, 0.052f);
            }

            //周期侧翼闪现：预兆→瞬移
            int blinkLocal = Timer % BlinkInterval;
            if (!VaultUtils.isClient) {
                if (blinkLocal == BlinkInterval - BlinkTelegraph && Timer < ChaseTime - 30) {
                    //预兆：目的地小裂隙
                    blinkPending = true;
                    Vector2 flank = (player.Center - npc.Center).SafeNormalize(Vector2.UnitX)
                        .RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2);
                    blinkDest = player.Center + flank * 250f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), blinkDest, Vector2.Zero,
                        ModContent.ProjectileType<BrainTeleportRift>(), 0, 0f, Main.myPlayer, 1f);
                }
                if (blinkPending && blinkLocal == BlinkInterval - 1) {
                    blinkPending = false;
                    BrainMotion.ServerTeleport(npc, blinkDest,
                        (player.Center - blinkDest).SafeNormalize(Vector2.UnitY) * 13f);
                    KillRifts();
                }
            }

            //整拍吐一粒瞄准血珠
            if (!VaultUtils.isClient) {
                long beatIndex = (long)(npc.ai[3] / context.BeatPeriod);
                if ((int)npc.ai[3] % context.BeatPeriod == 6 && beatIndex != lastSpitBeat) {
                    lastSpitBeat = beatIndex;
                    Vector2 aim = (player.Center + player.velocity * 12f - npc.Center).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim * 11.5f,
                        ModContent.ProjectileType<BrainBloodShard>(),
                        ShardDamage + (context.IsAsuraMode ? 3 : 0), 0f, Main.myPlayer, 0f);
                }
            }

            //高速拖雾
            if (!VaultUtils.isServer && speed > 15f && Main.rand.NextBool(3) && BrainMotion.OnScreen(npc.Center)) {
                BrainMotion.BloodMistBurst(npc.Center - npc.velocity * 0.6f, 0.4f, 0, 0f);
            }

            if (Timer >= ChaseTime && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            return null;
        }

        private static void KillRifts() {
            int riftType = ModContent.ProjectileType<BrainTeleportRift>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == riftType) {
                    proj.Kill();
                }
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                KillRifts();
            }
        }
    }
}
