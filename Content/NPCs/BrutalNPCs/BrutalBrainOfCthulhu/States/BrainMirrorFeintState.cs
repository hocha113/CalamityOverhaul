using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 瞬移预兆欺诈：多道裂隙围绕玩家，仅一道为真
    /// 可学习规则：真裂隙与心跳同拍搏动，假裂隙错半拍；脑必然在整拍出击
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.MirrorFeint, typeof(BrainStateContext))]
    internal class BrainMirrorFeintState : BrainStateBase
    {
        public override string StateName => "MirrorFeint";
        public override BrainStateIndex StateIndex => BrainStateIndex.MirrorFeint;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int WindupTime = 22;
        private const int OmenTime = 58;
        private const int ThreatTime = 16;   //末段全裂隙脉络威慑
        private const int LungeTime = 15;
        private const int BrakeTime = 22;
        #endregion

        /// <summary>0蓄势 1预兆 2突进 3收势</summary>
        private int phase;
        private int phaseTimer;
        private Vector2 realRiftPos;
        private Vector2 lungeDir;
        private int rounds;

        public BrainMirrorFeintState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            rounds = 0;
            context.Npc.damage = 0;
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //蓄势：原地颤动收拢
                    npc.velocity *= 0.9f;
                    npc.damage = 0;
                    context.TelegraphGlow = phaseTimer / (float)WindupTime * 0.6f;

                    if (phaseTimer >= WindupTime) {
                        //服务端布置裂隙并遁入
                        if (!VaultUtils.isClient) {
                            PlaceRifts(context);
                            //遁入屏外持位（裂隙之中）
                            BrainMotion.ServerTeleport(npc, player.Center - Vector2.UnitY * 1500f, Vector2.Zero);
                        }
                        context.Invulnerable = true;
                        phase = 1;
                        phaseTimer = 0;
                    }
                    return null;
                }
                case 1: {
                    //预兆：裂隙搏动，玩家读拍
                    npc.damage = 0;
                    context.GhostFade = 0f;
                    context.BeatIntensity = 0.7f;
                    context.HideFromMinions = true;
                    context.Invulnerable = true;

                    if (!VaultUtils.isClient) {
                        npc.velocity = Vector2.Zero;
                        //末段全裂隙射脉络威慑（不泄露真假）
                        if (phaseTimer == OmenTime - ThreatTime) {
                            ThreatenFromRifts(context);
                        }
                        //整拍出击：等到心跳时钟落在整拍
                        if (phaseTimer >= OmenTime && (int)npc.ai[3] % context.BeatPeriod == 0) {
                            //自真裂隙穿出并锁定突进向
                            lungeDir = (player.Center + player.velocity * 9f - realRiftPos).SafeNormalize(Vector2.UnitY);
                            BrainMotion.ServerTeleport(npc, realRiftPos, lungeDir * 4f);
                            KillAllRifts();
                            phase = 2;
                            phaseTimer = 0;
                        }
                    }
                    //客户端凭“脑已回到玩家身边”跟进相位（穿出瞬移由位移检测自播）
                    else if (phaseTimer >= OmenTime - 4 && npc.Distance(player.Center) < 900f) {
                        phase = 2;
                        phaseTimer = 0;
                    }
                    return null;
                }
                case 2: {
                    //突进：10帧宽限后极速贯穿
                    context.EyeGlint = MathHelper.Clamp(phaseTimer / 8f, 0f, 1f);

                    if (phaseTimer < 10) {
                        npc.damage = 0;
                        if (!VaultUtils.isClient) {
                            npc.velocity = lungeDir * 5f;
                        }
                        return null;
                    }
                    if (phaseTimer == 10) {
                        if (!VaultUtils.isClient) {
                            npc.velocity = lungeDir * (context.IsPhase2 ? 46f : 38f);
                            npc.netUpdate = true;
                        }
                        BrainMotion.Roar(npc.Center, 0.9f, 0.05f);
                        BrainHeartbeat.Thump(1.1f);
                    }
                    if (phaseTimer >= 10) {
                        npc.damage = (int)(npc.defDamage * 1.2f);
                    }
                    if (phaseTimer >= 10 + LungeTime) {
                        phase = 3;
                        phaseTimer = 0;
                    }
                    return null;
                }
                default: {
                    //收势急刹
                    npc.velocity *= 0.86f;
                    npc.damage = 0;
                    if (phaseTimer >= BrakeTime) {
                        rounds++;
                        //二阶段连打两轮
                        if (context.IsPhase2 && rounds < 2) {
                            phase = 0;
                            phaseTimer = 0;
                            return null;
                        }
                        return new BrainHoverState();
                    }
                    return null;
                }
            }
        }

        /// <summary>服务端布置一真多假裂隙</summary>
        private void PlaceRifts(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int riftCount = context.IsPhase2 ? 4 : 3;
            int realIndex = Main.rand.Next(riftCount);
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);

            for (int i = 0; i < riftCount; i++) {
                float angle = baseAngle + MathHelper.TwoPi * i / riftCount + Main.rand.NextFloat(-0.25f, 0.25f);
                float dist = Main.rand.NextFloat(310f, 430f);
                Vector2 pos = player.Center + angle.ToRotationVector2() * dist;
                bool real = i == realIndex;
                if (real) {
                    realRiftPos = pos;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                    ModContent.ProjectileType<BrainTeleportRift>(), 0, 0f, Main.myPlayer, real ? 1f : 0f);
            }
        }

        /// <summary>全部裂隙向玩家放脉络威慑线（不泄露真假）</summary>
        private static void ThreatenFromRifts(BrainStateContext context) {
            int riftType = ModContent.ProjectileType<BrainTeleportRift>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != riftType) {
                    continue;
                }
                Vector2 dir = (context.Target.Center - proj.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(), proj.Center, Vector2.Zero,
                    ModContent.ProjectileType<BrainVeinTelegraph>(), 0, 0f, Main.myPlayer,
                    dir.ToRotation(), 620f, ThreatTime + 14);
            }
        }

        private static void KillAllRifts() {
            int riftType = ModContent.ProjectileType<BrainTeleportRift>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == riftType) {
                    proj.Kill();
                }
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;
            if (!VaultUtils.isClient) {
                KillAllRifts();
            }
        }
    }
}
