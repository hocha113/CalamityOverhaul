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
    /// 低血大招·绯红大迁徙：整片地狱的血肉在玩家前方立起第二道死线，
    /// 口袋在墙与血幕之间收缩，不再是逃亡，是被押送。
    /// 血幕收拢到公平下限后保持；结束后力竭喘息，是留给玩家的DPS窗口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.CrimsonExodus, typeof(WofStateContext))]
    internal class WofCrimsonExodusState : WofStateBase
    {
        public override string StateName => "CrimsonExodus";
        public override WofStateIndex StateIndex => WofStateIndex.CrimsonExodus;

        private const int SurgeStart = WofDirector.ExodusWindup;
        private const int SurgeEnd = SurgeStart + WofDirector.ExodusDuration;
        private const int TotalTime = SurgeEnd + WofDirector.ExodusRestFrames;

        /// <summary>心跳提示拍(蓄势期，间隔递减)</summary>
        private int heartbeatTimer;
        private int heartbeatGap = 30;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            heartbeatTimer = 0;
            heartbeatGap = 30;
            context.Npc.ai[0] = 0f;
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //权威进度镜像：服务端写ai[0]，客户端只向前追赶(不回卷防重播)
            if (!VaultUtils.isClient) {
                npc.ai[0] = Timer;
            }
            else if (npc.ai[0] > Timer + 15) {
                Timer = (int)npc.ai[0];
            }

            //血幕本身就是反风筝，大招期间不叠脱屏激怒
            context.FarTimer = 0;

            if (Timer <= SurgeStart) {
                UpdateWindup(context);
            }
            else if (Timer <= SurgeEnd) {
                UpdateSurge(context);
            }
            else {
                UpdateRest(context);
            }

            if (Timer >= TotalTime) {
                //阶段推进：ai[1]=3，此后大招不再触发
                if (!VaultUtils.isClient) {
                    npc.ai[1] = 3f;
                    npc.ai[0] = 0f;
                    npc.netUpdate = true;
                }
                context.Phase = 3;
                //阶段3开场白：喘息窗刚过就亮出新王牌，饥饿长城(王牌扣到低血量才放)
                context.LastAttack = WofStateIndex.JawRipple;
                return new WofJawRippleState();
            }
            return null;
        }

        public override void OnExit(WofStateContext context) {
            base.OnExit(context);
            context.RearCurtainX = 0f;
            context.RearCurtainOpacity = 0f;
        }

        /// <summary>蓄势：心跳加速、血雾自身后聚拢、末段死寂</summary>
        private void UpdateWindup(WofStateContext context) {
            NPC npc = context.Npc;
            float p = Timer / (float)SurgeStart;
            context.AdvanceFactor = 0.25f;
            context.MouthCommand = 2;
            context.SetChargeState(3, p);
            context.WallFlush = 0.5f + 0.5f * p;

            if (VaultUtils.isServer) {
                return;
            }

            //心跳递紧(蓄力语法：密度随进度，末段死寂)
            bool silence = Timer > SurgeStart - 14;
            heartbeatTimer++;
            if (!silence && heartbeatTimer >= heartbeatGap) {
                heartbeatTimer = 0;
                heartbeatGap = Math.Max(8, heartbeatGap - 4);
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.8f, Volume = 0.95f }, Main.LocalPlayer.Center);
                WofMotionFX.CameraPunch(npc.Center, 2f + p * 2f, 8, "WofExodusHeart");
            }

            //血雾在玩家前方汇聚成幕的前兆
            if (!silence && Timer % 3 == 0 && context.Target.Alives()) {
                float foreX = npc.Center.X + npc.direction * WofDirector.CurtainStartGap;
                Vector2 pos = new Vector2(foreX + Main.rand.NextFloat(-160f, 160f),
                    Main.rand.NextFloat(WofWallField.Top, WofWallField.Bottom));
                if (WofMotionFX.OnScreen(pos)) {
                    PRTLoader.NewParticle<PRT_WofBloodMist>(pos,
                        new Vector2(-npc.direction * Main.rand.NextFloat(0.4f, 1.2f), Main.rand.NextFloat(-0.5f, 0.5f)),
                        WofMotionFX.BloodDark, Main.rand.NextFloat(1f, 1.8f))?.Configure(Main.rand.Next(40, 70), 0.5f);
                }
            }
        }

        /// <summary>大迁徙：墙全速推进，血幕自前方收拢，口袋挤压</summary>
        private void UpdateSurge(WofStateContext context) {
            NPC npc = context.Npc;
            int surgeT = Timer - SurgeStart;

            //起手爆发
            if (surgeT == 1 && !VaultUtils.isServer) {
                WofMotionFX.MouthRoar(npc, 1.8f);
                WofMotionFX.CameraPunch(npc.Center, 9f, 24, "WofExodusLaunch", new Vector2(npc.direction, 0f));
                SoundEngine.PlaySound(SoundID.ForceRoarPitched with { Pitch = -0.6f, Volume = 1.1f }, npc.Center);
            }

            context.AdvanceFactor = 1.55f;
            context.MouthCommand = 1;
            //长时段不驻留满档亮球，压迫感交给血幕与滤镜
            context.SetChargeState(3, 0.55f);
            context.WallFlush = 1f;

            //血幕几何：贴着墙面前方 gap 处，gap 随时间收缩到公平下限
            float gap = Math.Max(WofDirector.CurtainMinGap,
                WofDirector.CurtainStartGap - WofDirector.CurtainCloseRate * surgeT);
            if (context.IsDeathMode) {
                gap = Math.Max(gap - 120f, WofDirector.CurtainMinGap - 100f);
            }
            float curtainX = WofWallField.WallFaceX(npc) + npc.direction * gap;
            context.RearCurtainX = curtainX;
            context.RearCurtainOpacity = MathHelper.Clamp(surgeT / 30f, 0f, 1f);

            //血幕伤害与顶回：越过血幕=重击+被顶回口袋
            ApplyCurtainPressure(npc, curtainX);

            if (VaultUtils.isServer) {
                return;
            }

            //血幕前缘的喷沫与幕内暗流
            if (surgeT % 2 == 0) {
                Vector2 pos = new Vector2(curtainX + Main.rand.NextFloat(-30f, 30f) * npc.direction,
                    Main.rand.NextFloat(WofWallField.Top - 100f, WofWallField.Bottom + 100f));
                if (WofMotionFX.OnScreen(pos, 120f)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos,
                        new Vector2(-npc.direction * Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(-2f, 2f)),
                        WofMotionFX.BloodMid, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(20, 36), 0.3f);
                }
            }
            //推进途中的持续低鸣
            if (surgeT % 40 == 0) {
                WofMotionFX.CameraPunch(npc.Center, 2.6f, 12, "WofExodusRumble");
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.7f, Volume = 0.8f }, Main.LocalPlayer.Center);
            }
            WofMotionFX.SpawnWallSeep(npc, 3f);
        }

        /// <summary>力竭喘息：血幕散解，墙速跌落，这是留给你的窗口</summary>
        private void UpdateRest(WofStateContext context) {
            NPC npc = context.Npc;
            int restT = Timer - SurgeEnd;
            float p = restT / (float)WofDirector.ExodusRestFrames;

            context.AdvanceFactor = 0.35f;
            context.MouthCommand = 0;
            context.WallFlush = MathHelper.Lerp(0.8f, 0.18f, p);
            //血幕保持在崩解位置渐隐
            context.RearCurtainOpacity = MathHelper.Clamp(1f - restT / 40f, 0f, 1f);

            if (restT == 1 && !VaultUtils.isServer) {
                //幕体崩解成漫天血雾
                SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.9f, Volume = 1f }, npc.Center);
                float curtainX = context.RearCurtainX;
                if (curtainX != 0f) {
                    for (int i = 0; i < 22; i++) {
                        Vector2 pos = new Vector2(curtainX + Main.rand.NextFloat(-60f, 60f),
                            MathHelper.Lerp(WofWallField.Top, WofWallField.Bottom, (i + 0.5f) / 22f));
                        PRTLoader.NewParticle<PRT_WofBloodMist>(pos, Main.rand.NextVector2Circular(2.5f, 1.5f),
                            WofMotionFX.BloodDark, Main.rand.NextFloat(1.4f, 2.2f))?.Configure(Main.rand.Next(60, 100), 0.6f);
                    }
                }
            }
        }

        /// <summary>血幕压迫：本地玩家越线受击并被顶回(镜像原版舌头自伤模型)</summary>
        private static void ApplyCurtainPressure(NPC npc, float curtainX) {
            if (Main.dedServ || !Main.LocalPlayer.Alives() || Main.LocalPlayer.ghost) {
                return;
            }
            Player player = Main.LocalPlayer;
            //越线判定：玩家在血幕之外(推进方向更远处)
            bool beyond = (player.Center.X - curtainX) * npc.direction > 0f;
            if (!beyond) {
                //近幕警示推力：贴近40px内被血浪往回搡
                float near = (curtainX - player.Center.X) * npc.direction;
                if (near < 60f) {
                    player.velocity.X -= npc.direction * 0.4f;
                }
                return;
            }

            if (!player.immune) {
                player.Hurt(PlayerDeathReason.ByCustomReason(
                    WallOfFleshAI.CurtainDeathReason.Format(player.name)),
                    WallOfFleshAI.ScaleDamage(npc, WofDirector.CurtainDamage), -npc.direction);
            }
            //顶回口袋
            player.velocity.X = -npc.direction * 9f;
        }
    }
}
