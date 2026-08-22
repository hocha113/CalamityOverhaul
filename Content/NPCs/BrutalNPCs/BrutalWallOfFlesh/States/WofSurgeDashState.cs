using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 蓄力短程突进：墙面压缩蓄势→整面墙猛扑一段→碾磨刹车。
    /// 死线本身的杀招，推进速度就是武器。阶段3双连突进
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.SurgeDash, typeof(WofStateContext))]
    internal class WofSurgeDashState : WofStateBase
    {
        public override string StateName => "SurgeDash";
        public override WofStateIndex StateIndex => WofStateIndex.SurgeDash;

        private const int Recover = 30;

        /// <summary>当前段内计时</summary>
        private int segTimer;
        /// <summary>0蓄势 1冲刺 2刹车 3收尾</summary>
        private int stage;
        /// <summary>已完成的冲刺次数</summary>
        private int dashDone;
        /// <summary>刹车起始速度</summary>
        private float brakeSpeed;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            segTimer = 0;
            stage = 0;
            dashDone = 0;
            if (!VaultUtils.isServer) {
                //血肉紧绷的湿响
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.6f, Volume = 1f }, context.Npc.Center);
            }
        }

        /// <summary>本次蓄势帧数：第二段更短</summary>
        private int TelegraphFrames(WofStateContext ctx) {
            int frames = dashDone == 0 ? WofDirector.SurgeTelegraph : 26;
            if (ctx.IsDeathMode) {
                frames -= 8;
            }
            return frames;
        }

        private int TotalDashes(WofStateContext ctx) => ctx.Phase >= 3 ? 2 : 1;

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            segTimer++;

            switch (stage) {
                case 0:
                    UpdateTelegraph(context);
                    break;
                case 1:
                    UpdateDash(context);
                    break;
                case 2:
                    UpdateBrake(context);
                    break;
                default:
                    context.AdvanceFactor = 0.8f;
                    if (segTimer >= Recover) {
                        return new WofAdvanceState();
                    }
                    break;
            }
            return null;
        }

        /// <summary>蓄势：墙近乎停滞、面缘白热、向心汇聚，静止越久，扑出越凶</summary>
        private void UpdateTelegraph(WofStateContext context) {
            NPC npc = context.Npc;
            int telegraph = TelegraphFrames(context);
            float p = MathHelper.Clamp(segTimer / (float)telegraph, 0f, 1f);

            //压缩感：速度掐到几乎为零
            context.SpeedOverride = MathHelper.Lerp(2f, 0.25f, p * p);
            context.SetChargeState(1, p);
            context.WallFlush = 0.4f + 0.6f * p;
            context.MouthCommand = 2;

            if (!VaultUtils.isServer) {
                //向心汇聚：血珠被拽回墙面(蓄力语法)
                if (p < 0.75f && segTimer % 2 == 0) {
                    float faceX = WofWallField.WallFaceX(npc);
                    float y = Main.rand.NextFloat(WofWallField.Top, WofWallField.Bottom);
                    Vector2 from = new Vector2(faceX + npc.direction * Main.rand.NextFloat(90f, 320f), y);
                    if (WofMotionFX.OnScreen(from)) {
                        PRTLoader.NewParticle<PRT_Spark>(from, new Vector2(-npc.direction * Main.rand.NextFloat(5f, 11f), 0f),
                            WofMotionFX.BloodHot, Main.rand.NextFloat(0.9f, 1.5f))?.Configure(false, 14);
                    }
                }
                //低鸣爬升
                if (segTimer % 8 == 0) {
                    WofMotionFX.CameraPunch(npc.Center, 0.8f + 2.4f * p * p, 9, "WofSurgeRumble");
                }
                //预警拍：冲刺前12帧的破裂脆响
                if (segTimer == telegraph - 12) {
                    SoundEngine.PlaySound(SoundID.Item171 with { Pitch = -0.2f, Volume = 1f }, npc.Center);
                }
            }

            if (segTimer >= telegraph) {
                stage = 1;
                segTimer = 0;
                LaunchDash(context);
            }
        }

        /// <summary>一帧定速起跳(launch is a set)</summary>
        private void LaunchDash(WofStateContext context) {
            NPC npc = context.Npc;
            if (!VaultUtils.isServer) {
                WofMotionFX.MouthRoar(npc, 1.3f, playSound: false);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.35f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = 0.15f, Volume = 0.9f }, npc.Center);
                WofMotionFX.CameraPunch(npc.Center, 7f, 16, "WofSurgeLaunch", new Vector2(npc.direction, 0f));
                //整面墙的喷发帧
                for (int i = 0; i < 9; i++) {
                    float y = MathHelper.Lerp(WofWallField.Top, WofWallField.Bottom, (i + 0.5f) / 9f);
                    WofMotionFX.SpawnBloodBurst(new Vector2(WofWallField.WallFaceX(npc), y), 0.8f,
                        new Vector2(npc.direction, 0f));
                }
            }
        }

        /// <summary>冲刺：全高死线扑击，带状追加伤害</summary>
        private void UpdateDash(WofStateContext context) {
            NPC npc = context.Npc;
            float dashSpeed = WofDirector.SurgeSpeed;
            if (context.IsDeathMode) {
                dashSpeed += 3f;
            }
            context.SpeedOverride = dashSpeed;
            context.SetChargeState(1, 1f);
            context.WallFlush = 1f;
            context.MouthCommand = 1;

            //带状追加伤害：墙面扑到即撞飞(本地玩家自伤模型，镜像原版舌头判定)
            float faceX = WofWallField.WallFaceX(npc);
            float xMin = npc.direction > 0 ? faceX - 60f : faceX - 140f;
            float xMax = npc.direction > 0 ? faceX + 140f : faceX + 60f;
            WofWallField.HurtLocalPlayerInBand(npc, xMin, xMax,
                WallOfFleshAI.ScaleDamage(npc, WofDirector.SurgeBandDamage),
                PlayerDeathReason.ByNPC(npc.whoAmI), npc.direction);

            if (!VaultUtils.isServer) {
                //冲刺扬浪：面缘高频洒血
                WofMotionFX.SpawnWallSeep(npc, 4f);
                if (segTimer % 3 == 0) {
                    float y = Main.rand.NextFloat(WofWallField.Top, WofWallField.Bottom);
                    PRTLoader.NewParticle<PRT_WofBloodMist>(new Vector2(faceX - npc.direction * 40f, y),
                        new Vector2(-npc.direction * Main.rand.NextFloat(1f, 3f), 0f),
                        WofMotionFX.BloodDark, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(26, 40), 0.5f);
                }
                if (segTimer % 5 == 0) {
                    WofMotionFX.CameraPunch(npc.Center, 3.2f, 8, "WofSurgeDash", new Vector2(npc.direction, 0f));
                }
            }

            if (segTimer >= WofDirector.SurgeDashFrames) {
                stage = 2;
                segTimer = 0;
                dashDone++;
                brakeSpeed = dashSpeed;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.7f, Volume = 1f }, npc.Center);
                }
            }
        }

        /// <summary>碾磨刹车：速度指数衰减，血肉过载喘息</summary>
        private void UpdateBrake(WofStateContext context) {
            NPC npc = context.Npc;
            brakeSpeed *= 0.82f;
            context.SpeedOverride = Math.Max(brakeSpeed, 1.2f);
            context.WallFlush = MathHelper.Lerp(1f, 0.4f, segTimer / (float)WofDirector.SurgeBrakeFrames);
            context.MouthCommand = 2;

            if (!VaultUtils.isServer && segTimer % 3 == 0) {
                WofMotionFX.SpawnWallSeep(npc, 1.6f);
            }

            if (segTimer >= WofDirector.SurgeBrakeFrames) {
                segTimer = 0;
                //阶段3二段突进
                if (dashDone < TotalDashes(context)) {
                    stage = 0;
                }
                else {
                    stage = 3;
                }
            }
        }

        public override void OnExit(WofStateContext context) {
            base.OnExit(context);
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
