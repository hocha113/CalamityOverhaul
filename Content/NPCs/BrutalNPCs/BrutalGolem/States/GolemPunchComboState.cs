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
        private int hopTimer;
        private int nextFistSign;
        private bool airborne;

        public override void OnEnter(GolemStateContext context) {
            base.OnEnter(context);
            punchTimer = 0;
            hopTimer = 0;
            airborne = false;
            //首拳用目标同侧的拳，出手路径最短
            nextFistSign = Math.Sign(context.Target.Center.X - context.Npc.Center.X);
            if (nextFistSign == 0) {
                nextFistSign = 1;
            }
        }

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            RestoreTileCollide(context);
            //连段期宝石随节奏发亮
            context.VeinGlow = Math.Max(context.VeinGlow, 0.35f);

            //拳浪间穿插压近小跳：出拳不站桩，拳与躯干双线压迫
            if (OnGround(npc)) {
                GroundBrake(npc);
                npc.damage = 0;
                if (++hopTimer >= Tempo(context, 66)) {
                    hopTimer = 0;
                    float dx = context.Target.Center.X - npc.Center.X;
                    if (Math.Abs(dx) > 150f) {
                        LaunchJump(context, MathHelper.Clamp(dx / 65f, -10f, 10f), -9f);
                        if (!VaultUtils.isClient) {
                            npc.netUpdate = true;
                        }
                    }
                }
            }
            else {
                context.FrameMode = 2;
                npc.damage = npc.defDamage;
                AirSteer(context, 0.1f, 10f);
            }
            if (LandedThisFrame(npc, ref airborne)) {
                LandingImpact(context, context.Sundered ? 3 : 2);
            }

            int totalPunches = (context.Sundered ? 5 : 4) + (context.AsuraMode ? 1 : 0);
            int windup = context.Sundered ? GolemDirector.PunchWindupP2 : GolemDirector.PunchWindupP1;
            if (context.AsuraMode) {
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
            if (context.AsuraMode) {
                speed += 5f;
            }
            //激怒翻倍统一落在拳端 Launch，此处不再叠乘
            int windup = context.Sundered ? GolemDirector.PunchWindupP2 : GolemDirector.PunchWindupP1;
            if (context.AsuraMode) {
                windup -= 4;
            }
            int bounce = context.Sundered ? 2 : 1;

            //预读提前量：直拳打向玩家动向
            Vector2 point = context.Target.Center + context.Target.velocity * 11f;
            GolemBodyAI.CommandFist(fistIndex, GolemFistCommand.StraightPunch, point, windup, speed, bounce);
        }
    }
}
