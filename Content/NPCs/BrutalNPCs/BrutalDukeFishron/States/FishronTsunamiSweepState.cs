using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 潮汐平扫：撤到场边贴地蓄势，横贯冲刺拖起海啸浪墙。
    /// 浪比他慢半拍，先躲公爵，再躲浪。三阶段回程再扫一趟高浪
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.TsunamiSweep, typeof(FishronStateContext))]
    internal class FishronTsunamiSweepState : FishronStateBase
    {
        public override string StateName => "TsunamiSweep";
        public override FishronStateIndex StateIndex => FishronStateIndex.TsunamiSweep;
        public override bool AllowFarSnap => false;

        private const int RepositionEnd = 46;
        private const int TelegraphTime = 46;
        private const float SweepSpeed = 30f;
        /// <summary>浪墙出膛速度：入场即迅猛，弹幕自身还会一路增速到上限</summary>
        private const float WaveSpeed = 18f;

        private int sweepDir;
        private int passIndex;
        private Vector2 sweepAnchor;
        private bool launched;
        private int phaseStart;

        public FishronTsunamiSweepState() {
        }

        private static int MaxPasses(FishronStateContext ctx) => ctx.Phase >= 3 ? 2 : 1;

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            sweepDir = 0;
            passIndex = 0;
            launched = false;
            phaseStart = 0;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //解析本趟起扫侧：从玩家远侧起手（确定性，各端一致）
            if (sweepDir == 0) {
                sweepDir = Math.Sign(player.Center.X - npc.Center.X);
                if (sweepDir == 0) {
                    sweepDir = 1;
                }
                ResolveAnchor(player);
            }

            Timer++;
            int t = (int)Timer - phaseStart;

            //幕一：快速撤位到场边贴地
            if (t <= RepositionEnd) {
                Vector2 desired = (sweepAnchor - npc.Center).SafeNormalize(Vector2.UnitY)
                    * MathHelper.Lerp(12f, 34f, t / (float)RepositionEnd);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.18f);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;
                if (npc.WithinRange(sweepAnchor, 110f) && t < RepositionEnd - 4) {
                    Timer = phaseStart + RepositionEnd;
                }
                return null;
            }

            //幕二：贴地蓄势+横贯预告线
            if (t <= RepositionEnd + TelegraphTime) {
                int tt = t - RepositionEnd;
                float progress = tt / (float)TelegraphTime;

                npc.velocity *= 0.86f;
                FaceBody(npc, npc.Center + new Vector2(sweepDir * 200f, 0f), 0.18f);
                context.SetChargeState(1, progress);
                context.DashDirection = new Vector2(sweepDir, 0f);

                if (tt == 1 && !VaultUtils.isClient) {
                    //横贯预告线（定线模式）
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        new Vector2(sweepDir, 0f), ModContent.ProjectileType<FishronTelegraph>(),
                        0, 0f, Main.myPlayer, npc.whoAmI, -1, FishronTelegraph.PackParams(2, TelegraphTime));
                }
                if (tt > TelegraphTime - 12) {
                    context.FrameCommand = 1;
                }
                //脚下的海先起沫
                if (!VaultUtils.isServer && tt % 4 == 0) {
                    Vector2 foot = FishronMotionFX.FindSurfaceBelow(npc.Center, out _);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                        foot + new Vector2(Main.rand.NextFloat(-70f, 70f), -6f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.6f),
                        FishronMotionFX.FoamWhite * (0.3f + progress * 0.4f),
                        Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(20, 34), 0.02f);
                }
                return null;
            }

            //起扫帧：一帧写满速度并拖出浪墙
            if (!launched) {
                launched = true;
                npc.velocity = new Vector2(sweepDir * SweepSpeed, 0f);
                npc.netUpdate = true;
                FishronMotionFX.SpawnDashBurst(npc.Center, new Vector2(sweepDir, 0f), 1.2f);
                SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 1f, Pitch = 0f, MaxInstances = 3 }, npc.Center);

                if (!VaultUtils.isClient) {
                    bool tall = passIndex > 0;
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        npc.Center - new Vector2(sweepDir * 160f, 0f),
                        new Vector2(sweepDir * WaveSpeed, 0f),
                        ModContent.ProjectileType<FishronTsunamiWallProj>(),
                        FishronTsunamiWallProj.WaveDamage, 0f, Main.myPlayer,
                        sweepDir, tall ? 1f : 0f);
                }
            }

            //幕三：贴地横扫
            AimBodyAlongVelocity(npc);
            context.FrameCommand = 2;
            //高度锁在地表上方
            Vector2 ground = FishronMotionFX.FindSurfaceBelow(npc.Center - new Vector2(0, 100f), out _);
            float targetY = ground.Y - 150f - (passIndex > 0 ? 130f : 0f);
            npc.position.Y += MathHelper.Clamp(targetY - npc.Center.Y, -10f, 10f);
            npc.velocity.Y = 0f;

            //高速甩水
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                FishronMotionFX.SpawnSprayCone(npc.Center + Main.rand.NextVector2Circular(30f, 24f),
                    new Vector2(-sweepDir, -0.3f).SafeNormalize(Vector2.UnitX), 1, 3f, 8f, 0.5f, 0.9f);
            }

            //越过玩家足够远则收束本趟
            bool passed = Math.Sign(npc.Center.X - player.Center.X) == sweepDir
                && Math.Abs(npc.Center.X - player.Center.X) > 780f;
            if (passed || t > RepositionEnd + TelegraphTime + 90) {
                passIndex++;
                if (passIndex >= MaxPasses(context)) {
                    return new FishronHoverState();
                }
                //回程高扫：反向重跑三幕
                sweepDir = -sweepDir;
                launched = false;
                phaseStart = (int)Timer;
                ResolveAnchor(player);
                npc.netUpdate = true;
            }

            return null;
        }

        /// <summary>本趟起点：扫向 sweepDir，则起点在玩家 -sweepDir 侧场边贴地</summary>
        private void ResolveAnchor(Player player) {
            Vector2 side = player.Center + new Vector2(-sweepDir * 900f, 0f);
            Vector2 ground = FishronMotionFX.FindSurfaceBelow(side - new Vector2(0, 120f), out _);
            sweepAnchor = ground - new Vector2(0, 170f + (passIndex > 0 ? 130f : 0f));
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
