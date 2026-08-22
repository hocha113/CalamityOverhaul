using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 真假镜像同步进攻：假体点对称奴役真身，全员同时收势同时贯穿
    /// 可学习破绽：只有真身发光、出手前有眼芒；假体判定伤害折减
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.MirrorStrike, typeof(BrainStateContext))]
    internal class BrainMirrorStrikeState : BrainStateBase
    {
        public override string StateName => "MirrorStrike";
        public override BrainStateIndex StateIndex => BrainStateIndex.MirrorStrike;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int SetupTime = 30;
        private const int ReelTime = 24;      //反向收势
        private const int LungeTime = 12;     //极速贯穿
        private const int RecoverTime = 26;   //刹车回稳
        /// <summary>假体接触伤害（原始值，过怪物弹幕倍率）</summary>
        internal const int MirrorContactDamage = 11;
        #endregion

        /// <summary>0布阵 1游走 2收势 3贯穿 4回稳</summary>
        private int phase;
        private int phaseTimer;
        private int passesDone;
        private int totalPasses = 2;
        private Vector2 strikeAnchor;
        private Vector2 lungeDir;
        private float orbitDir = 1f;

        public BrainMirrorStrikeState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            passesDone = 0;
            context.Npc.damage = 0;

            if (!VaultUtils.isClient) {
                totalPasses = context.IsPhase2 ? 3 : 2;
                orbitDir = Main.rand.NextBool() ? 1f : -1f;
                SpawnMirrors(context);
            }
        }

        /// <summary>服务端生成点对称假体（一阶段1具，二阶段2具错轴）</summary>
        private static void SpawnMirrors(BrainStateContext context) {
            NPC npc = context.Npc;
            Vector2 anchor = context.Target.Center;
            int damage = MirrorContactDamage + (context.IsDeathMode ? 3 : 0);

            Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                ModContent.ProjectileType<BrainMirrorImage>(), damage, 0f, Main.myPlayer,
                BrainMirrorImage.PackMode(BrainMirrorImage.ModePointMirror, 0), anchor.X, anchor.Y);

            if (context.IsPhase2) {
                Vector2 anchor2 = anchor + new Vector2(0f, -150f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), anchor2, Vector2.Zero,
                    ModContent.ProjectileType<BrainMirrorImage>(), damage, 0f, Main.myPlayer,
                    BrainMirrorImage.PackMode(BrainMirrorImage.ModePointMirror, 1), anchor2.X, anchor2.Y);
            }
        }

        /// <summary>服务端把全部假体锚点重设为新预测点并同步</summary>
        private static void RetargetMirrors(BrainStateContext context, Vector2 anchor) {
            int mirrorType = ModContent.ProjectileType<BrainMirrorImage>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != mirrorType) {
                    continue;
                }
                int slot = (int)proj.ai[0] % 100;
                int mode = (int)proj.ai[0] / 100;
                if (mode != BrainMirrorImage.ModePointMirror) {
                    continue;
                }
                Vector2 slotAnchor = slot == 1 ? anchor + new Vector2(0f, -150f) : anchor;
                proj.ai[1] = slotAnchor.X;
                proj.ai[2] = slotAnchor.Y;
                proj.netUpdate = true;
            }
        }

        private static void KillMirrors() {
            int mirrorType = ModContent.ProjectileType<BrainMirrorImage>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == mirrorType) {
                    proj.Kill();
                }
            }
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //布阵：贴近玩家侧翼
                    npc.damage = 0;
                    if (!VaultUtils.isClient) {
                        Vector2 side = player.Center + new Vector2(orbitDir * 420f, -60f);
                        BrainMotion.SpringHover(npc, side, 0.03f, 0.12f, 26f);
                    }
                    if (phaseTimer >= SetupTime) {
                        phase = 1;
                        phaseTimer = 0;
                    }
                    return null;
                }
                case 1: {
                    //游走：环绕玩家弧行，假体自然反相位起舞
                    npc.damage = 0;
                    int orbitTime = context.IsPhase2 ? 26 : 38;
                    if (!VaultUtils.isClient) {
                        Vector2 toBrain = npc.Center - player.Center;
                        float radius = MathHelper.Lerp(toBrain.Length(), 430f, 0.08f);
                        float angle = toBrain.ToRotation() + orbitDir * 0.052f;
                        Vector2 orbitPos = player.Center + angle.ToRotationVector2() * radius;
                        npc.velocity = (orbitPos - npc.Center) * 0.42f;

                        //游走中锚点持续跟随玩家，镜像贴身压迫（10帧节流防包洪）
                        if (phaseTimer % 10 == 0) {
                            RetargetMirrors(context, player.Center);
                        }

                        if (phaseTimer >= orbitTime) {
                            //锁定打击锚点（预测位），此后锚不再动，给玩家一个可读的静态镜心
                            strikeAnchor = player.Center + player.velocity * 11f;
                            RetargetMirrors(context, strikeAnchor);
                            npc.netUpdate = true;
                            phase = 2;
                            phaseTimer = 0;
                        }
                    }
                    else if (phaseTimer >= orbitTime + 4) {
                        //客户端凭时间跟进
                        phase = 2;
                        phaseTimer = 0;
                    }
                    return null;
                }
                case 2: {
                    //收势：反向拉开（pow 曲线末段骤然吸开），真身眼芒渐亮
                    npc.damage = 0;
                    float t = phaseTimer / (float)ReelTime;
                    context.EyeGlint = t;
                    context.TelegraphGlow = t * 0.75f;

                    if (!VaultUtils.isClient) {
                        Vector2 away = (npc.Center - strikeAnchor).SafeNormalize(Vector2.UnitY);
                        npc.velocity = away * (float)Math.Pow(t, 6) * 26f;

                        if (phaseTimer >= ReelTime) {
                            lungeDir = (strikeAnchor - npc.Center).SafeNormalize(Vector2.UnitY);
                            npc.velocity = lungeDir * (context.IsPhase2 ? 44f : 38f);
                            npc.netUpdate = true;
                            BrainMotion.Roar(npc.Center, 0.85f, 0.12f);
                            BrainHeartbeat.Thump(1.05f);
                            phase = 3;
                            phaseTimer = 0;
                        }
                    }
                    else if (phaseTimer >= ReelTime + 2) {
                        phase = 3;
                        phaseTimer = 0;
                    }
                    return null;
                }
                case 3: {
                    //贯穿：真假同时对穿镜心
                    npc.damage = (int)(npc.defDamage * 1.25f);
                    if (phaseTimer >= LungeTime) {
                        phase = 4;
                        phaseTimer = 0;
                    }
                    return null;
                }
                default: {
                    //回稳急刹
                    npc.damage = 0;
                    npc.velocity *= 0.84f;
                    if (phaseTimer >= RecoverTime) {
                        passesDone++;
                        if (passesDone >= totalPasses && !VaultUtils.isClient) {
                            return new BrainHoverState();
                        }
                        phase = 1;
                        phaseTimer = 0;
                    }
                    return null;
                }
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                KillMirrors();
            }
        }
    }
}
