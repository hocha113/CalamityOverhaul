using System;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// RAM 弧的几何真源。绘制与布局避让都从这里取值，不各算一套
    /// <br/>查询一律用落位几何（忽略入场偏移），避让位置才不会跟着入场动画抖
    /// </summary>
    internal static class HackRamArcLayout
    {
        #region 几何常量

        public const float InnerR = 560f;
        public const float ArcThick = 24f;
        public const float OuterR = InnerR + ArcThick;
        /// <summary>弧顶距屏顶</summary>
        public const float TopY = 76f;

        public const float CellGap = 0.007f;
        /// <summary>单格基准角，8 格≈旧 400px</summary>
        public const float BaseCellAngle = 0.0826f;
        /// <summary>最大扫掠，防顶端溢出；≈π/2，约 16 格</summary>
        public const float MaxTotalSweep = MathHelper.PiOver2;

        public const float DecoGap = 6f;
        public const float DecoR = OuterR + DecoGap;
        public const float InnerDecoGap = 5f;
        public const float InnerDecoR = InnerR - InnerDecoGap;

        /// <summary>主刻度数字距装饰环</summary>
        public const float TickLabelGap = 16f;
        /// <summary>外缘可达半径，含刻度数字</summary>
        public const float ReachR = DecoR + TickLabelGap;
        /// <summary>内缘可达半径，内环扫描脉冲画到这里</summary>
        public const float InnerReachR = InnerDecoR - 6f;
        /// <summary>端点切向臂长</summary>
        public const float CapArm = 14f;

        /// <summary>避让时留出的呼吸间隙</summary>
        public const float ClearGap = 16f;

        #endregion

        /// <summary>按 maxRam 推导弧几何，超软上限收紧单格</summary>
        public static void Compute(int maxRam, out Vector2 center,
            out float halfSweep, out float cellAngle, out float arcSpanPx) {
            center = new Vector2(HackTheme.UIScreenW * 0.5f, TopY + InnerR);
            if (maxRam <= 0) {
                halfSweep = 0f;
                cellAngle = 0f;
                arcSpanPx = 0f;
                return;
            }

            float targetSweep = BaseCellAngle * maxRam + (maxRam - 1) * CellGap;
            float totalSweep;
            if (targetSweep <= MaxTotalSweep) {
                cellAngle = BaseCellAngle;
                totalSweep = targetSweep;
            }
            else {
                totalSweep = MaxTotalSweep;
                cellAngle = (MaxTotalSweep - (maxRam - 1) * CellGap) / maxRam;
            }
            halfSweep = totalSweep * 0.5f;
            //ArcSpanPx 由半扫掠角与中径反算
            arcSpanPx = 2f * (InnerR + ArcThick * 0.5f) * MathF.Sin(halfSweep);
        }

        /// <summary>该 x 处弧占用的最低 y；未覆盖返回 <see cref="float.MinValue"/></summary>
        public static float BottomAt(int maxRam, float x) {
            Compute(maxRam, out Vector2 center, out float halfSweep, out _, out _);
            return BottomAtCore(center, halfSweep, MathF.Abs(x - center.X));
        }

        /// <summary>[x0,x1] 带内弧占用的最低 y；未覆盖返回 <see cref="float.MinValue"/></summary>
        public static float BottomInBand(int maxRam, float x0, float x1) {
            Compute(maxRam, out Vector2 center, out float halfSweep, out _, out _);
            float sinH = MathF.Sin(halfSweep);
            if (sinH <= 0.0001f) {
                return float.MinValue;
            }

            if (x0 > x1) {
                (x0, x1) = (x1, x0);
            }

            //弧左右对称，折到中线一侧比较
            float dxLo, dxHi;
            if (x0 <= center.X && x1 >= center.X) {
                dxLo = 0f;
                dxHi = MathF.Max(center.X - x0, x1 - center.X);
            }
            else if (x1 < center.X) {
                dxLo = center.X - x1;
                dxHi = center.X - x0;
            }
            else {
                dxLo = x0 - center.X;
                dxHi = x1 - center.X;
            }

            float cover = ReachR * sinH + CapArm;
            if (dxLo > cover) {
                return float.MinValue;
            }
            dxHi = MathF.Min(dxHi, cover);

            //最低点落在内缘翼尖，夹进带内再求值
            float dx = MathHelper.Clamp(InnerReachR * sinH, dxLo, dxHi);
            return BottomAtCore(center, halfSweep, dx);
        }

        //dx 为到中线的横向距离。内缘翼尖之内贴内缘圆，之外贴端封径向线（越往外越高）
        private static float BottomAtCore(Vector2 center, float halfSweep, float dx) {
            float sinH = MathF.Sin(halfSweep);
            if (sinH <= 0.0001f) {
                return float.MinValue;
            }
            if (dx > ReachR * sinH + CapArm) {
                return float.MinValue;
            }
            if (dx <= InnerReachR * sinH) {
                return center.Y - MathF.Sqrt(MathF.Max(InnerReachR * InnerReachR - dx * dx, 0f));
            }
            return center.Y - dx * MathF.Cos(halfSweep) / sinH;
        }
    }
}
