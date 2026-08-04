using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI.Gamepad;

namespace CalamityOverhaul.Content.MainMenus.Themes.Himayo
{
    internal sealed class HimayoMenuRenderer
    {
        private const float VerticalFov = 78f * MathHelper.Pi / 180f;
        private const float TitleYawLimit = MathHelper.Pi / 10f;
        private const float TitlePitchLimit = MathHelper.Pi / 18f;

        private Asset<Texture2D> panorama;
        private Asset<Texture2D> fallbackBackground;
        private bool backgroundShaderDisabled;
        private bool petalsDisabled;
        private bool backgroundWarningLogged;
        private bool petalWarningLogged;

        internal void LoadAssets(Mod mod) {
            panorama = mod.Assets.Request<Texture2D>("Assets/ADV/Himayo/HimayoEquirectangular");
            fallbackBackground = mod.Assets.Request<Texture2D>("Assets/ADV/Himayo/HimayoBackground");
            backgroundShaderDisabled = false;
            petalsDisabled = false;
            backgroundWarningLogged = false;
            petalWarningLogged = false;
        }

        internal void UnloadAssets() {
            panorama = null;
            fallbackBackground = null;
            backgroundShaderDisabled = false;
            petalsDisabled = false;
        }

        internal void DrawBackground(SpriteBatch spriteBatch, HimayoMenuState state) {
            bool titlePage = state.IsTitlePage;
            if (!TryDrawEquirectangular(spriteBatch, state, titlePage)) {
                DrawFallbackBackground(spriteBatch, state, titlePage);
            }
            DrawFarAndMiddlePetals(spriteBatch, state, titlePage);
        }

        internal void DrawForeground(SpriteBatch spriteBatch, HimayoMenuState state) {
            state.ProcessRenderInput();
            if (!state.IsTitlePage) {
                return;
            }

            DrawNearPetals(spriteBatch, state);
            DrawPlaque(spriteBatch, state);
            DrawButtons(spriteBatch, state);
            DrawThemeSwitcher(spriteBatch, state);
            RegisterGamepadPoints(state.Layout);
        }

        private bool TryDrawEquirectangular(SpriteBatch spriteBatch, HimayoMenuState state, bool titlePage) {
            if (backgroundShaderDisabled || panorama == null || EffectLoader.HimayoMainMenu == null) {
                return false;
            }

            try {
                Effect effect = EffectLoader.HimayoMainMenu.Value;
                effect.CurrentTechnique = RequireTechnique(effect, "TechEquirectangular");
                RequireParameter(effect, "uViewportSize").SetValue(new Vector2(RenderWidth, RenderHeight));
                RequireParameter(effect, "uYaw").SetValue(state.CameraYaw);
                RequireParameter(effect, "uPitch").SetValue(state.CameraPitch);
                RequireParameter(effect, "uVerticalFov").SetValue(VerticalFov);
                Texture2D texture = panorama.Value;
                RequireParameter(effect, "uTextureTexelSize").SetValue(new Vector2(1f / texture.Width, 1f / texture.Height));
                RequireParameter(effect, "uDimAmount").SetValue(titlePage ? 0f : 0.35f);

                DrawIdentity(spriteBatch, effect, () => {
                    spriteBatch.Draw(texture, new Rectangle(0, 0, RenderWidth, RenderHeight), Color.White);
                });
                return true;
            } catch (Exception ex) {
                backgroundShaderDisabled = true;
                LogBackgroundFallback(ex);
                return false;
            }
        }

        private void DrawFallbackBackground(SpriteBatch spriteBatch, HimayoMenuState state, bool titlePage) {
            Texture2D texture = fallbackBackground?.Value;
            if (texture == null || texture.IsDisposed) {
                DrawIdentity(spriteBatch, null, () => {
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle(0, 0, RenderWidth, RenderHeight), new Color(20, 5, 18));
                });
                return;
            }

