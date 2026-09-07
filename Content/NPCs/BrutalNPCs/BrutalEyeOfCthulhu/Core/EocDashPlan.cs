using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core
{
    /// <summary>
    /// 变轨冲刺的承诺路径：起跑段擦向目标正侧方的拐点→拐点处猛拐→贯穿段直穿锁定时的目标位置（修罗再一段回钩）。<br/>
    /// 锁定时一次建好，车道画的与实际飞的是同一条折线；起跑后不再按目标实时位置重瞄，
    /// 谎言残影仍沿起跑段直线续飞，所以"看着要擦过去、忽然拐进来"的骗局保留，只是拐向哪里在预警里如实画出
    /// </summary>
    internal struct EocDashPlan
    {
        public bool Valid;
        /// <summary>拐点在目标哪一侧，±1</summary>
        public float Side;
        public Vector2 Dir1;
        public float Len1;
        /// <summary>起跑后第几帧变轨</summary>
        public int KinkFrame;
        public Vector2 Dir2;
        public float Len2;
        /// <summary>第二次变轨帧，&lt;0 表示没有</summary>
        public int Kink2Frame;
        public Vector2 Dir3;
        public float Len3;

        /// <summary>
        /// 拐点取目标正侧方 kinkLateral 处；贯穿段方向即拐点→目标，长度按剩余飞行帧如实给足
        /// </summary>
        /// <param name="flightFrames">起跑到刹车前的满速帧数</param>
        /// <param name="kinkSpeedMul">变轨后速度倍率</param>
        /// <param name="secondKink">修罗：贯穿过目标后再回钩一次</param>
        public static EocDashPlan Build(Vector2 eye, Vector2 target, float side, float speed, float kinkSpeedMul,
            float kinkLateral, int flightFrames, bool secondKink, float secondKinkAngle) {
            side = side < 0f ? -1f : 1f;
            Vector2 axis = (target - eye).SafeNormalize(Vector2.UnitY);
            Vector2 perp = axis.RotatedBy(MathHelper.PiOver2) * side;
            Vector2 kinkPoint = target + perp * kinkLateral;

            EocDashPlan plan = new() {
                Valid = true,
                Side = side,
                Dir1 = (kinkPoint - eye).SafeNormalize(axis),
            };
            //拐点至少留 6 帧给贯穿段，否则拐弯就成了贴脸修正
            float idealLen1 = Vector2.Distance(eye, kinkPoint);
            plan.KinkFrame = Math.Clamp((int)MathF.Round(idealLen1 / Math.Max(speed, 1f)), 4, Math.Max(flightFrames - 6, 4));
            //实际拐点 = 整帧飞行能到的位置（含帧数取整与飞行预算截断），贯穿段从这里直穿目标，
            //保证画出来的折线与飞出来的折线是同一条，而不是理想拐点的那条
            plan.Len1 = plan.KinkFrame * speed;
            Vector2 actualKink = eye + plan.Dir1 * plan.Len1;
            plan.Dir2 = (target - actualKink).SafeNormalize(-perp);

            float speed2 = speed * kinkSpeedMul;
            int framesAfterKink = Math.Max(flightFrames - plan.KinkFrame, 1);
            if (secondKink && framesAfterKink >= 7) {
                //贯穿段刚穿过目标就回钩
                float throughDist = Vector2.Distance(actualKink, target);
                int seg2Frames = Math.Clamp((int)MathF.Round(throughDist / Math.Max(speed2, 1f)), 3, framesAfterKink - 3);
                plan.Len2 = seg2Frames * speed2;
                plan.Kink2Frame = plan.KinkFrame + seg2Frames;
                plan.Dir3 = plan.Dir2.RotatedBy(-side * secondKinkAngle);
                plan.Len3 = (flightFrames - plan.Kink2Frame) * speed2 * 1.07f;
            }
            else {
                plan.Len2 = framesAfterKink * speed2;
                plan.Kink2Frame = -1;
            }
            return plan;
        }

        /// <summary>
        /// 把承诺路径写进车道：主段画到拐点再淡出一截（那是谎言残影要飞的去向），后段按实际飞行长度画。
        /// 车道起点跟着眼体当前位置走，蓄力后撤的几十像素位移不会让画的线和飞的线错开
        /// </summary>
        public void WriteLane(EocStateContext ctx, Vector2 eyeNow, float progress, bool locked) {
            //shader 的远端淡出从 72% 处开始，主段长度放到 1.28 倍，拐点恰好落在亮区末端
            const float TailStretch = 1.28f;
            ctx.LaneStart = eyeNow;
            ctx.LaneDir = Dir1;
            ctx.LaneLength = Len1 * TailStretch;
            ctx.LaneProgress = progress;
            ctx.LaneLocked = locked;
            ctx.LaneIntensity = locked ? 1f : 0.4f + 0.6f * progress;

            ctx.ClearLaneExtra();
            Vector2 kinkPoint = eyeNow + Dir1 * Len1;
            if (Kink2Frame >= 0) {
                ctx.AddLaneSegment(kinkPoint, Dir2, Len2 * TailStretch);
                ctx.AddLaneSegment(kinkPoint + Dir2 * Len2, Dir3, Len3);
            }
            else {
                ctx.AddLaneSegment(kinkPoint, Dir2, Len2);
            }
        }

        /// <summary>把变轨帧与侧向符号打包进 ai[3] 同步：|值|=变轨帧，符号=拐点侧</summary>
        public float Pack() => KinkFrame * Side;

        /// <summary>从 ai[3] 读回同步的变轨帧；不在合理区间（残留的其他状态数据）则回退本地几何值</summary>
        public int ResolveKinkFrame(float packed, int flightFrames) {
            int synced = (int)MathF.Abs(packed);
            return synced >= 4 && synced <= flightFrames ? synced : KinkFrame;
        }

        /// <summary>从 ai[3] 符号读拐点侧；0 视作 +1</summary>
        public static float SideFromPacked(float packed) => packed < 0f ? -1f : 1f;
    }
}
