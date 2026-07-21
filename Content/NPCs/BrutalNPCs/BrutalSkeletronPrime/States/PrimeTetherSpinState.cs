using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>电弧风车，收紧半径广播ai[1]，退出须清零</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.TetherSpin, typeof(PrimeStateContext))]
    internal class PrimeTetherSpinState : PrimeStateBase
    {
        public override string StateName => "TetherSpin";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.TetherSpin;

        /// <summary>预警帧数（环形预告 + 四臂飞散到位）</summary>
        internal static int TelegraphFrames => 40;
        /// <summary>风车旋转持续帧数</summary>
        internal static int SpinDuration => 240;
        /// <summary>终结脉冲后的收势帧数</summary>
        internal static int WindDownFrames => 24;

        /// <summary>链锁初始半径</summary>
        internal static float ChainRadiusStart => 1020f;
        /// <summary>收紧终点半径</summary>
        internal static float ChainRadiusEnd => 420f;
        /// <summary>基础角速度（弧度/帧），风车慢转</summary>
        internal static float SpinRateBase => 0.018f;
        /// <summary>随收紧进度追加的角速度</summary>
        internal static float SpinRateGain => 0.012f;
        /// <summary>头压玩家跟随强度</summary>
        internal static float FollowAccel => 0.03f;

        private static int Total => TelegraphFrames + SpinDuration + WindDownFrames;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            //风车中心缓压
            Vector2 anchor = context.Target.Center;
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * FollowAccel, 0.1f);

            float progress = MathHelper.Clamp((Timer - TelegraphFrames) / (float)SpinDuration, 0f, 1f);
            //风车旋转
            npc.rotation += SpinRateBase + progress * SpinRateGain;

            if (Timer < TelegraphFrames) {
                context.SetChargeState(1, Timer / (float)TelegraphFrames);
                if (!VaultUtils.isClient && Timer == 1) {
                    PrimeTelegraphLine.SpawnRing(npc, npc.Center, ChainRadiusStart, TelegraphFrames, true);
                }
            }
            else if (Timer == TelegraphFrames) {
                if (!VaultUtils.isClient) {
                    SpawnChains(context);
                }
            }
            else if (Timer < TelegraphFrames + SpinDuration) {
                context.SetChargeState(1, 0.4f + progress * 0.5f);
                //广播收紧半径
                npc.ai[PrimeAiSlots.HeadCommandSlot] = MathHelper.Lerp(ChainRadiusStart, ChainRadiusEnd, progress);

                if (!VaultUtils.isServer && Timer % 9 == 0) {
                    Vector2 sparkPos = npc.Center + Main.rand.NextVector2CircularEdge(
                        npc.ai[PrimeAiSlots.HeadCommandSlot], npc.ai[PrimeAiSlots.HeadCommandSlot]);
                    Dust dust = Dust.NewDustDirect(sparkPos, 1, 1, DustID.Electric, 0, 0, 100, Color.Gold, 1.3f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - sparkPos) * 0.06f;
                }
            }
            else if (Timer == TelegraphFrames + SpinDuration) {
                //终结脉冲
                npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
                if (!VaultUtils.isClient) {
                    FirePulseRing(context);
                }
                if (!VaultUtils.isServer) {
                    PrimeScreenEffects.PushShockRing(npc.Center, 1f, 700f);
                    PrimeDeathPerformancePlayer.RequestShake(12f, 18);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.1f, Pitch = -0.5f }, npc.Center);
                }
            }

            Timer++;
            if (Timer >= Total && !VaultUtils.isClient) {
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        public override void OnExit(PrimeStateContext context) {
            base.OnExit(context);
            //退出清指令槽半径
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
        }

        /// <summary>拉起电弧链锁，服务端</summary>
        private static void SpawnChains(PrimeStateContext context) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser));
            int[] armIndices = [CWRWorld.primeCannon, CWRWorld.primeVice, CWRWorld.primeSaw, CWRWorld.primeLaser];

            foreach (int armIndex in armIndices) {
                if (armIndex < 0 || armIndex >= Main.maxNPCs || !Main.npc[armIndex].active) {
                    continue;
                }
                Vector2 mid = (npc.Center + Main.npc[armIndex].Center) / 2f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), mid, Vector2.Zero,
                    ModContent.ProjectileType<PrimeArcChainProj>(), damage, 0f, Main.myPlayer,
                    armIndex, npc.whoAmI, SpinDuration);
            }
            npc.netUpdate = true;
        }

        /// <summary>终结八向导弹环</summary>
        private static void FirePulseRing(PrimeStateContext context) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            int count = context.MasterMode ? 10 : 8;

            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi / count * i;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, ang.ToRotationVector2() * 9f,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, npc.whoAmI, npc.target, ang);
            }
        }
    }
}
