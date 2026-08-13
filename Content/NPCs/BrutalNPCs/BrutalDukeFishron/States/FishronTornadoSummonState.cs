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
    /// 鲨鱼龙卷召唤：俯冲贴地一记尾拍，砸出驻场行走的水龙卷。
    /// 场上至多两座；已满则刷新最旧一座的寿命
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.TornadoSummon, typeof(FishronStateContext))]
    internal class FishronTornadoSummonState : FishronStateBase
    {
        public override string StateName => "TornadoSummon";
        public override FishronStateIndex StateIndex => FishronStateIndex.TornadoSummon;

        private const int DiveEnd = 44;
        private const int SlamEnd = 62;
        private const int TotalTime = 118;

        private Vector2 slamPoint;
        private bool pointResolved;
        private bool slammed;

        public FishronTornadoSummonState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            pointResolved = false;
            slammed = false;
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //各端确定性解析拍击点：玩家接近侧 380px 的地表
            if (!pointResolved) {
                pointResolved = true;
                int side = Math.Sign(npc.Center.X - player.Center.X);
                if (side == 0) {
                    side = 1;
                }
                slamPoint = FishronMotionFX.FindSurfaceBelow(
                    player.Center + new Vector2(side * 380f, -60f), out _);
            }

            Timer++;

            //幕一：俯冲扑向拍击点（俯冲轨迹本身就是预告）
            if (Timer <= DiveEnd) {
                Vector2 goal = slamPoint - new Vector2(0, 70f);
                Vector2 desired = (goal - npc.Center).SafeNormalize(Vector2.UnitY)
                    * MathHelper.Lerp(10f, 30f, Timer / (float)DiveEnd);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.16f);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;

                //提前抵达则跳拍
                if (npc.WithinRange(goal, 90f) && Timer < DiveEnd - 4) {
                    Timer = DiveEnd;
                }
                return null;
            }

            //尾拍帧：甩尾+落卷
            if (!slammed) {
                slammed = true;
                npc.velocity = new Vector2(npc.velocity.X * 0.2f, -7f);
                FishronMotionFX.SpawnSplashBurst(slamPoint, 1.5f);
                FishronMotionFX.CameraPunch(slamPoint, 7f, 14, "FishronSlam", Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.Zombie9 with { Volume = 1f, Pitch = -0.2f, MaxInstances = 3 }, slamPoint);

                if (!VaultUtils.isClient) {
                    SpawnOrRefreshTornado(npc);
                }
            }

            //幕二：甩尾腾起
            if (Timer <= SlamEnd) {
                npc.velocity *= 0.96f;
                npc.velocity.Y -= 0.35f;
                FaceBody(npc, player.Center, 0.1f);
                return null;
            }

            //幕三：退开看它立起来
            context.SkipDefaultMovement = false;
            int side2 = Math.Sign(npc.Center.X - slamPoint.X);
            if (side2 == 0) {
                side2 = 1;
            }
            SetMovement(context, slamPoint + new Vector2(side2 * 520f, -420f), 10f, 0.5f);

            if (Timer >= TotalTime) {
                return new FishronHoverState();
            }
            return null;
        }

        /// <summary>至多两座：满员刷新最旧，否则新落一座</summary>
        private void SpawnOrRefreshTornado(NPC npc) {
            int type = ModContent.ProjectileType<FishronSharkTornadoProj>();
            Projectile oldest = null;
            int count = 0;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type != type) {
                    continue;
                }
                count++;
                if (oldest == null || proj.timeLeft < oldest.timeLeft) {
                    oldest = proj;
                }
            }
            if (count >= 2 && oldest != null) {
                oldest.timeLeft = Math.Max(oldest.timeLeft, 900);
                oldest.netUpdate = true;
                return;
            }
            Projectile.NewProjectile(npc.GetSource_FromAI(),
                slamPoint - new Vector2(0, 200f), Vector2.Zero,
                type, FishronSharkTornadoProj.TornadoDamage, 0f, Main.myPlayer);
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
