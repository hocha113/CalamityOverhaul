using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Retinazer
{
    /// <summary>
    /// 激光眼二阶段垂直弹幕状态
    /// 在玩家侧面发射激光弹幕
    /// </summary>
    internal class RetinazerVerticalBarrageState : TwinsStateBase
    {
        public override string StateName => "RetinazerVerticalBarrage";

        /// <summary>
        /// 二阶段固定招式套路(有搭档时):
        /// 垂直弹幕→精准狙击→水平弹幕→聚焦光束→激光矩阵→合击→(循环)
        /// 
        /// 设计与魔焰眼配合:
        /// 激光眼:垂直弹幕(远程压制) ←→ 魔焰眼:喷火追击(近战突进)
        /// 激光眼:精准狙击(爆发输出) ←→ 魔焰眼:二阶冲刺(高速突袭)
        /// 激光眼:水平弹幕(封锁空间) ←→ 魔焰眼:影分身冲刺(多方向压制)
        /// 激光眼:聚焦光束(定点打击) ←→ 魔焰眼:喷火追击(持续追击)
        /// 激光眼:激光矩阵(区域封锁) ←→ 魔焰眼:火焰风暴(区域控制)
        /// 激光眼:合击(联合爆发)     ←→ 魔焰眼:合击(联合爆发)
        /// 
        /// 二阶段固定招式套路(独眼时):
        /// 垂直弹幕→精准狙击→水平弹幕→聚焦光束→激光矩阵→精准狙击→(循环)
        /// </summary>
        private static readonly string[] ComboSequenceWithPartner =
        [
            "PrecisionSniper",
            "HorizontalBarrage",
            "FocusedBeam",
            "LaserMatrix",
            "CombinedAttack"
        ];

        private static readonly string[] ComboSequenceSolo =
        [
            "PrecisionSniper",
            "HorizontalBarrage",
            "FocusedBeam",
            "LaserMatrix",
            "PrecisionSniper"
        ];

        private int Duration => Context.IsMachineRebellion ? 180 : (Context.IsDeathMode ? 120 : 150);
        private int RapidFireRate => Context.IsMachineRebellion ? 10 : (Context.IsDeathMode ? 12 : 15);
        private float LaserSpeed => Context.IsDeathMode ? 18f : 16f;

        private TwinsStateContext Context;
        private int comboStep;

        /// <param name="currentComboStep">二阶段固定招式循环的当前步骤索引</param>
        public RetinazerVerticalBarrageState(int currentComboStep = 0) {
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
                return new RetinazerSoloRageState();
            }

            //计算目标位置，在玩家侧面
            Vector2 targetPos = player.Center + new Vector2(npc.Center.X < player.Center.X ? -400 : 400, 0);

            //保持Y轴对齐
            float yDiff = player.Center.Y - npc.Center.Y;
            npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, yDiff * 0.1f, 0.1f);
            npc.velocity.X = MathHelper.Lerp(npc.velocity.X, (targetPos.X - npc.Center.X) * 0.05f, 0.1f);

            FaceTarget(npc, player.Center);

            Timer++;

            //发射激光
            if (Timer % RapidFireRate == 0) {
                if (!VaultUtils.isClient) {
                    Vector2 shootVel = GetDirectionToTarget(context) * LaserSpeed;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVel,
                        ProjectileID.DeathLaser,
                        30,
                        0f,
                        Main.myPlayer
                    );
                }
                SoundEngine.PlaySound(SoundID.Item12, npc.Center);
            }

            //按固定套路切换到下一招式
            if (Timer >= Duration) {
                //独眼模式下切换到狂暴状态
                if (context.IsSoloRageMode) {
                    return new RetinazerSoloRageState();
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
                "PrecisionSniper" => new RetinazerPrecisionSniperState(0, nextStep),
                "HorizontalBarrage" => new RetinazerHorizontalBarrageState(nextStep),
                "FocusedBeam" => new RetinazerFocusedBeamState(nextStep),
                "LaserMatrix" => new RetinazerLaserMatrixState(nextStep),
                "CombinedAttack" => new TwinsCombinedAttackState(nextStep),
                _ => new RetinazerPrecisionSniperState(0, nextStep)
            };
        }

        /// <summary>
        /// 检查是否有另一只眼睛存活
        /// </summary>
        private bool HasPartner() {
            foreach (var n in Main.npc) {
                if (n.active && n.type == NPCID.Spazmatism) {
                    return true;
                }
            }
            return false;
        }
    }
}
