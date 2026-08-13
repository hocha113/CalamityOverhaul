using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 飞眼轨道阵·辐条扫压：辐条绕脑旋转，随心跳伸缩，收缩期无害、伸展期有判定
    /// 脑本体贴近玩家慢压，整拍抛血珠
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.LanceWaves, typeof(BrainStateContext))]
    internal class BrainLanceWavesState : BrainStateBase
    {
        public override string StateName => "LanceWaves";
        public override BrainStateIndex StateIndex => BrainStateIndex.LanceWaves;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int GatherTime = 40;
        private const int SweepTime = 300;
        private const int DisperseTime = 24;
        private const int ExtendPulse = 20;    //伸展窗口（判定开）
        internal const int ShardDamage = 12;
        #endregion

        private float spinDir = 1f;
        private long lastVolleyBeat = -1;

        public BrainLanceWavesState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            context.Npc.damage = 0;
            lastVolleyBeat = -1;
            if (!VaultUtils.isClient) {
                spinDir = Main.rand.NextBool() ? 1f : -1f;
                //自旋方向借同步槽走网络（客户端凭此推演辐条相位）
                context.Master.ai[2] = spinDir;
                context.Npc.netUpdate = true;
                context.RefreshCreepers();
            }
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            if (Timer % 30 == 0) {
                context.RefreshCreepers();
            }
            if (!VaultUtils.isClient && context.Creepers.Count < 3 && Timer < GatherTime + SweepTime / 2) {
                return new BrainHoverState();
            }

            //客户端读同步的自旋方向
            float dir = context.Master.ai[2] >= 0f ? 1f : -1f;
            int period = context.IsPhase2 ? 40 : 54;
            context.BeatIntensity = 0.7f;

            //半程反转旋向
            float halfFlip = Timer > GatherTime + SweepTime / 2 ? -1f : 1f;
            float spin = Timer * 0.014f * dir * halfFlip;

            //伸缩：与全局心跳时钟同拍伸展（判定开），其余回缩
            int beatLocal = (int)(npc.ai[3] % period);
            float reach;
            bool damageOn;
            if (beatLocal < ExtendPulse) {
                reach = MathHelper.Lerp(0.35f, 1f, BrainMotion.SharpOut(beatLocal / (float)ExtendPulse, 6));
                damageOn = true;
            }
            else {
                float t = (beatLocal - ExtendPulse) / (float)(period - ExtendPulse);
                reach = MathHelper.Lerp(1f, 0.35f, MathHelper.Clamp(t * 1.6f, 0f, 1f));
                damageOn = false;
            }

            //集结/收场包络
            if (Timer < GatherTime) {
                reach = MathHelper.Lerp(0.1f, 0.35f, Timer / (float)GatherTime);
                damageOn = false;
            }
            else if (Timer > GatherTime + SweepTime) {
                reach *= MathHelper.Clamp(1f - (Timer - GatherTime - SweepTime) / (float)DisperseTime, 0f, 1f);
                damageOn = false;
            }

            int spokes = context.IsPhase2 || context.IsDeathMode ? 3 : 2;
            BrainFormationChannel.PushLance(npc.Center, spin, spokes, reach,
                damageOn, Math.Max(context.Creepers.Count, 1));

            //伸展拍反馈
            if (beatLocal == 1 && Timer >= GatherTime && Timer <= GatherTime + SweepTime) {
                BrainHeartbeat.Thump(0.9f);
                if (!VaultUtils.isServer && BrainMotion.OnScreen(npc.Center, 700f)) {
                    BrainMotion.FleshSquish(npc.Center, 0.55f, -0.4f);
                }
                context.TelegraphGlow = 0.6f;
            }

            //脑本体：慢压玩家（辐条中心随之推进）
            npc.damage = 0;
            if (!VaultUtils.isClient) {
                Vector2 chase = player.Center + (npc.Center - player.Center).SafeNormalize(Vector2.UnitX) * 330f;
                BrainMotion.SpringHover(npc, chase, 0.011f, 0.085f, 12.5f);

                //整拍抛两粒瞄准血珠
                long beatIndex = (long)(npc.ai[3] / period);
                if (Timer >= GatherTime && Timer <= GatherTime + SweepTime && beatLocal == 4 && beatIndex != lastVolleyBeat) {
                    lastVolleyBeat = beatIndex;
                    int damage = ShardDamage + (context.IsDeathMode ? 3 : 0);
                    for (int i = -1; i <= 1; i += 2) {
                        Vector2 aim = (player.Center + player.velocity * 14f - npc.Center).SafeNormalize(Vector2.UnitY);
                        Vector2 vel = aim.RotatedBy(i * 0.14f) * 10.5f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                            ModContent.ProjectileType<BrainBloodShard>(), damage, 0f, Main.myPlayer, 0f);
                    }
                }
            }

            if (Timer >= GatherTime + SweepTime + DisperseTime && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            return null;
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            BrainFormationChannel.Clear();
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
