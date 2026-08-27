using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 核心裸露转换：部件全破后的仪式拍，胸甲炸开、心跳加速、日蚀压满，
    /// 自此核心可被伤害，真眼集群成为常驻威胁。
    /// 收尾拍是四条瘫臂的复活：眼是手臂的控制器官，眼全爆了手就瘫着；
    /// 此刻中枢越过坏掉的外围亲自接管，抽搐→绷直→重新抓地，终局用断了眼的手爬
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.CoreExposure, typeof(MLordContext))]
    internal class MLordCoreExposureState : MLordStateBase
    {
        public override string StateName => "CoreExposure";
        public override MLordStateIndex StateIndex => MLordStateIndex.CoreExposure;

        internal const int CrackTick = 44;
        /// <summary>瘫臂接管拍：自此四臂开始抽搐绷直，到仪式结束正好抬成待抓姿态</summary>
        internal const int ReanimateTick = CrackTick + 84;
        internal const int CeremonyEnd = 170;

        /// <summary>
        /// 瘫臂复活进度 0~1（手部 AI 消费：0=完全瘫着，1=绷直待抓）。
        /// 不在本状态返回 0，手臂照常瘫着
        /// </summary>
        internal static float ReanimateProgress(NPC core, int stateTimer) {
            if (MLordFacts.GetCoreState(core) != MLordStateIndex.CoreExposure) {
                return 0f;
            }
            return MathHelper.Clamp((stateTimer - ReanimateTick) / (float)(CeremonyEnd - ReanimateTick), 0f, 1f);
        }

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                //转换即清场（公平阀门）
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.hostile) {
                        p.Kill();
                    }
                }
                context.Npc.TargetClosest();
                context.Npc.netUpdate = true;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;

            context.HoldAllParts = true;
            context.EclipseDrive = MathHelper.Max(context.EclipseDrive, MathHelper.Clamp(Timer / 60f, 0f, 1f));
            //仪式定格：本体失能悬滞（四臂此刻还瘫着，复活拍之后才接管爬行）
            npc.velocity *= 0.92f;

            if (Timer < CrackTick) {
                //震颤蓄势
                context.SetChargeState(Timer / (float)CrackTick);
                if (!VaultUtils.isServer) {
                    MLordScreenFX.ConvergeStreak(npc.Center, 380f, Timer / (float)CrackTick);
                }
            }
            else {
                context.HeartExposure = 1f;
            }

            if (Timer == CrackTick) {
                //胸甲炸开
                context.Owner.PopChestPlates();
                if (!VaultUtils.isServer) {
                    MoonlordDeathDrama.RequestLight(0.7f, npc.Center);
                    MLordScreenEffects.PushStarRing(npc.Center, 1.1f, 900f, 36);
                    MLordScreenFX.StarBurst(npc.Center, 2f, 30);
                    MLordScreenFX.Punch(npc.Center, 12f, 22);
                    SoundEngine.PlaySound(SoundID.NPCDeath62 with { Volume = 1.1f, Pitch = -0.4f }, npc.Center);
                }
            }
            if (Timer > CrackTick && Timer < CrackTick + 24 && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(0.7f * (1f - (Timer - CrackTick) / 24f), npc.Center);
            }

            //心跳三连加速
            if ((Timer == CrackTick + 30 || Timer == CrackTick + 54 || Timer == CrackTick + 72) && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 1f, Pitch = -0.8f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 4f, 8);
            }

            //瘫臂接管：中枢接线的那一记闷响，四臂自此绷起来
            if (Timer == ReanimateTick && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 0.9f, Pitch = -0.75f }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 7f, 14);
            }

            Timer++;
            if (Timer >= CeremonyEnd) {
                npc.ai[MLordAiSlots.CorePhase] = MLordPhase.CoreExposed;
                if (!VaultUtils.isClient) {
                    return NextAttack(context);
                }
            }
            return null;
        }
    }
}
