using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RAMSystems;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇客时间RAM弧形资源HUD
    /// <br/>以弧形分格条显示RAM状态，多层渲染营造赛博科技纵深感
    /// <br/>进入骇客时间即显示，不依赖目标选中
    /// <br/>弧线由大半径圆弧生成柔和曲率，分层绘制阴影、背景、填充、高光、装饰环
    /// </summary>
    internal class HackRamRenderer
    {
        private float timer;
        //飞入动画进度(0~1)
        private float flyInProgress;
        //平滑显示RAM值（视觉过渡用）
        private float displayRam;

        //===== 弧线几何常量 =====
        //弧线内径（大半径产生柔和曲率，弧线呈穹顶拱形）
        private const float InnerR = 560f;
        //弧线厚度
        private const float ArcThick = 24f;
        //弧线外径
        private const float OuterR = InnerR + ArcThick;
        //弧顶距屏幕顶部
        private const float TopY = 76f;
        //格间间隙弧度
        private const float CellGap = 0.007f;
        //单格基准角度：让 8 格视觉上对应旧的 400px 跨度
        //BaseCellAngle ≈ (asin(200/572)*2 - 7*CellGap)/8 ≈ 0.0826 rad
        private const float BaseCellAngle = 0.0826f;
        //最大允许的总扫掠角（防止 HUD 在屏幕顶端横向溢出）
        //≈ π/2，对应 ArcSpanPx ≈ 808px，足以容纳 16 格视觉拉伸
        private const float MaxTotalSweep = MathHelper.PiOver2;

        //外围装饰环（刻度轨道）
        private const float DecoGap = 6f;
        private const float DecoR = OuterR + DecoGap;
        //内侧装饰环（扫描轨道）
        private const float InnerDecoGap = 5f;
        private const float InnerDecoR = InnerR - InnerDecoGap;

        //===== 字体 =====
        private const float FTitle = 0.54f;
        private const float FValue = 0.64f;
        private const float FWarn = 0.50f;
        private const float FHex = 0.42f;

        /// <summary>
        /// 根据 maxRam 推导当前帧的弧线几何参数
        /// <br/>常规情况下保持单格视觉宽度恒定，整体跨度随 maxRam 线性增长
        /// <br/>超过软上限后反向收紧每格使整体跨度饱和
        /// </summary>
        private static void ComputeArcGeom(int maxRam,
            out float halfSweep, out float cellAngle, out float arcSpanPx) {
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
            //ArcSpanPx 由当前半扫掠角与中径反算，用于水平居中布局与 quad 大小
            arcSpanPx = 2f * (InnerR + ArcThick * 0.5f) * MathF.Sin(halfSweep);
        }

        public void Update() {
            timer += 0.016f;

            bool show = HackTime.Active || HackTime.Intensity > 0.01f;
            flyInProgress = MathHelper.Lerp(flyInProgress, show ? 1f : 0f, 0.065f);
            if (flyInProgress > 0.995f) flyInProgress = 1f;
            if (flyInProgress < 0.005f) flyInProgress = 0f;

            displayRam = MathHelper.Lerp(displayRam, RamSystem.CurrentRam, 0.12f);
        }

        public void Draw(SpriteBatch sb) {
            if (flyInProgress < 0.01f) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            float alpha = HackTime.Intensity * flyInProgress;
            if (alpha < 0.01f) return;

            int maxRam = RamSystem.MaxRam;
            if (maxRam <= 0) return;

            //弧线参数计算（随 maxRam 动态拉伸/收紧）
            ComputeArcGeom(maxRam, out float halfSweep, out float cellAngle, out float arcSpanPx);
            float midAngle = -MathHelper.PiOver2; //正上方
            float aStart = midAngle - halfSweep;
            float totalSweep = halfSweep * 2f;

            //弧线中心（在弧线正下方,使弧线呈穹顶拱形）
            //水平居中于屏幕顶部中间偏上位置，作为整个骇客HUD的主焦点
            float cx = Main.screenWidth * 0.5f;
            float flyOff = (1f - EaseOutCubic(flyInProgress)) * -50f;
            float cy = TopY + InnerR + flyOff;
            Vector2 center = new(cx, cy);

            //阴影层(保留在quad下方绘制,便于整体浮起感)
            DrawShadow(sb, px, center, aStart, totalSweep, alpha);

            //优先使用着色器渲染主弧带+装饰环
            bool shaderOK = TryDrawShaderArc(sb, px, center, aStart, cellAngle,
                totalSweep, arcSpanPx, maxRam, alpha);

            if (!shaderOK) {
                //CPU回退路径
                DrawOuterDecoRing(sb, px, center, aStart, totalSweep, maxRam, cellAngle, alpha);
                DrawCells(sb, px, center, aStart, cellAngle, maxRam, alpha);
                DrawLockCountdownFill(sb, px, center, aStart, cellAngle, maxRam, alpha);
                DrawInnerDecoRing(sb, px, center, aStart, totalSweep, alpha);
                DrawDataFlow(sb, center, aStart, totalSweep, alpha);
            }

            //端点角标 + 标签文字(始终CPU绘制,文字更锐利)
            DrawEndCaps(sb, px, center, aStart, totalSweep, alpha);
            DrawLabels(sb, center, alpha, maxRam);
        }

        #region 着色器渲染

        //使用HackRamArc.fx绘制主弧带+内外装饰环
        //通过单个quad把弧形几何、格子、扫描、故障等全部用GPU完成
        private bool TryDrawShaderArc(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float cellAngle, float totalSweep, float arcSpanPx, int maxRam, float alpha) {
            Effect effect = EffectLoader.HackRamArc?.Value;
            if (effect == null) return false;

            //quad包围盒:覆盖外侧装饰环到内侧装饰环
            float decoOuterR = OuterR + DecoGap;
            float decoInnerR = InnerR - InnerDecoGap;
            //外侧刻度最长9px + 外侧漏光4px,内侧粒子可下探到decoInnerR-4
            const float PadTop = 18f;
            const float PadBottom = 10f;
            const float PadSide = 30f;

            //quad上下边界按顶角(最高点)计算,两侧按aStart点的y计算取最小
            //简化:quad顶 = cy - (decoOuterR + PadTop)
            //quad底 = cy - (decoInnerR * MathF.Cos(halfSweep)) + PadBottom
            //但直接用简单估算即可,因为弧顶在正上方最靠上
            float qLeft = center.X - arcSpanPx * 0.5f - PadSide;
            float qTop = center.Y - decoOuterR - PadTop;
            float qRight = center.X + arcSpanPx * 0.5f + PadSide;
            float qBottom = center.Y - decoInnerR * MathF.Cos(totalSweep * 0.5f) + PadBottom;

            int qW = (int)MathF.Ceiling(qRight - qLeft);
            int qH = (int)MathF.Ceiling(qBottom - qTop);
            if (qW <= 0 || qH <= 0) return true;

            Rectangle dest = new((int)qLeft, (int)qTop, qW, qH);
            Vector2 relCenter = new(center.X - qLeft, center.Y - qTop);

            //低RAM警告强度(平滑)：组合自然低位 + 系统锁定/不足闪烁
            float lowRam = 0f;
            if (!HackTime.InfiniteHack) {
                if (RamSystem.CurrentRam < 0.5f) lowRam = 1f;
                else if (RamSystem.CurrentRam <= 2f)
                    lowRam = MathHelper.Clamp(1f - (RamSystem.CurrentRam - 0.5f) / 1.5f, 0f, 1f);
            }
            //系统锁定/不足闪烁直接拉满故障色
            lowRam = MathF.Max(lowRam, RamSystem.GetWarningPulse());

            effect.Parameters["uTime"]?.SetValue(timer);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(qW, qH));
            effect.Parameters["uArcCenter"]?.SetValue(relCenter);
            effect.Parameters["uInnerR"]?.SetValue(InnerR);
            effect.Parameters["uOuterR"]?.SetValue(OuterR);
            effect.Parameters["uAStart"]?.SetValue(aStart);
            effect.Parameters["uCellAngle"]?.SetValue(cellAngle);
            effect.Parameters["uCellGap"]?.SetValue(CellGap);
            effect.Parameters["uCellCount"]?.SetValue((float)maxRam);
            effect.Parameters["uFillValue"]?.SetValue(displayRam);
            effect.Parameters["uLowRam"]?.SetValue(lowRam);
            effect.Parameters["uLockFill"]?.SetValue(RamSystem.LockRemainRatio);
            effect.Parameters["uInfinite"]?.SetValue(HackTime.InfiniteHack ? 1f : 0f);
            effect.Parameters["uDecoOuterR"]?.SetValue(decoOuterR);
            effect.Parameters["uDecoInnerR"]?.SetValue(decoInnerR);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, dest, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            return true;
        }

        #endregion

        #region 阴影层

        //整体弧形投影阴影，制造浮起感
        private void DrawShadow(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float totalSweep, float alpha) {
            Vector2 offset = new(3, 4);
            DrawArc(sb, px, center + offset, InnerR - 2, OuterR + 2,
                aStart, aStart + totalSweep, HackTheme.BgDarkest * (alpha * 0.45f));
        }

        #endregion

        #region 外围装饰环

        //刻度轨道：薄弧线 + 大小刻度标记，增加仪表盘质感
        private void DrawOuterDecoRing(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float totalSweep, int maxRam, float cellAngle, float alpha) {
            float aEnd = aStart + totalSweep;

            //薄弧线轨道
            DrawArc(sb, px, center, DecoR, DecoR + 1.5f, aStart, aEnd,
                HackTheme.Border * (alpha * 0.25f));

            //刻度标记（主刻度对齐格子边界，次刻度等分）
            int ticks = maxRam * 4;
            float tickStep = totalSweep / ticks;
            for (int i = 0; i <= ticks; i++) {
                float a = aStart + i * tickStep;
                Vector2 dir = AngleDir(a);
                bool major = i % 4 == 0;
                float len = major ? 8f : 3.5f;
                float thick = major ? 1.5f : 0.8f;
                Color col = major ? HackTheme.BorderBright : HackTheme.Border;
                DrawLine(sb, px,
                    center + dir * DecoR,
                    center + dir * (DecoR + len),
                    thick, col * (alpha * 0.4f));
            }

            //主刻度旁的微型数字标识（每隔2格标一个）
            for (int i = 0; i <= maxRam; i += 2) {
                float a = aStart + i * (cellAngle + (i < maxRam ? CellGap : 0));
                if (i == maxRam) a = aEnd;
                Vector2 dir = AngleDir(a);
                Vector2 pos = center + dir * (DecoR + 14f);
                string mark = $"{i}";
                Vector2 mSize = FontAssets.MouseText.Value.MeasureString(mark) * 0.18f;
                Utils.DrawBorderString(sb, mark,
                    pos - mSize * 0.5f,
                    HackTheme.TextDim * (alpha * 0.25f), 0.18f);
            }
        }

        #endregion

        #region RAM格子

        //逐格绘制：背景层 → 填充层(含内侧高光+外侧暗化) → 边框 → 辉光
        private void DrawCells(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float cellAngle, int maxRam, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;

            for (int i = 0; i < maxRam; i++) {
                float cStart = aStart + i * (cellAngle + CellGap);
                float cEnd = cStart + cellAngle;
                float fill = Math.Clamp(displayRam - i, 0f, 1f);

                //--- 背景 ---
                DrawArc(sb, px, center, InnerR, OuterR, cStart, cEnd,
                    HackTheme.BgSlot * (alpha * 0.85f));

                //--- 填充 ---
                if (fill > 0.01f) {
                    float fillEnd = cStart + cellAngle * fill;

                    //主填充
                    Color fillBase = fill >= 1f
                        ? HackTheme.ProgressFill
                        : Color.Lerp(HackTheme.ProgressFill, HackTheme.ProgressGlow,
                            MathF.Sin(timer * 4f) * 0.25f + 0.25f);
                    DrawArc(sb, px, center, InnerR + 2, OuterR - 2, cStart, fillEnd,
                        fillBase * (alpha * 0.85f));

                    //内侧高光弧（模拟从内部照射的光泽，增加厚度层次）
                    DrawArc(sb, px, center, InnerR + 2, InnerR + 6, cStart, fillEnd,
                        HackTheme.ProgressGlow * (alpha * 0.30f));

                    //外侧暗化弧（压暗外缘，增加内凹感）
                    DrawArc(sb, px, center, OuterR - 6, OuterR - 2, cStart, fillEnd,
                        HackTheme.BgDarkest * (alpha * 0.18f));
                }

                //--- 边框 ---
                Color borderCol = fill >= 1f ? HackTheme.BorderBright : HackTheme.Border;
                //内外弧线
                DrawArc(sb, px, center, InnerR, InnerR + 1, cStart, cEnd,
                    borderCol * (alpha * 0.40f));
                DrawArc(sb, px, center, OuterR - 1, OuterR, cStart, cEnd,
                    borderCol * (alpha * 0.30f));
                //两侧径向封口线
                DrawRadialLine(sb, px, center, InnerR, OuterR, cStart, 1.2f,
                    borderCol * (alpha * 0.35f));
                DrawRadialLine(sb, px, center, InnerR, OuterR, cEnd, 1.2f,
                    borderCol * (alpha * 0.35f));

                //--- 满格辉光 ---
                if (fill >= 1f && glow != null) {
                    float midA = (cStart + cEnd) * 0.5f;
                    float midR = (InnerR + OuterR) * 0.5f;
                    Vector2 midPt = center + AngleDir(midA) * midR;
                    float pulse = MathF.Sin(timer * 2f + i * 0.7f) * 0.15f + 0.85f;
                    Color gc = HackTheme.ProgressGlow * (alpha * 0.08f * pulse);
                    gc.A = 0;
                    sb.Draw(glow, midPt, null, gc, 0, glow.Size() / 2, 0.07f, SpriteEffects.None, 0);
                }
            }
        }

        #endregion

        #region 锁定倒计时填充

        //系统锁定时用红色弧形填充显示剩余时长：满弧=刚锁定，清空=即将恢复
        private void DrawLockCountdownFill(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float cellAngle, int maxRam, float alpha) {
            float lockFill = RamSystem.LockRemainRatio;
            if (lockFill <= 0.001f) {
                return;
            }

            float filledCells = lockFill * maxRam;
            float pulse = MathF.Sin(timer * 8f) * 0.5f + 0.5f;
            Color fillCol = Color.Lerp(HackTheme.Danger, new Color(255, 95, 35), pulse * 0.35f);
            for (int i = 0; i < maxRam; i++) {
                float fill = MathHelper.Clamp(filledCells - i, 0f, 1f);
                if (fill <= 0.001f) {
                    continue;
                }

                float cStart = aStart + i * (cellAngle + CellGap);
                float fillEnd = cStart + cellAngle * fill;
                DrawArc(sb, px, center, InnerR + 3f, OuterR - 3f, cStart, fillEnd,
                    fillCol * (alpha * 0.62f));
                DrawArc(sb, px, center, InnerR + 2f, InnerR + 5f, cStart, fillEnd,
                    HackTheme.Danger * (alpha * 0.35f));

                if (fill < 0.999f) {
                    DrawRadialLine(sb, px, center, InnerR + 1f, OuterR - 1f, fillEnd, 2f,
                        Color.Lerp(HackTheme.TextBright, HackTheme.Danger, 0.45f) * (alpha * 0.75f));
                }
            }
        }

        #endregion

        #region 内侧装饰环

        //内环：细弧线 + 扫描脉冲光（像雷达扫描般循环扫过内环）
        private void DrawInnerDecoRing(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float totalSweep, float alpha) {
            float aEnd = aStart + totalSweep;

            //细弧线
            DrawArc(sb, px, center, InnerDecoR - 1f, InnerDecoR, aStart, aEnd,
                HackTheme.Border * (alpha * 0.20f));

            //扫描脉冲弧（沿内环周期扫过的亮带）
            float scanT = timer * 0.3f % 1f;
            float scanAngle = aStart + scanT * totalSweep;
            float scanWidth = totalSweep * 0.12f;
            float sStart = Math.Max(scanAngle - scanWidth * 0.5f, aStart);
            float sEnd = Math.Min(scanAngle + scanWidth * 0.5f, aEnd);
            if (sEnd > sStart) {
                float fade = MathF.Sin(scanT * MathF.PI);
                DrawArc(sb, px, center, InnerDecoR - 2f, InnerDecoR + 1f, sStart, sEnd,
                    HackTheme.Accent * (alpha * 0.22f * fade));
            }

            //内环上的微型呼吸脉冲点（沿弧间隔分布）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                int dots = 5;
                for (int d = 0; d < dots; d++) {
                    float t = (d + 0.5f) / dots;
                    float a = aStart + t * totalSweep;
                    Vector2 pt = center + AngleDir(a) * (InnerDecoR - 3f);
                    float dPulse = MathF.Sin(timer * 2.5f + d * 1.3f) * 0.3f + 0.7f;
                    Color dc = HackTheme.Accent * (alpha * 0.06f * dPulse);
                    dc.A = 0;
                    sb.Draw(glow, pt, null, dc, 0, glow.Size() / 2, 0.025f, SpriteEffects.None, 0);
                }
            }
        }

        #endregion

        #region 数据流粒子

        //沿弧线流动的光点，增加"数据传输"视觉暗示
        private void DrawDataFlow(SpriteBatch sb, Vector2 center,
            float aStart, float totalSweep, float alpha) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            float midR = (InnerR + OuterR) * 0.5f;

            for (int d = 0; d < 3; d++) {
                float t = (timer * 0.4f + d * 0.33f) % 1f;
                float angle = aStart + t * totalSweep;
                Vector2 pos = center + AngleDir(angle) * midR;
                float intensity = MathF.Sin(t * MathF.PI) * (1f - d * 0.2f);
                Color col = HackTheme.Accent * (alpha * 0.18f * intensity);
                col.A = 0;
                sb.Draw(glow, pos, null, col, 0, glow.Size() / 2, 0.045f, SpriteEffects.None, 0);
            }
        }

        #endregion

        #region 端点装饰

        //弧线两端的L形技术角标，强化终止感
        private void DrawEndCaps(SpriteBatch sb, Texture2D px, Vector2 center,
            float aStart, float totalSweep, float alpha) {
            float aEnd = aStart + totalSweep;
            Color capCol = HackTheme.Accent * (alpha * 0.50f);
            float capLen = 14f;

            //左端
            {
                Vector2 dir = AngleDir(aStart);
                Vector2 perp = new(-dir.Y, dir.X); //切线方向（沿弧线向右）
                Vector2 outerPt = center + dir * OuterR;
                Vector2 innerPt = center + dir * InnerR;
                //径向粗端封线
                DrawLine(sb, px, innerPt - dir * 2, outerPt + dir * 2, 2f, capCol);
                //切向延伸臂
                DrawLine(sb, px, outerPt, outerPt + perp * capLen, 1.5f, capCol * 0.6f);
                DrawLine(sb, px, innerPt, innerPt + perp * capLen, 1.5f, capCol * 0.4f);
            }

            //右端
            {
                Vector2 dir = AngleDir(aEnd);
                Vector2 perp = new(dir.Y, -dir.X); //切线方向（沿弧线向左）
                Vector2 outerPt = center + dir * OuterR;
                Vector2 innerPt = center + dir * InnerR;
                DrawLine(sb, px, innerPt - dir * 2, outerPt + dir * 2, 2f, capCol);
                DrawLine(sb, px, outerPt, outerPt + perp * capLen, 1.5f, capCol * 0.6f);
                DrawLine(sb, px, innerPt, innerPt + perp * capLen, 1.5f, capCol * 0.4f);
            }
        }

        #endregion

        #region 标签文字

        //在弧线内凹区域显示标题、数值读数、装饰性十六进制和低RAM警告
        private void DrawLabels(SpriteBatch sb, Vector2 center, float alpha, int maxRam) {
            //标签基准Y = 弧线内径顶点 + 偏移（位于弧线内凹空间中）
            float baseY = center.Y - InnerR + InnerDecoGap + 12f;

            //标题
            string title = "// BUFFER RAM";
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * FTitle;
            Utils.DrawBorderString(sb, title,
                new Vector2(center.X - titleSize.X * 0.5f, baseY),
                HackTheme.Accent * (alpha * 0.55f), FTitle);

            //数值读数（大号）
            string val = $"{RamSystem.DisplayCurrent}/{maxRam}";
            Vector2 valSize = FontAssets.MouseText.Value.MeasureString(val) * FValue;
            Color valColor = RamSystem.CurrentRam <= 2f && !HackTime.InfiniteHack
                ? Color.Lerp(HackTheme.TextBright, HackTheme.Danger,
                    MathF.Sin(timer * 5f) * 0.4f + 0.6f)
                : HackTheme.TextBright;
            Utils.DrawBorderString(sb, val,
                new Vector2(center.X - valSize.X * 0.5f, baseY + 20),
                valColor * (alpha * 0.85f), FValue);

            //装饰性十六进制地址
            string hex = $"0x{(int)(timer * 60) % 0xFFFF:X4}";
            Vector2 hexSize = FontAssets.MouseText.Value.MeasureString(hex) * FHex;
            Utils.DrawBorderString(sb, hex,
                new Vector2(center.X - hexSize.X * 0.5f, baseY + 46),
                HackTheme.TextDim * (alpha * 0.22f), FHex);

            //低RAM警告
            if (RamSystem.CurrentRam <= 2f && !HackTime.InfiniteHack) {
                float wPulse = MathF.Sin(timer * 5f) * 0.4f + 0.6f;
                string warn = RamSystem.CurrentRam < 0.5f
                    ? HackTime.RamDepleted.Value
                    : HackTime.LowRam.Value;
                Vector2 wSize = FontAssets.MouseText.Value.MeasureString(warn) * FWarn;
                Utils.DrawBorderString(sb, warn,
                    new Vector2(center.X - wSize.X * 0.5f, baseY + 62),
                    HackTheme.Danger * (alpha * wPulse * 0.75f), FWarn);
            }
        }

        #endregion

        #region 弧线绘制工具

        //用密集径向线段绘制填充弧形
        //rIn/rOut: 内外半径，aStart/aEnd: 起止角度(弧度)
        private static void DrawArc(SpriteBatch sb, Texture2D px, Vector2 center,
            float rIn, float rOut, float aStart, float aEnd, Color color) {
            if (aEnd <= aStart) return;
            float midR = (rIn + rOut) * 0.5f;
            float arcLen = (aEnd - aStart) * midR;
            int steps = Math.Max((int)(arcLen / 2.5f), 3);
            float aStep = (aEnd - aStart) / steps;
            //线宽略大于间距确保无缝
            float lineThick = Math.Max(aStep * midR + 0.8f, 1.5f);

            for (int i = 0; i <= steps; i++) {
                float a = aStart + i * aStep;
                Vector2 dir = AngleDir(a);
                DrawLine(sb, px, center + dir * rIn, center + dir * rOut, lineThick, color);
            }
        }

        //绘制径向线（格子两侧封口）
        private static void DrawRadialLine(SpriteBatch sb, Texture2D px, Vector2 center,
            float rIn, float rOut, float angle, float thickness, Color color) {
            Vector2 dir = AngleDir(angle);
            DrawLine(sb, px, center + dir * rIn, center + dir * rOut, thickness, color);
        }

        private static Vector2 AngleDir(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

        private static void DrawLine(SpriteBatch sb, Texture2D px,
            Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) return;
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private static float EaseOutCubic(float t) {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        #endregion
    }
}