            float scale = Math.Max(RenderWidth / (float)texture.Width, RenderHeight / (float)texture.Height);
            scale *= 1.045f;
            Vector2 parallax = new(
                MathHelper.Clamp(state.CameraYaw / TitleYawLimit, -1f, 1f) * -24f,
                MathHelper.Clamp(state.CameraPitch / TitlePitchLimit, -1f, 1f) * 18f);
            Vector2 center = new Vector2(RenderWidth, RenderHeight) * 0.5f + parallax;
            DrawIdentity(spriteBatch, null, () => {
                spriteBatch.Draw(texture, center, null, Color.White, 0f, texture.Size() * 0.5f,
                    scale, SpriteEffects.None, 0f);
                if (!titlePage) {
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                        new Rectangle(0, 0, RenderWidth, RenderHeight), Color.Black * 0.35f);
                }
            });
        }

        private void DrawFarAndMiddlePetals(SpriteBatch spriteBatch, HimayoMenuState state, bool titlePage) {
            if (!TryGetPetalEffect(out Effect effect)) {
                return;
            }

            try {
                DrawIdentity(spriteBatch, effect, () =>
                    state.Petals.DrawFarAndMiddle(spriteBatch, effect, titlePage, state.Interpolation));
            } catch (Exception ex) {
                DisablePetals(ex);
            }
        }

        private void DrawNearPetals(SpriteBatch spriteBatch, HimayoMenuState state) {
            if (!TryGetPetalEffect(out Effect effect)) {
                return;
            }

            try {
                DrawIdentity(spriteBatch, effect, () =>
                    state.Petals.DrawNear(spriteBatch, effect, true, state.Interpolation));
            } catch (Exception ex) {
                DisablePetals(ex);
            }
        }

        private bool TryGetPetalEffect(out Effect effect) {
            effect = null;
            if (petalsDisabled || EffectLoader.OniDomainDeco == null) {
                return false;
            }

            try {
                effect = EffectLoader.OniDomainDeco.Value;
                effect.CurrentTechnique = RequireTechnique(effect, "TechMenuPetal");
                _ = RequireParameter(effect, "uPetalSoftness");
                return true;
            } catch (Exception ex) {
                DisablePetals(ex);
                return false;
            }
        }

        private static void DrawPlaque(SpriteBatch spriteBatch, HimayoMenuState state) {
            DynamicSpriteFont font = HimayoMenuTheme.Font.Value;
            Vector2 center = state.Layout.PlaquePosition;
            string upper = "CALAMITY OVERHAUL";
            string lower = "/ HIMAYO";
            float upperScale = 0.72f;
            float lowerScale = 0.58f;
            Vector2 upperSize = font.MeasureString(upper) * upperScale;
            Vector2 lowerSize = font.MeasureString(lower) * lowerScale;
            float alpha = SmoothStep(MathHelper.Clamp(state.Elapsed / 0.42f, 0f, 1f));

            DrawShadowedText(spriteBatch, font, upper,
                center - new Vector2(upperSize.X * 0.5f, 0f), new Color(240, 226, 220) * alpha, upperScale);
            DrawShadowedText(spriteBatch, font, lower,
                center + new Vector2(-lowerSize.X * 0.5f, upperSize.Y + 2f), new Color(208, 76, 76) * alpha, lowerScale);
        }

        private static void DrawButtons(SpriteBatch spriteBatch, HimayoMenuState state) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            DynamicSpriteFont font = HimayoMenuTheme.Font.Value;
            ReadOnlySpan<HimayoMenuButtonLayout> buttons = state.Layout.Buttons;
            for (int i = 0; i < buttons.Length; i++) {
                HimayoMenuButtonLayout button = buttons[i];
                float hover = state.GetHover(i);
                float entry = state.GetEntry(i);
                if (entry <= 0.001f) {
                    continue;
                }

                Vector2 offset = new(-34f * (1f - entry) + 14f * hover, 0f);
                Vector2 position = button.TextPosition + offset;
                Color baseColor = button.Primary ? new Color(248, 236, 232) : new Color(222, 211, 211);
                Color textColor = Color.Lerp(baseColor, new Color(255, 246, 238), hover) * entry;
                float scale = button.TextScale * (1f + hover * 0.035f);

                float strokeWidth = (button.TextSize.X + 24f) * (0.18f + hover * 0.82f) * entry;
                Vector2 strokeStart = new(position.X - 8f, position.Y + button.TextSize.Y + 2f);
                Color stroke = new Color(170, 25, 38) * (entry * (0.35f + hover * 0.55f));
                spriteBatch.Draw(pixel, new Rectangle((int)strokeStart.X, (int)strokeStart.Y,
                    Math.Max(1, (int)strokeWidth), 2), stroke);
                spriteBatch.Draw(pixel, new Rectangle((int)(strokeStart.X + strokeWidth * 0.18f),
                    (int)strokeStart.Y + 3, Math.Max(1, (int)(strokeWidth * 0.72f)), 1), stroke * 0.55f);

                DrawShadowedText(spriteBatch, font, button.Text, position, textColor, scale);
                if (hover > 0.03f) {
                    DrawSeal(spriteBatch, new Vector2(button.HitBox.X - 17f, button.HitBox.Center.Y), hover * entry);
                }
            }
        }

        private static void DrawThemeSwitcher(SpriteBatch spriteBatch, HimayoMenuState state) {
            Rectangle rect = state.Layout.ThemeSwitchRect;
            Vector2 pointer = HimayoMenuInput.UIPointer;
            Point mouse = pointer.ToPoint();
            bool hover = rect.Contains(mouse) && !HimayoMenuInput.IsOverlayCapturing(pointer);
            DynamicSpriteFont font = HimayoMenuTheme.Font.Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color muted = hover ? new Color(235, 211, 207) : new Color(168, 153, 157);
            float alpha = SmoothStep(MathHelper.Clamp((state.Elapsed - 0.24f) / 0.42f, 0f, 1f));

            string label = "HIMAYO";
            float scale = 0.62f;
            Vector2 size = font.MeasureString(label) * scale;
            Vector2 labelPos = new(rect.Center.X - size.X * 0.5f, rect.Center.Y - size.Y * 0.5f);
            DrawShadowedText(spriteBatch, font, label, labelPos, muted * alpha, scale);

            DrawShadowedText(spriteBatch, font, "<", new Vector2(rect.X + 15f, labelPos.Y),
                muted * alpha, scale);
            Vector2 nextSize = font.MeasureString(">") * scale;
            DrawShadowedText(spriteBatch, font, ">", new Vector2(rect.Right - 15f - nextSize.X, labelPos.Y),
                muted * alpha, scale);

            int lineWidth = (int)(rect.Width * (hover ? 0.62f : 0.32f));
            spriteBatch.Draw(pixel, new Rectangle(rect.Center.X - lineWidth / 2, rect.Bottom - 2,
                lineWidth, 1), new Color(149, 38, 48) * (alpha * (hover ? 0.85f : 0.45f)));
        }

        private static void RegisterGamepadPoints(HimayoMenuLayout layout) {
            ReadOnlySpan<HimayoMenuButtonLayout> buttons = layout.Buttons;
            for (int i = 0; i < buttons.Length; i++) {
                GamepadMainMenuHandler.MenuItemPositions.Add(buttons[i].HitBox.Center.ToVector2());
            }
            GamepadMainMenuHandler.MenuItemPositions.Add(layout.ThemeSwitchRect.Center.ToVector2());
        }

        private static void DrawSeal(SpriteBatch spriteBatch, Vector2 center, float alpha) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new((int)center.X - 5, (int)center.Y - 5, 10, 10);
            Color color = new Color(184, 34, 43) * alpha;
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Center.X - 1, rect.Y + 3, 2, rect.Height - 6), color * 0.8f);
        }

        private static void DrawShadowedText(SpriteBatch spriteBatch, DynamicSpriteFont font, string text,
            Vector2 position, Color color, float scale) {
            Color shadow = Color.Black * (color.A / 255f * 0.78f);
            spriteBatch.DrawString(font, text, position + new Vector2(2f, 3f), shadow,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private static void DrawIdentity(SpriteBatch spriteBatch, Effect effect, Action drawAction) {
            spriteBatch.End();
            bool identityBatchBegun = false;
            try {
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, Main.Rasterizer, effect, Matrix.Identity);
                identityBatchBegun = true;
                drawAction();
            } finally {
                if (identityBatchBegun) {
                    spriteBatch.End();
                }
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            }
        }

        private static EffectTechnique RequireTechnique(Effect effect, string name)
            => effect?.Techniques[name] ?? throw new InvalidOperationException($"Missing effect technique: {name}");

        private static EffectParameter RequireParameter(Effect effect, string name)
            => effect?.Parameters[name] ?? throw new InvalidOperationException($"Missing effect parameter: {name}");

        private void LogBackgroundFallback(Exception ex) {
            if (backgroundWarningLogged) {
                return;
            }
            backgroundWarningLogged = true;
            CWRMod.Instance?.Logger.Warn($"Himayo panorama shader unavailable; using static fallback: {ex.Message}");
        }

        private void DisablePetals(Exception ex) {
            petalsDisabled = true;
            if (petalWarningLogged) {
                return;
            }
            petalWarningLogged = true;
            CWRMod.Instance?.Logger.Warn($"Himayo menu petals disabled: {ex.Message}");
        }

        private static int RenderWidth => PlayerInput.RealScreenWidth > 0
            ? PlayerInput.RealScreenWidth
            : Math.Max(1, (int)(Main.screenWidth * Main.UIScale));

        private static int RenderHeight => PlayerInput.RealScreenHeight > 0
            ? PlayerInput.RealScreenHeight
            : Math.Max(1, (int)(Main.screenHeight * Main.UIScale));

        private static float SmoothStep(float value) => value * value * (3f - 2f * value);
    }
}
