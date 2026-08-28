using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using System;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics
{
    /// <summary>
    /// 六足程序化步态（全仓无先例，自研）：
    /// 3 个髋站（头后部/体节1/体节2）× 近远两排 = 6 腿；
    /// 近排与远排交替迈步（三角步态），触发条件 = 髋-足距超阈值且对侧组全部着地；
    /// 迈步走抛物线抬脚，悬空/游泳切换为划桨摆。纯本地确定性表现
    /// </summary>
    internal class ShrimpLegGait
    {
        internal struct Leg
        {
            /// <summary>髋站索引 0..2（0 最靠头）</summary>
            public int Station;
            /// <summary>0=近排（观察者侧）1=远排</summary>
            public int Row;
            /// <summary>迈步组（三角步态交替）</summary>
            public int Group;
            /// <summary>当前足位（世界）</summary>
            public Vector2 Foot;
            public Vector2 StepFrom;
            public Vector2 StepTo;
            /// <summary>&lt;0 站立中；0..1 迈步进度</summary>
            public float StepT;
            /// <summary>足下有承托</summary>
            public bool Grounded;
            public bool Init;
        }

        public readonly Leg[] Legs = new Leg[6];

        /// <summary>划桨相位（游泳时推进）</summary>
        private float paddlePhase;

        /// <summary>髋站沿所属节前向的偏移（头节很长，髋挂在后半）</summary>
        private static readonly float[] StationAlong = [-58f, 0f, 2f];

        public ShrimpLegGait() {
            for (int i = 0; i < 6; i++) {
                int station = i / 2;
                int row = i % 2;
                Legs[i] = new Leg {
                    Station = station,
                    Row = row,
                    Group = (station + row) % 2,
                    StepT = -1f,
                };
            }
        }

        /// <summary>髋世界位：站点节位 + 沿轴偏移 + 贴地侧下潜；近远排微错开</summary>
        public Vector2 HipWorld(in Leg leg, ShrimpSkeleton skeleton) {
            ShrimpSkeleton.Node node = skeleton.StationNode(leg.Station);
            Vector2 forward = node.Forward;
            Vector2 down = skeleton.LocalDown(node);
            float along = StationAlong[leg.Station] + (leg.Row == 0 ? 5f : -5f);
            return node.Pos + forward * along + down * 14f;
        }

        private bool AnyStepping(int group) {
            for (int i = 0; i < Legs.Length; i++) {
                if (Legs[i].Group == group && Legs[i].StepT >= 0f) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 推进一帧。attached=贴地模式；tangentMove=本帧行进向（含符号）；
        /// groundDir=指向地面的单位向量（-法线）
        /// </summary>
        public void Update(ShrimpSkeleton skeleton, bool attached, Vector2 groundDir,
            Vector2 tangentMove, float speed) {
            bool moving = speed > 0.4f;
            if (!attached) {
                paddlePhase += 0.16f + speed * 0.012f;
            }

            for (int i = 0; i < Legs.Length; i++) {
                ref Leg leg = ref Legs[i];
                Vector2 hip = HipWorld(in leg, skeleton);

                if (attached) {
                    UpdateGrounded(ref leg, skeleton, hip, groundDir, tangentMove, moving);
                }
                else {
                    UpdatePaddle(ref leg, skeleton, hip, i);
                }
            }
        }

        private void UpdateGrounded(ref Leg leg, ShrimpSkeleton skeleton, Vector2 hip,
            Vector2 groundDir, Vector2 tangentMove, bool moving) {
            //期望落足：髋位沿行进向前探后向地投影
            Vector2 lead = moving ? tangentMove * SeaShrimpDirector.StrideLead : Vector2.Zero;
            Vector2 probeFrom = hip + lead - groundDir * 12f;
            bool hit = ShrimpTerrain.RaycastSurface(probeFrom, groundDir,
                SeaShrimpDirector.LegReach * 1.5f, out Vector2 desired);

            if (!leg.Init) {
                leg.Init = true;
                leg.Foot = desired;
                leg.Grounded = hit;
                return;
            }

            if (leg.StepT >= 0f) {
                //迈步中：抛物线抬脚，末端平滑落位
                leg.StepT += 1f / SeaShrimpDirector.StepFrames;
                if (leg.StepT >= 1f) {
                    leg.StepT = -1f;
                    leg.Foot = leg.StepTo;
                    leg.Grounded = true;
                }
                else {
                    float t = leg.StepT;
                    float ease = t * t * (3f - 2f * t);
                    Vector2 lift = -groundDir * (MathF.Sin(t * MathHelper.Pi) * SeaShrimpDirector.StepLift);
                    leg.Foot = Vector2.Lerp(leg.StepFrom, leg.StepTo, ease) + lift;
                }
                return;
            }

            if (!hit) {
                //足下悬空（崖边）：腿向自然下垂位软跟随
                leg.Grounded = false;
                Vector2 dangle = hip + groundDir * (SeaShrimpDirector.LegReach * 0.62f);
                leg.Foot = Vector2.Lerp(leg.Foot, dangle, 0.18f);
                return;
            }

            //髋-足偏离超阈值且对侧组全着地 → 触发迈步；
            //腿被拖到可及边缘时无视组约束强制迈（防拉断）
            float drift = Vector2.Distance(leg.Foot, desired);
            bool wantStep = drift > SeaShrimpDirector.StepThreshold;
            bool mustStep = Vector2.Distance(leg.Foot, hip) > SeaShrimpDirector.LegReach * 1.18f;
            if (wantStep && (mustStep || !AnyStepping(1 - leg.Group))) {
                leg.StepFrom = leg.Foot;
                leg.StepTo = desired;
                leg.StepT = 0f;
            }
        }

        private void UpdatePaddle(ref Leg leg, ShrimpSkeleton skeleton, Vector2 hip, int index) {
            //游泳/悬空：腿收拢成桨，沿体侧正弦划水，相位沿体轴递进
            ShrimpSkeleton.Node node = skeleton.StationNode(leg.Station);
            Vector2 down = skeleton.LocalDown(node);
            Vector2 back = -node.Forward;
            float swing = MathF.Sin(paddlePhase + index * 1.05f) * 0.55f;
            Vector2 paddleDir = Vector2.Normalize(down + back * (0.5f + swing));
            Vector2 target = hip + paddleDir * (SeaShrimpDirector.LegReach * 0.5f);
            if (!leg.Init) {
                leg.Init = true;
                leg.Foot = target;
            }
            leg.Foot = Vector2.Lerp(leg.Foot, target, 0.22f);
            leg.Grounded = false;
            leg.StepT = -1f;
        }

        /// <summary>硬重建（同步纠偏/入场摆位）</summary>
        public void SnapAll(ShrimpSkeleton skeleton, Vector2 groundDir) {
            for (int i = 0; i < Legs.Length; i++) {
                ref Leg leg = ref Legs[i];
                Vector2 hip = HipWorld(in leg, skeleton);
                leg.Foot = hip + groundDir * (SeaShrimpDirector.LegReach * 0.6f);
                leg.StepT = -1f;
                leg.Init = true;
            }
        }
    }
}
