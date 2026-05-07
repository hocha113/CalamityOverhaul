using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.VoidPortals
{
    internal class VoidReturnPanelUI : UIHandle
    {
        private const int EdgePad = 14;
        private static Rectangle panelRect;

        private float shaderTime;
        private float glitchTimer;

        public override bool Active => VoidColony.Active
            && (VoidReturnSession.IsOpen || VoidReturnSession.OpenProgress > 0.005f);

        public override void Update() {
            shaderTime += 1f / 60f;
            if (shaderTime > 100f) shaderTime -= 100f;

            float targetGlitch = VoidReturnSession.IsOpen ? 0.12f : 0f;
            glitchTimer = MathHelper.Lerp(glitchTimer, targetGlitch, 0.08f);

            UIHitBox = VoidReturnSession.OpenProgress > 0.05f ? panelRect : Rectangle.Empty;
        }

        public override void Draw(SpriteBatch sb) {
            VoidReturnPortalActor portal = VoidReturnSession.Portal;
            if (portal == null) return;

            float open = VoidReturnSession.OpenProgress;
            float eased = 1f - (float)Math.Pow(1f - MathHelper.Clamp(open, 0f, 1f), 3);
            int width = 660;
            int height = 330;
            float scale = 0.92f + eased * 0.08f;
            int drawW = (int)(width * scale);
            int drawH = (int)(height * scale);
            Rectangle rect = new(Main.screenWidth / 2 - drawW / 2, Main.screenHeight / 2 - drawH / 2, drawW, drawH);
            panelRect = rect;
            Texture2D px = TextureAssets.MagicPixel.Value;

            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * (0.55f * eased));

            DrawShaderPanel(sb, rect, eased);
            DrawPanelContent(sb, rect, eased);

            if (rect.Contains(Main.mouseX, Main.mouseY) && eased > 0.2f) {
                Main.LocalPlayer.mouseInterface = true;
                Main.LocalPlayer.cursorItemIconEnabled = false;
            }
        }

        //使用 AbandonedPortalPanel 着色器渲染背景，呈现"已修复/在线"状态外观
        private void DrawShaderPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Asset<Effect> effectAsset = EffectLoader.AbandonedPortalPanel;

            if (effectAsset?.Value == null) {
                DrawFallbackPanel(sb, rect, alpha);
                return;
            }

            Rectangle ext = rect;
            ext.Inflate(EdgePad, EdgePad);

            Effect effect = effectAsset.Value;
            effect.Parameters["uTime"]?.SetValue(shaderTime);
            effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(ext.Width, ext.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
            effect.Parameters["uRepair"]?.SetValue(1f);
            effect.Parameters["uState"]?.SetValue(2f);
            effect.Parameters["uGlitch"]?.SetValue(MathHelper.Clamp(glitchTimer, 0f, 1f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, ext, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private static void DrawFallbackPanel(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color bg = new Color(8, 12, 20) * (0.95f * alpha);
            Color edge = new Color(50, 130, 220) * alpha;
            Color dim = new Color(35, 90, 165) * (0.7f * alpha);

            sb.Draw(px, rect, bg);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 3), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), dim);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), dim * 0.85f);
        }

        private void DrawPanelContent(SpriteBatch sb, Rectangle rect, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Color accent = new Color(140, 210, 255);
            Color title = new Color(230, 240, 255) * alpha;
            Color body = new Color(178, 200, 220) * alpha;
            Color dim = new Color(100, 120, 150) * alpha;

            int padX = 36;
            int padY = 30;

            Utils.DrawBorderString(sb, "虚空出口控制台", new Vector2(rect.X + padX, rect.Y + padY), title, 0.95f);

            float blink = MathF.Sin(shaderTime * 1.6f) * 0.3f + 0.7f;
            DrawStatusBadge(sb, new Rectangle(rect.Right - 222, rect.Y + 26, 190, 26),
                "STATUS  ▌ ONLINE", accent * (alpha * blink));

            Utils.DrawBorderString(sb, "传送协议就绪 · 导航信标稳定",
                new Vector2(rect.X + padX, rect.Y + padY + 36f), accent * alpha, 0.74f);

            DrawDecoLine(sb, new Rectangle(rect.X + padX, rect.Y + padY + 64, rect.Width - padX * 2, 2),
                accent * (alpha * 0.6f), alpha);

            string bodyText = "传送门已稳定，可随时启动返回序列。目标锚点：主世界，离开坐标处。";
            string[] wrapped = Utils.WordwrapString(bodyText, font, rect.Width - 90, 5, out _);
            float bodyY = rect.Y + padY + 78f;
            for (int i = 0; i < wrapped.Length; i++) {
                if (string.IsNullOrEmpty(wrapped[i])) continue;
                Utils.DrawBorderString(sb, wrapped[i], new Vector2(rect.X + padX, bodyY + i * 22f), body, 0.66f);
            }

            Utils.DrawBorderString(sb, "[DIAG]  OK: 全部子系统在线 / 出口序列就绪",
                new Vector2(rect.X + padX, rect.Bottom - 96f), dim, 0.62f);

            int btnH = 38;
            int btnPadY = rect.Bottom - 58;
            Rectangle close = new(rect.X + padX, btnPadY, 130, btnH);
            Rectangle primary = new(rect.Right - 250, btnPadY, 216, btnH);

            DrawTechButton(sb, close, "关 闭", dim * 1.6f, alpha, VoidReturnSession.RequestClose, false);
            DrawTechButton(sb, primary, "返 回 主 世 界", accent, alpha, DoReturn, true);
        }

        private static void DoReturn() {
            VoidReturnPortalActor portal = VoidReturnSession.Portal;
            if (portal == null) return;
            VoidReturnSession.RequestClose();
            portal.TriggerReturn();
        }

        private static void DrawDecoLine(SpriteBatch sb, Rectangle rect, Color c, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, new Rectangle(rect.X, rect.Y - 3, 4, 8), c);
            sb.Draw(px, new Rectangle(rect.X + 6, rect.Y, rect.Width - 6, rect.Height), c * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 26, rect.Y - 3, 26, 1), c * 0.7f);
            sb.Draw(px, new Rectangle(rect.Right - 12, rect.Y - 5, 12, 1), c * 0.5f);
        }

        private static void DrawStatusBadge(SpriteBatch sb, Rectangle rect, string text, Color color) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, rect, Color.Black * (color.A / 255f * 0.45f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 4, rect.Height), color);
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Y, rect.Width - 4, 1), color * 0.85f);
            sb.Draw(px, new Rectangle(rect.X + 4, rect.Bottom - 1, rect.Width - 4, 1), color * 0.6f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.55f;
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.X + 12, rect.Center.Y - size.Y * 0.5f),
                color, 0.55f);
        }

        private void DrawTechButton(SpriteBatch sb, Rectangle rect, string text, Color color, float alpha,
            Action onClick, bool isPrimary) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            bool hover = rect.Contains(Main.mouseX, Main.mouseY);

            Color bg = (hover ? color * 0.32f : Color.Black * 0.40f) * alpha;
            sb.Draw(px, rect, bg);

            Color edge = color * (alpha * (hover ? 1f : 0.7f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), edge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edge * 0.55f);

            int cs = 5;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, cs, 2), color * alpha);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, cs), color * alpha);
            sb.Draw(px, new Rectangle(rect.Right - cs, rect.Bottom - 2, cs, 2), color * (alpha * 0.7f));
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Bottom - cs, 2, cs), color * (alpha * 0.7f));

            if (isPrimary && hover) {
                float w = shaderTime * 1.4f % 1f * rect.Width;
                int beam = 64;
                for (int dx = -beam; dx <= beam; dx++) {
                    int x = rect.X + (int)w + dx;
                    if (x < rect.X || x >= rect.Right) continue;
                    float f = 1f - Math.Abs(dx) / (float)beam;
                    sb.Draw(px, new Rectangle(x, rect.Y + 2, 1, rect.Height - 4), color * (alpha * 0.28f * f * f));
                }
            }

            sb.Draw(px, new Rectangle(rect.X + 10, rect.Center.Y - 7, 3, 14), color * alpha);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.66f;
            Utils.DrawBorderString(sb, text,
                new Vector2(rect.X + 22 + (rect.Width - 22 - size.X) * 0.5f, rect.Center.Y - size.Y * 0.5f),
                Color.White * alpha, 0.66f);

            if (hover) {
                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    Main.mouseLeftRelease = false;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    onClick?.Invoke();
                }
            }
        }
    }
}
