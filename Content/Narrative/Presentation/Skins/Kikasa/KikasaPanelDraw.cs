using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Common;
using CalamityOverhaul.Content.UIs.UIEffect;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Kikasa
{
    /// <summary>鬼雨水系笔触:涟漪/波光/垂滴/伞章,shader 面板见 <see cref="KikasaShaderPanel"/></summary>
    internal static class KikasaPanelDraw
    {
        /// <summary>阴影 + KikasaNarrativePanel,无 shader 走 CPU</summary>
        public static void DrawShaderBackground(SpriteBatch spriteBatch, Rectangle rect, float alpha, KikasaPanelState state) {
            //阴影按 alpha²,揭示期不抢戏
            SkinDrawUtil.DrawPanelShadow(spriteBatch, rect, new Color(3, 6, 8) * (alpha * alpha * 0.60f), 6, 8);

            if (!KikasaShaderPanel.Available) {
                DrawFallbackPanel(spriteBatch, rect, alpha);
                return;
            }

            //reveal 跟开合,体不透明度快上斜
            float body = Math.Min(1f, alpha * 1.6f);
            KikasaShaderPanel.Draw(spriteBatch, rect, body, alpha, state.ShaderTime, KikasaPanelState.ShaderEdgePad, Color.White);
        }

        /// <summary>CPU 降级,湿墨底双描边 + 底缘积水线</summary>
        public static void DrawFallbackPanel(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            spriteBatch.Draw(pixel, rect, src, KikasaStoryTheme.PanelBg * (alpha * 0.96f));
            SkinDrawUtil.DrawRectBorder(spriteBatch, rect, KikasaPanelState.Rain * (alpha * 0.42f), 2);
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            SkinDrawUtil.DrawRectBorder(spriteBatch, inner, KikasaPanelState.Deep * (alpha * 0.85f), 1);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + 8, rect.Bottom + 3, rect.Width - 16, 2), src, KikasaPanelState.Rain * (alpha * 0.35f));
        }

        /// <summary>涟漪环:压扁的椭圆细环,12 段允许微碎</summary>
        public static void DrawRippleRing(SpriteBatch spriteBatch, Vector2 center, float radius, float alpha, float squash = 0.32f) {
            if (radius < 0.8f || alpha <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f, 0.5f);
            const int segs = 12;
            float segLen = radius * 0.42f;
            for (int i = 0; i < segs; i++) {
                float ang = MathHelper.TwoPi * i / segs;
                Vector2 pos = center + new Vector2(MathF.Cos(ang) * radius, MathF.Sin(ang) * radius * squash);
                float rot = MathF.Atan2(MathF.Cos(ang) * squash, -MathF.Sin(ang));
                //远侧(上半)略暗,近侧受光
                float lit = 0.62f + 0.38f * MathF.Max(0f, MathF.Sin(ang));
                spriteBatch.Draw(pixel, pos, src, Color.Lerp(KikasaPanelState.Rain, KikasaPanelState.Moon, 0.30f) * (alpha * lit),
                    rot, half, new Vector2(segLen, 0.9f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>波光线:横向缓波,波峰提亮,sweep 0~1 截断长度</summary>
        public static void DrawWaterline(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float amp, float alpha, float phase, float sweep = 1f) {
            Vector2 span = end - start;
            float dist = span.Length();
            if (dist < 2f || alpha <= 0.01f || sweep <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f, 0.5f);
            Vector2 dir = span / dist;
            Vector2 normal = new(-dir.Y, dir.X);
            float cycles = MathF.Max(1.6f, dist / 95f);
            int segs = Math.Max(4, (int)(dist * sweep / 7f));

            Vector2 PointAt(float t) => start + dir * (dist * t) + normal * (MathF.Sin(t * cycles * MathHelper.TwoPi + phase) * amp);

            Vector2 prev = PointAt(0f);
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs * sweep;
                Vector2 cur = PointAt(t);
                Vector2 delta = cur - prev;
                float crest = 0.5f + 0.5f * MathF.Sin(t * cycles * MathHelper.TwoPi + phase);
                float taper = MathF.Pow(MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi), 0.55f);
                Color col = Color.Lerp(KikasaPanelState.Rain, KikasaPanelState.Moon, crest * 0.60f);
                spriteBatch.Draw(pixel, (prev + cur) * 0.5f, src, col * (alpha * taper),
                    delta.ToRotation(), half, new Vector2(delta.Length() + 0.8f, 1.15f + crest * 0.5f), SpriteEffects.None, 0f);
                prev = cur;
            }
        }

        /// <summary>伞形章:半椭圆伞盖 + 顶针 + 伞柄弯钩,rotation 盖章用</summary>
        public static void DrawUmbrellaGlyph(SpriteBatch spriteBatch, Vector2 center, float size, float alpha, float rotation = 0f) {
            if (size < 2f || alpha <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f, 0.5f);
            float s = size / 16f;
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            void Quad(float lx, float ly, float w, float h, Color col, float extraRot = 0f) {
                Vector2 world = center + new Vector2(lx * cos - ly * sin, lx * sin + ly * cos) * s;
                spriteBatch.Draw(pixel, world, src, col, rotation + extraRot, half, new Vector2(w, h) * s, SpriteEffects.None, 0f);
            }

            Color body = KikasaPanelState.Rain * (alpha * 0.88f);
            Color dim = KikasaPanelState.Rain * (alpha * 0.66f);
            const float rimY = -1f;

            //伞盖:7 条竖切片填出半椭圆壳
            for (int i = 0; i < 7; i++) {
                float xi = -6.86f + i * 2.29f;
                float hi = 6.4f * MathF.Sqrt(MathF.Max(0f, 1f - xi * xi / 64f));
                if (hi < 0.4f) {
                    continue;
                }
                Quad(xi, rimY - hi * 0.5f, 2.5f, hi, body);
            }
            //顶针
            Quad(0f, rimY - 7.6f, 1.2f, 2.6f, dim);
            //伞柄 + 弯钩
            Quad(0f, rimY + 4.2f, 1.1f, 8.4f, dim);
            Quad(1.4f, rimY + 8.2f, 2.8f, 1.1f, dim);
            //伞盖左肩一点溺月湿光
            Quad(-2.6f, rimY - 5.0f, 2.0f, 1.0f, KikasaPanelState.Moon * (alpha * 0.40f));
        }

        /// <summary>垂滴水线:自锚点垂下的细水线 + 端珠</summary>
        public static void DrawDrip(SpriteBatch spriteBatch, Vector2 top, float length, float alpha, float brightness = 1f) {
            if (length < 1.5f || alpha <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f, 0.5f);
            //上细下略粗的水线
            spriteBatch.Draw(pixel, top + new Vector2(0f, length * 0.35f), src, KikasaPanelState.Rain * (alpha * 0.38f * brightness),
                MathHelper.PiOver2, half, new Vector2(length * 0.7f, 0.9f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, top + new Vector2(0f, length * 0.78f), src, KikasaPanelState.Rain * (alpha * 0.58f * brightness),
                MathHelper.PiOver2, half, new Vector2(length * 0.45f, 1.1f), SpriteEffects.None, 0f);
            //端珠
            spriteBatch.Draw(pixel, top + new Vector2(0f, length), src, KikasaPanelState.Moon * (alpha * 0.55f * brightness),
                0f, half, new Vector2(1.8f, 2.4f), SpriteEffects.None, 0f);
        }

        /// <summary>檐滴:底沿三处水珠周期聚落,相位由 swayTimer 决定,无粒子状态</summary>
        public static void DrawEaveDrips(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f, 0.5f);
            ReadOnlySpan<float> ratios = [0.16f, 0.52f, 0.86f];
            ReadOnlySpan<float> seeds = [0.13f, 0.47f, 0.81f];
            float cycle = swayTimer / MathHelper.TwoPi;

            for (int i = 0; i < 3; i++) {
                float x = rect.X + rect.Width * ratios[i];
                Vector2 anchor = new(x, rect.Bottom + 1f);
                //常驻一点湿痕
                spriteBatch.Draw(pixel, anchor, src, KikasaPanelState.Rain * (alpha * 0.22f), 0f, half, new Vector2(2.4f, 1f), SpriteEffects.None, 0f);

                float p = (cycle + seeds[i]) % 1f;
                if (p < 0.62f) {
                    //聚珠:水珠在檐口慢慢涨大
                    float grow = p / 0.62f;
                    float bodyA = alpha * (0.25f + grow * 0.45f);
                    Vector2 sizeV = new(1.1f + grow * 1.3f, 1.5f + grow * 2.1f);
                    spriteBatch.Draw(pixel, anchor + new Vector2(0f, sizeV.Y * 0.4f), src,
                        Color.Lerp(KikasaPanelState.Rain, KikasaPanelState.Moon, grow * 0.40f) * bodyA, 0f, half, sizeV, SpriteEffects.None, 0f);
                }
                else if (p < 0.86f) {
                    //坠落:离檐加速下坠,渐隐
                    float ft = (p - 0.62f) / 0.24f;
                    float drop = ft * ft * 17f;
                    float fade = 1f - ft;
                    spriteBatch.Draw(pixel, anchor + new Vector2(0f, 2.2f + drop), src,
                        KikasaPanelState.Moon * (alpha * 0.55f * fade), 0f, half, new Vector2(1.6f, 2.6f), SpriteEffects.None, 0f);
                }
                //其余相位:檐口回潮,静置
            }
        }

        /// <summary>四角水痕角签(弹窗):顶角垂滴,底角波光</summary>
        public static void DrawCornerDrips(SpriteBatch spriteBatch, Rectangle rect, float alpha, float pulse) {
            float a = alpha * (0.52f + pulse * 0.20f);
            const float len = 12f;
            const int inset = 5;
            //顶部两角:短波光 + 垂滴(雨自上来)
            DrawWaterline(spriteBatch, new Vector2(rect.X + inset, rect.Y + inset + 1), new Vector2(rect.X + inset + len, rect.Y + inset + 1), 0.7f, a, pulse * 2f);
            DrawDrip(spriteBatch, new Vector2(rect.X + inset + 1, rect.Y + inset + 2), 8f, a * 0.9f);
            DrawWaterline(spriteBatch, new Vector2(rect.Right - inset - len, rect.Y + inset + 1), new Vector2(rect.Right - inset, rect.Y + inset + 1), 0.7f, a, pulse * 2f + 1.7f);
            DrawDrip(spriteBatch, new Vector2(rect.Right - inset - 1, rect.Y + inset + 2), 8f, a * 0.9f);
            //底部两角:短波光(积水承角)
            DrawWaterline(spriteBatch, new Vector2(rect.X + inset, rect.Bottom - inset - 1), new Vector2(rect.X + inset + len, rect.Bottom - inset - 1), 0.9f, a * 0.85f, pulse * 2.4f + 0.8f);
            DrawWaterline(spriteBatch, new Vector2(rect.Right - inset - len, rect.Bottom - inset - 1), new Vector2(rect.Right - inset, rect.Bottom - inset - 1), 0.9f, a * 0.85f, pulse * 2.4f + 2.9f);
        }

        /// <summary>悬珠(弹窗顶心):一滴悬着的水珠,轻摆欲坠</summary>
        public static void DrawHangingDroplet(SpriteBatch spriteBatch, Rectangle rect, float alpha, float swayTimer) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f, 0.5f);
            Vector2 anchor = new(rect.Center.X, rect.Y - 2f);
            float sway = MathF.Sin(swayTimer * 2.1f) * 1.3f;
            float len = 7.5f + MathF.Sin(swayTimer * 3.3f) * 0.8f;

            //水线随摆微斜
            Vector2 tip = anchor + new Vector2(sway * 0.6f, len);
            Vector2 delta = tip - anchor;
            spriteBatch.Draw(pixel, (anchor + tip) * 0.5f, src, KikasaPanelState.Rain * (alpha * 0.45f),
                delta.ToRotation(), half, new Vector2(delta.Length(), 0.9f), SpriteEffects.None, 0f);
            //珠体 + 溺月高光
            spriteBatch.Draw(pixel, tip + new Vector2(0f, 1.2f), src, KikasaPanelState.Rain * (alpha * 0.85f),
                0f, half, new Vector2(2.4f, 3.1f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, tip + new Vector2(-0.5f, 0.4f), src, KikasaPanelState.Moon * (alpha * 0.60f),
                0f, half, new Vector2(0.9f, 1.2f), SpriteEffects.None, 0f);
        }
    }
}
