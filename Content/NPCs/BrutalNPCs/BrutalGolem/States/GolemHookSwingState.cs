using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>回旋勾拳：双拳镜像弧线在玩家处交汇，再接贴地对向横扫</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.HookSwing, typeof(GolemStateContext))]
    internal class GolemHookSwingState : GolemStateBase
    {
        public override string StateName => "HookSwing";
        public override GolemStateIndex StateIndex => GolemStateIndex.HookSwing;

        internal static int HookTick => 12;    //双弧勾拳
        internal static int HopTick => 58;     //拍间压近跳
        internal static int SweepTick => 106;  //贴地横扫
        internal static int EndTime => 220;

        private bool airborne;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            airborne = false;
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            RestoreTileCollide(context);
            context.VeinGlow = Math.Max(context.VeinGlow, 0.3f);

            //拍间压近跳：勾拳交汇后、横扫之前躯干跃向目标，双线威胁不断档
            if (Timer == Tempo(context, HopTick) && OnGround(npc)) {
                float dx = context.Target.Center.X - npc.Center.X;
                LaunchJump(context, MathHelper.Clamp(dx / 55f, -11f, 11f), -9.5f);
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }
            if (OnGround(npc)) {
                GroundBrake(npc);
                npc.damage = 0;
            }
            else {
                context.FrameMode = 2;
                npc.damage = npc.defDamage;
                AirSteer(context, 0.12f, 10f);
            }
            if (LandedThisFrame(npc, ref airborne)) {
                LandingImpact(context, context.Sundered ? 3 : 2);
            }

            if (!VaultUtils.isClient) {
                //第一拍：双弧勾拳交汇
                if (Timer == Tempo(context, HookTick)) {
                    Vector2 cross = context.Target.Center + context.Target.velocity * 14f;
                    int windup = Tempo(context, 26);
                    float speed = (context.Sundered ? 34f : 30f) + (context.AsuraMode ? 4f : 0f);
                    CommandBoth(context, GolemFistCommand.HookSwing, cross, windup, speed, 1);
                }

                //第二拍：贴地对向横扫（找玩家脚下地面高度，两侧按墙距收窄扫程）
                if (Timer == Tempo(context, SweepTick)) {
                    float groundY = FindGroundY(context.Target);
                    float sweepY = groundY - 34f;
                    float leftReach = LateralReach(context.Target, sweepY, -1);
                    float rightReach = LateralReach(context.Target, sweepY, 1);
                    GolemLimbStatus limbs = context.Limbs;
                    float sweepSpeed = (context.Sundered ? 30f : 26f) + (context.AsuraMode ? 4f : 0f);
                    int windup = Tempo(context, 30);
                    if (limbs.LeftFistAlive) {
                        //左拳从右起扫向左端
                        Vector2 point = new(context.Target.Center.X - leftReach, sweepY);
                        GolemBodyAI.CommandFist(limbs.LeftFistIndex, GolemFistCommand.LowSweep, point,
                            windup, sweepSpeed, 0, context.Target.Center.X + rightReach);
                    }
                    if (limbs.RightFistAlive) {
                        //右拳从左起扫向右端
                        Vector2 point = new(context.Target.Center.X + rightReach, sweepY);
                        GolemBodyAI.CommandFist(limbs.RightFistIndex, GolemFistCommand.LowSweep, point,
                            windup, sweepSpeed, 0, context.Target.Center.X - leftReach);
                    }
                }
            }

            Timer++;
            if ((Timer >= Tempo(context, EndTime) || context.Limbs.FistCount == 0) && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        private static void CommandBoth(GolemStateContext context, GolemFistCommand kind,
            Vector2 point, int windup, float speed, int bounce) {
            GolemLimbStatus limbs = context.Limbs;
            if (limbs.LeftFistAlive) {
                GolemBodyAI.CommandFist(limbs.LeftFistIndex, kind, point, windup, speed, bounce);
            }
            if (limbs.RightFistAlive) {
                GolemBodyAI.CommandFist(limbs.RightFistIndex, kind, point, windup, speed, bounce);
            }
        }

        /// <summary>向下扫描玩家脚下最近的实心地面（世界Y坐标）</summary>
        internal static float FindGroundY(Player target) {
            int tileX = (int)(target.Center.X / 16f);
            int tileY = (int)(target.Center.Y / 16f);
            for (int y = tileY; y < tileY + 50 && y < Main.maxTilesY - 10; y++) {
                if (WorldGen.SolidTile(tileX, y)) {
                    return y * 16f;
                }
            }
            return target.Bottom.Y + 60f;
        }

        /// <summary>横向可扫开阔距离（像素，遇墙收窄，220~900）</summary>
        internal static float LateralReach(Player target, float worldY, int side) {
            int tileY = (int)(worldY / 16f);
            int startX = (int)(target.Center.X / 16f);
            for (int i = 2; i <= 56; i++) {
                int x = startX + side * i;
                if (x < 10 || x > Main.maxTilesX - 10) {
                    return MathHelper.Clamp((i - 1) * 16f, 220f, 900f);
                }
                if (WorldGen.SolidTile(x, tileY)) {
                    return MathHelper.Clamp((i - 2) * 16f, 220f, 900f);
                }
            }
            return 900f;
        }
    }
}
