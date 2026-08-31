using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>爪姿包：尖端目标 + 卷曲 + 刃展 + 跟手速度</summary>
    internal readonly struct FssClawPose
    {
        public readonly Vector2 Tip;
        /// <summary>卷曲 −1 外张 ~ +1 内收</summary>
        public readonly float Curl;
        /// <summary>刃展 0 收 ~ 1 全展（镰 = 刃翻角，杵 = 疮锤鼓胀）</summary>
        public readonly float BladeOpen;
        /// <summary>跟手速度 0 慢摆 ~ 1 瞬发</summary>
        public readonly float Snap;

        public FssClawPose(Vector2 tip, float curl, float bladeOpen, float snap) {
            Tip = tip;
            Curl = curl;
            BladeOpen = bladeOpen;
            Snap = snap;
        }
    }

    /// <summary>
    /// 变异鳌足确定性编舞库——头尾双对制：前对 = 双疮杵（对称，锚头部，管护嘴/
    /// 撕咬/夯地），后对 = 双长镰（对称，锚尾前体节，管自刈剪切/甩痰）。
    /// 输入只有锚位姿 + 相位等已同步/同算量，状态（权威端弹幕/夯点）与骨架（各端
    /// 绘制）消费同一函数。地面探测走 FindGroundY（物块状态已同步 = 确定性）。
    /// 此文件禁止 Main.rand / 全局时钟（装饰性摆动归骨架层）。
    /// </summary>
    internal static class FssClawScript
    {
        /// <summary>疮杵臂全展（短粗）</summary>
        internal const float ClubReach = 250f;
        /// <summary>长镰臂全展（细长）</summary>
        internal const float SickleReach = 300f;

        internal static Vector2 Forward(float rotation)
            => (rotation - FssHead.FacingRot).ToRotationVector2();

        internal static Vector2 Lateral(float rotation, int side)
            => Forward(rotation).RotatedBy(side * MathHelper.PiOver2);

        /// <summary>前对爪基锚点（头两侧偏前，随体格缩放）</summary>
        internal static Vector2 FrontMount(Vector2 center, float rotation, int side, float scale)
            => center + Forward(rotation) * 12f * scale + Lateral(rotation, side) * 17f * scale;

        /// <summary>后对爪基锚点（尾前体节两侧）</summary>
        internal static Vector2 RearMount(Vector2 anchor, float anchorRot, int side, float scale)
            => anchor + Lateral(anchorRot, side) * 15f * scale;

        /// <summary>嘴位（与 FssStateBase.MouthPos 同约定）</summary>
        internal static Vector2 MouthPos(Vector2 center, float rotation, float scale)
            => center + Forward(rotation) * 34f * scale;

        private static float FacingSign(float rotation) {
            float x = Forward(rotation).X;
            return x >= 0f ? 1f : -1f;
        }

        #region 前对（双疮杵）
        /// <summary>前对待机：双杵胸前对称折叠（沉重的螳臂底色）</summary>
        internal static FssClawPose FrontIdle(Vector2 center, float rotation, int side, float scale) {
            Vector2 tip = center + Forward(rotation) * 54f * scale + Lateral(rotation, side) * 32f * scale;
            return new FssClawPose(tip, 0.7f, 0.2f, 0.1f);
        }

        /// <summary>前对拄地稳桩（后对出招时的支撑读数）</summary>
        internal static FssClawPose FrontBrace(Vector2 center, float rotation, int side, float scale) {
            float hf = FacingSign(rotation);
            Vector2 tip = center + new Vector2(hf * 24f + side * 34f, 84f) * scale;
            return new FssClawPose(tip, 0.35f, 0.15f, 0.18f);
        }

        /// <summary>护嘴：双杵合拢护在嘴前，burst 拍猛推摊开（配合喷吐后坐）</summary>
        internal static FssClawPose Guard(Vector2 center, float rotation, int side, float close01, float burst) {
            Vector2 mouth = MouthPos(center, rotation, 1.15f);
            Vector2 fwd = Forward(rotation);
            Vector2 lat = Lateral(rotation, side);
            float b = 1f - MathF.Pow(1f - MathHelper.Clamp(burst, 0f, 1f), 3f);

            Vector2 closed = mouth + fwd * 20f + lat * 12f;
            Vector2 open = mouth + fwd * 62f + lat * 60f;
            Vector2 tip = Vector2.Lerp(closed, open, b);
            if (close01 < 1f) {
                tip = Vector2.Lerp(FrontIdle(center, rotation, side, 1.15f).Tip, tip,
                    MathHelper.Clamp(close01, 0f, 1f));
            }
            return new FssClawPose(tip,
                MathHelper.Lerp(0.5f, -0.25f, b),
                MathHelper.Lerp(0.25f, 0.9f, b),
                0.25f + 0.55f * b);
        }

        /// <summary>撕咬挣抱：双杵从两侧包夹压向目标</summary>
        internal static FssClawPose Snatch(Vector2 center, float rotation, int side, Vector2 aim, float snap01) {
            float s = MathHelper.Clamp(snap01, 0f, 1f);
            Vector2 mouth = MouthPos(center, rotation, 1.15f);
            Vector2 to = aim - mouth;
            float dist = to.Length();
            Vector2 dir = dist > 0.01f ? to / dist : Forward(rotation);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2) * side;
            Vector2 tip = mouth + dir * Math.Min(dist, ClubReach * 0.92f)
                + perp * MathHelper.Lerp(40f, 10f, s);
            return new FssClawPose(tip,
                MathHelper.Lerp(-0.1f, 0.5f, s),
                MathHelper.Lerp(1f, 0.1f, s),
                0.6f);
        }

        /// <summary>
        /// 夯点（双杵合砸的地面点；状态取它做喷发/泉列原点，与绘制同源）。
        /// 探地确定性：物块状态各端同步。
        /// </summary>
        internal static Vector2 SlamImpact(Vector2 center, float rotation, float scale) {
            float hf = FacingSign(rotation);
            float x = center.X + hf * 165f * scale;
            float groundY = FssVfx.FindGroundY(new Vector2(x, center.Y - 60f), 900f);
            return new Vector2(x, groundY);
        }

        /// <summary>
        /// 双杵合砸编舞（slam01 全程）：双杵同举过顶蓄势（慢段 0~0.5）→ 合砸夯点
        /// （0.5~0.62）→ 钉地回弹（0.62~0.82）→ 半收（0.82~1）。两侧各带小分位
        /// （side*14px）避免完全重叠，读作合力一锤。
        /// </summary>
        internal static FssClawPose Slam(Vector2 center, float rotation, int side, float slam01, float scale) {
            float hf = FacingSign(rotation);
            float t = MathHelper.Clamp(slam01, 0f, 1f);
            Vector2 impact = SlamImpact(center, rotation, scale) + new Vector2(side * 14f, 0f);

            if (t < 0.5f) {
                float p = t / 0.5f;
                float e = p * p * (3f - 2f * p);
                float a = MathHelper.Lerp(0.5f, 1.95f, e);
                float r = ClubReach * MathHelper.Lerp(0.55f, 0.92f, e);
                Vector2 mount = FrontMount(center, rotation, side, scale);
                Vector2 dir = new(hf * MathF.Cos(a), -MathF.Sin(a));
                //两杵蓄势微分位：侧向张开一点，砸落时向中线合拢
                return new FssClawPose(mount + dir * r + new Vector2(side * 26f * e, 0f),
                    -0.2f, MathHelper.Lerp(0.2f, 0.9f, e), 0.22f);
            }
            if (t < 0.62f) {
                float p = (t - 0.5f) / 0.12f;
                Vector2 mount = FrontMount(center, rotation, side, scale);
                Vector2 high = mount + new Vector2(hf * MathF.Cos(1.95f), -MathF.Sin(1.95f)) * (ClubReach * 0.92f)
                    + new Vector2(side * 26f, 0f);
                return new FssClawPose(Vector2.Lerp(high, impact, p * p), 0.1f, 1f, 0.9f);
            }
            if (t < 0.82f) {
                float p = (t - 0.62f) / 0.2f;
                float bounce = MathF.Sin(p * MathHelper.Pi) * 10f;
                return new FssClawPose(impact - new Vector2(0f, bounce), 0.2f, 0.8f, 0.7f);
            }
            float r2 = (t - 0.82f) / 0.18f;
            Vector2 lift = impact + new Vector2(-hf * 30f * r2, -70f * r2);
            return new FssClawPose(lift, 0.3f, 0.4f, 0.3f);
        }
        #endregion

        #region 后对（双长镰）
        /// <summary>后对待机：双镰沿体后半举拖行（蝎尾式的悬而未落）</summary>
        internal static FssClawPose RearIdle(Vector2 anchor, float anchorRot, int side, float scale) {
            Vector2 mount = RearMount(anchor, anchorRot, side, scale);
            Vector2 back = -Forward(anchorRot);
            Vector2 tip = mount + back * (SickleReach * 0.4f)
                + Lateral(anchorRot, side) * 44f * scale
                - new Vector2(0f, SickleReach * 0.22f);
            return new FssClawPose(tip, 0.55f, 0.3f, 0.11f);
        }

        /// <summary>
        /// 双镰剪切自刈（scissor：两镰从上下两侧对剪囊肿；slice01 = 切弧进度，
        /// aim = 当前被割囊肿位）。side +1 自上压入，side -1 自下挑起，
        /// 弧中点两刃交剪 = 割破帧。
        /// </summary>
        internal static FssClawPose ReapScissor(Vector2 anchor, float anchorRot, int side, Vector2 aim, float slice01, float scale) {
            float t = MathHelper.Clamp(slice01, 0f, 1f);
            float e = t * t * (3f - 2f * t);
            //上刃从 -1.15 扫到 +0.95，下刃镜像（弧中点 ≈ 0 处两刃交剪）
            float ang = side > 0
                ? MathHelper.Lerp(-1.15f, 0.95f, e)
                : MathHelper.Lerp(1.15f + MathHelper.Pi, -0.95f + MathHelper.Pi, e);
            Vector2 offset = ang.ToRotationVector2().RotatedBy(-MathHelper.PiOver2) * 54f;
            return new FssClawPose(aim + offset,
                MathHelper.Lerp(-0.2f, 0.45f, t),
                t < 0.4f ? 1f : MathHelper.Lerp(1f, 0.25f, (t - 0.4f) / 0.6f),
                0.45f);
        }

        /// <summary>甩痰镰尖（过顶回甩弧；spawn 点与绘制同源）</summary>
        internal static Vector2 FlingTip(Vector2 anchor, float anchorRot, int side, float fling01, float scale) {
            float hf = FacingSign(anchorRot);
            Vector2 mount = RearMount(anchor, anchorRot, side, scale);
            float a = MathHelper.Lerp(2.2f, -0.35f, MathHelper.Clamp(fling01, 0f, 1f));
            float r = SickleReach * MathHelper.Lerp(0.62f, 0.94f, fling01);
            return mount + new Vector2(hf * MathF.Cos(a), -MathF.Sin(a)) * r
                + new Vector2(0f, side * 8f);
        }

        /// <summary>甩痰姿（双镰同甩，微错位）：刃随甩出翻开</summary>
        internal static FssClawPose Fling(Vector2 anchor, float anchorRot, int side, float fling01, float scale) {
            float open = fling01 > 0.7f ? MathHelper.Lerp(0.3f, 1f, (fling01 - 0.7f) / 0.3f) : 0.3f;
            return new FssClawPose(FlingTip(anchor, anchorRot, side, fling01, scale),
                MathHelper.Lerp(0.5f, -0.3f, fling01), open, 0.3f + 0.6f * fling01);
        }
        #endregion

        #region 通用
        /// <summary>收拢贴体（钻沙/掠冲；front = 前对锚头，否则锚尾前节）</summary>
        internal static FssClawPose Tuck(Vector2 anchor, float anchorRot, int side, float scale) {
            Vector2 tip = anchor - Forward(anchorRot) * 36f * scale + Lateral(anchorRot, side) * 20f * scale;
            return new FssClawPose(tip, 0.9f, 0.05f, 0.3f);
        }

        /// <summary>死亡垂软（摇晃归骨架层）</summary>
        internal static FssClawPose Collapse(Vector2 anchor, int side, float reach, float scale) {
            Vector2 tip = anchor + new Vector2(side * 30f * scale, reach * 0.62f);
            return new FssClawPose(tip, 0.15f, 0.5f, 0.06f);
        }
        #endregion
    }
}
