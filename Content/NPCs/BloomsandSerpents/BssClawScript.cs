using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>爪姿包：尖端目标 + 卷曲偏好 + 钳口 + 跟手速度</summary>
    internal readonly struct BssClawPose
    {
        /// <summary>爪尖目标（世界坐标）</summary>
        public readonly Vector2 Tip;
        /// <summary>肘弯收拢度 0 近直臂 ~ 1 深弓（骨架层只当幅度提示，极向与限位由解算决定）</summary>
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
    /// 鳌足位姿帧：编舞函数的全部输入。头位姿给嘴位与朝向，
    /// 挂点位姿给爪基（0 号体节：鳌足长在头后第一节，节肢动物的前附肢构造）。
    /// </summary>
    internal readonly struct BssClawFrame
    {
        public readonly Vector2 HeadCenter;
        public readonly float HeadRotation;
        public readonly Vector2 MountCenter;
        public readonly float MountRotation;
        public readonly float Scale;

        public BssClawFrame(Vector2 headCenter, float headRotation, Vector2 mountCenter, float mountRotation, float scale) {
            HeadCenter = headCenter;
            HeadRotation = headRotation;
            MountCenter = mountCenter;
            MountRotation = mountRotation;
            Scale = scale;
        }

        /// <summary>头前向</summary>
        public Vector2 Forward => BssClawScript.Forward(HeadRotation);
        /// <summary>挂点体节的链向（前向）</summary>
        public Vector2 MountForward => BssClawScript.Forward(MountRotation);
        /// <summary>水平朝向符号（世界系编舞用）</summary>
        public float FacingSign => Forward.X >= 0f ? 1f : -1f;
    }

    /// <summary>
    /// 鳌足确定性编舞函数库：输入只有头/挂点位姿 + 相位等已同步/同算量，输出爪姿。
    /// 状态与骨架消费同一函数，不需要任何网络包。此文件禁止使用 Main.rand / 全局时钟
    /// （装饰性摆动归骨架层，或读传入的步态相位）。
    /// 所有像素量按贴图尺度写，帧里的 Scale 传宿主 NPC.scale（图鉴端传 1）。
    /// </summary>
    internal static class BssClawScript
    {
        /// <summary>全肢触及（贴图尺度；= <see cref="BssClawRig"/> 三件套节长之和）</summary>
        internal const float Reach = 166f;
        /// <summary>爪基离挂点体节体轴的侧向偏移（肩球从体节侧缘探出）</summary>
        private const float MountSide = 24f;

        /// <summary>前向（贴图前方朝下约定的反解）</summary>
        internal static Vector2 Forward(float rotation)
            => (rotation - BssHead.FacingRot).ToRotationVector2();

        /// <summary>翻缘侧向（side = ±1）</summary>
        internal static Vector2 Lateral(float rotation, int side)
            => Forward(rotation).RotatedBy(side * MathHelper.PiOver2);

        /// <summary>爪基锚点：0 号体节两侧缘，略靠前（肩球压在体节扇冠之下）</summary>
        internal static Vector2 Mount(in BssClawFrame f, int side)
            => f.MountCenter + f.MountForward * (6f * f.Scale) + Lateral(f.MountRotation, side) * (MountSide * f.Scale);

        /// <summary>嘴位（喷沙炮口 / 撕咬判距）：头底尖端，落在两瓣颚刃之间</summary>
        internal static Vector2 MouthPos(Vector2 headCenter, float headRotation, float scale = 1f)
            => headCenter + Forward(headRotation) * (66f * scale);

        /// <summary>
        /// 常态待机：双螯从第一节伸到头前两侧探路，螯尖略低于头心（前附肢触地探路的读数），
        /// 随步态相位前后小幅交替划动（左右反相），钳口微张。
        /// </summary>
        internal static BssClawPose Idle(in BssClawFrame f, int side, float gaitPhase) {
            float s = f.Scale;
            float sway = MathF.Sin(gaitPhase * 0.5f + side * 1.3f) * 7f;
            Vector2 tip = f.HeadCenter + f.Forward * ((30f + sway) * s) + Lateral(f.HeadRotation, side) * (44f * s);
            return new BssClawPose(tip, 0.6f, 0.28f, 0.14f);
        }

        /// <summary>
        /// 护嘴：close01 = 合拢度（吸气进度），burst = 猛推包络。
        /// 合拢点在嘴前交叠护住，推开点沿前向 + 各自翻缘猛摊。
        /// </summary>
        internal static BssClawPose Guard(in BssClawFrame f, int side, float close01, float burst, float gaitPhase) {
            float s = f.Scale;
            Vector2 mouth = MouthPos(f.HeadCenter, f.HeadRotation, s);
            Vector2 fwd = f.Forward;
            Vector2 lat = Lateral(f.HeadRotation, side);
            Vector2 closed = mouth + fwd * (14f * s) + lat * (10f * s);
            Vector2 open = mouth + fwd * (40f * s) + lat * (46f * s);
            float b = 1f - MathF.Pow(1f - MathHelper.Clamp(burst, 0f, 1f), 3f);
            Vector2 tip = Vector2.Lerp(closed, open, b);
            //未合拢时从待机位渐入
            if (close01 < 1f) {
                Vector2 idleTip = Idle(in f, side, gaitPhase).Tip;
                tip = Vector2.Lerp(idleTip, tip, MathHelper.Clamp(close01, 0f, 1f));
            }
            return new BssClawPose(tip,
                MathHelper.Lerp(0.6f, 0.25f, b),
                MathHelper.Lerp(0.15f, 0.9f, b),
                0.25f + 0.55f * b);
        }

        /// <summary>撕咬挣抱：双爪从两侧包夹伸向目标，snap01 收拢钳口</summary>
        internal static BssClawPose Snatch(in BssClawFrame f, int side, Vector2 aim, float snap01) {
            float s = f.Scale;
            Vector2 mouth = MouthPos(f.HeadCenter, f.HeadRotation, s);
            Vector2 to = aim - mouth;
            float dist = to.Length();
            Vector2 dir = dist > 0.01f ? to / dist : f.Forward;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2) * side;
            float sn = MathHelper.Clamp(snap01, 0f, 1f);
            Vector2 tip = mouth + dir * Math.Min(dist, Reach * s * 0.7f)
                + perp * (MathHelper.Lerp(30f, 8f, sn) * s);
            return new BssClawPose(tip,
                MathHelper.Lerp(0.3f, 0.6f, sn),
                MathHelper.Lerp(1f, 0.08f, sn),
                0.6f);
        }

        /// <summary>
        /// 蹲伏张螯（open01 = 张开进度）：世界系编舞，双螯举向前上方大张威吓，
        /// 近层螯略前、远层螯略后，不重叠。
        /// </summary>
        internal static BssClawPose Brace(in BssClawFrame f, int side, float open01) {
            float s = f.Scale;
            float hf = f.FacingSign;
            float e = MathHelper.Clamp(open01, 0f, 1f);
            e = e * e * (3f - 2f * e);
            Vector2 mount = Mount(in f, side);
            Vector2 raised = mount + new Vector2(hf * (58f + side * 12f) * s, -(92f + side * 6f) * s)
                + Lateral(f.MountRotation, side) * (10f * s);
            Vector2 tip = Vector2.Lerp(mount + f.MountForward * (90f * s) + Lateral(f.MountRotation, side) * (36f * s), raised, e);
            return new BssClawPose(tip, 0.5f, MathHelper.Lerp(0.3f, 0.95f, e), 0.3f);
        }

        /// <summary>
        /// 掘沙（dig01 = 插沙进度）：双螯从探路位探向头前下方的沙面，螯尖分列头前两侧插进沙里，
        /// 钳口随下探合拢舀住沙。世界系编舞：沙面在下，插沙方向恒为世界向下。
        /// </summary>
        internal static BssClawPose Scoop(in BssClawFrame f, int side, float dig01) {
            float s = f.Scale;
            float hf = f.FacingSign;
            float e = MathHelper.Clamp(dig01, 0f, 1f);
            e = e * e * (3f - 2f * e);
            Vector2 mount = Mount(in f, side);
            Vector2 start = mount + f.MountForward * (90f * s) + Lateral(f.MountRotation, side) * (36f * s);
            Vector2 dug = mount + new Vector2(hf * (74f + side * 10f) * s, (78f + side * 4f) * s);
            Vector2 tip = Vector2.Lerp(start, dug, e);
            return new BssClawPose(tip, 0.55f, MathHelper.Lerp(0.6f, 0.12f, e), 0.3f);
        }

        /// <summary>抡掷进度分界：此前向后上抡起拉弓，此后鞭向前上释放（状态的出手帧与之对齐）</summary>
        internal const float FlingWhipStart = 0.55f;

        /// <summary>
        /// 过顶抡掷（hurl01）：前 <see cref="FlingWhipStart"/> 从掘沙位向后上方迟滞抡起，
        /// 之后三次幂极快鞭向前上方甩出。角度按世界系写（y 向下为正），随水平朝向镜像。
        /// </summary>
        internal static BssClawPose Fling(in BssClawFrame f, int side, float hurl01) {
            float s = f.Scale;
            float hf = f.FacingSign;
            float e = MathHelper.Clamp(hurl01, 0f, 1f);
            Vector2 mount = Mount(in f, side);
            const float FrontLow = 0.75f;
            const float BackHigh = -2.3f;
            const float FrontHigh = -0.85f;
            float ang;
            float blade;
            if (e < FlingWhipStart) {
                float k = e / FlingWhipStart;
                k *= k;
                ang = MathHelper.Lerp(FrontLow, BackHigh, k);
                blade = 0.1f;
            }
            else {
                float k = (e - FlingWhipStart) / (1f - FlingWhipStart);
                k = 1f - MathF.Pow(1f - k, 3f);
                ang = MathHelper.Lerp(BackHigh, FrontHigh, k);
                blade = MathHelper.Lerp(0.1f, 0.9f, k);
            }
            Vector2 dir = new(hf * MathF.Cos(ang), MathF.Sin(ang));
            Vector2 tip = mount + dir * (Reach * s * 0.92f) + Lateral(f.MountRotation, side) * (10f * s);
            return new BssClawPose(tip, 0.3f, blade, 0.85f);
        }

        /// <summary>钻沙/掠冲收拢贴体：螯折回沿体节侧缘向后掠平</summary>
        internal static BssClawPose Tuck(in BssClawFrame f, int side) {
            float s = f.Scale;
            Vector2 mount = Mount(in f, side);
            Vector2 tip = mount - f.MountForward * (34f * s) + Lateral(f.MountRotation, side) * (16f * s);
            return new BssClawPose(tip, 0.9f, 0.05f, 0.3f);
        }

        /// <summary>死亡垂软（摇晃由骨架层叠加）</summary>
        internal static BssClawPose Collapse(in BssClawFrame f, int side) {
            float s = f.Scale;
            Vector2 mount = Mount(in f, side);
            Vector2 tip = mount + new Vector2(side * 24f * s, Reach * s * 0.62f);
            return new BssClawPose(tip, 0.15f, 0.55f, 0.06f);
        }
    }
}
