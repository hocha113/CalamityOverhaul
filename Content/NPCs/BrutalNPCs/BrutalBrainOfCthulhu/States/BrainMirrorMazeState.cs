using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 二阶段镜像环阵轮舞：六具身影环游玩家，逐一离环贯穿环心
    /// 可学习规则：真身必然最后一个出手；出手前有眼芒与灯光；假体冷暗无光
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.MirrorMaze, typeof(BrainStateContext))]
    internal class BrainMirrorMazeState : BrainStateBase
    {
        public override string StateName => "MirrorMaze";
        public override BrainStateIndex StateIndex => BrainStateIndex.MirrorMaze;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int FakeCount = 5;
        /// <summary>真身占用的槽位（最后出手）</summary>
        private const int BrainSlot = 5;
        private const int SetupTime = 26;
        private const int GlintLead = 22;
        private const int BrainDashTime = 15;
        private const int WrapUpTime = 42;
        internal const int ShardDamage = 12;
        #endregion

        private int BrainDashTick => SetupTime + BrainMirrorImage.MazeFirstDashDelay
            + BrainSlot * BrainMirrorImage.MazeDashInterval;

        /// <summary>0布阵 1环游 2真身贯穿 3收场</summary>
        private int phase;
        private int phaseTimer;
        private Vector2 dashDir;

        public BrainMirrorMazeState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            NPC npc = context.Npc;
            npc.damage = 0;

            if (!VaultUtils.isClient) {
                //环心锚=玩家当前位（此后慢速追踪）
                context.Master.ai[0] = context.Target.Center.X;
                context.Master.ai[1] = context.Target.Center.Y;
                npc.netUpdate = true;

                Vector2 anchor = context.Target.Center;
                int damage = BrainMirrorStrikeState.MirrorContactDamage + (context.IsDeathMode ? 3 : 0);
                for (int i = 0; i < FakeCount; i++) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                        ModContent.ProjectileType<BrainMirrorImage>(), damage, 0f, Main.myPlayer,
                        BrainMirrorImage.PackMode(BrainMirrorImage.ModeMazeOrbit, i), anchor.X, anchor.Y);
                }
            }
        }

        private Vector2 Anchor(BrainStateContext context) => new(context.Master.ai[0], context.Master.ai[1]);

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            context.HideFromMinions = phase < 2;
            context.BeatIntensity = 0.8f;

            //环心慢速追踪玩家（服务端写同步槽，假体锚节流重瞄）
            if (!VaultUtils.isClient && phase < 2) {
                Vector2 anchor = Anchor(context);
                anchor = Vector2.Lerp(anchor, player.Center, 0.02f);
                context.Master.ai[0] = anchor.X;
                context.Master.ai[1] = anchor.Y;
                if (Timer % 8 == 0) {
                    RetargetFakes(anchor);
                }
            }

            switch (phase) {
                case 0: {
                    //布阵：真身瞬移入自己的环位
                    npc.damage = 0;
                    if (phaseTimer >= SetupTime) {
                        if (!VaultUtils.isClient) {
                            BrainMotion.ServerTeleport(npc, OrbitSlotPos(context, phaseTimer), Vector2.Zero);
                        }
                        phase = 1;
                        phaseTimer = 0;
                    }
                    return null;
                }
                case 1: {
                    //环游：真身走与假体同一轨道公式（槽位5）
                    npc.damage = 0;
                    if (!VaultUtils.isClient) {
                        Vector2 slotPos = OrbitSlotPos(context, Timer);
                        npc.velocity = (slotPos - npc.Center) * 0.35f;
                    }

                    //真身出手前的眼芒（唯一预告，学了就能抓真身）
                    int untilDash = BrainDashTick - Timer;
                    if (untilDash <= GlintLead && untilDash > 0) {
                        context.EyeGlint = 1f - untilDash / (float)GlintLead;
                        context.TelegraphGlow = context.EyeGlint * 0.7f;
                    }

                    if (Timer >= BrainDashTick) {
                        if (!VaultUtils.isClient) {
                            dashDir = (Anchor(context) - npc.Center).SafeNormalize(Vector2.UnitY);
                            npc.velocity = dashDir * 36f;
                            npc.netUpdate = true;
                            //贯穿伴射：环心散珠
                            ScatterShards(context);
                        }
                        BrainMotion.Roar(npc.Center, 1f, 0.1f);
                        BrainHeartbeat.Thump(1.25f);
                        phase = 2;
                        phaseTimer = 0;
                    }
                    return null;
                }
                case 2: {
                    //真身贯穿
                    npc.damage = (int)(npc.defDamage * 1.25f);
                    context.EyeGlint = 1f - phaseTimer / (float)BrainDashTime;
                    if (phaseTimer >= BrainDashTime) {
                        phase = 3;
                        phaseTimer = 0;
                    }
                    return null;
                }
                default: {
                    //收场急刹+残余假体崩解
                    npc.damage = 0;
                    npc.velocity *= 0.85f;
                    if (phaseTimer == 6 && !VaultUtils.isClient) {
                        KillFakes();
                    }
                    if (phaseTimer >= WrapUpTime && !VaultUtils.isClient) {
                        return new BrainHoverState();
                    }
                    return null;
                }
            }
        }

        /// <summary>槽位5的轨道位（与 BrainMirrorImage.UpdateMazeOrbit 同公式同相位，环距均匀）</summary>
        private Vector2 OrbitSlotPos(BrainStateContext context, int t) {
            float age = t;
            float angle = MathHelper.TwoPi * BrainSlot / 6f + age * 0.011f;
            float breathing = 1f + 0.05f * (float)Math.Sin(age * 0.05f + BrainSlot);
            //出手前反向外撑（与假体 reel 同语法）
            int untilDash = BrainDashTick - t;
            float reel = untilDash is <= 12 and > 0 ? (12 - untilDash) / 12f * 55f : 0f;
            return Anchor(context) + angle.ToRotationVector2() * (BrainMirrorImage.MazeRadius * breathing + reel);
        }

        private void ScatterShards(BrainStateContext context) {
            NPC npc = context.Npc;
            int damage = ShardDamage + (context.IsDeathMode ? 3 : 0);
            for (int i = 0; i < 6; i++) {
                float angle = dashDir.ToRotation() + MathHelper.TwoPi * i / 6f + 0.26f;
                Vector2 vel = angle.ToRotationVector2() * 7.5f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), Anchor(context), vel,
                    ModContent.ProjectileType<BrainBloodShard>(), damage, 0f, Main.myPlayer, 0f);
            }
        }

        private static void RetargetFakes(Vector2 anchor) {
            int mirrorType = ModContent.ProjectileType<BrainMirrorImage>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != mirrorType || (int)proj.ai[0] / 100 != BrainMirrorImage.ModeMazeOrbit) {
                    continue;
                }
                //冲刺中的假体不再重瞄（保持直线可读）
                if (proj.localAI[1] != 0f) {
                    continue;
                }
                proj.ai[1] = anchor.X;
                proj.ai[2] = anchor.Y;
                proj.netUpdate = true;
            }
        }

        private static void KillFakes() {
            int mirrorType = ModContent.ProjectileType<BrainMirrorImage>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == mirrorType) {
                    proj.Kill();
                }
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                KillFakes();
            }
        }
    }
}
