using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 露心搏动：强制开壳锚定，收缩期放射血环（缺口逐拍旋转），舒张期吸入血雾
    /// 高风险高回报：露心期间防御归零
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.BloodPulse, typeof(BrainStateContext))]
    internal class BrainBloodPulseState : BrainStateBase
    {
        public override string StateName => "BloodPulse";
        public override BrainStateIndex StateIndex => BrainStateIndex.BloodPulse;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int ApproachTime = 34;
        private const int PulsePeriod = 48;     //本状态的心跳压拍
        private const int PulseCount = 4;
        private const int CloseTime = 26;
        internal const int ShardDamage = 13;
        #endregion

        /// <summary>已放的搏动环数（各端本地推进，与全局拍对齐）</summary>
        private int firesDone;
        private long lastFiredBeat = -1;
        private bool shellClosed;

        public BrainBloodPulseState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            context.Npc.damage = 0;
            firesDone = 0;
            lastFiredBeat = -1;
            shellClosed = false;
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            npc.damage = 0;
            context.BeatPeriod = PulsePeriod;

            //接近：占位玩家侧翼
            if (Timer <= ApproachTime) {
                if (!VaultUtils.isClient) {
                    Vector2 anchor = player.Center + new Vector2(Math.Sign(npc.Center.X - player.Center.X) * 370f, -110f);
                    BrainMotion.SpringHover(npc, anchor, 0.026f, 0.12f, 24f);
                }
                context.TelegraphGlow = Timer / (float)ApproachTime * 0.5f;
                return null;
            }

            long beatIndex = (long)(npc.ai[3] / PulsePeriod);
            int beatPhase = (int)(npc.ai[3] % PulsePeriod);

            //搏动段：开壳露心，放射与全局心音同拍
            if (!shellClosed) {
                context.FrameCommand = 1;
                context.HeartExposed = true;
                context.BeatIntensity = 0.9f;

                //锚定微漂
                if (!VaultUtils.isClient) {
                    npc.velocity *= 0.9f;
                    npc.velocity += Main.rand.NextVector2Circular(0.25f, 0.25f);
                }

                //收缩期：放射血环（缺口对准玩家并逐拍旋转，鼓励贴身走位）
                if (beatPhase == 0 && firesDone < PulseCount && beatIndex != lastFiredBeat) {
                    lastFiredBeat = beatIndex;
                    if (!VaultUtils.isClient) {
                        FirePulseRing(context, firesDone);
                    }
                    firesDone++;
                    BrainHeartbeat.Thump(1.15f, 0.92f);
                    BrainMotion.Shake(npc.Center, 3f, 8);
                }

                //舒张期：血雾向心吸入（末段渐静，爆发前的收势）
                if (!VaultUtils.isServer && beatPhase > 14 && beatPhase < PulsePeriod - 8 && Timer % 3 == 0
                    && BrainMotion.OnScreen(npc.Center)) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(190f, 190f);
                    BrainMotion.BloodMistBurst(pos + (npc.Center - pos) * 0.15f, 0.35f, 0, 0f);
                }

                context.TelegraphGlow = 1f - beatPhase / (float)PulsePeriod;

                //四环放完，半拍后合壳
                if (firesDone >= PulseCount && beatPhase >= PulsePeriod / 2) {
                    shellClosed = true;
                    Counter = Timer;
                    BrainMotion.FleshSquish(npc.Center, 0.9f, -0.2f);
                    BrainHeartbeat.Thump(0.8f);
                }
                return null;
            }

            //合壳收场
            context.FrameCommand = 0;
            context.HeartExposed = false;

            if (Timer >= Counter + CloseTime && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            //兜底：异常拖长直接收场
            if (Timer >= ApproachTime + (PulseCount + 3) * PulsePeriod + CloseTime && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            return null;
        }

        /// <summary>放射血环：缺口=玩家方向+逐拍旋转偏移</summary>
        private static void FirePulseRing(BrainStateContext context, int beatNumber) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int count = context.IsDeathMode ? 15 : 12;
            int damage = ShardDamage + (context.IsDeathMode ? 3 : 0);

            float gapDir = (player.Center - npc.Center).ToRotation() + beatNumber * 0.62f;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                if (Math.Abs(MathHelper.WrapAngle(angle - gapDir)) < 0.42f) {
                    continue;
                }
                Vector2 vel = angle.ToRotationVector2() * 8.2f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                    ModContent.ProjectileType<BrainBloodShard>(), damage, 0f, Main.myPlayer, 0f);
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
