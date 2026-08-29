using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States
{
    /// <summary>死亡演出：垂死挣扎→尾至头连锁溃爆→昂首死寂→头颅炸裂真死</summary>
    [InnoVault.StateMachines.VaultState((int)EowStateIndex.Death, typeof(EowStateContext))]
    internal class EowDeathState : EowStateBase
    {
        public override string StateName => "Death";
        public override EowStateIndex StateIndex => EowStateIndex.Death;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int AgonyTime = 66;
        private const int RuptureTime = 150;
        private const int RuptureEnd = AgonyTime + RuptureTime;
        private const int RearUpTime = 44;
        private const int SilenceTime = 22;
        private const int FinaleFrame = RuptureEnd + RearUpTime + SilenceTime;
        private const int TotalTime = FinaleFrame + 34;
        #endregion

        private bool finaleFired;

        public EowDeathState() {
        }

        public override void OnEnter(EowStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            context.DeathPerformanceFinished = false;
            finaleFired = false;

            NPC npc = context.Npc;
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            context.RefreshSegments();

            //垂死哀嚎
            EowMotionFX.PlayRoar(npc.Center, -0.2f, 1.1f);
            SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = -0.5f, Volume = 0.9f }, npc.Center);
        }

        public override IEowState OnUpdate(EowStateContext context) {
            NPC npc = context.Npc;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }

            Tick();
            context.MiasmaLevel = MathHelper.Clamp(Timer / (float)RuptureEnd, 0f, 1f) * 0.9f;

            if (Timer % 15 == 0) {
                context.RefreshSegments();
            }

            //幕一 垂死挣扎：剧烈扭动减速
            if (Timer <= AgonyTime) {
                UpdateAgony(context);
                return null;
            }

            //幕二 尾→头连锁溃爆：波前相位交给体节本地消费
            if (Timer <= RuptureEnd) {
                UpdateRupture(context);
                return null;
            }

            //幕三 昂首+死寂
            if (Timer < FinaleFrame) {
                UpdateRearUp(context);
                return null;
            }

            //终爆帧
            if (!finaleFired) {
                finaleFired = true;
                DoFinale(context);
            }

            //真死放行(服务端)
            if (Timer >= TotalTime && !VaultUtils.isClient) {
                FinishForReal(context);
            }

            return null;
        }

        #region 幕一 垂死挣扎
        private void UpdateAgony(EowStateContext context) {
            NPC npc = context.Npc;

            //挣扎摆头：速度快速衰减+随机甩动
            npc.velocity *= 0.93f;
            float thrash = 1f - Timer / (float)AgonyTime;
            npc.velocity += new Vector2(
                (float)Math.Sin(Timer * 0.31f) * 1.4f,
                (float)Math.Cos(Timer * 0.23f) * 1.1f) * thrash;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            context.PulseKind = 3;
            context.PulsePhase = 1.05f; //波前尚未入场

            //零星迸酸
            if (!VaultUtils.isServer && Timer % 5 == 0 && context.Segments.Count > 0) {
                NPC seg = context.Segments[Main.rand.Next(context.Segments.Count)];
                if (seg.Alives() && EowMotionFX.OnScreen(seg.Center)) {
                    EowMotionFX.SpawnAcidBurst(seg.Center, 0.6f);
                }
            }
            if (Timer % 14 == 0) {
                EowMotionFX.CameraPunch(npc.Center, 2.2f, 12, "EowDeathAgony");
            }
        }
        #endregion

        #region 幕二 连锁溃爆
        private void UpdateRupture(EowStateContext context) {
            NPC npc = context.Npc;
            npc.velocity *= 0.9f;

            //波前 1(尾)→0(头)，体节各自本地起爆(见 EowBodyAI.UpdateDeathRuptureFX)
            float t = (Timer - AgonyTime) / (float)RuptureTime;
            context.PulseKind = 3;
            context.PulsePhase = 1f - t;

            //波前伴随震屏与声浪渐密
            if (Timer % Math.Max(10 - (int)(t * 6f), 3) == 0) {
                EowMotionFX.CameraPunch(npc.Center, 2.5f + t * 4f, 10, "EowDeathRupture");
            }
        }
        #endregion

        #region 幕三 昂首死寂
        private void UpdateRearUp(EowStateContext context) {
            NPC npc = context.Npc;
            int localTimer = Timer - RuptureEnd;

            context.PulseKind = 3;
            context.PulsePhase = 0f;

            if (localTimer <= RearUpTime) {
                //缓缓昂起
                npc.velocity = new Vector2(0f, -1.9f);
                npc.rotation = npc.rotation.AngleLerp(0f, 0.09f);
                if (localTimer == RearUpTime / 2) {
                    EowMotionFX.PlayRoar(npc.Center, 0.4f, 1.2f);
                }
            }
            else {
                //死寂：一切停摆(爆发前的静默)
                npc.velocity = Vector2.Zero;
            }
        }
        #endregion

        #region 终爆
        private void DoFinale(EowStateContext context) {
            NPC npc = context.Npc;
            if (VaultUtils.isServer) {
                return;
            }

            //头颅酸爆：环+酸泉+腐雾+闷响
            PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                EowMotionFX.AcidGreen, 0.1f).Configure(0.1f, 2.4f, 34);
            for (int i = 0; i < 46; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 16f);
                PRTLoader.NewParticle<PRT_AcidSplash>(npc.Center, vel, Color.White,
                    Main.rand.NextFloat(0.6f, 1.3f)).Configure(Main.rand.Next(28, 52));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_ToxicMist>(npc.Center + Main.rand.NextVector2Circular(50f, 50f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f) - Vector2.UnitY,
                    Color.White, Main.rand.NextFloat(1.1f, 1.7f)).Configure(Main.rand.Next(55, 90), 0.7f);
            }
            EowMotionFX.CameraPunch(npc.Center, 13f, 30, "EowDeathFinale");
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.7f, Volume = 1.2f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Pitch = -0.6f, Volume = 1.1f }, npc.Center);
            Lighting.AddLight(npc.Center, EowMotionFX.AcidGreen.ToVector3() * 3f);
        }

        /// <summary>
        /// 真死：先解除 realLife 重定向再按链序放行体节原版死亡(每节掉鳞/魔矿)，<br/>
        /// 头最后死，它作为场上最后一节触发 DropEoWLoot 的 boss 结算(袋/旗标/事件)
        /// </summary>
        private void FinishForReal(EowStateContext context) {
            NPC npc = context.Npc;
            context.DeathPerformanceFinished = true;

            context.RefreshSegments();
            foreach (var seg in context.Segments) {
                if (!seg.Alives()) {
                    continue;
                }
                //checkDead 对 realLife 重定向节直接早退，必须先解绑
                seg.realLife = -1;
                seg.dontTakeDamage = false;
                seg.life = 0;
                seg.HitEffect();
                seg.checkDead();
                if (Main.dedServ) {
                    Terraria.NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, seg.whoAmI);
                }
            }

            npc.realLife = -1;
            npc.dontTakeDamage = false;
            npc.life = 0;
            npc.HitEffect();
            npc.checkDead();
            if (Main.dedServ) {
                Terraria.NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }

            //入演出那帧的假死烧掉了讨伐追踪闩，腐环这类多节判定的祝福在那帧又因残部未清不入档，
            //真死的这一击则被闩挡住——收尾补账（幂等，反馈 #44）
            GameModes.Blessings.BlessingKillNPC.RecordPerformanceKill(npc);
        }
        #endregion
    }
}
