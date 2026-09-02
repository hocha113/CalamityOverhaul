using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>爪姿包：尖端目标 + 卷曲 + 钳口 + 跟手速度</summary>
    internal readonly struct BssClawPose
    {
        /// <summary>爪尖目标（世界坐标）</summary>
        public readonly Vector2 Tip;
        /// <summary>卷曲 −1 外张 ~ +1 内收（弯向由骨架按侧位镜像）</summary>
        public readonly float Curl;
        /// <summary>钳口开合 0 闭 ~ 1 全张</summary>
        public readonly float BladeOpen;
        /// <summary>跟手速度 0 慢摆 ~ 1 瞬发</summary>
        public readonly float Snap;

        public BssClawPose(Vector2 tip, float curl, float bladeOpen, float snap) {
            Tip = tip;
            Curl = curl;
            BladeOpen = bladeOpen;
            Snap = snap;
        }
    }

    /// <summary>
    /// 鳌足确定性编舞函数库：输入只有头位姿 + 相位等已同步/同算量，输出爪姿。
    /// 状态（权威端取弹幕出生点）与骨架（各端绘制）消费同一函数，尖端天然咬合，
    /// 不需要任何网络包。此文件禁止使用 Main.rand / 全局时钟（装饰性摆动归骨架层）。
    /// </summary>
    internal static class BssClawScript
    {
        /// <summary>全肢触及（= <see cref="BssClawRig"/> 三件套节长之和）</summary>
        internal const float Reach = 166f;

        /// <summary>头前向（贴图前方朝下约定的反解）</summary>
        internal static Vector2 Forward(float headRotation)
            => (headRotation - BssHead.FacingRot).ToRotationVector2();

        /// <summary>翻缘侧向（side = ±1）</summary>
        internal static Vector2 Lateral(float headRotation, int side)
            => Forward(headRotation).RotatedBy(side * MathHelper.PiOver2);

        /// <summary>爪基锚点：头心两侧近边缘（头中段半宽 53px，肩球从头侧探出）</summary>
        internal static Vector2 Mount(Vector2 headCenter, float headRotation, int side)
            => headCenter + Forward(headRotation) * 6f + Lateral(headRotation, side) * 40f;

        /// <summary>嘴位（喷沙炮口 / 撕咬判距）：头底尖端，落在两瓣颚刃之间</summary>
        internal static Vector2 MouthPos(Vector2 headCenter, float headRotation)
            => headCenter + Forward(headRotation) * 66f;

        /// <summary>水平朝向符号（世界系编舞用：挥掷/祭舞以它定"向前"）</summary>
        private static float FacingSign(float headRotation) {
            float x = Forward(headRotation).X;
            return x >= 0f ? 1f : -1f;
        }

        /// <summary>常态待机：胸前折叠螳臂（呼吸摆由骨架层叠加）</summary>
        internal static BssClawPose Idle(Vector2 center, float rotation, int side) {
            Vector2 tip = center + Forward(rotation) * 48f + Lateral(rotation, side) * 24f;
            return new BssClawPose(tip, 0.8f, 0.25f, 0.12f);
        }

        /// <summary>
        /// 护嘴：close01 = 合拢度（吸气进度），burst = 猛推包络。
        /// 合拢点在嘴前交叠护住，推开点沿前向 + 各自翻缘猛摊。
        /// </summary>
        internal static BssClawPose Guard(Vector2 center, float rotation, int side, float close01, float burst) {
            Vector2 mouth = MouthPos(center, rotation);
            Vector2 fwd = Forward(rotation);
            Vector2 lat = Lateral(rotation, side);
            Vector2 closed = mouth + fwd * 18f + lat * 8f;
            Vector2 open = mouth + fwd * 48f + lat * 42f;
            float b = 1f - MathF.Pow(1f - MathHelper.Clamp(burst, 0f, 1f), 3f);
            Vector2 tip = Vector2.Lerp(closed, open, b);
            //未合拢时从待机位渐入
            if (close01 < 1f) {
                Vector2 idleTip = Idle(center, rotation, side).Tip;
                tip = Vector2.Lerp(idleTip, tip, MathHelper.Clamp(close01, 0f, 1f));
            }
            return new BssClawPose(tip,
                MathHelper.Lerp(0.55f, -0.25f, b),
                MathHelper.Lerp(0.2f, 0.9f, b),
                0.25f + 0.55f * b);
        }

        /// <summary>撕咬挣抱：双爪从两侧包夹伸向目标，snap01 收拢钳口</summary>
        internal static BssClawPose Snatch(Vector2 center, float rotation, int side, Vector2 aim, float snap01) {
            Vector2 mouth = MouthPos(center, rotation);
            Vector2 to = aim - mouth;
            float dist = to.Length();
            Vector2 dir = dist > 0.01f ? to / dist : Forward(rotation);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2) * side;
            float s = MathHelper.Clamp(snap01, 0f, 1f);
            Vector2 tip = mouth + dir * Math.Min(dist, Reach * 0.92f)
                + perp * MathHelper.Lerp(28f, 6f, s);
            return new BssClawPose(tip,
                MathHelper.Lerp(-0.15f, 0.45f, s),
                MathHelper.Lerp(1f, 0.08f, s),
                0.6f);
        }

        /// <summary>
        /// 过顶挥掷尖端（世界系竖直面弧：后上蓄势 → 前下甩出）。
        /// swing01 由状态整形（蓄势慢段 + 甩出快段），本函数只做线性弧映射——
        /// 权威端在甩出帧用同一函数取弹幕出生点。
        /// </summary>
        internal static Vector2 FlickTip(Vector2 center, float rotation, int side, float swing01) {
            float hf = FacingSign(rotation);
            Vector2 mount = Mount(center, rotation, side);
            //仰角：2.3（后上方）→ −0.4（前下方甩出）
            float a = MathHelper.Lerp(2.3f, -0.4f, MathHelper.Clamp(swing01, 0f, 1f));
            float radius = Reach * MathHelper.Lerp(0.6f, 0.95f, swing01);
            Vector2 dir = new(hf * MathF.Cos(a), -MathF.Sin(a));
            //双爪微错位（同面挥掷不重叠）
            return mount + dir * radius + new Vector2(0f, side * 6f);
        }

        /// <summary>挥掷姿（主甩侧）：钳口随甩出张开抛球</summary>
        internal static BssClawPose Flick(Vector2 center, float rotation, int side, float swing01) {
            float open = swing01 > 0.7f ? MathHelper.Lerp(0.2f, 1f, (swing01 - 0.7f) / 0.3f) : 0.2f;
            return new BssClawPose(FlickTip(center, rotation, side, swing01),
                MathHelper.Lerp(0.5f, -0.3f, swing01), open, 0.28f + 0.6f * swing01);
        }

        /// <summary>挥掷备手姿（副侧半举戒备，下一记接手）</summary>
        internal static BssClawPose FlickReady(Vector2 center, float rotation, int side) {
            float hf = FacingSign(rotation);
            Vector2 mount = Mount(center, rotation, side);
            Vector2 dir = new(hf * MathF.Cos(1.85f), -MathF.Sin(1.85f));
            return new BssClawPose(mount + dir * (Reach * 0.55f), 0.45f, 0.35f, 0.2f);
        }

        /// <summary>
        /// 祭舞三拍（rite01 全程 0..1）：展开大张（0~0.34）→ 环绕划弧（0.34~0.72）→
        /// 过顶合掌（0.72~1）。世界系编舞（悬空仪式，重力空间里的"手势"才可读）。
        /// </summary>
        internal static BssClawPose Rite(Vector2 center, float rotation, int side, float rite01) {
            float t = MathHelper.Clamp(rite01, 0f, 1f);
            Vector2 mount = Mount(center, rotation, side);

            if (t < 0.34f) {
                //展开大张：双侧横向张开并抬升
                float p = t / 0.34f;
                float e = p * p * (3f - 2f * p);
                float a = MathHelper.Lerp(0.12f, 0.78f, e);
                float r = Reach * MathHelper.Lerp(0.5f, 0.93f, e);
                Vector2 dir = new(side * MathF.Cos(a), -MathF.Sin(a));
                return new BssClawPose(mount + dir * r,
                    MathHelper.Lerp(0.4f, -0.35f, e), 0.7f, 0.3f);
            }
            if (t < 0.72f) {
                //环绕划弧：双爪对称织圆（1.5 圈缓波）
                float p = (t - 0.34f) / 0.38f;
                float a = 0.78f + MathF.Sin(p * MathHelper.TwoPi * 1.5f) * 0.52f;
                float r = Reach * (0.82f + 0.13f * MathF.Sin(p * MathHelper.TwoPi * 3f + side));
                Vector2 dir = new(side * MathF.Cos(a), -MathF.Sin(a));
                return new BssClawPose(mount + dir * r, 0.1f, 0.5f, 0.35f);
            }
            //过顶合掌：向头顶正上收拢，末段咬合
            float p3 = (t - 0.72f) / 0.28f;
            float e3 = p3 * p3 * (3f - 2f * p3);
            Vector2 clasp = center + new Vector2(side * MathHelper.Lerp(48f, 6f, e3), -Reach * 0.86f);
            return new BssClawPose(clasp,
                MathHelper.Lerp(0.1f, 0.55f, e3),
                MathHelper.Lerp(0.5f, 0.05f, e3),
                0.3f + 0.5f * e3);
        }

        /// <summary>合掌点（祭舞召唤帧的演出锚，风沙收束目标）</summary>
        internal static Vector2 ClaspPoint(Vector2 center)
            => center + new Vector2(0f, -Reach * 0.86f);

        /// <summary>钻沙/掠冲收拢贴体</summary>
        internal static BssClawPose Tuck(Vector2 center, float rotation, int side) {
            Vector2 tip = center - Forward(rotation) * 28f + Lateral(rotation, side) * 16f;
            return new BssClawPose(tip, 0.9f, 0.05f, 0.3f);
        }

        /// <summary>死亡垂软（摇晃由骨架层叠加）</summary>
        internal static BssClawPose Collapse(Vector2 center, int side) {
            Vector2 tip = center + new Vector2(side * 22f, Reach * 0.68f);
            return new BssClawPose(tip, 0.15f, 0.55f, 0.06f);
        }
    }
}
