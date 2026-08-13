using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 终焉时刻：假死坍缩（部件被引力吞回）→引力内爆聚星→死寂三十帧→
    /// 超新星白爆（原版躯体残片抛洒）→余烬星尘落幕→放行真死。
    /// 全程锁血，时间轴各端本地推进，运镜由玩家侧驱动
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.Death, typeof(MLordContext))]
    internal class MLordDeathState : MLordStateBase
    {
        public override string StateName => "Death";
        public override MLordStateIndex StateIndex => MLordStateIndex.Death;

        //时间轴（阶段截止帧）
        internal const int PhaseCollapseEnd = 96;
        internal const int PhaseImplosionEnd = 186;
        internal const int PhaseSilenceEnd = 218;
        internal const int PhaseSupernovaEnd = 268;
        internal const int PhaseEmbersEnd = 462;

        /// <summary>计时推阶段</summary>
        internal static MLordDeathPhase GetPhase(int t) {
            if (t < PhaseCollapseEnd) {
                return MLordDeathPhase.Collapse;
            }
            if (t < PhaseImplosionEnd) {
                return MLordDeathPhase.Implosion;
            }
            if (t < PhaseSilenceEnd) {
                return MLordDeathPhase.Silence;
            }
            if (t < PhaseSupernovaEnd) {
                return MLordDeathPhase.Supernova;
            }
            if (t < PhaseEmbersEnd) {
                return MLordDeathPhase.Embers;
            }
            return MLordDeathPhase.Done;
        }

        private bool piecesThrown;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            piecesThrown = false;

            npc.ai[MLordAiSlots.CorePhase] = MLordPhase.DeathShow;
            //一击致死走原版 398 特判会把生命回满，这里压回 1 保持败像
            npc.life = 1;
            npc.velocity *= 0.4f;
            context.DeathTimer = 0;
            context.DeathPhase = MLordDeathPhase.Collapse;
            MoonLordCoreAI.ActivePerformanceCore = npc.whoAmI;

            //清 debuff 计时
            for (int i = 0; i < npc.buffType.Length; i++) {
                npc.buffTime[i] = 0;
            }
            if (!VaultUtils.isClient) {
                //死亡演出开场清弹幕（观演公平）
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.hostile) {
                        p.Kill();
                    }
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie92 with { Volume = 1.2f, Pitch = -0.7f }, npc.Center);
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (MoonLordCoreAI.ActivePerformanceCore == context.Npc.whoAmI) {
                MoonLordCoreAI.ActivePerformanceCore = -1;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;

            //锁血急停
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.velocity *= 0.86f;
            context.HoldAllParts = true;
            context.HeartExposure = 1f;

            MLordDeathPhase phase = GetPhase(Timer);
            context.DeathPhase = phase;
            context.DeathTimer = Timer;

            switch (phase) {
                case MLordDeathPhase.Collapse:
                    UpdateCollapse(context);
                    break;
                case MLordDeathPhase.Implosion:
                    UpdateImplosion(context);
                    break;
                case MLordDeathPhase.Silence:
                    UpdateSilence(context);
                    break;
                case MLordDeathPhase.Supernova:
                    UpdateSupernova(context);
                    break;
                case MLordDeathPhase.Embers:
                    UpdateEmbers(context);
                    break;
            }

            Timer++;
            if (Timer >= PhaseEmbersEnd) {
                context.DeathPerformanceFinished = true;
                if (MoonLordCoreAI.ActivePerformanceCore == npc.whoAmI) {
                    MoonLordCoreAI.ActivePerformanceCore = -1;
                }
                if (!VaultUtils.isClient) {
                    //挂原版真死哨兵放行：ai[0]=2 让 checkDead 走正规掉落/进度
                    npc.ai[MLordAiSlots.CorePhase] = MLordPhase.VanillaDeathSentinel;
                    npc.dontTakeDamage = false;
                    npc.life = 0;
                    npc.HitEffect();
                    npc.checkDead();
                    npc.netUpdate = true;
                }
            }
            return null;
        }

        /// <summary>假死坍缩：部件依次被引力吞回体内</summary>
        private void UpdateCollapse(MLordContext context) {
            NPC npc = context.Npc;
            context.EclipseDrive = 1f;

            //吞回节拍：真眼×5→四手残口→头残口（服务端裁定移除，客户端表现）。
            //死亡必经核心裸露，场上恒有 5 真眼 + 5 残口 = 10 名从属；
            //9 帧一拍自 8 起共 10 拍（8~89），坍缩段 96 帧内吞完，从属不足时空拍无害
            if (!VaultUtils.isClient && Timer >= 8 && Timer < PhaseCollapseEnd && (Timer - 8) % 9 == 0) {
                context.Owner.ConsumeOneServant();
            }

            if (VaultUtils.isServer) {
                return;
            }
            MLordScreenEffects.PushGravityDim(npc.Center, MathHelper.Clamp(Timer / (float)PhaseCollapseEnd, 0f, 1f) * 0.6f);
            MLordScreenFX.ConvergeStreak(npc.Center, 700f, Timer / (float)PhaseCollapseEnd * 0.5f);
            if (Timer % 14 == 0) {
                MLordScreenFX.Punch(npc.Center, 3f, 8);
            }
        }

        /// <summary>引力内爆：身躯收缩，星辉全数向心</summary>
        private void UpdateImplosion(MLordContext context) {
            NPC npc = context.Npc;
            float t = (Timer - PhaseCollapseEnd) / (float)(PhaseImplosionEnd - PhaseCollapseEnd);
            //收缩到 0.93
            npc.scale = MathHelper.Lerp(1f, 0.93f, VaultUtils.EaseInQuad(t));

            if (VaultUtils.isServer) {
                return;
            }
            MLordScreenEffects.PushGravityDim(npc.Center, 0.6f + t * 0.4f);
            MLordScreenFX.ConvergeStreak(npc.Center, 820f, t);
            MoonlordDeathDrama.RequestLight(t * 0.25f, npc.Center);
            if (Timer == PhaseImplosionEnd - 30) {
                SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 1f, Pitch = -0.8f }, npc.Center);
            }
        }

        /// <summary>死寂：一切声画骤停，只余心跳</summary>
        private void UpdateSilence(MLordContext context) {
            NPC npc = context.Npc;
            if (VaultUtils.isServer) {
                return;
            }
            //刻意什么都不放——静默本身是演出
            if (Timer == PhaseSilenceEnd - 16) {
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 1.2f, Pitch = -0.9f }, npc.Center);
            }
        }

        /// <summary>超新星：白爆抛洒躯体残片，一场唯一的最大冲击</summary>
        private void UpdateSupernova(MLordContext context) {
            NPC npc = context.Npc;
            npc.scale = 1f;

            if (Timer == PhaseSilenceEnd && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(1f, npc.Center);
                MLordScreenEffects.PushNova(npc.Center, 1f, 46);
                MLordScreenEffects.PushStarRing(npc.Center, 1.2f, 1500f, 44);
                MLordScreenFX.Punch(npc.Center, 21f, 40);
                SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1.3f, Pitch = -0.5f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.8f }, npc.Center);
            }
            if (!piecesThrown && Timer >= PhaseSilenceEnd + 6 && !VaultUtils.isServer) {
                piecesThrown = true;
                //原版躯体残片抛洒 + 爆点群
                MoonlordDeathDrama.ThrowPieces(npc.Center, (int)(npc.whoAmI + Main.GameUpdateCount));
                for (int i = 0; i < 7; i++) {
                    MoonlordDeathDrama.AddExplosion(npc.Center + Main.rand.NextVector2Circular(260f, 300f));
                }
                MLordScreenFX.StarBurst(npc.Center, 3f, 46);
            }
            if (Timer > PhaseSilenceEnd && !VaultUtils.isServer) {
                float t = (Timer - PhaseSilenceEnd) / (float)(PhaseSupernovaEnd - PhaseSilenceEnd);
                MoonlordDeathDrama.RequestLight(1f - VaultUtils.EaseInQuad(t) * 0.6f, npc.Center);
            }
        }

        /// <summary>余烬：白光退潮，星屑与残片飘落，日蚀松开天光</summary>
        private void UpdateEmbers(MLordContext context) {
            NPC npc = context.Npc;
            float t = (Timer - PhaseSupernovaEnd) / (float)(PhaseEmbersEnd - PhaseSupernovaEnd);
            context.EclipseDrive = MathHelper.Clamp(1f - t * 1.4f, 0f, 1f);
            //本体隐去
            npc.alpha = (int)MathHelper.Clamp(t * 3f * 255f, 0f, 255f);

            if (VaultUtils.isServer) {
                return;
            }
            if (t < 0.35f) {
                MoonlordDeathDrama.RequestLight(0.4f * (1f - t / 0.35f), npc.Center);
            }
            if (Timer % 5 == 0 && t < 0.7f) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(420f, 300f);
                MLordScreenFX.StarBurst(pos, 0.35f, 3);
            }
            if (Timer % 24 == 0) {
                MoonlordDeathDrama.AddExplosion(npc.Center + Main.rand.NextVector2Circular(340f, 260f));
            }
        }
    }
}
