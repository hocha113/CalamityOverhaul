using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 签名招·饥饿长城：整面墙裂开成一列巨口，按车道接力噬咬(第二轮反向)。
    /// 每轮掷一张死颚永不咬合，耷拉滴涎的哑口就是贴墙安全屋；
    /// 或退到咬程之外，但墙仍在缓推，咬程圈会逐渐吃掉退路。
    /// 阶段3专属，大迁徙喘息后首秀
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.JawRipple, typeof(WofStateContext))]
    internal class WofJawRippleState : WofStateBase
    {
        public override string StateName => "JawRipple";
        public override WofStateIndex StateIndex => WofStateIndex.JawRipple;

        private static int Volley2Start => WofDirector.JawIntroFrames + WofDirector.JawVolleyLife + WofDirector.JawVolleyGap;
        private static int TotalTime => Volley2Start + WofDirector.JawVolleyLife + WofDirector.JawOutroFrames;

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            if (Timer <= WofDirector.JawIntroFrames) {
                //蓄势：墙身紧咬绷紧，血肉自内侧鼓胀
                float p = Timer / (float)WofDirector.JawIntroFrames;
                context.AdvanceFactor = 0.4f;
                context.MouthCommand = 2;
                context.WallFlush = 0.4f + 0.5f * p;
                if (Timer == 2 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Pitch = -0.55f, Volume = 1f }, npc.Center);
                    WofMotionFX.CameraPunch(npc.Center, 3.5f, 12, "WofJawSwell");
                }
                if (!VaultUtils.isServer && Timer % 4 == 0) {
                    WofMotionFX.SpawnWallSeep(npc, 2.5f);
                }
                return null;
            }

            //两轮颚浪：第一轮自上而下，第二轮自下而上(重掷死颚)
            if (Timer == WofDirector.JawIntroFrames + 1) {
                SpawnVolley(context, topDown: true);
            }
            if (Timer == Volley2Start + 1) {
                SpawnVolley(context, topDown: false);
            }

            bool inBreath = Timer > WofDirector.JawIntroFrames + WofDirector.JawVolleyLife
                && Timer <= Volley2Start;
            //咬浪期缓推：咬程圈随墙前移本身就是车道压迫
            context.AdvanceFactor = inBreath ? 0.75f : 0.5f;
            context.MouthCommand = inBreath ? 2 : 1;
            context.WallFlush = inBreath ? 0.45f : 0.7f;

            if (Timer >= TotalTime) {
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>
        /// 生成一轮颚阵(服务端)：五道车道各一张颚，咬合帧按波次错拍写入ai[2]；
        /// 死颚ai[2]=-1，视觉上耷拉滴涎，缺口有身份，能被读出来
        /// </summary>
        private static void SpawnVolley(WofStateContext context, bool topDown) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int deadLane = Main.rand.Next(WofDirector.JawLaneCount);
            int damage = WallOfFleshAI.ScaleDamage(npc, WofDirector.JawDamage);
            int type = ModContent.ProjectileType<WofJawMawProj>();

            for (int lane = 0; lane < WofDirector.JawLaneCount; lane++) {
                int rank = topDown ? lane : WofDirector.JawLaneCount - 1 - lane;
                float snapTick = lane == deadLane
                    ? -1f
                    : WofDirector.JawGrowFrames + WofDirector.JawGapeMin + rank * WofDirector.JawSnapStagger;
                float yFrac = (lane + 0.5f) / WofDirector.JawLaneCount;
                Vector2 pos = AheadPoint(context, 0f, yFrac);
                Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero, type,
                    damage, 0f, Main.myPlayer, npc.whoAmI, lane, snapTick);
            }
            npc.netUpdate = true;
        }
    }
}
