using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼二阶段喷火追击状态
    /// </summary>
    internal class SpazmatismFlameChaseState : TwinsStateBase
    {
        public override string StateName => "SpazmatismFlameChase";
        public override TwinsStateIndex StateIndex => TwinsStateIndex.SpazmatismFlameChase;

        /// <summary>
        /// 二阶段固定招式套路(有搭档时):
        /// 喷火追击→二阶冲刺→影分身冲刺→喷火追击→火焰风暴→合击→(循环)
        /// 
        /// 二阶段固定招式套路(独眼时):
        /// 喷火追击→二阶冲刺→影分身冲刺→喷火追击→火焰风暴→二阶冲刺→(循环)
        /// </summary>
        private static readonly string[] ComboSequenceWithPartner =
        [
            "Phase2Dash",
            "ShadowDash",
            "FlameChase",
            "FlameStorm",
            "CombinedAttack"
        ];

        private static readonly string[] ComboSequenceSolo =
        [
            "Phase2Dash",
            "ShadowDash",
            "FlameChase",
            "FlameStorm",
            "Phase2Dash"
        ];

        private float ChaseSpeed => Context.IsMachineRebellion ? 10f : (Context.IsDeathMode ? 8f : 6f);
        private float TurnSpeed => Context.IsMachineRebellion ? 0.2f : (Context.IsDeathMode ? 0.16f : 0.12f);
        private int FlameDuration => Context.IsMachineRebellion ? 200 : (Context.IsDeathMode ? 120 : 150);
        private int FlameInterval => Context.IsDeathMode ? 6 : 8;

        private TwinsStateContext Context;
        private int comboStep;

        /// <param name="currentComboStep">二阶段固定招式循环的当前步骤索引</param>
        public SpazmatismFlameChaseState(int currentComboStep = 0) {
            comboStep = currentComboStep;
        }

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //检测独眼狂暴模式触发
            if (context.SoloRageJustTriggered) {
                return new SpazmatismSoloRageState();
            }

            //追击玩家
            Vector2 targetDir = GetDirectionToTarget(context);
            npc.velocity = Vector2.Lerp(npc.velocity, targetDir * ChaseSpeed, TurnSpeed);
            FaceVelocity(npc);

            Timer++;

            //喷火
            if (Timer % FlameInterval == 0) {
                if (!VaultUtils.isClient) {
                    Vector2 fireVel = npc.velocity.SafeNormalize(Vector2.UnitY) * 12f;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        fireVel,
                        ProjectileID.EyeFire,
                        32,
                        0f,
                        Main.myPlayer
                    );
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item34, npc.Center);
                }
            }

            //喷火结束，按固定套路切换到下一招式
            if (Timer >= FlameDuration) {
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new SpazmatismSoloRageState();
                }

                return GetNextComboState();
            }

            return null;
        }

        /// <summary>
        /// 根据固定套路获取下一个状态
        /// </summary>
        private ITwinsState GetNextComboState() {
            bool hasPartner = HasPartner();
            string[] sequence = hasPartner ? ComboSequenceWithPartner : ComboSequenceSolo;
            string nextMove = sequence[comboStep % sequence.Length];
            int nextStep = comboStep + 1;

            return nextMove switch {
                "Phase2Dash" => new SpazmatismPhase2DashPrepareState(0, nextStep),
                "ShadowDash" => new SpazmatismShadowDashState(nextStep),
                "FlameChase" => new SpazmatismFlameChaseState(nextStep),
                "FlameStorm" => new SpazmatismFlameStormState(nextStep),
                "CombinedAttack" => new TwinsCombinedAttackState(nextStep),
                _ => new SpazmatismPhase2DashPrepareState(0, nextStep)
            };
        }

        /// <summary>
        /// 检查是否有另一只眼睛存活
        /// </summary>
        private bool HasPartner() {
            foreach (var n in Main.npc) {
                if (n.active && n.type == NPCID.Retinazer) {
                    return true;
                }
            }
            return false;
        }
    }
}
