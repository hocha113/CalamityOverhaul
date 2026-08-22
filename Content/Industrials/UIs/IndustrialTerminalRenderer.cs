using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Text;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Industrials.UIs
{
    /// <summary>
    /// 工业域机器界面的共享主题与笔刷:野外仪器语言，切角钢壳(shader)、机加工凹槽、
    /// 指针仪表、刻度条、模块插座、黄铜铭牌。勘探终端与发电机系列共用。<br/>
    /// 富层交给 <c>IndustrialTerminal.fx</c>,锐利前景交给 <see cref="SvgPathPen"/> 与 1px 蚀刻线;
    /// 暗部一律是紧贴的机加工线,不做同心放大的假羽化
    /// </summary>
    internal static class IndustrialTerminalRenderer
    {
        #region 主题
        //色板与 IndustrialTerminal.fx 同族:暗钢底、黄铜件、琥珀唯一亮色
        internal static readonly Color Steel = new(26, 22, 20);
        internal static readonly Color SteelLit = new(52, 44, 38);
        internal static readonly Color RecessBed = new(13, 11, 9);
        internal static readonly Color Brass = new(148, 118, 62);
        internal static readonly Color BrassBright = new(210, 172, 100);
        internal static readonly Color TextMain = new(232, 210, 180);
        internal static readonly Color TextDim = new(150, 132, 112);
        internal static readonly Color Amber = new(235, 170, 90);
        internal static readonly Color WarnRed = new(255, 100, 80);
        internal static readonly Color OkGreen = new(150, 220, 120);

        /// <summary>机壳切角(px),与 shader 的 uChamfer 对齐</summary>
        internal const int Chamfer = 12;

        private static Texture2D Px => VaultAsset.placeholder2.Value;
        private static readonly Rectangle One = new(0, 0, 1, 1);
        #endregion

        #region 机壳面板
        /// <summary>
        /// 钢壳面板底:IndustrialTerminal.fx 拉丝钢 + 锈斑 + 磨亮棱线;
        /// 着色器缺失回退为切角实底 + 顶部受光
        /// </summary>
        /// <param name="mode">0 主机壳(暗钢) 1 铭牌(黄铜)</param>
        /// <param name="heat">机壳受热度 0..1,底缘沁暖(热力炉体用,常温机器传 0)</param>
        internal static void ShaderPanel(SpriteBatch sb, Rectangle rect, float alpha, int mode = 0, float heat = 0f) {
            if (rect.Width < 4 || rect.Height < 4 || alpha < 0.01f) {
                return;
            }
            Effect effect = EffectLoader.IndustrialTerminal?.Value;
            int chamfer = mode == 0 ? Chamfer : 5;
            if (effect == null) {
                FallbackPanel(sb, rect, alpha, mode, chamfer);
                return;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uChamfer"]?.SetValue((float)chamfer);
            effect.Parameters["uMode"]?.SetValue((float)mode);
            effect.Parameters["uHeat"]?.SetValue(MathHelper.Clamp(heat, 0f, 1f));
            ShaderQuad(sb, effect, rect);
        }

        private static void ShaderQuad(SpriteBatch sb, Effect effect, Rectangle dest) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Px, dest, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>切角实底填充:中带全宽 + 上下带内缩,斜边在图标尺度下读不出台阶</summary>
        internal static void FillChamfer(SpriteBatch sb, Rectangle rect, int chamfer, Color color) {
            sb.Draw(Px, new Rectangle(rect.X, rect.Y + chamfer, rect.Width, rect.Height - chamfer * 2), One, color);
            sb.Draw(Px, new Rectangle(rect.X + chamfer, rect.Y, rect.Width - chamfer * 2, chamfer), One, color);
            sb.Draw(Px, new Rectangle(rect.X + chamfer, rect.Bottom - chamfer, rect.Width - chamfer * 2, chamfer), One, color);
        }

        //着色器缺失时的降级面板:切角实底 + 顶部受光 + 底部沉影
        private static void FallbackPanel(SpriteBatch sb, Rectangle rect, float alpha, int mode, int chamfer) {
            Color body = mode == 0 ? Steel : new Color(66, 50, 26);
            FillChamfer(sb, rect, chamfer, body * (alpha * 0.96f));
            sb.Draw(Px, new Rectangle(rect.X + chamfer, rect.Y, rect.Width - chamfer * 2, 1), One,
                SteelLit * (alpha * 0.9f));
            sb.Draw(Px, new Rectangle(rect.X + chamfer, rect.Y + 1, rect.Width - chamfer * 2, 22), One,
                SteelLit * (alpha * 0.16f));
            sb.Draw(Px, new Rectangle(rect.X + chamfer, rect.Bottom - 1, rect.Width - chamfer * 2, 1), One,
                Color.Black * (alpha * 0.5f));
        }
        #endregion

        #region 机加工细部
        /// <summary>
        /// 机加工凹槽:暗沉槽底 + 上缘 1px 阴影 + 下缘 1px 受光唇。
        /// 全部紧贴,不做任何放大羽化
        /// </summary>
        internal static void DrawRecess(SpriteBatch sb, Rectangle rect, float alpha, float bedAlpha = 0.55f) {
            sb.Draw(Px, rect, One, RecessBed * (alpha * bedAlpha));
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, rect.Width, 1), One, Color.Black * (alpha * 0.55f));
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, 1, rect.Height), One, Color.Black * (alpha * 0.4f));
            sb.Draw(Px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), One, SteelLit * (alpha * 0.75f));
            sb.Draw(Px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), One, SteelLit * (alpha * 0.45f));
        }

        /// <summary>蚀刻线:1px 暗刻 + 下方 1px 微光唇,读作刻进钢面的槽而不是描边</summary>
        internal static void DrawEtchedLine(SpriteBatch sb, int x, int width, int y, float alpha, float strength = 1f) {
            if (width <= 0) {
                return;
            }
            sb.Draw(Px, new Rectangle(x, y, width, 1), One, Color.Black * (alpha * 0.5f * strength));
            sb.Draw(Px, new Rectangle(x, y + 1, width, 1), One, SteelLit * (alpha * 0.35f * strength));
        }

        /// <summary>铆钉:斜置方钉 + 左上受光点,与模块钢牌同语汇</summary>
        internal static void DrawRivet(SpriteBatch sb, Vector2 center, float alpha, float size = 3.4f) {
            sb.Draw(Px, center + new Vector2(0.8f), One, Color.Black * (alpha * 0.45f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, Brass * (alpha * 0.9f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);
            sb.Draw(Px, center - new Vector2(size * 0.22f), One, BrassBright * (alpha * 0.8f),
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(size * 0.4f), SpriteEffects.None, 0f);
        }

        /// <summary>状态指示灯:暗座圈 + 灯芯 + 小辉,唯一允许的裸辉光点缀</summary>
        internal static void DrawLamp(SpriteBatch sb, Vector2 center, Color color, float alpha, float bright) {
            sb.Draw(Px, center, One, Color.Black * (alpha * 0.6f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(7.4f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, Color.Lerp(RecessBed, color, 0.18f + bright * 0.72f) * alpha,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5.2f), SpriteEffects.None, 0f);
            SvgPathPen.SoftDot(sb, center, 7f + bright * 4f, color, alpha * (0.12f + bright * 0.3f));
        }
        #endregion

        #region 指针仪表
        //表盘弧:240°,自 150°(左下)经顶扫到 390°(右下);折线 26 段在 30px 半径下平滑
        private const float GaugeStartDeg = 150f;
        private const float GaugeSweepDeg = 240f;
        private static readonly string gaugeArcPath = BuildArcPath(GaugeStartDeg, GaugeStartDeg + GaugeSweepDeg, 26);
        private static readonly string gaugeTickPath = BuildTickPath(9, 0.86f, 1.0f);
        private static readonly string gaugeTickMajorPath = BuildTickPath(3, 0.78f, 1.0f);

        private static string BuildArcPath(float fromDeg, float toDeg, int segments) {
            StringBuilder path = new("M");
            for (int i = 0; i <= segments; i++) {
                float ang = MathHelper.ToRadians(MathHelper.Lerp(fromDeg, toDeg, i / (float)segments));
                path.Append(i == 0 ? " " : " L ");
                path.Append(MathF.Cos(ang).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                path.Append(' ');
                path.Append(MathF.Sin(ang).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
            }
            return path.ToString();
        }

        private static string BuildTickPath(int count, float inner, float outer) {
            StringBuilder path = new();
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.ToRadians(GaugeStartDeg + GaugeSweepDeg * i / (count - 1f));
                float cos = MathF.Cos(ang);
                float sin = MathF.Sin(ang);
                path.Append("M ");
                path.Append((cos * inner).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                path.Append(' ');
                path.Append((sin * inner).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                path.Append(" L ");
                path.Append((cos * outer).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                path.Append(' ');
                path.Append((sin * outer).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                path.Append(' ');
            }
            return path.ToString();
        }

        /// <summary>
        /// 指针仪表盘:蚀刻弧规 + 刻度梳 + 琥珀行程弧 + 锥形指针 + 黄铜轴帽。
        /// <paramref name="value"/> 取显示值(调用方负责缓动/微颤)
        /// </summary>
        /// <param name="dangerFrom">危险区起点 0..1,弧规上该段标红;负值不画</param>
        internal static void DrawGauge(SpriteBatch sb, Vector2 center, float radius, float value,
            Color accent, float alpha, string label, string reading, float dangerFrom = -1f) {
            value = MathHelper.Clamp(value, 0f, 1f);
            SvgPath arc = SvgPathPen.Path(gaugeArcPath);
            SvgPath ticks = SvgPathPen.Path(gaugeTickPath);
            SvgPath majors = SvgPathPen.Path(gaugeTickMajorPath);

            //表窝:凹进机壳的圆窝,用两枚旋转方料交叠凑圆,再压暗
            sb.Draw(Px, center, One, RecessBed * (alpha * 0.66f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(radius * 1.9f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, RecessBed * (alpha * 0.66f), 0f,
                new Vector2(0.5f), new Vector2(radius * 1.9f), SpriteEffects.None, 0f);

            //弧规与刻度:钢面蚀刻
            SvgPathPen.Stroke(sb, arc, center, radius, 0f, TextDim, 1.2f, alpha * 0.55f);
            //危险区:弧规末段标红
            if (dangerFrom >= 0f && dangerFrom < 1f) {
                SvgPathPen.Stroke(sb, arc, center, radius, 0f, WarnRed, 1.7f, alpha * 0.6f,
                    MathHelper.Clamp(dangerFrom, 0f, 1f), 1f);
            }
            SvgPathPen.Stroke(sb, ticks, center, radius, 0f, TextDim, 1.1f, alpha * 0.6f);
            SvgPathPen.Stroke(sb, majors, center, radius, 0f, TextMain, 1.3f, alpha * 0.7f);

            //行程弧:走到哪亮到哪,与掷骰同源的读数
            if (value > 0.004f) {
                SvgPathPen.Stroke(sb, arc, center, radius * 0.93f, 0f, accent, 2.2f, alpha * 0.85f,
                    0f, value, core: Color.Lerp(accent, Color.White, 0.4f));
            }

            //指针:暗影杆 + 主杆 + 亮芯,尾部配重
            float ang = MathHelper.ToRadians(GaugeStartDeg + GaugeSweepDeg * value);
            Vector2 dir = ang.ToRotationVector2();
            sb.Draw(Px, center + new Vector2(1f, 1.4f), One, Color.Black * (alpha * 0.4f), ang,
                new Vector2(0f, 0.5f), new Vector2(radius * 0.80f, 2.4f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, Color.Lerp(TextMain, accent, 0.35f) * alpha, ang,
                new Vector2(0f, 0.5f), new Vector2(radius * 0.80f, 2.2f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, Color.Lerp(accent, Color.White, 0.55f) * (alpha * 0.9f), ang,
                new Vector2(0f, 0.5f), new Vector2(radius * 0.74f, 1f), SpriteEffects.None, 0f);
            //尾部配重:反向一小段
            sb.Draw(Px, center, One, TextDim * (alpha * 0.8f), ang + MathHelper.Pi,
                new Vector2(0f, 0.5f), new Vector2(radius * 0.2f, 2f), SpriteEffects.None, 0f);

            //黄铜轴帽
            sb.Draw(Px, center, One, Brass * alpha, MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(5f), SpriteEffects.None, 0f);
            sb.Draw(Px, center - new Vector2(1f), One, BrassBright * (alpha * 0.85f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(2.2f), SpriteEffects.None, 0f);

            //玻璃高光:左上一小段亮弧,亮色合法
            SvgPathPen.Stroke(sb, arc, center, radius * 1.04f, 0f, Color.White, 1f, alpha * 0.12f,
                0.12f, 0.30f);

            //标签与读数
            if (!string.IsNullOrEmpty(label)) {
                Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.58f;
                Utils.DrawBorderString(sb, label, new Vector2(center.X - size.X * 0.5f, center.Y + radius * 0.52f),
                    TextDim * alpha, 0.58f);
            }
            if (!string.IsNullOrEmpty(reading)) {
                Vector2 size = FontAssets.MouseText.Value.MeasureString(reading) * 0.62f;
                Utils.DrawBorderString(sb, reading, new Vector2(center.X - size.X * 0.5f, center.Y + radius * 0.52f + 14f),
                    TextMain * alpha, 0.62f);
            }
        }
        #endregion

        #region 刻度条
        /// <summary>
        /// 刻度条:蚀刻轨 + 分段琥珀填充 + 逐 10% 刻度齿，仪表读法,不是进度条
        /// </summary>
        internal static void DrawTickBar(SpriteBatch sb, Rectangle rect, float value, Color accent, float alpha) {
            value = MathHelper.Clamp(value, 0f, 1f);
            //轨:细蚀刻槽
            int midY = rect.Center.Y;
            sb.Draw(Px, new Rectangle(rect.X, midY - 1, rect.Width, 2), One, RecessBed * (alpha * 0.8f));
            sb.Draw(Px, new Rectangle(rect.X, midY - 2, rect.Width, 1), One, Color.Black * (alpha * 0.4f));
            sb.Draw(Px, new Rectangle(rect.X, midY + 1, rect.Width, 1), One, SteelLit * (alpha * 0.4f));

            //分段填充:6px 一段 1px 缝,填到哪亮到哪
            int fillW = (int)(rect.Width * value);
            for (int x = 0; x < fillW; x += 7) {
                int w = Math.Min(6, fillW - x);
                if (w <= 0) {
                    break;
                }
                float t = x / (float)rect.Width;
                sb.Draw(Px, new Rectangle(rect.X + x, midY - 2, w, 4), One,
                    Color.Lerp(accent, BrassBright, t * 0.3f) * (alpha * 0.88f));
            }

            //刻度齿:每 10% 一齿,首中尾加高
            for (int i = 0; i <= 10; i++) {
                int x = rect.X + (int)(rect.Width * i / 10f);
                bool major = i == 0 || i == 5 || i == 10;
                int h = major ? 4 : 2;
                sb.Draw(Px, new Rectangle(x, midY + 3, 1, h), One,
                    (major ? TextDim : TextDim * 0.6f) * alpha);
            }
        }
        #endregion

        #region 模块插座
        //四角键槽:两笔短刻
        private const string KeywayPath =
            "M -1 -0.55 L -1 -1 L -0.55 -1 M 0.55 -1 L 1 -1 L 1 -0.55 "
            + "M 1 0.55 L 1 1 L 0.55 1 M -0.55 1 L -1 1 L -1 0.55";

        /// <summary>
        /// 模块插座:凹槽床 + 四角键槽刻痕 + 两侧黄铜簧片。
        /// <paramref name="deny"/> 为拒绝闪烁强度(0..1),打在键槽上
        /// </summary>
        internal static void DrawSocket(SpriteBatch sb, Rectangle rect, float alpha, float hover, float deny) {
            DrawRecess(sb, rect, alpha, 0.72f);

            //键槽刻痕
            Color key = Color.Lerp(Brass, BrassBright, hover * 0.7f);
            if (deny > 0.01f) {
                key = Color.Lerp(key, WarnRed, deny);
            }
            SvgPath keyway = SvgPathPen.Path(KeywayPath);
            SvgPathPen.Stroke(sb, keyway, rect.Center.ToVector2(), rect.Width * 0.5f - 3f, 0f,
                key, 1.3f, alpha * (0.5f + hover * 0.4f + deny * 0.5f));

            //两侧簧片记号
            int midY = rect.Center.Y;
            sb.Draw(Px, new Rectangle(rect.X + 1, midY - 4, 2, 8), One, Brass * (alpha * 0.55f));
            sb.Draw(Px, new Rectangle(rect.Right - 3, midY - 4, 2, 8), One, Brass * (alpha * 0.55f));
        }

        /// <summary>空插座的键位蚀刻:一枚旋转 45° 的小方孔记号</summary>
        internal static void DrawSocketKeyMark(SpriteBatch sb, Vector2 center, float alpha) {
            sb.Draw(Px, center, One, Color.Black * (alpha * 0.5f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(9f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, SteelLit * (alpha * 0.5f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(7f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, RecessBed * (alpha * 0.9f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(5.6f), SpriteEffects.None, 0f);
        }
        #endregion

        #region 铭牌 / 按钮 / 闩钮
        /// <summary>黄铜铭牌:小切角黄铜板(shader mode 1)+ 底缘走线,亮笔巡行由调用方驱动</summary>
        internal static void DrawNameplate(SpriteBatch sb, Rectangle rect, float alpha) {
            ShaderPanel(sb, rect, alpha, mode: 1);
            //两枚固定小钉
            DrawRivet(sb, new Vector2(rect.X + 7, rect.Center.Y), alpha, 2.2f);
            DrawRivet(sb, new Vector2(rect.Right - 7, rect.Center.Y), alpha, 2.2f);
        }

        /// <summary>铭牌标题字色:亮暖填漆(旧的暗棕蚀刻在黄铜底上对比不足,直接糊掉)</summary>
        internal static readonly Color PlateText = new(250, 234, 198);

        /// <summary>铭牌标题:居中亮暖字,黑描边把字从黄铜底上提出来</summary>
        internal static void DrawPlateTitle(SpriteBatch sb, Rectangle plate, string text, float alpha, float scale) {
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
            Utils.DrawBorderString(sb, text,
                new Vector2(plate.Center.X - size.X * 0.5f, plate.Center.Y - size.Y * 0.5f + 1),
                PlateText * alpha, scale);
        }

        /// <summary>机加工按钮:凹座 + 凸帽,按下时帽体下沉一像素</summary>
        internal static void DrawButton(SpriteBatch sb, Rectangle rect, float alpha, float hover, bool pressed, string label) {
            DrawRecess(sb, rect, alpha, 0.6f);
            Rectangle cap = rect;
            cap.Inflate(-2, -2);
            if (pressed) {
                cap.Y += 1;
            }
            sb.Draw(Px, cap, One, Color.Lerp(Steel, SteelLit, 0.5f + hover * 0.3f) * (alpha * 0.95f));
            sb.Draw(Px, new Rectangle(cap.X, cap.Y, cap.Width, 1), One,
                Color.Lerp(SteelLit, BrassBright, hover) * (alpha * 0.8f));
            sb.Draw(Px, new Rectangle(cap.X, cap.Bottom - 1, cap.Width, 1), One, Color.Black * (alpha * 0.4f));

            if (!string.IsNullOrEmpty(label)) {
                Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.6f;
                Utils.DrawBorderString(sb, label,
                    new Vector2(cap.Center.X - size.X * 0.5f, cap.Center.Y - size.Y * 0.5f + (pressed ? 1 : 0)),
                    Color.Lerp(TextMain, Amber, hover) * alpha, 0.6f);
            }
        }

        /// <summary>闩钮(关闭):圆头螺栓 + 一字槽,悬停时槽口轻旋，"拧开面板"的隐喻</summary>
        internal static void DrawLatch(SpriteBatch sb, Vector2 center, float alpha, float hover) {
            //螺头:斜置方料交叠凑圆
            sb.Draw(Px, center + new Vector2(0.8f), One, Color.Black * (alpha * 0.4f), MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(13f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, Color.Lerp(Steel, SteelLit, 0.6f) * alpha, MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(13f), SpriteEffects.None, 0f);
            sb.Draw(Px, center, One, Color.Lerp(Steel, SteelLit, 0.6f) * alpha, 0f,
                new Vector2(0.5f), new Vector2(13f), SpriteEffects.None, 0f);
            //一字槽:悬停转向 ✕ 暗示"拧开"
            float ang = MathHelper.PiOver4 + hover * MathHelper.PiOver4 * 0.6f;
            Color slot = Color.Lerp(RecessBed, WarnRed, hover * 0.65f);
            sb.Draw(Px, center, One, slot * (alpha * 0.95f), ang,
                new Vector2(0.5f), new Vector2(11f, 2.4f), SpriteEffects.None, 0f);
            sb.Draw(Px, center + new Vector2(0f, 1f), One, SteelLit * (alpha * 0.5f), ang,
                new Vector2(0.5f), new Vector2(11f, 1f), SpriteEffects.None, 0f);
        }
        #endregion

        #region 岩芯样本管
        /// <summary>
        /// 岩芯样本管外壳:黄铜端盖 + 玻璃管壁 + 慢漂玻璃高光。管内地层由调用方绘制
        /// </summary>
        internal static void DrawCoreTube(SpriteBatch sb, Rectangle rect, float alpha, float time) {
            //端盖:上下两圈黄铜箍,带滚花刻痕
            DrawTubeCap(sb, new Rectangle(rect.X - 3, rect.Y - 8, rect.Width + 6, 8), alpha);
            DrawTubeCap(sb, new Rectangle(rect.X - 3, rect.Bottom, rect.Width + 6, 8), alpha);

            //玻璃管壁:两道竖线,左壁略亮(受光)
            sb.Draw(Px, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), One, SteelLit * (alpha * 0.85f));
            sb.Draw(Px, new Rectangle(rect.Right, rect.Y, 1, rect.Height), One, Color.Black * (alpha * 0.5f));

            //玻璃高光:一道竖亮线慢漂 + 固定左缘微光
            sb.Draw(Px, new Rectangle(rect.X + 2, rect.Y + 2, 1, rect.Height - 4), One,
                Color.White * (alpha * 0.06f));
            float drift = MathF.Sin(time * 0.35f) * 0.5f + 0.5f;
            int hx = rect.X + 3 + (int)(drift * (rect.Width - 10));
            sb.Draw(Px, new Rectangle(hx, rect.Y + 2, 2, rect.Height - 4), One,
                Color.White * (alpha * 0.045f));
        }

        private static void DrawTubeCap(SpriteBatch sb, Rectangle rect, float alpha) {
            sb.Draw(Px, rect, One, Brass * (alpha * 0.8f));
            sb.Draw(Px, new Rectangle(rect.X, rect.Y, rect.Width, 1), One, BrassBright * (alpha * 0.7f));
            sb.Draw(Px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), One, Color.Black * (alpha * 0.45f));
            //滚花:逐 3px 一道细刻
            for (int x = rect.X + 2; x < rect.Right - 2; x += 3) {
                sb.Draw(Px, new Rectangle(x, rect.Y + 2, 1, rect.Height - 4), One, Color.Black * (alpha * 0.22f));
            }
        }
        #endregion

        #region 工具提示底
        /// <summary>提示牌:小切角钢牌 + 蚀刻边,取代平面矩形提示框</summary>
        internal static void DrawTooltipPlate(SpriteBatch sb, Rectangle rect, float alpha) {
            FillChamfer(sb, rect, 4, new Color(18, 15, 12) * (alpha * 0.96f));
            sb.Draw(Px, new Rectangle(rect.X + 4, rect.Y, rect.Width - 8, 1), One, Brass * (alpha * 0.6f));
            sb.Draw(Px, new Rectangle(rect.X + 4, rect.Bottom - 1, rect.Width - 8, 1), One, Color.Black * (alpha * 0.5f));
            sb.Draw(Px, new Rectangle(rect.X, rect.Y + 4, 1, rect.Height - 8), One, Brass * (alpha * 0.4f));
        }
        #endregion
    }
}
