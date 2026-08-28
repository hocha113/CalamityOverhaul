using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 爬行索敌 hub（选招枢纽）：贴地爬向目标、目标高悬且身处水中时起泳，
    /// connector 喘息后按手写轮换表出招（压近→压制→爆发→走位交替）。
    /// 轮换表各端一致，只有权威端的转场被采纳
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.Hub, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpHubState : SeaShrimpStateBase
    {
        public override string StateName => "Hub";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.Hub;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            Player target = ctx.Target;
            ShrimpLocomotion loco = ctx.Owner.Locomotion;
            Timer++;
            int t = (int)Timer;

            //弹道余势（被打断的尾弹等）：保持后卷渐展直到刹停
            if (loco.Mode == ShrimpMoveMode.Ballistic) {
                float unroll = MathHelper.Clamp(npc.velocity.Length() / SeaShrimpDirector.TailFlipSpeed, 0f, 1f);
                ctx.SpineCurl = -0.55f * unroll;
                ctx.TailFlare = 1f;
                ctx.WaveGain = 0.2f;
                ctx.AfterimageStrength = unroll;
                return null;
            }

            Vector2 toTarget = target.Center - npc.Center;
            float dist = toTarget.Length();

            //主场在海底：爬行目标 = 目标脚下的海床（高空目标交给远程招覆盖）；
            //深水失附时游泳只作"回床手段"，锚点是海床而不是玩家（贴玩家悬停会挂在水面缠团）
            Vector2 seabed = new(target.Center.X,
                FindGroundY(new Vector2(target.Center.X, target.Center.Y - 120f)) - SeaShrimpDirector.RideHeight);
            bool deepWaterAdrift = !loco.Attached && loco.Wet
                && npc.Center.Y < seabed.Y - SeaShrimpDirector.RideHeight * 3f;
            if (deepWaterAdrift) {
                loco.RequestSwim(seabed, 1.1f);
                ctx.WaveGain = 1.7f;
                ctx.TailFlare = 0.6f;
            }
            else {
                float speedScale = dist > SeaShrimpDirector.LeashDistance ? 1.5f : 1f;
                loco.RequestCrawlTo(seabed, speedScale);
            }

            //出招裁决：connector 喘息走完 + 冷却归零 + 目标在交战圈内
            if (t > SeaShrimpDirector.ConnectorFrames && ctx.AttackCooldown <= 0
                && !ctx.Owner.TargetInvalid() && dist < SeaShrimpDirector.EngageDistance) {
                ctx.AttackIndex++;
                return PickAttack(ctx, dist);
            }
            return null;
        }

        /// <summary>
        /// 手写轮换表（压力/走位/压制交替），距离自适应：
        /// 螯刺/空泡拳要够近，够不着时换成远程压制或穿场突袭。
        /// P2 插入晶刺阵/泡幕，P3 押上超空泡终拳（好招押到低血量）
        /// </summary>
        private static ISeaShrimpState PickAttack(SeaShrimpStateContext ctx, float dist) {
            ctx.QueuedChainState = -1;

            if (ctx.Phase >= 3) {
                switch (ctx.AttackIndex % 6) {
                    case 1:
                        return dist < 480f ? new SeaShrimpClawJabState() : new SeaShrimpWaterVolleyState();
                    case 2:
                        return new SeaShrimpCrystalSpikesState();
                    case 3:
                        return new SeaShrimpSuperCavitationState();
                    case 4:
                        return new SeaShrimpBubbleCurtainState();
                    case 5:
                        return new SeaShrimpTailFlipStrikeState();
                    default:
                        return dist < 560f ? new SeaShrimpCavitationPunchState() : new SeaShrimpTailFlipStrikeState();
                }
            }

            if (ctx.Phase >= 2) {
                switch (ctx.AttackIndex % 6) {
                    case 1:
                        return dist < 480f ? new SeaShrimpClawJabState() : new SeaShrimpWaterVolleyState();
                    case 2:
                        return new SeaShrimpCrystalSpikesState();
                    case 3:
                        return new SeaShrimpWaterVolleyState();
                    case 4:
                        return new SeaShrimpTailFlipStrikeState();
                    case 5:
                        return new SeaShrimpBubbleCurtainState();
                    default:
                        return dist < 560f ? new SeaShrimpCavitationPunchState() : new SeaShrimpTailFlipStrikeState();
                }
            }

            switch (ctx.AttackIndex % 4) {
                case 1:
                    return dist < 480f ? new SeaShrimpClawJabState() : new SeaShrimpWaterVolleyState();
                case 2:
                    return new SeaShrimpWaterVolleyState();
                case 3:
                    return dist < 560f ? new SeaShrimpCavitationPunchState() : new SeaShrimpTailFlipStrikeState();
                default:
                    return new SeaShrimpTailFlipStrikeState();
            }
        }
    }
}
