using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>拳击连段：交替蓄力直拳，前一拳出手后一拳即蓄，撞墙反弹成二次弹道</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.PunchCombo, typeof(GolemStateContext))]
    internal class GolemPunchComboState : GolemStateBase
    {
        public override string StateName => "PunchCombo";
        public override GolemStateIndex StateIndex => GolemStateIndex.PunchCombo;

        private int punchTimer;
        private int nextFistSign;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            punchTimer = 0;
            //首拳用目标同侧的拳，出手路径最短
            nextFistSign = Math.Sign(context.Target.Center.X - context.Npc.Center.X);
            if (nextFistSign == 0) {
                nextFistSign = 1;
            }
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            GroundBrake(npc);
            //站桩状态强制恢复地形碰撞，防止承接跳跃状态时坠穿
            npc.noTileCollide = false;
            //连段期宝石随节奏发亮
            context.VeinGlow = Math.Max(context.VeinGlow, 0.35f);

            int totalPunches = (context.Sundered ? 5 : 4) + (context.DeathMode ? 1 : 0);
            int windup = context.Sundered ? GolemDirector.PunchWindupP2 : GolemDirector.PunchWindupP1;
            if (context.DeathMode) {
                windup -= 4;
            }
            //紧凑衔接：上一拳蓄力完出手时，下一拳已开始蓄
            int interval = Tempo(context, windup + 14);

            if (!VaultUtils.isClient && Counter < totalPunches) {
                if (++punchTimer >= interval) {
                    punchTimer = 0;
                    DispatchPunch(context);
                    Counter++;
                }
            }

            Timer++;
            int endTime = interval * totalPunches + 90;
            if ((Timer >= endTime || context.Limbs.FistCount == 0) && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        /// <summary>轮换拳出击（服务端）</summary>
        private void DispatchPunch(GolemStateContext context) {
            GolemLimbStatus limbs = context.Limbs;
            int fistIndex = nextFistSign < 0 ? limbs.LeftFistIndex : limbs.RightFistIndex;
            //该侧拳缺失则换边
            if (fistIndex < 0) {
                fistIndex = nextFistSign < 0 ? limbs.RightFistIndex : limbs.LeftFistIndex;
            }
            nextFistSign = -nextFistSign;
            if (fistIndex < 0) {
                return;
            }

            float speed = context.Sundered ? GolemDirector.PunchSpeedP2 : GolemDirector.PunchSpeedP1;
            if (context.DeathMode) {
                speed += 5f;
            }
            if (context.Enraged) {
                speed *= 1.2f;
            }
            int windup = context.Sundered ? GolemDirector.PunchWindupP2 : GolemDirector.PunchWindupP1;
            if (context.DeathMode) {
                windup -= 4;
            }
            int bounce = context.Sundered ? 2 : 1;

            //预读提前量：直拳打向玩家动向
            Vector2 point = context.Target.Center + context.Target.velocity * 11f;
            GolemBodyAI.CommandFist(fistIndex, GolemFistCommand.StraightPunch, point, windup, speed, bounce);
        }
    }
}
