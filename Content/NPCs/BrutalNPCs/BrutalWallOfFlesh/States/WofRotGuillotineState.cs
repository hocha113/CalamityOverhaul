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
    /// 签名招·腐眼断头闸：墙面在玩家所在高度长出一只腐眼，
    /// 成形期跟踪高度→锁定闪烁(预告即承诺，此后不再追)→静默拍→
    /// 水平斩束贴着跑道轰过去，整条车道在那一拍被封锁，唯一答案是离地。
    /// 阶段2入袋；阶段3连发第二只(重新取高度，二段跳节拍)。
    /// 网络形状：锁定高度写npc.ai[3](服务端权威随NPC同步)，光束由服务端生成
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.RotGuillotine, typeof(WofStateContext))]
    internal class WofRotGuillotineState : WofStateBase
    {
        public override string StateName => "RotGuillotine";
        public override WofStateIndex StateIndex => WofStateIndex.RotGuillotine;

        private const int BudSpawnTick = 6;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = 0f;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = -0.7f, Volume = 0.8f }, context.Npc.Center);
            }
        }

        public override void OnExit(WofStateContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = 0f;
                context.Npc.netUpdate = true;
            }
        }

        /// <summary>单只腐眼的完整节拍长(成形帧由参数给)</summary>
        private static int BudCycle(int growFrames) {
            return growFrames + WofDirector.GuillotineLockFlash + WofDirector.GuillotineSilence
                + WofDirector.GuillotineSustain + WofDirector.GuillotineDecay;
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            int cycle1End = BudSpawnTick + BudCycle(WofDirector.GuillotineGrow);
            bool second = context.Phase >= 3;
            int bud2Spawn = cycle1End + WofDirector.GuillotineInterval;
            int cycle2End = bud2Spawn + BudCycle(WofDirector.GuillotineGrow2);
            int total = (second ? cycle2End : cycle1End) + WofDirector.GuillotineRecover;

            //击发段墙身滞重，其余缓推
            context.MouthCommand = 2;
            context.AdvanceFactor = 0.5f;
            context.WallFlush = 0.5f;

            RunBudCycle(context, (int)Timer - BudSpawnTick, WofDirector.GuillotineGrow, 0);
            if (second) {
                //第二只前清锁，重新取当前高度
                if (Timer == bud2Spawn - 2 && !VaultUtils.isClient) {
                    npc.ai[3] = 0f;
                    npc.netUpdate = true;
                }
                RunBudCycle(context, (int)Timer - bud2Spawn, WofDirector.GuillotineGrow2, 1);
            }

            if (Timer >= total) {
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>推进一只腐眼的节拍：生成→锁定→击发(服务端权威动作，各端演出走弹体)</summary>
        private static void RunBudCycle(WofStateContext context, int t, int growFrames, int budIndex) {
            if (t < 0) {
                return;
            }
            NPC npc = context.Npc;
            int lockTick = growFrames;
            int fireTick = growFrames + WofDirector.GuillotineLockFlash + WofDirector.GuillotineSilence;

            if (t == 0 && !VaultUtils.isClient) {
                //腐眼芽：纯预告实体，自带瞄准线与锁定演出
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    AheadPoint(context, 0f, 0.5f), Vector2.Zero,
                    ModContent.ProjectileType<WofRotEyeBudProj>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, budIndex, growFrames);
                npc.netUpdate = true;
            }

            if (t == lockTick && !VaultUtils.isClient) {
                //锁定承诺：取目标当前中心高度，夹在墙域内侧(不会贴出上下缘)
                float y = context.Target.Alives() ? context.Target.Center.Y : WofWallField.MiddleY;
                float margin = WofDirector.GuillotineHalfHeight + 24f;
                npc.ai[3] = MathHelper.Clamp(y, WofWallField.Top + margin, WofWallField.Bottom - margin);
                npc.netUpdate = true;
            }

            if (t == fireTick && !VaultUtils.isClient && npc.ai[3] != 0f) {
                //斩束：服务端在锁定高度生成，水平贴线，此后永不再瞄
                float faceX = WofWallField.WallFaceX(npc);
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    new Vector2(faceX - npc.direction * 6f, npc.ai[3]), Vector2.Zero,
                    ModContent.ProjectileType<WofRotBeamProj>(),
                    WallOfFleshAI.ScaleDamage(npc, WofDirector.GuillotineDamage), 0f, Main.myPlayer,
                    npc.whoAmI, npc.ai[3]);
                npc.netUpdate = true;
            }

            //击发拍的滞重与震屏(演出本地)
            if (t == fireTick && !VaultUtils.isServer) {
                WofMotionFX.CameraPunch(npc.Center, 6f, 14, "WofGuillotineFire", new Vector2(npc.direction, 0f));
            }
            if (t >= fireTick && t < fireTick + WofDirector.GuillotineSustain) {
                context.AdvanceFactor = 0.32f;
                context.WallFlush = 0.8f;
            }
        }
    }
}
