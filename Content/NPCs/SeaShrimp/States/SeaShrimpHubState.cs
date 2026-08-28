using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>
    /// 凝视逼近 hub（选招枢纽）：头恒对玩家、环距弹簧进退（NightmareReaper 式分镜），
    /// 双手在骨架层交替抓着屏幕平面拖动身体；connector 喘息后按手写轮换表出招
    /// （压近→压制→爆发→走位交替）。轮换表各端一致，只有权威端的转场被采纳
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

            //凝视逼近：头恒对玩家，环距弹簧进退；双手在骨架层交替抓着屏幕平面拖动身体
            float speedScale = dist > SeaShrimpDirector.LeashDistance ? 1.6f : 1f;
            loco.RequestCrawlTo(target.Center, speedScale);
            ctx.WaveGain = MathHelper.Clamp(npc.velocity.Length() / 7f, 0.4f, 1.4f);

            //connector 喘息：收招回枢纽的整备身语——尾扇甩水的一次呼吸，段落间的可见标点
            if (t <= SeaShrimpDirector.ConnectorFrames) {
                float ct = t / (float)SeaShrimpDirector.ConnectorFrames;
                float pulse = MathF.Sin(ct * MathF.PI);
                ctx.TailFlare = 0.35f + 0.45f * pulse;
                ctx.SpineCurl = -0.12f * pulse;
                if (t == 6 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = 0.45f, MaxInstances = 2 }, npc.Center);
                    Vector2 tailPos = ctx.Owner.Skeleton.Nodes[4].Pos;
                    for (int i = 0; i < 3; i++) {
                        EverdeepVFX.ShedDroplet(tailPos + Main.rand.NextVector2Circular(10f, 8f),
                            ctx.Owner.Skeleton.Nodes[4].Forward * 1.5f + new Vector2(0f, -1.6f), 0.8f);
                    }
                }
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
