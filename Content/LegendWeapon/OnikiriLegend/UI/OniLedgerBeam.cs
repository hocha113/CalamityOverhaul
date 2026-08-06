using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 两屏共用的顶梁:一根贴屏顶的黑漆横梁,梁上钉着两块驿牌(点鬼簿/改铭台),
    /// 牌下各出一枚铁钩——本屏器物的钩空着(绳收成一圈,"已取下在案"),
    /// 对面器物挂在另一钩上作切换门。梁不随换乘滑移,是"同一夜屋"的持续骨架
    /// </summary>
    internal static class OniLedgerBeam
    {
        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        /// <summary>梁身高(屏顶到梁下缘)</summary>
        public const float Height = 13f;
        /// <summary>驿牌中心 Y(骑在梁上,下缘略探出梁底)</summary>
        private const float BoardCenterY = 12f;
        /// <summary>钩梢 Y(挂绳锚;物件与收绳圈都从这里垂下)</summary>
        private const float HookTipY = 30f;

        /// <summary>空钩收绳圈:两匝松绕,静即是空</summary>
        private const string CoilData =
            "M 0.02 -1 C 0.5 -0.62 0.62 -0.12 0.06 0.08 C -0.66 0.32 -0.7 0.9 0.02 0.86 C 0.56 0.82 0.5 0.4 -0.04 0.44";

        /// <summary>卷轴钩横位(点鬼簿之门/之家)</summary>
        public static float ScrollHookX => OnikiriUITheme.UIScreenW * OnikiriUITheme.MeiHangLeftXRatio;

        /// <summary>太刀钩横位:名义在卷轴钩东侧一档,窄屏夹在点鬼簿卷纸左缘外</summary>
        public static float TachiHookX {
            get {
                float x = Math.Min(ScrollHookX + OnikiriUITheme.BeamHookGap, RegisterScrollLeft() - 52f);
                return Math.Max(x, ScrollHookX + 44f);
            }
        }

        /// <summary>点鬼簿卷轴左缘(与 OniRegisterUI.LayoutCompute 同式,两屏算出同一根梁)</summary>
        private static float RegisterScrollLeft() {
            float sw = OnikiriUITheme.UIScreenW;
            float scrollW = Math.Min(OnikiriUITheme.ScrollMaxWidth, sw * OnikiriUITheme.ScrollWidthRatio);
            scrollW = Math.Max(scrollW, Math.Min(340f, sw * 0.5f));
            return sw * OnikiriUITheme.ScrollCenterXRatio - scrollW * 0.5f;
        }

        private static Vector2 HookTip(float x) => new(x, HookTipY);

        /// <summary>本屏切换门的挂绳锚(挂着对面器物)</summary>
        public static Vector2 DoorAnchor(OniLedgerView current)
            => HookTip(current == OniLedgerView.Mei ? ScrollHookX : TachiHookX);

        /// <summary>本屏器物的空钩锚(它已被取下在案,钩上只剩收绳圈)</summary>
        public static Vector2 VacantAnchor(OniLedgerView current)
            => HookTip(current == OniLedgerView.Mei ? TachiHookX : ScrollHookX);

        /// <summary>
        /// 门侧驿牌(+钩颈)命中区。梁上牌文与垂挂器物同属一扇门,并进热区
        /// </summary>
        public static Rectangle DoorBoardHit(OniLedgerView current) {
            float x = current == OniLedgerView.Mei ? ScrollHookX : TachiHookX;
            Vector2 size = OnikiriUITheme.BeamBoardSize;
            float top = BoardCenterY - size.Y * 0.5f - 2f;
            float bottom = HookTipY + 6f;
            return new Rectangle(
                (int)(x - size.X * 0.5f - 6f),
                (int)top,
                (int)(size.X + 12f),
                Math.Max(1, (int)(bottom - top)));
        }

        /// <summary>
        /// 画整根梁+两块驿牌+双钩;current 的钩空置(收绳圈+现驻朱点),
        /// 对面钩由调用方把门挂物画在 <see cref="DoorAnchor"/> 之下
        /// </summary>
        public static void Draw(SpriteBatch sb, float alpha, float time, OniLedgerView current, float doorHover) {
            if (alpha <= 0.01f) {
                return;
            }
            float sw = OnikiriUITheme.UIScreenW;
            //开屏落梁:整梁自上压下一线
            float drop = -(1f - alpha) * 8f;
            int y0 = (int)drop;

            //梁下投影(紧贴两段,不做同心扩层)
            sb.Draw(Pixel, new Rectangle(-1, y0 + (int)Height, (int)sw + 2, 2), PixelSrc,
                new Color(8, 2, 5) * (alpha * 0.4f));
            sb.Draw(Pixel, new Rectangle(-1, y0 + (int)Height + 2, (int)sw + 2, 1), PixelSrc,
                new Color(8, 2, 5) * (alpha * 0.18f));

            //梁身:上亮下沉的黑漆
            int h2 = (int)(Height * 0.5f);
            sb.Draw(Pixel, new Rectangle(-1, y0, (int)sw + 2, h2), PixelSrc,
                Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.42f) * (alpha * 0.97f));
            sb.Draw(Pixel, new Rectangle(-1, y0 + h2, (int)sw + 2, (int)Height - h2), PixelSrc,
                Color.Lerp(OnikiriUITheme.Ink, OnikiriUITheme.Dark, 0.55f) * (alpha * 0.97f));
            sb.Draw(Pixel, new Rectangle(-1, y0, (int)sw + 2, 1), PixelSrc,
                OnikiriUITheme.Paper * (alpha * 0.05f));

            //漆下木理:顺梁走向的几截淡纹
            for (int i = 0; i < 8; i++) {
                float u = OniBrush.Hash01(i * 53 + 7);
                float gy = 2f + OniBrush.Hash01(i * 29 + 3) * (Height - 4f);
                float len = 46f + OniBrush.Hash01(i * 91 + 17) * 150f;
                sb.Draw(Pixel, new Vector2(u * sw, y0 + gy), PixelSrc, Color.Black * (alpha * 0.20f),
                    0f, new Vector2(0f, 0.5f), new Vector2(len, 1f), SpriteEffects.None, 0f);
            }

            //下缘金压线,内衬一线绯红(与台账黑漆板同语)
            sb.Draw(Pixel, new Rectangle(-1, y0 + (int)Height - 2, (int)sw + 2, 1), PixelSrc,
                OnikiriUITheme.GoldDeep * (alpha * 0.5f));
            sb.Draw(Pixel, new Rectangle(-1, y0 + (int)Height - 1, (int)sw + 2, 1), PixelSrc,
                OnikiriUITheme.Deep * (alpha * 0.32f));

            //====两块驿牌:卷轴钩挂「点鬼簿」,太刀钩挂「改铭台」====
            bool onMei = current == OniLedgerView.Mei;
            string scrollLabel = OniMeiUI.RegisterTabText?.Value ?? "";
            string tachiLabel = OniRegisterUI.MeiTabText?.Value ?? "";
            //door 牌 = 对面驿站(可点);current 牌 = 现驻(器物已取下,钩空)
            DrawBoard(sb, ScrollHookX, drop, scrollLabel, isCurrent: !onMei,
                hover: onMei ? doorHover : 0f, alpha, time);
            DrawBoard(sb, TachiHookX, drop, tachiLabel, isCurrent: onMei,
                hover: onMei ? 0f : doorHover, alpha, time);

            //现驻空钩:收绳圈
            DrawVacantCoil(sb, VacantAnchor(current) + new Vector2(0f, drop), alpha, time);
        }

        /// <summary>一块驿牌+牌下铁钩;isCurrent=现驻(左侧压现驻朱点),hover=门牌悬停点亮</summary>
        private static void DrawBoard(SpriteBatch sb, float x, float drop, string label,
            bool isCurrent, float hover, float alpha, float time) {
            Vector2 size = OnikiriUITheme.BeamBoardSize;
            Vector2 center = new(x, BoardCenterY + drop);
            Vector2 half = new(0.5f);

            //牌影/牌缘/牌体(漆木,与收卷牌同料)
            sb.Draw(Pixel, center + new Vector2(1.4f, 1.8f), PixelSrc, new Color(8, 2, 5) * (alpha * 0.5f),
                0f, half, size, SpriteEffects.None, 0f);
            sb.Draw(Pixel, center, PixelSrc,
                OnikiriUITheme.Deep * (alpha * (0.5f + hover * 0.35f)),
                0f, half, size + new Vector2(2.6f, 2.6f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, center, PixelSrc,
                Color.Lerp(new Color(52, 18, 16), new Color(74, 26, 22), hover) * (alpha * 0.97f),
                0f, half, size, SpriteEffects.None, 0f);
            //两枚钉帽
            foreach (float nx in new[] { -size.X * 0.5f + 5f, size.X * 0.5f - 5f }) {
                sb.Draw(Pixel, center + new Vector2(nx, 0f), PixelSrc,
                    OnikiriUITheme.GoldDeep * (alpha * 0.75f), MathHelper.PiOver4, half,
                    new Vector2(2.2f), SpriteEffects.None, 0f);
            }

            //牌文:横书,量宽收字号;现驻牌左压朱点
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            if (!string.IsNullOrEmpty(label)) {
                float scale = 0.6f;
                Vector2 measured = font.MeasureString(label);
                float maxW = size.X - (isCurrent ? 22f : 14f);
                if (measured.X * scale > maxW) {
                    scale = maxW / measured.X;
                }
                Color textCol = isCurrent
                    ? OnikiriUITheme.Paper * (alpha * 0.92f)
                    : Color.Lerp(OnikiriUITheme.TextDim, OnikiriUITheme.HotWhite, hover) * (alpha * (0.8f + hover * 0.2f));
                Vector2 tSize = measured * scale;
                float textX = center.X - tSize.X * 0.5f + (isCurrent ? 5f : 0f);
                Utils.DrawBorderString(sb, label, new Vector2(textX, center.Y - tSize.Y * 0.5f), textCol, scale);
                if (isCurrent) {
                    OniBrush.DrawSoftDot(sb, new Vector2(textX - 7f, center.Y), 2.6f,
                        OnikiriUITheme.Seal, alpha * 0.9f);
                }
            }
            //门牌悬停:牌底缘一线亮
            if (hover > 0.03f) {
                sb.Draw(Pixel, center + new Vector2(0f, size.Y * 0.5f - 1f), PixelSrc,
                    OnikiriUITheme.Bright * (alpha * 0.5f * hover), 0f, half,
                    new Vector2(size.X - 6f, 1f), SpriteEffects.None, 0f);
            }

            //牌下铁钩:短杆+小卷唇+一点受光
            Vector2 stubTop = center + new Vector2(0f, size.Y * 0.5f);
            sb.Draw(Pixel, stubTop, PixelSrc, Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.2f) * (alpha * 0.95f),
                0f, new Vector2(0.5f, 0f), new Vector2(2.2f, HookTipY - drop - stubTop.Y + 1f), SpriteEffects.None, 0f);
            Vector2 tip = new(x, HookTipY + drop);
            sb.Draw(Pixel, tip, PixelSrc, Color.Lerp(OnikiriUITheme.Ink, Color.Black, 0.2f) * (alpha * 0.95f),
                0.85f, new Vector2(0f, 0.5f), new Vector2(4.2f, 2f), SpriteEffects.None, 0f);
            sb.Draw(Pixel, tip + new Vector2(-0.8f, -0.8f), PixelSrc, OnikiriUITheme.Paper * (alpha * 0.22f),
                0f, half, new Vector2(1.2f), SpriteEffects.None, 0f);
        }

        /// <summary>空钩上的收绳圈:器物取下在案,绳松松绕成两匝——一眼读出"现驻此屏"</summary>
        private static void DrawVacantCoil(SpriteBatch sb, Vector2 tip, float alpha, float time) {
            SvgPath coil = SvgPathPen.Path(CoilData);
            if (coil == null) {
                return;
            }
            Vector2 center = tip + new Vector2(0.4f, 9f);
            //极缓的呼吸摆,静物不死
            float sway = (float)Math.Sin(time * 0.7f) * 0.02f;
            SvgPathPen.Stroke(sb, coil, center + new Vector2(1f, 1.4f), 8.5f, sway,
                new Color(8, 2, 5), 1.8f, alpha * 0.4f);
            SvgPathPen.Stroke(sb, coil, center, 8.5f, sway,
                OnikiriUITheme.Deep, 1.7f, alpha * 0.85f);
            //绳梢一小截垂尾
            OniBrush.DrawGradientLine(sb, center + new Vector2(-0.4f, 7.4f), center + new Vector2(-1.6f, 13f),
                OnikiriUITheme.Deep * (alpha * 0.7f), OnikiriUITheme.Dark * (alpha * 0.1f), 1.3f);
        }
    }
}
