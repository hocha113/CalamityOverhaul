using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 大仪式召龙：环阵吟唱，真身可被打断（累伤阈值→法阵崩碎+长硬直），
    /// 打分身=错误献祭（仪式加速）；圆满=幻影龙/远古幻视降临
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.GrandRitual, typeof(CultistStateContext))]
    internal class CultistGrandRitualState : CultistStateBase
    {
        public override string StateName => "GrandRitual";
        public override CultistStateIndex StateIndex => CultistStateIndex.GrandRitual;

        /// <summary>环阵半径（分身AI共用）</summary>
        internal const float RingRadius = 300f;
        /// <summary>打断累伤阈值（最大生命占比）</summary>
        private const float InterruptRatio = 0.08f;
        /// <summary>每次错误献祭跳进的进度帧</summary>
        private const float PunishJumpFrames = 60f;
        private const int SetupTime = 40;
        private const int CompleteHold = 46;

        private int ritualCircleIndex = -1;
        private bool completed;

        private int ChannelTime(CultistStateContext ctx) => ctx.IsDeathMode ? 380 : 480;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            completed = false;
            ritualCircleIndex = -1;
            context.TrueBodyHurtAccum = 0;
            context.LifeSnapshot = context.Npc.life;
            context.RitualProgress = 0f;
            context.RitualPunishRequests = 0;

            if (!VaultUtils.isClient) {
                CultistBossAI.EnsureClones(context, context.DesiredCloneCount);
                Player player = context.Target;
                Vector2 center = player.Alives()
                    ? player.Center + new Vector2(0f, -140f)
                    : context.Npc.Center;
                context.RitualCenter = center;

                //真身站环顶（index 0），分身序位重排
                context.RefreshClones();
                for (int i = 0; i < context.Clones.Count; i++) {
                    context.Clones[i].ai[0] = i;
                    context.Clones[i].netUpdate = true;
                }

                CultistBossAI.BlinkTo(context, RingSlot(context, 0f));

                ritualCircleIndex = Projectile.NewProjectile(context.Npc.GetSource_FromAI(),
                    center, Vector2.Zero, ModContent.ProjectileType<CultistRitualCircle>(),
                    0, 0f, Main.myPlayer, context.Npc.whoAmI, 0f);
                context.Npc.netUpdate = true;
            }
        }

        /// <summary>真身环位（index 0 顶位起，缓旋与分身同步）</summary>
        private static Vector2 RingSlot(CultistStateContext context, float extraDrift) {
            int slotCount = Math.Max(context.Clones.Count + 1, 2);
            float angle = MathHelper.TwoPi * 0f / slotCount + Main.GameUpdateCount * 0.006f - MathHelper.PiOver2 + extraDrift;
            return context.RitualCenter + angle.ToRotationVector2() * RingRadius;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.SkipDefaultHover = true;
            context.CastPose = CultistPose.CastUp;
            context.CastGlow = MathHelper.Clamp(Timer / 60f, 0f, 1f);
            context.ElementAura = 1f;
            CultistScreenFX.DeclareVeil(context.RitualCenter, 0.42f, context.Element);

            //环位吟唱（真身缓旋保持环列）
            Vector2 slot = RingSlot(context, 0f);
            npc.velocity = (slot - npc.Center) * 0.08f;
            if (npc.velocity.Length() > 14f) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * 14f;
            }
            int faceSign = Math.Sign(context.RitualCenter.X - npc.Center.X);
            if (faceSign != 0) {
                npc.direction = npc.spriteDirection = faceSign;
            }

            if ((int)Timer == 6 && !VaultUtils.isServer) {
                CultistBossAI.LocalText(CultistBossAI.LunaticCultist_RitualBeginText, CultistPalette.Main(context.Element));
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.2f }, npc.Center);
            }

            if (Timer <= SetupTime) {
                return null;
            }

            //收束嘶吼姿态各端按同步进度自判（客户端没有 completed 旗）
            if (context.RitualProgress >= 0.995f) {
                context.CastPose = CultistPose.Scream;
            }

            //完成收束演出
            if (completed) {
                if (Timer >= Counter + CompleteHold) {
                    if (!VaultUtils.isClient) {
                        CultistBossAI.DismissClones(context);
                        KillCircle(gracefully: true);
                        return new CultistWeaveState();
                    }
                }
                return null;
            }

            //服务端推进仪式
            if (!VaultUtils.isClient) {
                float step = 1f / ChannelTime(context);
                context.RitualProgress += step;

                //错误献祭：进度跳进
                while (context.RitualPunishRequests > 0) {
                    context.RitualPunishRequests--;
                    context.RitualProgress += PunishJumpFrames * step;
                }

                //真身累伤打断判定
                int hurt = context.LifeSnapshot - npc.life;
                context.LifeSnapshot = npc.life;
                if (hurt > 0) {
                    context.TrueBodyHurtAccum += hurt;
                }
                if (context.TrueBodyHurtAccum >= npc.lifeMax * InterruptRatio) {
                    Collapse(context);
                    return new CultistWeaveState();
                }

                //圆满：召唤降临
                if (context.RitualProgress >= 1f) {
                    context.RitualProgress = 1f;
                    Complete(context);
                }
            }

            //超时保险：进度条早该走完却没收到完成（防呆）
            if (Timer > SetupTime + ChannelTime(context) + PunishJumpFrames * 6f + 120f && !VaultUtils.isClient) {
                CultistBossAI.DismissClones(context);
                KillCircle(gracefully: true);
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>圆满降临（服务端裁决；演出由仪式圈按同步进度各端自放）</summary>
        private void Complete(CultistStateContext context) {
            completed = true;
            Counter = (int)Timer;
            NPC npc = context.Npc;
            Vector2 center = context.RitualCenter;

            //幻影龙优先，已在场则远古幻视
            if (!NPC.AnyNPCs(NPCID.CultistDragonHead)) {
                NPC.NewNPC(npc.GetSource_FromAI(), (int)center.X, (int)center.Y, NPCID.CultistDragonHead);
                //召唤强化：P2/死亡模式带幻视护航
                if (context.IsPhase2 || context.IsDeathMode) {
                    NPC.NewNPC(npc.GetSource_FromAI(),
                        (int)center.X, (int)center.Y - 60, NPCID.AncientCultistSquidhead);
                }
            }
            else {
                NPC.NewNPC(npc.GetSource_FromAI(), (int)center.X, (int)center.Y, NPCID.AncientCultistSquidhead);
            }
        }

        /// <summary>被打断：法阵崩碎+长硬直（服务端）</summary>
        private void Collapse(CultistStateContext context) {
            NPC npc = context.Npc;
            context.StaggerTimer = 130;
            npc.ai[1] = 2f;
            npc.netUpdate = true;
            CultistBossAI.DismissClones(context);
            KillCircle(gracefully: false);
            context.RitualProgress = 0f;

            if (!VaultUtils.isServer) {
                //单机端直接演出（多人端由法阵弹幕的碎裂路径播报）
                CultistScreenFX.PushFlash(0.5f, 20);
                SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.9f, Pitch = 0.2f }, npc.Center);
            }
        }

        /// <summary>处理仪式法阵收场：gracefully=完成淡出，否则碎裂</summary>
        private void KillCircle(bool gracefully) {
            if (ritualCircleIndex < 0 || ritualCircleIndex >= Main.maxProjectiles) {
                return;
            }
            Projectile circle = Main.projectile[ritualCircleIndex];
            if (!circle.active || circle.type != ModContent.ProjectileType<CultistRitualCircle>()) {
                return;
            }
            if (gracefully) {
                //交给尾段淡出而非瞬删
                circle.timeLeft = Math.Min(circle.timeLeft, CultistRitualCircle.EndFadeTime);
                circle.netUpdate = true;
            }
            else {
                circle.ai[1] = 1f;
                circle.netUpdate = true;
            }
        }

        public override void OnExit(CultistStateContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.RitualProgress = 0f;
                //异常离开时兜底收掉法阵
                KillCircle(gracefully: false);
            }
        }
    }
}
