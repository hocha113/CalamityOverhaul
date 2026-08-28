using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 月总之头：焊接于核心正上方的主炮台。天眼开阖即弱点窗口（睁眼可打），
    /// 死光扫描/星陨颂唱/月蚀噬咬期间承诺兑换。破坏后残口仍供噬咬锚定
    /// </summary>
    internal class MoonLordHeadAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.MoonLordHead;

        private MLordEyePose pose;
        private float eyelidFrame;
        private float mouthFrame;
        private float wriggleTimer;
        private Player targetPlayer;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0f;
            int newMaxLife = (int)(npc.lifeMax * MLordDirector.HeadLifeFactor);
            npc.life = npc.lifeMax = newMaxLife;
        }

        public override bool AI() {
            npc.aiStyle = -1;
            npc.netOffset = Vector2.Zero;
            npc.knockBackResist = 0f;
            npc.damage = 0;

            NPC core = MLordFacts.GetCore(npc);
            if (core == null) {
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            for (int i = 0; i < npc.buffImmune.Length; i++) {
                npc.buffImmune[i] = true;
            }

            //焊接：所有端确定性贴核心（与原版一致的刚性拼装）
            npc.velocity = Vector2.Zero;
            npc.Center = core.Center + MLordDirector.HeadWeldOffset;
            npc.rotation = core.rotation * 0.5f;

            targetPlayer = Main.player[Math.Clamp(core.target, 0, Main.maxPlayers - 1)];
            MLordStateIndex coreState = MLordFacts.GetCoreState(core);
            bool broken = npc.ai[MLordAiSlots.PartBroken] == MLordAiSlots.BrokenMark;

            if (broken) {
                UpdateBroken(coreState);
            }
            else {
                UpdateAlive(coreState, core);
            }

            if (!VaultUtils.isClient && (Main.GameUpdateCount + (uint)npc.whoAmI) % 2 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        #region 行为

        private void UpdateAlive(MLordStateIndex coreState, NPC core) {
            bool eyeOpen = ComputeEyeOpen(coreState);
            bool mouthOpen = ComputeMouthOpen(coreState);
            npc.dontTakeDamage = !eyeOpen;

            core.TryGetOverride(out MoonLordCoreAI coreAI);
            int stateTimer = coreAI?.StateTimer ?? 0;

            //每技能一种额眼预备语言，基线仍由弱点开阖主导（睁眼亮、闭眼暗）
            float wantLid = eyeOpen ? 0f : 3f;
            Vector2 gazePoint = targetPlayer.Center;
            float wantOut = eyeOpen ? 0.85f : 0.25f;
            float wantGlow = eyeOpen ? 0.8f : 0.08f;
            float angleGain = 0.3f;

            switch (coreState) {
                case MLordStateIndex.DeathrayScan: {
                    //主炮台预备：瞳孔锁向下一束将落下的扫描角——头看哪、束落哪；
                    //眼睑自瞄准的眯眼随蓄力渐渐睁圆，炮口转动带迟重感
                    if (coreAI?.Context != null
                        && MLordDeathrayScanState.TryGetHeadAim(coreAI.Context, stateTimer, out float aim)) {
                        gazePoint = npc.Center + aim.ToRotationVector2() * 300f;
                    }
                    float charge = MathHelper.Clamp(stateTimer / (float)MLordDeathrayScanState.FirstPass, 0f, 1f);
                    wantLid = MathHelper.Lerp(1.6f, 0f, charge);
                    wantOut = MathHelper.Lerp(0.6f, 1f, charge);
                    wantGlow = MathHelper.Max(0.8f, charge);
                    angleGain = 0.14f;
                    break;
                }
                case MLordStateIndex.Starfall: {
                    //仰颂：瞳孔翻向天穹星图（跟着头看就知道星要从哪落），辉光随颂唱明灭
                    Vector2 sky = npc.Center - Vector2.UnitY;
                    if (stateTimer >= MLordStarfallState.WaveOneReveal) {
                        sky = new Vector2(
                            MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorX, npc.Center.X),
                            MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvAnchorY, npc.Center.Y - 600f));
                    }
                    gazePoint = sky;
                    wantGlow = 0.55f + 0.3f * (float)Math.Sin(stateTimer * 0.09f);
                    angleGain = 0.12f;
                    break;
                }
                case MLordStateIndex.Concerto: {
                    //指挥席：交叉波弹出手前的预备窗里睁大提亮（额眼的节拍预告）
                    float windup = coreAI?.Context != null
                        ? MLordConcertoState.HeadBoltWindup(coreAI.Context, stateTimer) : 0f;
                    wantOut = MathHelper.Max(0.85f, 0.85f + 0.15f * windup);
                    wantGlow = MathHelper.Max(0.8f, windup);
                    break;
                }
                case MLordStateIndex.GravityCollapse: {
                    //闭目也在看：暗瞳追着引力井的位置（井在哪，暗面朝哪）
                    if (coreAI?.Context != null) {
                        gazePoint = MLordGravityCollapseState.WellFocusPoint(coreAI.Context, stateTimer);
                    }
                    break;
                }
                case MLordStateIndex.MoonBite: {
                    //噬咬：饿相全张，辉光高频明灭
                    wantOut = 1f;
                    wantGlow = 0.7f + 0.25f * (float)Math.Sin(stateTimer * 0.33f);
                    angleGain = 0.4f;
                    break;
                }
                case MLordStateIndex.PalmExecution: {
                    //处刑期盯死被抓者
                    int victimIndex = (int)MLordFacts.ReadCoreOverrideAi(core, MLordAiSlots.OvGrabTarget) - 1;
                    if (victimIndex >= 0 && victimIndex < Main.maxPlayers && Main.player[victimIndex].active) {
                        gazePoint = Main.player[victimIndex].Center;
                    }
                    break;
                }
            }

            //眼睑帧：0 全开 ↔ 3 闭合（闭合=无敌的可读信号）
            eyelidFrame = MathHelper.Lerp(eyelidFrame, wantLid, 0.16f);
            mouthFrame = MathHelper.Lerp(mouthFrame, mouthOpen ? 2f : 0f, 0.12f);
            pose.PupilAngle = pose.PupilAngle.AngleLerp((gazePoint - npc.Center).ToRotation(), angleGain);
            pose.PupilOut = MathHelper.Lerp(pose.PupilOut, wantOut, 0.09f);
            pose.Glow = MathHelper.Lerp(pose.Glow, wantGlow, 0.1f);
            pose.Broken = false;

            Lighting.AddLight(npc.Center, MLordDirector.Phantasmal.ToVector3() * (0.3f + pose.Glow * 0.5f));
        }

        private void UpdateBroken(MLordStateIndex coreState) {
            npc.dontTakeDamage = true;
            wriggleTimer++;
            pose.Broken = true;
            pose.WriggleTimer = wriggleTimer;
            eyelidFrame = MathHelper.Lerp(eyelidFrame, 3f, 0.1f);
            //噬咬期残口仍张口
            mouthFrame = MathHelper.Lerp(mouthFrame, coreState == MLordStateIndex.MoonBite ? 2f : 0.6f, 0.08f);
        }

        /// <summary>头部弱点窗：死光扫描全程、星陨颂唱、月蚀噬咬（高风险回报）、协奏、掌中处刑（贴身即暴露）</summary>
        private static bool ComputeEyeOpen(MLordStateIndex coreState) {
            return coreState is MLordStateIndex.DeathrayScan or MLordStateIndex.Starfall
                or MLordStateIndex.MoonBite or MLordStateIndex.Concerto
                or MLordStateIndex.PalmExecution;
        }

        /// <summary>口须开阖：噬咬与掌中处刑全开（触须抽打自口而出），星陨颂唱半开</summary>
        private static bool ComputeMouthOpen(MLordStateIndex coreState) {
            return coreState is MLordStateIndex.MoonBite or MLordStateIndex.Starfall
                or MLordStateIndex.PalmExecution;
        }

        #endregion

        #region 死亡与绘制

        /// <summary>
        /// 防御性兜底：原版 checkDead 的 396 特判先于本钩子执行并自行转破（写 -2 + 生成真眼），
        /// 常规路径走不到这里。若钩子先行（顺序变动），这里镜像原版完成同一件事
        /// </summary>
        public override bool? CheckDead() {
            if (npc.ai[MLordAiSlots.PartBroken] != MLordAiSlots.BrokenMark) {
                npc.ai[MLordAiSlots.PartBroken] = MLordAiSlots.BrokenMark;
                npc.life = npc.lifeMax;
                npc.dontTakeDamage = true;
                npc.netUpdate = true;
                if (!VaultUtils.isClient) {
                    int eye = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y,
                        NPCID.MoonLordFreeEye);
                    if (eye < Main.maxNPCs) {
                        Main.npc[eye].ai[MLordAiSlots.PartCoreIndex] = npc.ai[MLordAiSlots.PartCoreIndex];
                        Main.npc[eye].netUpdate = true;
                    }
                }
            }
            return false;
        }

        public override bool CheckActive() => false;

        public override bool FindFrame(int frameHeight) => false;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            MLordDrawHelper.DrawHeadAssembly(spriteBatch, npc, screenPos, in pose,
                (int)Math.Round(eyelidFrame), (int)Math.Round(mouthFrame));
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        #endregion
    }
}
