using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>
    /// 黑暗闪电雨：公爵隐入雨幕成剪影，唯有闪电照亮他。
    /// 四波落雷：包夹三连→行进五连→反向回推→预判三雷，波距 40 帧；预告线恒定 25 帧
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.LightningRain, typeof(FishronStateContext))]
    internal class FishronLightningRainState : FishronStateBase
    {
        public override string StateName => "LightningRain";
        public override FishronStateIndex StateIndex => FishronStateIndex.LightningRain;

        /// <summary>预告→落雷的恒定提前量（危险等级常数，玩家可背；末相令 36→25 帧）</summary>
        internal const int BoltTelegraphTime = 25;
        private const int TotalTime = 186;

        //服务端专用落雷计划（帧, 地面点）
        private readonly List<(int frame, Vector2 ground)> plan = [];
        //波二行进方向，波三反向回推复用（仅服务端排程读写）
        private int marchDir;

        public FishronLightningRainState() {
        }

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            plan.Clear();
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, context.Npc.Center);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //雨幕剪影：维持半隐（主控每帧衰减 12，此处补偿后净升）
            if (npc.alpha < 170) {
                npc.alpha = System.Math.Min(npc.alpha + 26, 170);
            }
            else {
                npc.alpha = 170;
            }
            FishronStormSky.PushRainBoost(0.35f);

            //高空慢漂（缓慢、可读，威胁全在雷上）
            Vector2 goal = player.Center + new Vector2(0, -440f);
            Vector2 desired = (goal - npc.Center).SafeNormalize(Vector2.Zero) * 6f;
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.05f);
            FaceBody(npc, player.Center, 0.05f);

            //服务端：三段落雷排程
            if (!VaultUtils.isClient) {
                SchedulePlan(context);
                ExecutePlan(npc);
            }

            //偶发天光，让剪影闪现（纯本地视觉）
            if (!VaultUtils.isServer && Timer % 32 == 0) {
                FishronStormSky.PushFlash(0.3f, npc.Center);
            }

            if (Timer >= TotalTime) {
                return new FishronHoverState();
            }
            return null;
        }

        /// <summary>Timer 到点时铺预告与落雷（四波，波距 40 帧）</summary>
        private void SchedulePlan(FishronStateContext context) {
            Player player = context.Target;

            //波一（t=14）：包夹三连，中央预判
            if (Timer == 14) {
                Push(14, player.Center.X - 260f, player);
                Push(19, player.Center.X + player.velocity.X * 26f, player);
                Push(24, player.Center.X + 260f, player);
            }
            //波二（t=54）：行进弹幕线，五雷推移
            if (Timer == 54) {
                marchDir = Main.rand.NextBool() ? 1 : -1;
                for (int i = 0; i < 5; i++) {
                    Push(54 + i * 7, player.Center.X + marchDir * (-400f + i * 200f), player);
                }
            }
            //波三（t=94）：反向回推，刚学会的节奏倒着再考一遍
            if (Timer == 94) {
                for (int i = 0; i < 5; i++) {
                    Push(94 + i * 7, player.Center.X - marchDir * (-400f + i * 200f), player);
                }
            }
            //波四（t=134）：预判三雷，越跑越往前堵
            if (Timer == 134) {
                Push(134, player.Center.X + player.velocity.X * 30f, player);
                Push(141, player.Center.X + player.velocity.X * 52f, player);
                Push(148, player.Center.X + player.velocity.X * 74f, player);
            }
        }

        /// <summary>登记一处落点：立即亮预告，25 帧后落雷</summary>
        private void Push(int frame, float x, Player player) {
            Vector2 ground = FishronMotionFX.FindSurfaceBelow(new Vector2(x, player.Center.Y - 40f), out _);
            plan.Add((frame, ground));
        }

        private void ExecutePlan(NPC npc) {
            for (int i = plan.Count - 1; i >= 0; i--) {
                (int frame, Vector2 ground) = plan[i];
                if ((int)Timer == frame) {
                    //自地面向天的垂直预告线
                    Projectile.NewProjectile(npc.GetSource_FromAI(), ground, -Vector2.UnitY,
                        ModContent.ProjectileType<FishronTelegraph>(), 0, 0f, Main.myPlayer,
                        -1, -1, FishronTelegraph.PackParams(1, BoltTelegraphTime));
                }
                else if ((int)Timer == frame + BoltTelegraphTime) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        new Vector2(ground.X, ground.Y - 980f), Vector2.Zero,
                        ModContent.ProjectileType<FishronSkyBoltProj>(),
                        FishronSkyBoltProj.BoltDamage, 0f, Main.myPlayer,
                        0f, 0f, ground.Y);
                    plan.RemoveAt(i);
                }
            }
        }

        public override void OnExit(FishronStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            //出幕即显形
            context.Npc.alpha = 0;
            FishronStormSky.PushFlash(0.4f, context.Npc.Center);
        }
    }
}
