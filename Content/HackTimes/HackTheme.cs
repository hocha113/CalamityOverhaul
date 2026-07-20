using CalamityOverhaul.Content.HackTimes.Scannables;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间主题：红青双主色体系 + 共享几何绘制辅助</summary>
    internal static class HackTheme
    {
        #region 深色基底（不随敌我切换）

        public static readonly Color BgDarkest = new(6, 8, 12);
        public static readonly Color BgPanel = new(10, 14, 20);
        public static readonly Color BgSection = new(14, 18, 26);
        public static readonly Color BgSlot = new(16, 22, 30);
        public static readonly Color BgSlotHover = new(20, 30, 40);
        public static readonly Color InnerShadow = new(3, 5, 8);
        public static readonly Color GridLine = new(18, 28, 35);

        //警告色
        public static readonly Color Danger = new(220, 45, 45);
        //上传中色
        public static readonly Color Uploading = new(200, 170, 40);
        //蔓延色
        public static readonly Color Contagion = new(160, 40, 200);

        //文字层级
        public static readonly Color TextDim = new(70, 85, 95);
        public static readonly Color TextNormal = new(140, 160, 170);
        public static readonly Color TextBright = new(210, 225, 230);

        //进度条（义体侧资源，恒定青）
        public static readonly Color ProgressBg = new(12, 16, 22);
        public static readonly Color ProgressFill = new(0, 190, 200);
        public static readonly Color ProgressGlow = new(40, 220, 230);

        //义体OS专属青，RAM 弧等玩家侧资源与 HackRamArc.fx 固定调色对齐
        public static readonly Color DeckAccent = new(0, 200, 210);
        public static readonly Color DeckBorder = new(35, 50, 60);
        public static readonly Color DeckBorderBright = new(50, 70, 80);

        #endregion

        #region 敌我双主色

        /// <summary>敌对混合度 0..1，选中敌对目标时框架色滑向扫描仪红</summary>
        public static float HostileBlend { get; private set; }

        //中立态（义体OS青）
        private static readonly Color NeutralAccent = new(0, 200, 210);
        private static readonly Color NeutralAccentAlt = new(40, 180, 160);
        private static readonly Color NeutralBorder = new(35, 50, 60);
        private static readonly Color NeutralBorderBright = new(50, 70, 80);
        private static readonly Color NeutralEdgeGlow = new(30, 200, 210);

        //敌对态（扫描仪红）
        private static readonly Color HostileAccentColor = new(230, 56, 68);
        private static readonly Color HostileAccentAltColor = new(255, 122, 92);
        private static readonly Color HostileBorderColor = new(86, 32, 40);
        private static readonly Color HostileBorderBrightColor = new(126, 46, 56);
        private static readonly Color HostileEdgeGlowColor = new(235, 70, 82);

        /// <summary>主强调色，随目标敌我在青红间过渡</summary>
        public static Color Accent => Color.Lerp(NeutralAccent, HostileAccentColor, HostileBlend);
        /// <summary>副强调色</summary>
        public static Color AccentAlt => Color.Lerp(NeutralAccentAlt, HostileAccentAltColor, HostileBlend);
        /// <summary>边框</summary>
        public static Color Border => Color.Lerp(NeutralBorder, HostileBorderColor, HostileBlend);
        /// <summary>亮边框</summary>
        public static Color BorderBright => Color.Lerp(NeutralBorderBright, HostileBorderBrightColor, HostileBlend);
        /// <summary>边缘辉光</summary>
        public static Color EdgeGlow => Color.Lerp(NeutralEdgeGlow, HostileEdgeGlowColor, HostileBlend);

        /// <summary>每帧推进敌我混合度，由 HackTimeUI.Update 驱动</summary>
        public static void UpdateProfile() {
            float target = EvaluateHostile(HackTime.CurrentScanTarget) ? 1f : 0f;
            HostileBlend = MathHelper.Lerp(HostileBlend, target, 0.09f);
            if (HostileBlend < 0.002f) HostileBlend = 0f;
            else if (HostileBlend > 0.998f) HostileBlend = 1f;
        }

        /// <summary>目标是否敌对，决定框架色相</summary>
        public static bool EvaluateHostile(IScannable target) {
            if (target is NpcScannable n && n.IsValid) {
                NPC npc = Main.npc[n.NpcIndex];
                if (npc.boss) return true;
                if (npc.townNPC || npc.friendly || npc.CountsAsACritter) return false;
                return npc.damage > 0 || npc.lifeMax > 5000;
            }
            return target is WraithScannable || target is IHackableTurret;
        }

        #endregion

        #region 类别辅助

        public static Color CategoryColor(QuickHackCategory cat) => cat switch {
            QuickHackCategory.Lethal => Danger,
            QuickHackCategory.Control => Uploading,
            QuickHackCategory.Covert => AccentAlt,
            QuickHackCategory.Contagion => Contagion,
            QuickHackCategory.TileManip => new Color(80, 200, 255),
            QuickHackCategory.Paranormal => new Color(180, 60, 220),
            _ => Accent,
        };

        public static string CategorySymbol(QuickHackCategory cat) => cat switch {
            QuickHackCategory.Lethal => "◆",
            QuickHackCategory.Control => "◇",
            QuickHackCategory.Covert => "○",
            QuickHackCategory.Contagion => "◎",
            QuickHackCategory.TileManip => "▣",
            QuickHackCategory.Paranormal => "☠",
            _ => "●",
        };

        public static string CategoryLabel(QuickHackCategory cat) => cat switch {
            QuickHackCategory.Lethal => HackTime.CatLethal.Value,
            QuickHackCategory.Control => HackTime.CatControl.Value,
            QuickHackCategory.Covert => HackTime.CatCovert.Value,
            QuickHackCategory.Contagion => HackTime.CatContagion.Value,
            QuickHackCategory.TileManip => HackTime.CatTileManip.Value,
            QuickHackCategory.Paranormal => HackTime.CatParanormal.Value,
            _ => HackTime.CatUnknown.Value,
        };

        #endregion

        #region 共享绘制辅助

        /// <summary>1px 白色像素纹理，可能为 null</summary>
        public static Texture2D Pixel => VaultAsset.placeholder2?.Value;
        /// <summary>Pixel 的 1px 源矩形</summary>
        public static readonly Rectangle SrcPixel = new(0, 0, 1, 1);

        public static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            Texture2D px = Pixel;
            if (px == null) return;
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) return;
            sb.Draw(px, start, SrcPixel, color, diff.ToRotation(),
                Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>虚线段，dashLen 实段长 gapLen 空隙长</summary>
        public static void DrawDashedLine(SpriteBatch sb, Vector2 start, Vector2 end,
            float thickness, Color color, float dashLen = 5f, float gapLen = 4f) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.5f) return;
            Vector2 dir = diff / length;
            float step = dashLen + gapLen;
            for (float d = 0; d < length; d += step) {
                float segEnd = Math.Min(d + dashLen, length);
                DrawLine(sb, start + dir * d, start + dir * segEnd, thickness, color);
            }
        }

        /// <summary>旋转 45° 的实心菱形</summary>
        public static void DrawDiamond(SpriteBatch sb, Vector2 center, float size, Color color) {
            Texture2D px = Pixel;
            if (px == null) return;
            sb.Draw(px, center, SrcPixel, color, MathHelper.PiOver4,
                new Vector2(0.5f), size, SpriteEffects.None, 0f);
        }

        /// <summary>旋转菱形描边（四段线）</summary>
        public static void DrawDiamondOutline(SpriteBatch sb, Vector2 center, float radius, float thickness, Color color) {
            Vector2 t = center + new Vector2(0, -radius);
            Vector2 r = center + new Vector2(radius, 0);
            Vector2 b = center + new Vector2(0, radius);
            Vector2 l = center + new Vector2(-radius, 0);
            DrawLine(sb, t, r, thickness, color);
            DrawLine(sb, r, b, thickness, color);
            DrawLine(sb, b, l, thickness, color);
            DrawLine(sb, l, t, thickness, color);
        }

        /// <summary>矩形区域内的斜线剖面纹，机械感禁用态覆盖</summary>
        public static void DrawHatch(SpriteBatch sb, Rectangle rect, float step, Color color) {
            for (float d = -rect.Height; d < rect.Width; d += step) {
                float x0 = rect.X + d;
                float y0 = (float)rect.Bottom;
                float x1 = rect.X + d + rect.Height;
                float y1 = (float)rect.Y;
                //裁剪到矩形横向范围
                if (x0 < rect.X) {
                    float cut = rect.X - x0;
                    x0 = rect.X;
                    y0 -= cut;
                }
                if (x1 > rect.Right) {
                    float cut = x1 - rect.Right;
                    x1 = rect.Right;
                    y1 += cut;
                }
                if (x1 <= x0) continue;
                DrawLine(sb, new Vector2(x0, y0), new Vector2(x1, y1), 1f, color);
            }
        }

        /// <summary>燕尾旗填充：矩形主体 + 左/右端斜切（正值为切掉的横向宽度）</summary>
        public static void DrawPennantFill(SpriteBatch sb, Rectangle rect, float taperLeft, float taperRight, Color color) {
            Texture2D px = Pixel;
            if (px == null) return;
            for (int dy = 0; dy < rect.Height; dy++) {
                float t = (float)dy / rect.Height;
                //左端上宽下窄、右端上窄下宽的斜切
                int cutL = (int)(taperLeft * (1f - t));
                int cutR = (int)(taperRight * t);
                int x = rect.X + cutL;
                int w = rect.Width - cutL - cutR;
                if (w <= 0) continue;
                sb.Draw(px, new Rectangle(x, rect.Y + dy, w, 1), SrcPixel, color);
            }
        }

        /// <summary>
        /// 无描边文字。深底上的弱化/装饰文本用——<see cref="Utils.DrawBorderString"/> 的黑描边
        /// 在低亮度填充下会把小字糊成黑块，此路径让文字随透明度干净淡出
        /// </summary>
        public static void DrawRawText(SpriteBatch sb, string text, Vector2 pos, Color color, float scale) {
            if (string.IsNullOrEmpty(text)) return;
            sb.DrawString(FontAssets.MouseText.Value, text, new Vector2((int)pos.X, (int)pos.Y),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>微型状态徽章：细描边小框 + 文字，返回徽章像素宽</summary>
        public static float DrawBadge(SpriteBatch sb, Vector2 pos, string text, Color color, float alpha, float fontScale = 0.58f) {
            Texture2D px = Pixel;
            if (px == null || string.IsNullOrEmpty(text)) return 0f;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * fontScale;
            int padX = 5;
            int w = (int)size.X + padX * 2;
            int h = (int)size.Y + 2;
            Rectangle box = new((int)pos.X, (int)pos.Y, w, h);
            sb.Draw(px, box, SrcPixel, color * (alpha * 0.12f));
            //只描上下两条细边，保持开放感
            sb.Draw(px, new Rectangle(box.X, box.Y, w, 1), SrcPixel, color * (alpha * 0.50f));
            sb.Draw(px, new Rectangle(box.X, box.Bottom - 1, w, 1), SrcPixel, color * (alpha * 0.35f));
            Utils.DrawBorderString(sb, text, new Vector2((int)(pos.X + padX), (int)pos.Y), color * alpha, fontScale);
            return w;
        }

        /// <summary>L 形角标</summary>
        public static void DrawCornerBracket(SpriteBatch sb, Vector2 corner, int dirX, int dirY, int arm, float thickness, Color color) {
            Texture2D px = Pixel;
            if (px == null) return;
            int t = Math.Max(1, (int)thickness);
            int hx = dirX > 0 ? (int)corner.X : (int)corner.X - arm;
            sb.Draw(px, new Rectangle(hx, (int)corner.Y - (dirY < 0 ? t - 1 : 0), arm, t), SrcPixel, color);
            int vy = dirY > 0 ? (int)corner.Y : (int)corner.Y - arm;
            sb.Draw(px, new Rectangle((int)corner.X - (dirX < 0 ? t - 1 : 0), vy, t, arm), SrcPixel, color);
        }

        /// <summary>CRT 水平暗纹，每 3px 一条</summary>
        public static void DrawCRTOverlay(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = Pixel;
            if (px == null) return;
            Color line = BgDarkest * alpha;
            for (int dy = 0; dy < rect.Height; dy += 3) {
                sb.Draw(px, new Rectangle(rect.X, rect.Y + dy, rect.Width, 1), SrcPixel, line);
            }
        }

        public static float EaseOutCubic(float t) {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        #endregion
    }
}
