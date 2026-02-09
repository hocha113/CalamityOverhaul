using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    internal class OverhaulSettingsUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static OverhaulSettingsUI Instance => UIHandleLoader.GetUIHandleOfType<OverhaulSettingsUI>();

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText CloseText { get; private set; }
        public static LocalizedText PlaceholderText { get; private set; }

        //UI控制
        internal bool _active;
        private float _sengs;
        private float contentFade;
        private bool closing;
        private float hideProgress;

        //动画时间轴
        private float globalTime;
        private float panelSlideOffset;
        private float panelScaleAnim;
        private float breatheAnim;
        private float shimmerPhase;

        //关闭按钮动画
        private float closeHoverAnim;
        private float closePressAnim;

        //粒子系统
        private readonly List<SettingsParticle> particles = [];
        private float particleSpawnTimer;

        //面板尺寸
        private static float PanelWidth => Main.screenWidth * 0.8f;
        private static float PanelHeight => Main.screenHeight * 0.8f;
        //布局常量
        private const float Padding = 24f;
        private const float ButtonHeight = 42f;
        private const float ButtonWidth = 130f;
        private const float CornerRadius = 10f;
        private const float TitleBarHeight = 50f;

        //按钮
        private Rectangle closeButtonRect;
        private bool hoveringClose;

        //设置项列表(预留)
        private readonly List<SettingEntry> settingEntries = [];

        //粒子结构
        private struct SettingsParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
            public Color BaseColor;
        }

        //设置项结构(预留)
        internal class SettingEntry
        {
            public string Name;
            public string Description;
            public bool Value;
            public Action<bool> OnChanged;
            public float HoverAnim;
            public Rectangle HitBox;
        }

        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => CWRLoad.OnLoadContentBool;

        public static bool OnActive() {
            if (Instance == null) {
                return false;
            }
            return Instance._active || Instance._sengs > 0;
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "大修设置");
            CloseText = this.GetLocalization(nameof(CloseText), () => "关闭");
            PlaceholderText = this.GetLocalization(nameof(PlaceholderText), () => "设置项将在后续版本中添加...");
        }

        public override void UnLoad() {
            _sengs = 0;
            _active = false;
        }

        private void ResetAnimations() {
            _sengs = 0f;
            contentFade = 0f;
            panelSlideOffset = 60f;
            panelScaleAnim = 0.85f;
            closeHoverAnim = 0f;
            closePressAnim = 0f;
            closing = false;
            hideProgress = 0f;
            particles.Clear();
        }

        public override void Update() {
            globalTime += 0.016f;

            //淡入淡出
            if (_active && !closing) {
                if (_sengs < 1f) {
                    _sengs += 0.05f;
                }
                if (_sengs > 1f) {
                    _sengs = 1f;
                }
            }
            else if (!_active || closing) {
                if (closing) {
                    hideProgress += 0.06f;
                    if (hideProgress >= 1f) {
                        hideProgress = 1f;
                        closing = false;
                        _active = false;
                        ResetAnimations();
                        return;
                    }
                    _sengs = 1f - EaseOutQuad(hideProgress);
                }
                else {
                    if (_sengs > 0f) {
                        _sengs -= 0.05f;
                    }
                    if (_sengs <= 0f) {
                        _sengs = 0f;
                        return;
                    }
                }
            }

            if (_sengs <= 0.001f) {
                particles.Clear();
                return;
            }

            //面板滑入动画
            float targetSlide = _active && !closing ? 0f : 60f;
            panelSlideOffset += (targetSlide - panelSlideOffset) * 0.15f;

            //面板缩放动画
            float targetScale = _active && !closing ? 1f : 0.85f;
            panelScaleAnim += (targetScale - panelScaleAnim) * 0.1f;

            //内容淡入
            if (_sengs > 0.5f && !closing) {
                contentFade += (1f - contentFade) * 0.12f;
            }
            else {
                contentFade *= 0.85f;
            }
            contentFade = Math.Clamp(contentFade, 0f, 1f);

            //呼吸动画
            breatheAnim = MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f;
            shimmerPhase = globalTime * 2f;

            //按钮动画
            float hoverSpeed = 0.15f;
            closeHoverAnim += ((hoveringClose ? 1f : 0f) - closeHoverAnim) * hoverSpeed;
            closePressAnim *= 0.85f;

            //更新粒子
            UpdateParticles();

            //生成新粒子
            if (_sengs > 0.3f && !closing) {
                particleSpawnTimer += 1f;
                if (particleSpawnTimer > 4f) {
                    SpawnParticle();
                    particleSpawnTimer = 0f;
                }
            }

            //计算面板位置(居中)
            float scaledWidth = PanelWidth * panelScaleAnim;
            float scaledHeight = PanelHeight * panelScaleAnim;
            Vector2 panelCenter = new(Main.screenWidth / 2f, Main.screenHeight / 2f + panelSlideOffset);
            DrawPosition = panelCenter - new Vector2(scaledWidth, scaledHeight) / 2f;
            Size = new Vector2(scaledWidth, scaledHeight);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)scaledWidth, (int)scaledHeight);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            //悬停检测
            hoveringClose = closeButtonRect.Contains(MouseHitBox) && contentFade > 0.5f;

            //点击处理
            if (keyLeftPressState == KeyPressState.Pressed && contentFade > 0.8f) {
                if (hoveringClose) {
                    closePressAnim = 1f;
                    OnClose();
                }
            }

            //ESC关闭
            if (OnActive()) {
                KeyboardState currentKeyState = Main.keyState;
                KeyboardState previousKeyState = Main.oldKeyState;
                if (currentKeyState.IsKeyDown(Keys.Escape) && !previousKeyState.IsKeyDown(Keys.Escape)) {
                    OnClose();
                }
            }
        }

        private void OnClose() {
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            Main.menuMode = 0;//恢复主菜单
            if (!closing) {
                closing = true;
                hideProgress = 0f;
            }
        }

        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                p.Life -= 0.016f;
                p.Position += p.Velocity;
                p.Velocity *= 0.98f;
                p.Velocity.Y -= 0.015f;
                p.Rotation += p.RotationSpeed;
                particles[i] = p;

                if (p.Life <= 0f) {
                    particles.RemoveAt(i);
                }
            }
        }

        private void SpawnParticle() {
            if (particles.Count > 25) return;

            var p = new SettingsParticle {
                Position = new Vector2(
                    DrawPosition.X + Main.rand.NextFloat(Size.X),
                    DrawPosition.Y + Size.Y
                ),
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.2f, -0.4f)),
                Life = Main.rand.NextFloat(1.5f, 3f),
                MaxLife = 0f,
                Size = Main.rand.NextFloat(1.5f, 4f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.04f, 0.04f),
                BaseColor = Color.Lerp(new Color(180, 40, 40), new Color(120, 20, 20), Main.rand.NextFloat())
            };
            p.MaxLife = p.Life;
            particles.Add(p);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        public override void Draw(SpriteBatch spriteBatch) {
            if (_sengs <= 0.001f) return;

            //运行环境比较敏感，防止资源释放后的交互
            if (CWRAsset.Placeholder_White == null || CWRAsset.Placeholder_White.IsDisposed) {
                _active = false;
                return;
            }

            float alpha = _sengs;

            //绘制背景遮罩
            //暂时不决定启用这个的绘制
            //DrawBackdrop(spriteBatch, alpha * 0.6f);

            //绘制粒子(在面板后面)
            DrawParticles(spriteBatch, alpha);

            //绘制主面板
            DrawPanel(spriteBatch, alpha);

            //绘制内容
            if (contentFade > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFade);
            }
        }

        private void DrawBackdrop(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, Vector2.Zero
                , new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                , Color.Black * alpha, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawParticles(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            foreach (var p in particles) {
                float lifeRatio = p.Life / p.MaxLife;
                float particleAlpha = lifeRatio * alpha;
                float size = p.Size * (0.5f + lifeRatio * 0.5f);

                Color color = p.BaseColor * particleAlpha;
                spriteBatch.Draw(pixel, p.Position, new Rectangle(0, 0, 1, 1), color, p.Rotation,
                    new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);

                Color glowColor = color * 0.25f;
                spriteBatch.Draw(pixel, p.Position, new Rectangle(0, 0, 1, 1), glowColor, p.Rotation,
                    new Vector2(0.5f), new Vector2(size * 2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = UIHitBox;

            //多层阴影
            for (int i = 4; i >= 1; i--) {
                Rectangle shadowRect = panelRect;
                shadowRect.Offset(i * 2, i * 3);
                float shadowAlpha = alpha * 0.15f * (5 - i) / 4f;
                DrawRoundedRect(spriteBatch, shadowRect, Color.Black * shadowAlpha, CornerRadius + i);
            }

            //背景渐变(深红色严肃质感)
            Color bgTop = new Color(30, 12, 12);
            Color bgBottom = new Color(50, 18, 18);
            DrawGradientRoundedRect(spriteBatch, panelRect, bgTop * (alpha * 0.97f), bgBottom * (alpha * 0.97f), CornerRadius);

            //内发光效果
            float innerGlowIntensity = 0.12f + breatheAnim * 0.08f;
            DrawInnerGlow(spriteBatch, panelRect, new Color(160, 50, 50) * (alpha * innerGlowIntensity), CornerRadius, 18);

            //流光边框
            DrawAnimatedBorder(spriteBatch, panelRect, alpha);

            //顶部标题栏分割(深红高光条)
            int titleBarBottom = panelRect.Y + (int)(TitleBarHeight * panelScaleAnim);
            float highlightAlpha = 0.5f + breatheAnim * 0.2f;
            Rectangle highlightBar = new(panelRect.X + 15, titleBarBottom, panelRect.Width - 30, 2);
            DrawHorizontalGradient(spriteBatch, highlightBar,
                Color.Transparent, new Color(200, 60, 60) * (alpha * highlightAlpha), Color.Transparent);

            //角落装饰
            DrawCornerOrnaments(spriteBatch, panelRect, alpha);
        }

        private void DrawAnimatedBorder(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            //基础边框
            Color baseColor = new Color(100, 35, 35) * (alpha * 0.8f);
            DrawRoundedRectBorder(spriteBatch, rect, baseColor, CornerRadius, 2);

            //流光效果(暗红色)
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float shimmerPos = shimmerPhase % 4f / 4f;

            for (int i = 0; i < 3; i++) {
                float offset = (shimmerPos + i * 0.33f) % 1f;
                Vector2 pos = GetPointOnRectPerimeter(rect, offset);
                float intensity = MathF.Sin(offset * MathHelper.Pi) * 0.7f;
                Color shimmerColor = new Color(220, 80, 80) * (alpha * intensity);

                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), shimmerColor,
                    0f, new Vector2(0.5f), new Vector2(7f, 3f), SpriteEffects.None, 0f);

                for (int j = 1; j <= 4; j++) {
                    float trailOffset = (offset - j * 0.01f + 1f) % 1f;
                    Vector2 trailPos = GetPointOnRectPerimeter(rect, trailOffset);
                    float trailIntensity = intensity * (1f - j / 5f);
                    spriteBatch.Draw(pixel, trailPos, new Rectangle(0, 0, 1, 1),
                        shimmerColor * trailIntensity * 0.5f, 0f, new Vector2(0.5f),
                        new Vector2(5f - j, 2f - j * 0.3f), SpriteEffects.None, 0f);
                }
            }
        }

        private static Vector2 GetPointOnRectPerimeter(Rectangle rect, float t) {
            float perimeter = (rect.Width + rect.Height) * 2f;
            float dist = t * perimeter;

            if (dist < rect.Width) {
                return new Vector2(rect.X + dist, rect.Y);
            }
            dist -= rect.Width;
            if (dist < rect.Height) {
                return new Vector2(rect.Right, rect.Y + dist);
            }
            dist -= rect.Height;
            if (dist < rect.Width) {
                return new Vector2(rect.Right - dist, rect.Bottom);
            }
            dist -= rect.Width;
            return new Vector2(rect.X, rect.Bottom - dist);
        }

        private void DrawCornerOrnaments(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float ornamentAlpha = alpha * (0.5f + breatheAnim * 0.3f);
            Color ornamentColor = new Color(200, 70, 70) * ornamentAlpha;

            Vector2[] corners = [
                new(rect.X + 8, rect.Y + 8),
                new(rect.Right - 8, rect.Y + 8),
                new(rect.X + 8, rect.Bottom - 8),
                new(rect.Right - 8, rect.Bottom - 8)
            ];

            for (int i = 0; i < 4; i++) {
                //菱形装饰
                spriteBatch.Draw(pixel, corners[i], new Rectangle(0, 0, 1, 1), ornamentColor,
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);

                //光芒
                for (int j = 0; j < 4; j++) {
                    float rayRot = j * MathHelper.PiOver2 + globalTime * 0.3f;
                    Vector2 rayDir = rayRot.ToRotationVector2();
                    spriteBatch.Draw(pixel, corners[i] + rayDir * 3f, new Rectangle(0, 0, 1, 1),
                        ornamentColor * 0.4f, rayRot, new Vector2(0f, 0.5f), new Vector2(7f, 1.2f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            float scale = panelScaleAnim;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding * scale, Padding * scale * 0.6f);
            string title = TitleText.Value;

            //标题光晕
            float titleGlow = 0.4f + breatheAnim * 0.4f;
            Color titleGlowColor = new Color(200, 60, 60) * (alpha * titleGlow * 0.35f);
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + globalTime * 0.4f;
                Vector2 offset = angle.ToRotationVector2() * (2.5f + breatheAnim * 1.5f);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlowColor, scale * 1.1f);
            }

            //标题主体
            Color titleColor = Color.Lerp(new Color(240, 200, 200), new Color(220, 100, 100), breatheAnim * 0.3f);
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor * alpha, scale * 1.1f);

            //内容区域(标题栏下方)
            float contentTop = DrawPosition.Y + TitleBarHeight * scale + 15f * scale;
            float contentLeft = DrawPosition.X + Padding * scale;
            float contentWidth = Size.X - Padding * scale * 2;
            float contentHeight = Size.Y - TitleBarHeight * scale - Padding * scale - ButtonHeight * scale - 20f * scale;

            //占位提示文本
            if (settingEntries.Count == 0) {
                string placeholder = PlaceholderText.Value;
                Vector2 placeholderSize = FontAssets.MouseText.Value.MeasureString(placeholder) * 0.8f * scale;
                Vector2 placeholderPos = new(
                    contentLeft + contentWidth / 2f - placeholderSize.X / 2f,
                    contentTop + contentHeight / 2f - placeholderSize.Y / 2f
                );
                Color phColor = new Color(150, 100, 100) * (alpha * 0.6f);
                Utils.DrawBorderString(spriteBatch, placeholder, placeholderPos, phColor, 0.8f * scale);

                //装饰分割线
                Vector2 lineStart = new(contentLeft + 40f * scale, contentTop + contentHeight / 2f - 25f * scale);
                Vector2 lineEnd = new(contentLeft + contentWidth - 40f * scale, contentTop + contentHeight / 2f - 25f * scale);
                DrawDividerLine(spriteBatch, lineStart, lineEnd, alpha * 0.5f);

                Vector2 lineStart2 = new(contentLeft + 40f * scale, contentTop + contentHeight / 2f + 25f * scale);
                Vector2 lineEnd2 = new(contentLeft + contentWidth - 40f * scale, contentTop + contentHeight / 2f + 25f * scale);
                DrawDividerLine(spriteBatch, lineStart2, lineEnd2, alpha * 0.5f);
            }

            //关闭按钮(底部居中)
            float buttonY = DrawPosition.Y + Size.Y - Padding * scale - ButtonHeight * scale;
            float buttonCenterX = DrawPosition.X + Size.X / 2f;
            float scaledButtonWidth = ButtonWidth * scale;
            float scaledButtonHeight = ButtonHeight * scale;

            closeButtonRect = new Rectangle(
                (int)(buttonCenterX - scaledButtonWidth / 2f),
                (int)buttonY,
                (int)scaledButtonWidth,
                (int)scaledButtonHeight
            );

            DrawButton(spriteBatch, closeButtonRect, CloseText.Value, closeHoverAnim, closePressAnim, alpha, scale);
        }

        private void DrawDividerLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float length = (end - start).Length();
            if (length < 1f) return;

            //底层线条
            Color baseColor = new Color(100, 40, 40) * (alpha * 0.5f);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), baseColor, 0f,
                Vector2.Zero, new Vector2(length, 1f), SpriteEffects.None, 0f);

            //流光效果
            float shimmerT = globalTime * 0.5f % 1f;
            Vector2 shimmerPos = Vector2.Lerp(start, end, shimmerT);
            Color shimmerColor = new Color(200, 80, 80, 0) * (alpha * 0.6f);

            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            float glowScale = 0.12f;
            spriteBatch.Draw(softGlow, shimmerPos, null, shimmerColor * 0.5f, 0f,
                softGlow.Size() / 2f, new Vector2(glowScale * 2f, glowScale * 0.5f), SpriteEffects.None, 0f);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            float hoverAnim, float pressAnim, float alpha, float scale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //按压效果
            Rectangle drawRect = rect;
            if (pressAnim > 0.01f) {
                int pressOffset = (int)(pressAnim * 2f);
                drawRect.Y += pressOffset;
            }

            //悬停膨胀
            int expandAmount = (int)(hoverAnim * 3f);
            drawRect.Inflate(expandAmount, expandAmount / 2);

            //背景渐变(暗红色)
            Color bgTop = Color.Lerp(new Color(55, 20, 20), new Color(80, 30, 30), hoverAnim);
            Color bgBottom = Color.Lerp(new Color(40, 15, 15), new Color(60, 22, 22), hoverAnim);
            DrawGradientRoundedRect(spriteBatch, drawRect, bgTop * (alpha * 0.95f), bgBottom * (alpha * 0.95f), 6f);

            //边框
            Color borderColor = Color.Lerp(new Color(140, 60, 60), new Color(220, 90, 90), hoverAnim);
            DrawRoundedRectBorder(spriteBatch, drawRect, borderColor * alpha, 6f, 1 + (int)hoverAnim);

            //悬停时的内发光
            if (hoverAnim > 0.01f) {
                Color innerGlow = new Color(200, 80, 80) * (alpha * hoverAnim * 0.15f);
                DrawInnerGlow(spriteBatch, drawRect, innerGlow, 6f, 8);
            }

            //按钮文字
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.85f * scale;
            Vector2 textPos = drawRect.Center.ToVector2() - textSize / 2f + new Vector2(0, 2);

            //文字阴影
            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 2),
                Color.Black * (alpha * 0.5f), 0.85f * scale);

            //文字主体
            Color textColor = Color.Lerp(new Color(200, 180, 170), Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.85f * scale);

            //悬停文字光晕
            if (hoverAnim > 0.3f) {
                Color textGlow = new Color(220, 100, 100) * (alpha * (hoverAnim - 0.3f) * 0.4f);
                Utils.DrawBorderString(spriteBatch, text, textPos, textGlow, 0.85f * scale);
            }
        }

        #region 绘制辅助方法

        private static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, float radius) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            Rectangle center = new(rect.X + r, rect.Y, rect.Width - r * 2, rect.Height);
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color);

            Rectangle left = new(rect.X, rect.Y + r, r, rect.Height - r * 2);
            Rectangle right = new(rect.Right - r, rect.Y + r, r, rect.Height - r * 2);
            sb.Draw(pixel, left, new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, right, new Rectangle(0, 0, 1, 1), color);

            for (int i = 0; i < r; i++) {
                float t = i / (float)r;
                int cornerWidth = (int)(r * MathF.Sqrt(1f - (1f - t) * (1f - t)));

                sb.Draw(pixel, new Rectangle(rect.X + r - cornerWidth, rect.Y + i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Y + i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.X + r - cornerWidth, rect.Bottom - 1 - i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Bottom - 1 - i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawGradientRoundedRect(SpriteBatch sb, Rectangle rect, Color topColor, Color bottomColor, float radius) {
            int segments = rect.Height;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Color color = Color.Lerp(topColor, bottomColor, t);

                int y = rect.Y + i;
                int inset = 0;

                int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
                if (i < r) {
                    float cornerT = i / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - cornerT) * (1f - cornerT))));
                }
                else if (i > rect.Height - r) {
                    float cornerT = (rect.Height - i) / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - cornerT) * (1f - cornerT))));
                }

                Rectangle line = new(rect.X + inset, y, rect.Width - inset * 2, 1);
                sb.Draw(VaultAsset.placeholder2.Value, line, new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawRoundedRectBorder(SpriteBatch sb, Rectangle rect, Color color, float radius, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Bottom - thickness, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);

            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Y + r), r, MathHelper.Pi, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Y + r), r, -MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Bottom - r), r, MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Bottom - r), r, 0, MathHelper.PiOver2, color, thickness);
        }

        private static void DrawCornerArc(SpriteBatch sb, Vector2 center, float radius, float startAngle, float sweep, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = Math.Max(4, (int)(radius * sweep / 2f));

            for (int i = 0; i <= segments; i++) {
                float angle = startAngle + sweep * i / segments;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
            }
        }

        private static void DrawInnerGlow(SpriteBatch sb, Rectangle rect, Color color, float radius, int glowSize) {
            for (int i = 0; i < glowSize; i++) {
                float t = i / (float)glowSize;
                float glowAlpha = (1f - t) * (1f - t);
                Color glowColor = color * glowAlpha;

                Rectangle glowRect = rect;
                glowRect.Inflate(-i, -i);

                if (glowRect.Width > 0 && glowRect.Height > 0) {
                    DrawRoundedRectBorder(sb, glowRect, glowColor, Math.Max(0, radius - i), 1);
                }
            }
        }

        private static void DrawHorizontalGradient(SpriteBatch sb, Rectangle rect, Color left, Color center, Color right) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < rect.Width; i++) {
                float t = i / (float)rect.Width;
                Color color;
                if (t < 0.5f) {
                    color = Color.Lerp(left, center, t * 2f);
                }
                else {
                    color = Color.Lerp(center, right, (t - 0.5f) * 2f);
                }

                sb.Draw(pixel, new Rectangle(rect.X + i, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
            }
        }

        #endregion
    }
}
