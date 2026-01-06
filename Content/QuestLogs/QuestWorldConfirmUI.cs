using CalamityOverhaul.Common;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.QuestLogs
{
    /// <summary>
    /// 任务系统世界切换确认UI，在检测到进入不同世界时弹出确认
    /// </summary>
    internal class QuestWorldConfirmUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static QuestWorldConfirmUI Instance => UIHandleLoader.GetUIHandleOfType<QuestWorldConfirmUI>();

        public override bool IsLoadingEnabled(Mod mod) => CWRServerConfig.Instance.QuestLog;

        //本地化文本
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText DescText { get; private set; }
        public static LocalizedText ConfirmText { get; private set; }
        public static LocalizedText CancelText { get; private set; }
        public static LocalizedText EnabledText { get; private set; }
        public static LocalizedText DisabledText { get; private set; }
        public static LocalizedText DisabledOverlayText { get; private set; }

        //UI控制
        private float showProgress;
        private float contentFade;
        private bool closing;
        private float hideProgress;

        //动画时间轴
        private float globalTime;
        private float panelSlideOffset;
        private float panelScaleAnim;
        private float breatheAnim;
        private float shimmerPhase;

        //按钮动画
        private float confirmHoverAnim;
        private float cancelHoverAnim;
        private float confirmPressAnim;
        private float cancelPressAnim;

        //图标动画
        private float iconFloatPhase;
        private float iconGlowIntensity;

        //粒子系统
        private readonly List<FloatingParticle> particles = [];
        private float particleSpawnTimer;

        //布局常量(位置在屏幕上方，避免与LegendUpgradeConfirmUI重叠)
        private const float PanelWidth = 460f;
        private const float PanelHeight = 220f;
        private const float Padding = 24f;
        private const float ButtonHeight = 40f;
        private const float ButtonWidth = 130f;
        private const float CornerRadius = 12f;
        private const float IconShowcaseWidth = 100f;

        //按钮
        private Rectangle confirmButtonRect;
        private Rectangle cancelButtonRect;
        private bool hoveringConfirm;
        private bool hoveringCancel;

        //待处理数据
        private static string pendingWorldName;
        private static string pendingLastWorldName;
        private static bool isPending;

        //粒子结构
        private struct FloatingParticle
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

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "任务检测确认");
            DescText = this.GetLocalization(nameof(DescText), () => "检测到您从其他世界进入当前世界\n是否在当前世界中检测任务进度？");
            ConfirmText = this.GetLocalization(nameof(ConfirmText), () => "检测任务");
            CancelText = this.GetLocalization(nameof(CancelText), () => "跳过");
            EnabledText = this.GetLocalization(nameof(EnabledText), () => "已启用任务检测");
            DisabledText = this.GetLocalization(nameof(DisabledText), () => "已跳过任务检测");
            DisabledOverlayText = this.GetLocalization(nameof(DisabledOverlayText), () => "任务检测已在当前世界中被禁止\n重新进入世界以重新选择配置");
        }

        public override bool Active => isPending || showProgress > 0f;

        public override void LoadUIData(TagCompound tag) {
            CancelPending();
            ResetAnimations();
        }

        public override void SaveUIData(TagCompound tag) {
            CancelPending();
            ResetAnimations();
        }

        private void ResetAnimations() {
            showProgress = 0f;
            contentFade = 0f;
            panelSlideOffset = -50f;
            panelScaleAnim = 0.8f;
            confirmHoverAnim = 0f;
            cancelHoverAnim = 0f;
            confirmPressAnim = 0f;
            cancelPressAnim = 0f;
            particles.Clear();
        }

        /// <summary>
        /// 请求显示世界切换确认UI
        /// </summary>
        public static void RequestConfirm(string currentWorldName, string lastWorldName) {
            if (isPending || string.IsNullOrEmpty(currentWorldName)) {
                return;
            }

            pendingWorldName = currentWorldName;
            pendingLastWorldName = lastWorldName;
            isPending = true;

            SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f, Pitch = 0.1f });
        }

        /// <summary>
        /// 取消待处理的请求
        /// </summary>
        public static void CancelPending() {
            isPending = false;
            pendingWorldName = null;
            pendingLastWorldName = null;
        }

        public override void Update() {
            globalTime += 0.016f;

            //主面板展开动画
            float targetShow = isPending && !closing ? 1f : 0f;
            float showSpeed = closing ? 0.12f : 0.08f;
            showProgress += (targetShow - showProgress) * showSpeed;
            if (Math.Abs(showProgress - targetShow) < 0.001f) {
                showProgress = targetShow;
            }

            if (showProgress <= 0.001f && !isPending) {
                particles.Clear();
                return;
            }

            //面板滑入动画(从上方滑入)
            float targetSlide = isPending && !closing ? 0f : -60f;
            panelSlideOffset += (targetSlide - panelSlideOffset) * 0.15f;

            //面板缩放动画
            float targetScale = isPending && !closing ? 1f : 0.85f;
            panelScaleAnim += (targetScale - panelScaleAnim) * 0.1f;
            if (panelScaleAnim > 0.98f && targetScale == 1f) {
                panelScaleAnim += (1.02f - panelScaleAnim) * 0.3f;
            }

            //内容淡入
            if (showProgress > 0.5f && !closing) {
                float targetFade = 1f;
                contentFade += (targetFade - contentFade) * 0.12f;
            }
            else {
                contentFade *= 0.85f;
            }
            contentFade = Math.Clamp(contentFade, 0f, 1f);

            //动画
            breatheAnim = MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f;
            shimmerPhase = globalTime * 2f;
            iconFloatPhase += 0.04f;
            iconGlowIntensity = 0.6f + MathF.Sin(globalTime * 3f) * 0.4f;

            //按钮悬停动画
            float hoverSpeed = 0.15f;
            confirmHoverAnim += ((hoveringConfirm ? 1f : 0f) - confirmHoverAnim) * hoverSpeed;
            cancelHoverAnim += ((hoveringCancel ? 1f : 0f) - cancelHoverAnim) * hoverSpeed;

            //按钮按压动画衰减
            confirmPressAnim *= 0.85f;
            cancelPressAnim *= 0.85f;

            //关闭动画
            if (closing) {
                hideProgress += 0.06f;
                if (hideProgress >= 1f) {
                    hideProgress = 1f;
                    closing = false;
                    CancelPending();
                    ResetAnimations();
                }
            }

            //更新粒子
            UpdateParticles();

            //生成新粒子
            if (showProgress > 0.3f && !closing) {
                particleSpawnTimer += 1f;
                if (particleSpawnTimer > 4f) {
                    SpawnParticle();
                    particleSpawnTimer = 0f;
                }
            }

            //计算面板位置(屏幕上方)
            float scaledWidth = PanelWidth * panelScaleAnim;
            float scaledHeight = PanelHeight * panelScaleAnim;
            Vector2 panelCenter = new(Main.screenWidth / 2f, PanelHeight / 2f + 60f + panelSlideOffset);
            DrawPosition = panelCenter - new Vector2(scaledWidth, scaledHeight) / 2f;
            Size = new Vector2(scaledWidth, scaledHeight);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)scaledWidth, (int)scaledHeight);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage && showProgress > 0.5f) {
                player.mouseInterface = true;
            }

            //按钮位置在DrawContent中计算，这里只更新悬停检测
            hoveringConfirm = confirmButtonRect.Contains(MouseHitBox) && contentFade > 0.5f;
            hoveringCancel = cancelButtonRect.Contains(MouseHitBox) && contentFade > 0.5f;

            //点击处理
            if (keyLeftPressState == KeyPressState.Pressed && contentFade > 0.8f) {
                if (hoveringConfirm) {
                    confirmPressAnim = 1f;
                    OnConfirm();
                }
                else if (hoveringCancel) {
                    cancelPressAnim = 1f;
                    OnCancel();
                }
            }
        }

        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                p.Life -= 0.016f;
                p.Position += p.Velocity;
                p.Velocity *= 0.98f;
                p.Velocity.Y += 0.01f;
                p.Rotation += p.RotationSpeed;
                particles[i] = p;

                if (p.Life <= 0f) {
                    particles.RemoveAt(i);
                }
            }
        }

        private void SpawnParticle() {
            if (particles.Count > 25) return;

            var p = new FloatingParticle {
                Position = new Vector2(
                    DrawPosition.X + Main.rand.NextFloat(Size.X),
                    DrawPosition.Y
                ),
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.5f, 1.2f)),
                Life = Main.rand.NextFloat(1.5f, 2.5f),
                MaxLife = 0f,
                Size = Main.rand.NextFloat(2f, 4f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f),
                BaseColor = Color.Lerp(new Color(100, 180, 255), new Color(150, 220, 255), Main.rand.NextFloat())
            };
            p.MaxLife = p.Life;
            particles.Add(p);
        }

        private void OnConfirm() {
            //启用任务检测
            var qlPlayer = Main.LocalPlayer.GetModPlayer<QLPlayer>();
            qlPlayer.DontCheckQuestInWorld = string.Empty;
            qlPlayer.LastWorldFullName = SaveWorld.WorldFullName;

            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.3f });
            CombatText.NewText(player.Hitbox, new Color(100, 200, 255), EnabledText.Value, true);

            for (int i = 0; i < 10; i++) {
                SpawnParticle();
            }

            BeginClose();
        }

        private void OnCancel() {
            //跳过任务检测
            var qlPlayer = Main.LocalPlayer.GetModPlayer<QLPlayer>();
            qlPlayer.DontCheckQuestInWorld = SaveWorld.WorldFullName;

            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            CombatText.NewText(player.Hitbox, new Color(200, 150, 100), DisabledText.Value, true);

            BeginClose();
        }

        private void BeginClose() {
            if (closing) return;
            closing = true;
            hideProgress = 0f;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.001f) return;

            float alpha = Math.Min(showProgress * 1.5f, 1f);
            if (closing) {
                alpha *= 1f - EaseOutQuad(hideProgress);
            }

            //绘制背景遮罩
            DrawBackdrop(spriteBatch, alpha * 0.35f);

            //绘制粒子
            DrawParticles(spriteBatch, alpha);

            //绘制主面板
            DrawPanel(spriteBatch, alpha);

            //绘制内容
            if (contentFade > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFade);
            }
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private void DrawBackdrop(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //顶部渐变遮罩
            int gradientHeight = 280;
            for (int i = 0; i < gradientHeight; i++) {
                float t = 1f - i / (float)gradientHeight;
                float gradientAlpha = t * t * alpha;
                Rectangle line = new(0, i, Main.screenWidth, 1);
                spriteBatch.Draw(pixel, line, new Rectangle(0, 0, 1, 1), Color.Black * gradientAlpha);
            }
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

                Color glowColor = color * 0.3f;
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

            //背景渐变(蓝色调任务风格)
            Color bgTop = new Color(20, 28, 38);
            Color bgBottom = new Color(30, 42, 55);
            DrawGradientRoundedRect(spriteBatch, panelRect, bgTop * (alpha * 0.97f), bgBottom * (alpha * 0.97f), CornerRadius);

            //内发光效果
            float innerGlowIntensity = 0.12f + breatheAnim * 0.08f;
            DrawInnerGlow(spriteBatch, panelRect, new Color(80, 140, 200) * (alpha * innerGlowIntensity), CornerRadius, 18);

            //流光边框
            DrawAnimatedBorder(spriteBatch, panelRect, alpha);

            //顶部高光条
            Rectangle highlightBar = new(panelRect.X + 20, panelRect.Y + 2, panelRect.Width - 40, 2);
            float highlightAlpha = 0.35f + breatheAnim * 0.2f;
            DrawHorizontalGradient(spriteBatch, highlightBar,
                Color.Transparent, new Color(150, 200, 255) * (alpha * highlightAlpha), Color.Transparent);

            //角落装饰
            DrawCornerOrnaments(spriteBatch, panelRect, alpha);
        }

        private void DrawAnimatedBorder(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color baseColor = new Color(60, 100, 140) * (alpha * 0.8f);
            DrawRoundedRectBorder(spriteBatch, rect, baseColor, CornerRadius, 2);

            float shimmerPos = (shimmerPhase % 4f) / 4f;

            for (int i = 0; i < 3; i++) {
                float offset = (shimmerPos + i * 0.33f) % 1f;
                Vector2 pos = GetPointOnRectPerimeter(rect, offset);
                float intensity = MathF.Sin(offset * MathHelper.Pi) * 0.8f;
                Color shimmerColor = new Color(100, 180, 255) * (alpha * intensity);

                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), shimmerColor,
                    0f, new Vector2(0.5f), new Vector2(8f, 4f), SpriteEffects.None, 0f);

                for (int j = 1; j <= 5; j++) {
                    float trailOffset = (offset - j * 0.01f + 1f) % 1f;
                    Vector2 trailPos = GetPointOnRectPerimeter(rect, trailOffset);
                    float trailIntensity = intensity * (1f - j / 6f);
                    spriteBatch.Draw(pixel, trailPos, new Rectangle(0, 0, 1, 1),
                        shimmerColor * trailIntensity * 0.5f, 0f, new Vector2(0.5f),
                        new Vector2(6f - j, 3f - j * 0.4f), SpriteEffects.None, 0f);
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
            float ornamentAlpha = alpha * (0.5f + breatheAnim * 0.4f);
            Color ornamentColor = new Color(100, 180, 255) * ornamentAlpha;

            Vector2[] corners = [
                new(rect.X + 8, rect.Y + 8),
                new(rect.Right - 8, rect.Y + 8),
                new(rect.X + 8, rect.Bottom - 8),
                new(rect.Right - 8, rect.Bottom - 8)
            ];

            for (int i = 0; i < 4; i++) {
                float rot = globalTime * 0.5f;

                spriteBatch.Draw(pixel, corners[i], new Rectangle(0, 0, 1, 1), ornamentColor,
                    MathHelper.PiOver4 + rot * 0.1f, new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);

                for (int j = 0; j < 4; j++) {
                    float rayRot = j * MathHelper.PiOver2 + globalTime * 0.3f;
                    Vector2 rayDir = rayRot.ToRotationVector2();
                    spriteBatch.Draw(pixel, corners[i] + rayDir * 4f, new Rectangle(0, 0, 1, 1),
                        ornamentColor * 0.5f, rayRot, new Vector2(0f, 0.5f), new Vector2(7f, 1.2f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            float scale = panelScaleAnim;

            float showcaseWidth = IconShowcaseWidth * scale;
            float textAreaWidth = Size.X - showcaseWidth - Padding * scale * 2;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding * scale, Padding * scale);
            string title = TitleText.Value;

            float titleGlow = 0.5f + breatheAnim * 0.5f;
            Color titleGlowColor = new Color(100, 180, 255) * (alpha * titleGlow * 0.4f);
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + globalTime * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * (3f + breatheAnim * 2f);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlowColor, scale);
            }

            Color titleColor = Color.Lerp(new Color(200, 230, 255), new Color(150, 200, 255), breatheAnim * 0.3f);
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor * alpha, scale);

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, 38 * scale);
            Vector2 dividerEnd = dividerStart + new Vector2(textAreaWidth, 0);
            DrawAnimatedDivider(spriteBatch, dividerStart, dividerEnd, alpha);

            //描述文本
            Vector2 descPos = dividerStart + new Vector2(0, 16 * scale);
            string desc = DescText.Value;
            string[] lines = desc.Split('\n');

            float lineHeight = FontAssets.MouseText.Value.MeasureString("A").Y * 0.85f * scale;
            Color textColor = new Color(200, 210, 220) * alpha;

            for (int i = 0; i < lines.Length; i++) {
                Vector2 linePos = descPos + new Vector2(0, i * lineHeight);
                Utils.DrawBorderString(spriteBatch, lines[i], linePos, textColor, 0.85f * scale);
            }

            //右侧图标展示区
            Rectangle showcaseRect = new(
                (int)(DrawPosition.X + Size.X - showcaseWidth - Padding * scale * 0.5f),
                (int)(DrawPosition.Y + Padding * scale),
                (int)showcaseWidth,
                (int)(Size.Y - ButtonHeight * scale - Padding * scale * 2f)
            );
            DrawIconShowcase(spriteBatch, showcaseRect, alpha, scale);

            //按钮
            float buttonY = DrawPosition.Y + Size.Y - Padding * scale - ButtonHeight * scale;
            float buttonCenterX = DrawPosition.X + Padding * scale + textAreaWidth / 2f;
            float buttonSpacing = 16f * scale;
            float scaledButtonWidth = ButtonWidth * scale;
            float scaledButtonHeight = ButtonHeight * scale;

            confirmButtonRect = new Rectangle(
                (int)(buttonCenterX - scaledButtonWidth - buttonSpacing / 2f),
                (int)buttonY,
                (int)scaledButtonWidth,
                (int)scaledButtonHeight
            );

            cancelButtonRect = new Rectangle(
                (int)(buttonCenterX + buttonSpacing / 2f),
                (int)buttonY,
                (int)scaledButtonWidth,
                (int)scaledButtonHeight
            );

            DrawButton(spriteBatch, confirmButtonRect, ConfirmText.Value, confirmHoverAnim, confirmPressAnim, alpha, true, scale);
            DrawButton(spriteBatch, cancelButtonRect, CancelText.Value, cancelHoverAnim, cancelPressAnim, alpha, false, scale);
        }

        private void DrawIconShowcase(SpriteBatch spriteBatch, Rectangle rect, float alpha, float scale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color bgTop = new Color(25, 35, 45) * (alpha * 0.9f);
            Color bgBottom = new Color(18, 25, 32) * (alpha * 0.9f);
            DrawGradientRoundedRect(spriteBatch, rect, bgTop, bgBottom, 8f);

            Color borderColor = new Color(60, 100, 140) * (alpha * 0.7f);
            DrawRoundedRectBorder(spriteBatch, rect, borderColor, 8f, 1);

            DrawInnerGlow(spriteBatch, rect, new Color(80, 150, 200) * (alpha * 0.06f), 8f, 10);

            Vector2 iconCenter = new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            float floatOffset = MathF.Sin(iconFloatPhase) * 3f;
            Vector2 iconPos = iconCenter + new Vector2(0, floatOffset);

            //使用SoftGlow绘制光晕
            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            float glowScale = (0.7f + iconGlowIntensity * 0.3f) * scale;
            Color glowColor = new Color(80, 150, 220, 0) * (alpha * 0.3f * iconGlowIntensity);
            spriteBatch.Draw(softGlow, iconPos, null, glowColor, 0f,
                softGlow.Size() / 2f, glowScale, SpriteEffects.None, 0f);

            //绘制任务书图标
            Texture2D questIcon = QuestLog.QuestLogStart?.Value;
            if (questIcon != null) {
                float iconScale = 0.8f * scale;
                Rectangle rectangle = questIcon.GetRectangle(2, 3);
                spriteBatch.Draw(questIcon, iconPos, rectangle, Color.White * alpha,
                    0f, rectangle.Size() / 2f, iconScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawAnimatedDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float length = (end - start).Length();
            if (length < 1f) return;
            Vector2 dir = Vector2.Normalize(end - start);

            Color baseColor = new Color(50, 80, 100) * (alpha * 0.6f);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), baseColor, 0f,
                Vector2.Zero, new Vector2(length, 1f), SpriteEffects.None, 0f);

            float shimmerT = (globalTime * 0.5f) % 1f;
            Vector2 shimmerPos = Vector2.Lerp(start, end, shimmerT);
            Color shimmerColor = new Color(100, 180, 255, 0) * (alpha * 0.7f);

            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            float glowScale = 0.12f;
            spriteBatch.Draw(softGlow, shimmerPos, null, shimmerColor * 0.5f, 0f,
                softGlow.Size() / 2f, new Vector2(glowScale * 2f, glowScale * 0.4f), SpriteEffects.None, 0f);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            float hoverAnim, float pressAnim, float alpha, bool isConfirm, float scale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Rectangle drawRect = rect;
            if (pressAnim > 0.01f) {
                int pressOffset = (int)(pressAnim * 2f);
                drawRect.Y += pressOffset;
            }

            int expandAmount = (int)(hoverAnim * 3f);
            drawRect.Inflate(expandAmount, expandAmount / 2);

            Color bgTop, bgBottom;
            if (isConfirm) {
                bgTop = Color.Lerp(new Color(25, 50, 70), new Color(35, 70, 100), hoverAnim);
                bgBottom = Color.Lerp(new Color(18, 35, 50), new Color(25, 50, 70), hoverAnim);
            }
            else {
                bgTop = Color.Lerp(new Color(50, 40, 35), new Color(70, 55, 45), hoverAnim);
                bgBottom = Color.Lerp(new Color(35, 28, 22), new Color(50, 38, 30), hoverAnim);
            }
            DrawGradientRoundedRect(spriteBatch, drawRect, bgTop * (alpha * 0.95f), bgBottom * (alpha * 0.95f), 6f);

            Color borderColor = isConfirm
                ? Color.Lerp(new Color(70, 130, 180), new Color(100, 180, 255), hoverAnim)
                : Color.Lerp(new Color(130, 100, 70), new Color(180, 140, 100), hoverAnim);
            DrawRoundedRectBorder(spriteBatch, drawRect, borderColor * alpha, 6f, 1 + (int)(hoverAnim));

            if (hoverAnim > 0.01f) {
                Color innerGlow = (isConfirm ? new Color(100, 180, 255) : new Color(200, 150, 100)) * (alpha * hoverAnim * 0.12f);
                DrawInnerGlow(spriteBatch, drawRect, innerGlow, 6f, 8);
            }

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.85f * scale;
            Vector2 textPos = drawRect.Center.ToVector2() - textSize / 2f + new Vector2(0, 2);

            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 2),
                Color.Black * (alpha * 0.5f), 0.85f * scale);

            Color textColor = Color.Lerp(new Color(180, 200, 220), Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.85f * scale);

            if (hoverAnim > 0.3f) {
                Color textGlow = (isConfirm ? new Color(100, 180, 255) : new Color(220, 180, 140)) * (alpha * (hoverAnim - 0.3f) * 0.5f);
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
