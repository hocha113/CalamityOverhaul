using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Onikiri
{
    /// <summary>鬼切叙事皮肤的静态绘制件:面板背景(shader/CPU 降级)、纸垂、朱印、刀痕笔触</summary>
    internal static class OnikiriPanelDraw
    {
        /// <summary>面板背景:阴影 + OniNarrativePanel.fx;shader 缺失时走 CPU 降级</summary>
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, OnikiriPanelState state) {
            //阴影按 alpha 平方衰减:拔刀揭示还只是一条线时不能先出现整块暗影
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, new Color(8, 2, 5) * (alpha * alpha * 0.62f), 6, 8);

            if (!OniShaderPanel.Available) {
                DrawFallbackPanel(spriteBatch, rect, alpha);
                return;
            }

            //reveal 直接吃面板开合进度;面板体不透明度快速上斜,避免"半透明面板"长时间存在
            float body = Math.Min(1f, alpha * 1.6f);
            OniShaderPanel.Draw(spriteBatch, rect, body, alpha, state.ShaderTime, OnikiriPanelState.ShaderEdgePad, Color.White);
        }

        /// <summary>CPU 降级面板:墨黑底 + 深红双描边 + 顶沿绸线残影,保证无 shader 时依然成立</summary>
        public static void DrawFallbackPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            spriteBatch.Draw(pixel, rect, src, OnikiriPanelState.Ink * (alpha * 0.96f));
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect, OnikiriPanelState.Deep * (alpha * 0.58f), 2);
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            SkinDrawUtil.DrawRectBorder(spriteBatch, inner, OnikiriPanelState.Dark * (alpha * 0.85f), 1);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + 8, rect.Y - 4, rect.Width - 16, 3), src, OnikiriPanelState.Deep * (alpha * 0.5f));
        }

        /// <summary>
        /// 纸垂:两条白纸之字形垂片挂在顶沿注连墨绸上。
        /// 落点与 shader 绸带的中央下垂公式同源(sin(πu)*3.4),纸垂长度只吃边沿带,不进正文区
        /// </summary>
        public static void DrawShide(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer) {
            DrawSingleShide(spriteBatch, rect, 0.10f, 15f, alpha, swayTimer, 0f);
            DrawSingleShide(spriteBatch, rect, 0.78f, 18f, alpha * 0.92f, swayTimer, 2.1f);
        }

        private static void DrawSingleShide(SpriteBatch sb, Rectangle rect, float u, float length, float alpha, float swayTimer, float phase) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float sag = (float)Math.Sin(u * Math.PI) * 3.4f;
            Vector2 anchor = new(rect.X + rect.Width * u, rect.Y - 6f + sag);
            float sway = (float)Math.Sin(swayTimer * 1.5f + phase) * 0.085f;

            //绳结
            sb.Draw(pixel, anchor, src, OnikiriPanelState.Deep * (alpha * 0.9f), sway * 0.5f + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.2f, 4.2f), SpriteEffects.None, 0f);

            //三段之字折纸,摆角向纸尾递增(钟摆感)
            Vector2 pos = anchor + new Vector2(0f, 1.5f);
            float segLen = length / 3f;
            const float zig = 0.46f;
            for (int i = 0; i < 3; i++) {
                float lean = (i % 2 == 0 ? zig : -zig) * 0.9f;
                float rot = MathHelper.PiOver2 + lean + sway * (0.5f + i * 0.45f);
                Vector2 dir = rot.ToRotationVector2();
                Vector2 size = new(segLen + 1.2f, 4.6f - i * 0.5f);
                sb.Draw(pixel, pos + new Vector2(0.8f, 0.8f), src, OnikiriPanelState.Dark * (alpha * 0.45f), rot, new Vector2(0f, 0.5f), size, SpriteEffects.None, 0f);
                sb.Draw(pixel, pos, src, OnikiriPanelState.Paper * (alpha * 0.85f), rot, new Vector2(0f, 0.5f), size, SpriteEffects.None, 0f);
                pos += dir * segLen * 0.9f;
            }
        }

        /// <summary>朱印方章:阴影/深红衬底/朱红章体/纸白刻痕(简化印文)。rotation 供盖章动画用</summary>
        public static void DrawSealGlyph(SpriteBatch spriteBatch, Vector2 center, float size, float alpha, float rotation = 0f) {
            if (size < 1f || alpha <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);

            spriteBatch.Draw(pixel, center + new Vector2(1f, 1.4f), src, OnikiriPanelState.Dark * (alpha * 0.6f), rotation, half, new Vector2(size), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center, src, OnikiriPanelState.Deep * (alpha * 0.95f), rotation, half, new Vector2(size + 2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center, src, OnikiriPanelState.Seal * alpha, rotation, half, new Vector2(size), SpriteEffects.None, 0f);

            //刻痕:一横一竖一点,偏移随章体一起旋转
            Color carve = OnikiriPanelState.Paper * (alpha * 0.92f);
            Vector2 hOff = new Vector2(0f, -size * 0.24f).RotatedBy(rotation);
            Vector2 vOff = new Vector2(-size * 0.08f, size * 0.10f).RotatedBy(rotation);
            Vector2 dOff = new Vector2(size * 0.24f, size * 0.24f).RotatedBy(rotation);
            spriteBatch.Draw(pixel, center + hOff, src, carve, rotation, half, new Vector2(size * 0.54f, 1.6f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center + vOff, src, carve, rotation, half, new Vector2(1.6f, size * 0.46f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, center + dOff, src, carve * 0.9f, rotation, half, new Vector2(2.1f, 2.1f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 刀痕笔触:两端收尖、中段最宽、带轻微上弓的渐变笔画,底色深红、前段叠白热芯。
        /// 分隔线与选项扫线共用;sweep 取 0~1 截断绘制长度(hover 扫入动画)
        /// </summary>
        public static void DrawTaperedSlash(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float maxThick, float bow, float alpha, float sweep = 1f) {
            Vector2 edge = end - start;
            float fullLen = edge.Length();
            if (fullLen < 2f || alpha <= 0.01f || sweep <= 0.02f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 dir = edge / fullLen;
            Vector2 perp = new(dir.Y, -dir.X);
            float len = fullLen * MathHelper.Clamp(sweep, 0f, 1f);
            const int Seg = 14;
            float segLen = len / Seg;
            float rot = dir.ToRotation();

            for (int i = 0; i < Seg; i++) {
                float tm = (i + 0.5f) / Seg;
                //形状参数按完整长度归一,截断只影响画到哪(扫线时笔锋在前沿)
                float tShape = tm * MathHelper.Clamp(sweep, 0f, 1f);
                float profile = (float)Math.Pow(Math.Sin(tShape * Math.PI), 0.62);
                float thick = maxThick * Math.Max(profile, 0.12f);
                Vector2 pos = start + dir * (segLen * i) + perp * ((float)Math.Sin(tShape * Math.PI) * bow);
                Color col = Color.Lerp(OnikiriPanelState.Dark, OnikiriPanelState.Bright, profile) * alpha;
                spriteBatch.Draw(pixel, pos, src, col, rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.7f, thick), SpriteEffects.None, 0f);

                //前 45% 叠一条更细的白热芯,像刚划开还没冷却的部分
                if (tShape > 0.04f && tShape < 0.45f) {
                    float core = (float)Math.Sin((tShape - 0.04f) / 0.41f * Math.PI);
                    spriteBatch.Draw(pixel, pos, src, OnikiriPanelState.HotWhite * (alpha * 0.75f * core), rot, new Vector2(0f, 0.5f), new Vector2(segLen + 0.7f, thick * 0.4f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>四角朱笔角签:短促的收笔笔触压住面板四角(弹窗用)</summary>
        public static void DrawCornerTicks(SpriteBatch spriteBatch, Rectangle rect, float alpha, float pulse) {
            float a = alpha * (0.55f + pulse * 0.2f);
            const float len = 12f;
            const int inset = 4;
            DrawTaperedSlash(spriteBatch, new Vector2(rect.X + inset, rect.Y + inset + 1), new Vector2(rect.X + inset + len, rect.Y + inset + 1), 1.7f, 0.6f, a);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.X + inset + 1, rect.Y + inset), new Vector2(rect.X + inset + 1, rect.Y + inset + len), 1.7f, 0.6f, a);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.Right - inset - len, rect.Bottom - inset - 1), new Vector2(rect.Right - inset, rect.Bottom - inset - 1), 1.7f, 0.6f, a * 0.85f);
            DrawTaperedSlash(spriteBatch, new Vector2(rect.Right - inset - 1, rect.Bottom - inset - len), new Vector2(rect.Right - inset - 1, rect.Bottom - inset), 1.7f, 0.6f, a * 0.85f);
        }

        /// <summary>绘马挂绳:两根斜绳收到顶结,结下垂一缕随摆的流苏(弹窗用)</summary>
        public static void DrawHangingKnot(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer) {
            Vector2 knot = new(rect.Center.X, rect.Y - 15f);
            Color rope = OnikiriPanelState.Deep * (alpha * 0.8f);
            Color ropeFade = OnikiriPanelState.Dark * (alpha * 0.25f);
            SkinDrawUtil.DrawGradientLine(spriteBatch, new Vector2(rect.X + 14f, rect.Y + 1f), knot, ropeFade, rope, 1.4f);
            SkinDrawUtil.DrawGradientLine(spriteBatch, new Vector2(rect.Right - 14f, rect.Y + 1f), knot, ropeFade, rope, 1.4f);

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            spriteBatch.Draw(pixel, knot, src, OnikiriPanelState.Seal * alpha, MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);

            float sway = (float)Math.Sin(swayTimer * 2.4f) * 0.22f;
            float tasselRot = MathHelper.PiOver2 + sway;
            Vector2 tasselEnd = knot + tasselRot.ToRotationVector2() * 10f;
            SkinDrawUtil.DrawGradientLine(spriteBatch, knot, tasselEnd, OnikiriPanelState.Bright * (alpha * 0.75f), OnikiriPanelState.Deep * (alpha * 0.1f), 1.6f);
        }
    }
}
