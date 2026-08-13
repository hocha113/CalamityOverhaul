using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 阶段转换演出：飞眼被拽回贴身环→护壳四拍崩应→爆壳抛片→裸脑亮相
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.PhaseTransition, typeof(BrainStateContext))]
    internal class BrainPhaseTransitionState : BrainStateBase
    {
        public override string StateName => "PhaseTransition";
        public override BrainStateIndex StateIndex => BrainStateIndex.PhaseTransition;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int StageTime = 24;
        private const int BurstTime = 148;
        private const int RevealEnd = 208;
        /// <summary>护壳崩应四拍（加速逼近）</summary>
        private static readonly int[] CrackBeats = [30, 64, 94, 118];
        internal const int FragmentDamage = 14;
        internal const int ShardDamage = 11;
        #endregion

        private int nextCrack;
        private bool burstDone;

        public BrainPhaseTransitionState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.Invulnerable = true;
            npc.damage = 0;
            nextCrack = 0;
            burstDone = false;

            if (!VaultUtils.isClient) {
                BrainProjectileUtils.ClearBrainProjectiles();
                context.RefreshCreepers();
            }
            BrainMotion.Roar(npc.Center, 0.9f, -0.5f);
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            context.Invulnerable = Timer < BurstTime + 8;
            npc.damage = 0;
            context.BeatSilenced = true;   //整段接管节拍

            //登台：抬升到玩家上侧
            if (Timer <= StageTime) {
                if (!VaultUtils.isClient) {
                    Vector2 stage = player.Center + new Vector2(Math.Sign(npc.Center.X - player.Center.X) * 210f, -290f);
                    BrainMotion.SpringHover(npc, stage, 0.03f, 0.13f, 26f);
                }
                return null;
            }

            //飞眼被无形之力拽回贴身密环（皈依姿态）
            if (Timer < BurstTime) {
                float yank = MathHelper.Clamp((Timer - StageTime) / 40f, 0f, 1f);
                float ringR = MathHelper.Lerp(420f, 120f, yank);
                BrainFormationChannel.PushCage(npc.Center, ringR, Timer * 0.02f, -10f, 0f,
                    false, Math.Max(context.Creepers.Count, 1));

                if (!VaultUtils.isClient) {
                    npc.velocity *= 0.9f;
                }

                //护壳崩应四拍
                if (nextCrack < CrackBeats.Length && Timer - StageTime == CrackBeats[nextCrack]) {
                    nextCrack++;
                    context.ShellCrack = nextCrack / (float)CrackBeats.Length;
                    BrainHeartbeat.Thump(0.9f + nextCrack * 0.12f, 0.92f);
                    BrainHeartbeat.PlayThumpSound(npc.Center, 0.9f, nextCrack * 0.08f);
                    BrainMotion.Shake(npc.Center, 2.5f + nextCrack * 1.2f, 10);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.NPCHit2 with {
                            Volume = 0.85f, Pitch = -0.35f + nextCrack * 0.1f, MaxInstances = 4
                        }, npc.Center);
                        BrainMotion.BloodMistBurst(npc.Center + Main.rand.NextVector2Circular(46f, 40f),
                            0.6f + nextCrack * 0.2f, 3 + nextCrack, 6f);
                    }
                }
                //裂纹常驻显示（按已崩拍数）
                context.ShellCrack = Math.Max(context.ShellCrack, nextCrack / (float)CrackBeats.Length);
                context.TelegraphGlow = nextCrack / (float)CrackBeats.Length * 0.8f;

                //崩前颤抖升级
                if (!VaultUtils.isServer && nextCrack >= 3 && Timer % 2 == 0) {
                    npc.position += Main.rand.NextVector2Circular(1.6f, 1.6f);
                }
                return null;
            }

            //爆壳帧
            if (!burstDone) {
                burstDone = true;
                DoBurst(context);
                return null;
            }

            //裸脑亮相：悬停喘息，血雾垂落
            context.ShellCrack = 0f;
            float revealT = MathHelper.Clamp((Timer - BurstTime) / 30f, 0f, 1f);
            context.GhostFade = MathHelper.Lerp(0.5f, 1f, revealT);
            context.BeatSilenced = false;
            context.BeatPeriod = 40;
            context.BeatIntensity = 0.85f;

            if (!VaultUtils.isServer && Timer % 4 == 0 && BrainMotion.OnScreen(npc.Center)) {
                BrainMotion.BloodMistBurst(npc.Center + Main.rand.NextVector2Circular(60f, 46f), 0.5f, 2, 4f);
            }

            if (Timer >= RevealEnd && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            return null;
        }

        /// <summary>爆壳：置二阶段旗标，壳片抛射，飞眼殉爆成慢速血珠</summary>
        private void DoBurst(BrainStateContext context) {
            NPC npc = context.Npc;

            //二阶段旗标（原版帧组+头图标隐匿随之生效）
            npc.ai[0] = -1f;
            context.GhostFade = 0.5f;
            BrainFormationChannel.Clear();

            BrainMotion.Roar(npc.Center, 1.25f, -0.15f, true);
            BrainHeartbeat.Thump(1.5f, 0.94f);
            BrainMotion.Shake(npc.Center, 12f, 26);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 1f, Pitch = -0.4f }, npc.Center);
                BrainMotion.BloodMistBurst(npc.Center, 2.6f, 20, 11f);
            }

            if (VaultUtils.isClient) {
                return;
            }
            npc.netUpdate = true;

            //壳片四象限×2轮抛射
            int fragDamage = FragmentDamage + (context.IsDeathMode ? 3 : 0);
            for (int round = 0; round < 2; round++) {
                for (int q = 0; q < 4; q++) {
                    float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.2f, 1.2f);
                    float speed = Main.rand.NextFloat(7f, 12f) + round * 2f;
                    Vector2 vel = angle.ToRotationVector2() * speed;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                        ModContent.ProjectileType<BrainShellFragment>(), fragDamage, 0f, Main.myPlayer,
                        q, Main.rand.NextFloat(-0.22f, 0.22f));
                }
            }

            //存活飞眼殉爆：各放两粒慢速血珠后移除（前期清眼的回报=更干净的爆场）
            int shardDamage = ShardDamage + (context.IsDeathMode ? 2 : 0);
            context.RefreshCreepers();
            foreach (var creeper in context.Creepers) {
                if (!creeper.Alives()) {
                    continue;
                }
                for (int i = 0; i < 2; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4.5f, 6f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), creeper.Center, vel,
                        ModContent.ProjectileType<BrainBloodShard>(), shardDamage, 0f, Main.myPlayer, 0f);
                }
                creeper.life = 0;
                creeper.HitEffect();
                creeper.active = false;
                creeper.netUpdate = true;
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            context.ShellCrack = 0f;
            BrainFormationChannel.Clear();
        }
    }
}
