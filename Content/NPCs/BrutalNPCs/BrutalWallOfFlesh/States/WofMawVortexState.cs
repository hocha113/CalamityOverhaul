using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 口部吸引漩涡：巨口洞开把玩家往嘴里拽，同时咳出血凝块弹幕。
    /// 逆着吸力跑或借位闪避，被拽进嘴=原版口部接触重击
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.MawVortex, typeof(WofStateContext))]
    internal class WofMawVortexState : WofStateBase
    {
        public override string StateName => "MawVortex";
        public override WofStateIndex StateIndex => WofStateIndex.MawVortex;

        private int PullDuration(WofStateContext ctx) => ctx.IsAsuraMode ? WofDirector.VortexDuration + 40 : WofDirector.VortexDuration;
        private const int ReleaseFrames = 34;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.7f, Volume = 1f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            int windup = WofDirector.VortexWindup;
            int pullEnd = windup + PullDuration(context);
            int totalEnd = pullEnd + ReleaseFrames;

            if (Timer <= windup) {
                UpdateWindup(context, windup);
            }
            else if (Timer <= pullEnd) {
                UpdatePull(context, windup, pullEnd);
            }
            else {
                UpdateRelease(context, pullEnd);
            }

            if (Timer >= totalEnd) {
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>吸气：口洞展开、周遭血雾被卷入</summary>
        private void UpdateWindup(WofStateContext context, int windup) {
            NPC npc = context.Npc;
            float p = Timer / (float)windup;
            context.AdvanceFactor = 0.55f;
            context.MouthCommand = 1;
            context.SetChargeState(2, p * 0.5f);
            context.WallFlush = 0.5f + 0.3f * p;

            if (!VaultUtils.isServer && Timer % 2 == 0) {
                //血雾自远处卷入口器
                Vector2 from = npc.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(220f, 560f);
                if (WofMotionFX.OnScreen(from)) {
                    PRTLoader.NewParticle<PRT_WofBloodMist>(from, (npc.Center - from) * 0.03f,
                        WofMotionFX.BloodDark, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(30, 0.5f);
                }
            }
            if (Timer == windup - 8 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.6f, Volume = 0.9f }, npc.Center);
            }
        }

        /// <summary>拽引：对本地玩家施加向口加速度，周期咳出血凝块</summary>
        private void UpdatePull(WofStateContext context, int windup, int pullEnd) {
            NPC npc = context.Npc;
            float pullT = (Timer - windup) / (float)(pullEnd - windup);
            //吸力包络：快速到满、末段收油
            float envelope = MathHelper.Clamp(pullT * 5f, 0f, 1f) * MathHelper.Lerp(1f, 0.6f, MathHelper.Clamp((pullT - 0.85f) / 0.15f, 0f, 1f));

            context.AdvanceFactor = 0.55f;
            context.MouthCommand = 1;
            context.SetChargeState(2, 0.5f + 0.5f * envelope);
            context.WallFlush = 0.7f;

            ApplyLocalPull(npc, envelope);

            //咳出血凝块(服务端)：小口径压制弹幕，逼玩家边抗吸边躲
            if (!VaultUtils.isClient && context.Target.Alives()) {
                int clotInterval = context.IsAsuraMode ? 22 : 28;
                if ((Timer - windup) % clotInterval == 0) {
                    int clots = context.Phase >= 3 ? 3 : 2;
                    for (int i = 0; i < clots; i++) {
                        Vector2 aim = (context.Target.Center - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
                        aim = aim.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f));
                        Vector2 vel = aim * Main.rand.NextFloat(8.5f, 12f) - Vector2.UnitY * Main.rand.NextFloat(2f, 5f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 40f, vel,
                            ModContent.ProjectileType<WofBloodClot>(),
                            WallOfFleshAI.ScaleDamage(npc, WofDirector.BloodClotDamage), 0f, Main.myPlayer);
                    }
                    npc.netUpdate = true;
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //向心飞沫
            if (Timer % 2 == 0) {
                Vector2 from = npc.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(160f, 620f);
                if (WofMotionFX.OnScreen(from)) {
                    PRTLoader.NewParticle<PRT_Spark>(from, (npc.Center - from) * 0.055f,
                        WofMotionFX.BloodHot, Main.rand.NextFloat(0.7f, 1.3f))?.Configure(false, 18);
                }
            }
            //吞咽声浪
            if ((Timer - windup) % 24 == 0) {
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.3f + pullT * 0.35f, Volume = 0.75f }, npc.Center);
                WofMotionFX.CameraPunch(npc.Center, 1.6f, 10, "WofVortexGulp");
            }
        }

        /// <summary>吐息释放：猛合口、反向排斥波送走玩家，收招</summary>
        private void UpdateRelease(WofStateContext context, int pullEnd) {
            NPC npc = context.Npc;
            context.AdvanceFactor = 0.7f;
            context.MouthCommand = 2;
            float p = (Timer - pullEnd) / (float)ReleaseFrames;
            context.SetChargeState(2, MathHelper.Lerp(1f, 0f, p));
            context.WallFlush = MathHelper.Lerp(0.7f, 0.35f, p);

            if (Timer == pullEnd + 1) {
                //合口反冲：轻推玩家离口(公平阀，防止贴口秒杀链)
                if (!Main.dedServ && Main.LocalPlayer.Alives()) {
                    Player player = Main.LocalPlayer;
                    float dist = player.Distance(npc.Center);
                    if (dist < 520f) {
                        Vector2 push = (player.Center - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
                        player.velocity += push * MathHelper.Lerp(7.5f, 2f, dist / 520f);
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.5f, Volume = 1.1f }, npc.Center);
                    WofMotionFX.MouthRoar(npc, 0.9f, playSound: false);
                    WofMotionFX.CameraPunch(npc.Center, 5f, 14, "WofVortexSnap");
                }
            }
        }

        /// <summary>对本地玩家施加向口加速度(各端只拽自己，镜像原版舌头的本地判定模型)</summary>
        private static void ApplyLocalPull(NPC npc, float envelope) {
            if (Main.dedServ || !Main.LocalPlayer.Alives() || Main.LocalPlayer.ghost) {
                return;
            }
            Player player = Main.LocalPlayer;
            //被舌头拖拽时不叠加(原版机制优先)
            if (player.tongued) {
                return;
            }
            Vector2 toMouth = npc.Center - player.Center;
            float dist = toMouth.Length();
            if (dist > WofDirector.VortexRange || dist < 30f) {
                return;
            }
            //前方锥形作用域：墙身后不吸
            if (WofWallField.BehindFace(npc, player.Center.X)) {
                return;
            }

            float falloff = (float)Math.Pow(1f - dist / WofDirector.VortexRange, 1.4f);
            float accel = WofDirector.VortexPullMax * falloff * envelope;
            Vector2 pull = toMouth.SafeNormalize(Vector2.Zero) * accel;

            //吸向口器的分速度设限，永远可以反抗
            Vector2 predicted = player.velocity + pull;
            float towardSpeed = Vector2.Dot(predicted, toMouth.SafeNormalize(Vector2.Zero));
            if (towardSpeed < 9.5f) {
                player.velocity = predicted;
            }
        }
    }
}
