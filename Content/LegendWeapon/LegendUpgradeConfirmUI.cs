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

namespace CalamityOverhaul.Content.LegendWeapon
{
    /// <summary>
    /// 传奇武器升级确认UI，在检测到进入高等级世界时弹出确认
    /// </summary>
    internal class LegendUpgradeConfirmUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static LegendUpgradeConfirmUI Instance => UIHandleLoader.GetUIHandleOfType<LegendUpgradeConfirmUI>();

        //本地化文本
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText DescText { get; private set; }
        public static LocalizedText ConfirmText { get; private set; }
        public static LocalizedText CancelText { get; private set; }
        public static LocalizedText Success { get; private set; }

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

        //物品图标动画
        private float itemFloatPhase;
        private float itemGlowIntensity;
        private float itemRotation;

        //粒子系统
        private readonly List<FloatingParticle> particles = [];
        private float particleSpawnTimer;

        //布局常量
        private const float PanelWidth = 420f;
        private const float PanelHeight = 260f;
        private const float Padding = 24f;
        private const float ButtonHeight = 42f;
        private const float ButtonWidth = 150f;
        private const float CornerRadius = 12f;

        //按钮
        private Rectangle confirmButtonRect;
        private Rectangle cancelButtonRect;
        private bool hoveringConfirm;
        private bool hoveringCancel;

        //待升级数据
        private static Item pendingItem;
        private static LegendData pendingLegendData;
        private static int targetLevel;
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
            TitleText = this.GetLocalization(nameof(TitleText), () => "传奇武器升级确认");
            DescText = this.GetLocalization(nameof(DescText), () => "检测到当前世界等级高于武器等级\n是否将{0}升级到等级 {1}？");
            ConfirmText = this.GetLocalization(nameof(ConfirmText), () => "确认升级");
            CancelText = this.GetLocalization(nameof(CancelText), () => "取消");
            Success = this.GetLocalization(nameof(Success), () => "[ITEM]已经升级到[LEVEL]级");
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
            panelSlideOffset = 50f;
            panelScaleAnim = 0.8f;
            confirmHoverAnim = 0f;
            cancelHoverAnim = 0f;
            confirmPressAnim = 0f;
            cancelPressAnim = 0f;
            particles.Clear();
        }

        /// <summary>
        /// 请求显示升级确认UI
        /// </summary>
        public static void RequestUpgrade(Item item, LegendData legendData, int newLevel) {
            if (isPending || item == null || legendData == null) {
                return;
            }

            pendingItem = item;
            pendingLegendData = legendData;
            targetLevel = newLevel;
            isPending = true;

            //播放打开音效
            SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f, Pitch = 0.2f });
        }

        /// <summary>
        /// 取消待处理的升级请求
        /// </summary>
        public static void CancelPending() {
            isPending = false;
            pendingItem = null;
            pendingLegendData = null;
            targetLevel = 0;
        }

        public override void Update() {
            globalTime += 0.016f;

            //主面板展开动画(带弹性)
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

            //面板滑入动画
            float targetSlide = isPending && !closing ? 0f : 60f;
            panelSlideOffset += (targetSlide - panelSlideOffset) * 0.15f;

            //面板缩放动画(带过冲效果)
            float targetScale = isPending && !closing ? 1f : 0.85f;
            panelScaleAnim += (targetScale - panelScaleAnim) * 0.1f;
            if (panelScaleAnim > 0.98f && targetScale == 1f) {
                panelScaleAnim += (1.02f - panelScaleAnim) * 0.3f;
            }

            //内容淡入(延迟启动)
            if (showProgress > 0.5f && !closing) {
                float targetFade = 1f;
                contentFade += (targetFade - contentFade) * 0.12f;
            }
            else {
                contentFade *= 0.85f;
            }
            contentFade = Math.Clamp(contentFade, 0f, 1f);

            //呼吸动画
            breatheAnim = MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f;
            shimmerPhase = globalTime * 2f;

            //物品浮动动画
            itemFloatPhase += 0.04f;
            itemGlowIntensity = 0.6f + MathF.Sin(globalTime * 3f) * 0.4f;
            itemRotation = MathF.Sin(globalTime * 0.8f) * 0.05f;

            //按钮悬停动画(平滑过渡)
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
                if (particleSpawnTimer > 3f) {
                    SpawnParticle();
                    particleSpawnTimer = 0f;
                }
            }

            //计算面板位置(居中偏下)
            float scaledWidth = PanelWidth * panelScaleAnim;
            float scaledHeight = PanelHeight * panelScaleAnim;
            Vector2 panelCenter = new(Main.screenWidth / 2f, Main.screenHeight - PanelHeight / 2f - 40f + panelSlideOffset);
            DrawPosition = panelCenter - new Vector2(scaledWidth, scaledHeight) / 2f;
            Size = new Vector2(scaledWidth, scaledHeight);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)scaledWidth, (int)scaledHeight);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage && showProgress > 0.5f) {
                player.mouseInterface = true;
            }

            //按钮位置(相对于缩放后的面板)
            float buttonY = DrawPosition.Y + scaledHeight - Padding * panelScaleAnim - ButtonHeight * panelScaleAnim;
            float centerX = DrawPosition.X + scaledWidth / 2f;
            float buttonSpacing = 24f * panelScaleAnim;
            float scaledButtonWidth = ButtonWidth * panelScaleAnim;
            float scaledButtonHeight = ButtonHeight * panelScaleAnim;

            confirmButtonRect = new Rectangle(
                (int)(centerX - scaledButtonWidth - buttonSpacing / 2f),
                (int)buttonY,
                (int)scaledButtonWidth,
                (int)scaledButtonHeight
            );

            cancelButtonRect = new Rectangle(
                (int)(centerX + buttonSpacing / 2f),
                (int)buttonY,
                (int)scaledButtonWidth,
                (int)scaledButtonHeight
            );

            //悬停检测
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
                p.Velocity.Y -= 0.02f;
                p.Rotation += p.RotationSpeed;
                particles[i] = p;

                if (p.Life <= 0f) {
                    particles.RemoveAt(i);
                }
            }
        }

        private void SpawnParticle() {
            if (particles.Count > 30) return;

            var p = new FloatingParticle {
                Position = new Vector2(
                    DrawPosition.X + Main.rand.NextFloat(Size.X),
                    DrawPosition.Y + Size.Y
                ),
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.5f, -0.5f)),
                Life = Main.rand.NextFloat(1.5f, 3f),
                MaxLife = 0f,
                Size = Main.rand.NextFloat(2f, 5f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f),
                BaseColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 150, 50), Main.rand.NextFloat())
            };
            p.MaxLife = p.Life;
            particles.Add(p);
        }

        private void OnConfirm() {
            if (pendingLegendData != null && pendingItem != null) {
                pendingLegendData.UpgradeWorldName = Main.worldName;
                pendingLegendData.UpgradeWorldFullName = SaveWorld.WorldFullName;
                pendingLegendData.Level = targetLevel;

                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.4f });

                string message = Success.Value.Replace("[ITEM]", pendingItem.Name).Replace("[LEVEL]", targetLevel.ToString());
                CombatText.NewText(player.Hitbox, Color.Gold, message, true);

                //生成庆祝粒子
                for (int i = 0; i < 15; i++) {
                    SpawnParticle();
                }
            }

            BeginClose();
        }

        private void OnCancel() {
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            if (pendingLegendData != null) {
                pendingLegendData.DontUpgradeName = SaveWorld.WorldFullName;
            }
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
            DrawBackdrop(spriteBatch, alpha * 0.4f);

            //绘制粒子(在面板后面)
            DrawParticles(spriteBatch, alpha);

            //绘制主面板
            DrawPanel(spriteBatch, alpha);

            //绘制内容
            if (contentFade > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFade);
            }
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
        }

        private void DrawBackdrop(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //底部渐变遮罩
            int gradientHeight = 300;
            int startY = Main.screenHeight - gradientHeight;
            for (int i = 0; i < gradientHeight; i++) {
                float t = i / (float)gradientHeight;
                float gradientAlpha = t * t * alpha;
                Rectangle line = new(0, startY + i, Main.screenWidth, 1);
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

                //发光层
                Color glowColor = color * 0.3f;
                spriteBatch.Draw(pixel, p.Position, new Rectangle(0, 0, 1, 1), glowColor, p.Rotation,
                    new Vector2(0.5f), new Vector2(size * 2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = UIHitBox;

            //多层阴影(更柔和的投影效果)
            for (int i = 4; i >= 1; i--) {
                Rectangle shadowRect = panelRect;
                shadowRect.Offset(i * 2, i * 3);
                float shadowAlpha = alpha * 0.15f * (5 - i) / 4f;
                DrawRoundedRect(spriteBatch, shadowRect, Color.Black * shadowAlpha, CornerRadius + i);
            }

            //背景渐变(深色质感)
            Color bgTop = new Color(25, 22, 18);
            Color bgBottom = new Color(45, 38, 28);
            DrawGradientRoundedRect(spriteBatch, panelRect, bgTop * (alpha * 0.97f), bgBottom * (alpha * 0.97f), CornerRadius);

            //内发光效果
            float innerGlowIntensity = 0.15f + breatheAnim * 0.1f;
            DrawInnerGlow(spriteBatch, panelRect, new Color(180, 140, 60) * (alpha * innerGlowIntensity), CornerRadius, 20);

            //流光边框
            DrawAnimatedBorder(spriteBatch, panelRect, alpha);

            //顶部高光条
            Rectangle highlightBar = new(panelRect.X + 20, panelRect.Y + 2, panelRect.Width - 40, 2);
            float highlightAlpha = 0.4f + breatheAnim * 0.2f;
            DrawHorizontalGradient(spriteBatch, highlightBar,
                Color.Transparent, new Color(255, 220, 150) * (alpha * highlightAlpha), Color.Transparent);

            //角落装饰
            DrawCornerOrnaments(spriteBatch, panelRect, alpha);
        }

        private void DrawAnimatedBorder(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //基础边框
            Color baseColor = new Color(120, 100, 60) * (alpha * 0.8f);
            DrawRoundedRectBorder(spriteBatch, rect, baseColor, CornerRadius, 2);

            //流光效果
            float shimmerPos = (shimmerPhase % 4f) / 4f;
            int perimeter = (rect.Width + rect.Height) * 2;

            for (int i = 0; i < 3; i++) {
                float offset = (shimmerPos + i * 0.33f) % 1f;
                Vector2 pos = GetPointOnRectPerimeter(rect, offset);
                float intensity = MathF.Sin(offset * MathHelper.Pi) * 0.8f;
                Color shimmerColor = new Color(255, 200, 100) * (alpha * intensity);

                //流光点
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), shimmerColor,
                    0f, new Vector2(0.5f), new Vector2(8f, 4f), SpriteEffects.None, 0f);

                //流光拖尾
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
            float ornamentAlpha = alpha * (0.6f + breatheAnim * 0.4f);
            Color ornamentColor = new Color(255, 200, 100) * ornamentAlpha;

            Vector2[] corners = [
                new(rect.X + 8, rect.Y + 8),
                new(rect.Right - 8, rect.Y + 8),
                new(rect.X + 8, rect.Bottom - 8),
                new(rect.Right - 8, rect.Bottom - 8)
            ];

            float[] rotations = [0f, MathHelper.PiOver2, -MathHelper.PiOver2, MathHelper.Pi];

            for (int i = 0; i < 4; i++) {
                float rot = rotations[i] + globalTime * 0.5f;

                //菱形装饰
                spriteBatch.Draw(pixel, corners[i], new Rectangle(0, 0, 1, 1), ornamentColor,
                    MathHelper.PiOver4 + rot * 0.1f, new Vector2(0.5f), new Vector2(6f, 6f), SpriteEffects.None, 0f);

                //光芒
                for (int j = 0; j < 4; j++) {
                    float rayRot = j * MathHelper.PiOver2 + globalTime * 0.3f;
                    Vector2 rayDir = rayRot.ToRotationVector2();
                    spriteBatch.Draw(pixel, corners[i] + rayDir * 4f, new Rectangle(0, 0, 1, 1),
                        ornamentColor * 0.5f, rayRot, new Vector2(0f, 0.5f), new Vector2(8f, 1.5f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scale = panelScaleAnim;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding * scale, Padding * scale);
            string title = TitleText.Value;

            //标题光晕
            float titleGlow = 0.5f + breatheAnim * 0.5f;
            Color titleGlowColor = new Color(255, 200, 100) * (alpha * titleGlow * 0.4f);
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + globalTime * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * (3f + breatheAnim * 2f);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlowColor, scale);
            }

            //标题主体
            Color titleColor = Color.Lerp(new Color(255, 240, 200), new Color(255, 200, 100), breatheAnim * 0.3f);
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor * alpha, scale);

            //分割线(带渐变和动画)
            Vector2 dividerStart = titlePos + new Vector2(0, 40 * scale);
            Vector2 dividerEnd = dividerStart + new Vector2((PanelWidth - Padding * 2) * scale, 0);

            //分割线背景
            DrawAnimatedDivider(spriteBatch, dividerStart, dividerEnd, alpha);

            //描述文本
            if (pendingItem != null) {
                Vector2 descPos = dividerStart + new Vector2(0, 20 * scale);
                string itemName = pendingItem.Name;
                string desc = string.Format(DescText.Value, itemName, targetLevel);
                string[] lines = desc.Split('\n');

                float lineHeight = FontAssets.MouseText.Value.MeasureString("A").Y * 0.9f * scale;
                Color textColor = new Color(220, 210, 190) * alpha;
                Color highlightColor = new Color(255, 220, 150) * alpha;

                for (int i = 0; i < lines.Length; i++) {
                    Vector2 linePos = descPos + new Vector2(0, i * lineHeight);
                    string lineText = lines[i];

                    //如果包含物品名或等级，高亮显示
                    if (lineText.Contains(itemName) || lineText.Contains(targetLevel.ToString())) {
                        Utils.DrawBorderString(spriteBatch, lineText, linePos, highlightColor, 0.9f * scale);
                    }
                    else {
                        Utils.DrawBorderString(spriteBatch, lineText, linePos, textColor, 0.9f * scale);
                    }
                }

                //物品图标展示区
                float iconAreaY = descPos.Y + lineHeight * lines.Length + 15 * scale;
                DrawItemShowcase(spriteBatch, pendingItem, new Vector2(DrawPosition.X + Size.X / 2f, iconAreaY), alpha, scale);
            }

            //按钮
            DrawButton(spriteBatch, confirmButtonRect, ConfirmText.Value, confirmHoverAnim, confirmPressAnim, alpha, true, scale);
            DrawButton(spriteBatch, cancelButtonRect, CancelText.Value, cancelHoverAnim, cancelPressAnim, alpha, false, scale);
        }

        private void DrawAnimatedDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float length = (end - start).Length();
            Vector2 dir = Vector2.Normalize(end - start);

            //底层线条
            Color baseColor = new Color(80, 70, 50) * (alpha * 0.6f);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), baseColor, 0f,
                Vector2.Zero, new Vector2(length, 1f), SpriteEffects.None, 0f);

            //流光效果
            float shimmerT = (globalTime * 0.5f) % 1f;
            Vector2 shimmerPos = Vector2.Lerp(start, end, shimmerT);
            Color shimmerColor = new Color(255, 200, 100) * (alpha * 0.8f);

            //流光主体
            float shimmerWidth = length * 0.15f;
            spriteBatch.Draw(pixel, shimmerPos - dir * shimmerWidth / 2f, new Rectangle(0, 0, 1, 1),
                shimmerColor, 0f, new Vector2(0, 0.5f), new Vector2(shimmerWidth, 2f), SpriteEffects.None, 0f);

            //流光光晕
            spriteBatch.Draw(pixel, shimmerPos - dir * shimmerWidth, new Rectangle(0, 0, 1, 1),
                shimmerColor * 0.3f, 0f, new Vector2(0, 0.5f), new Vector2(shimmerWidth * 2f, 4f), SpriteEffects.None, 0f);
        }

        private void DrawItemShowcase(SpriteBatch spriteBatch, Item item, Vector2 center, float alpha, float scale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //物品浮动效果
            float floatOffset = MathF.Sin(itemFloatPhase) * 4f;
            Vector2 itemPos = center + new Vector2(0, floatOffset);

            //背景光环
            float glowSize = 50f + itemGlowIntensity * 20f;
            Color glowColor = new Color(255, 180, 80) * (alpha * 0.2f * itemGlowIntensity);
            for (int i = 3; i >= 0; i--) {
                float layerSize = glowSize * (1f + i * 0.3f);
                float layerAlpha = 0.15f / (i + 1);
                spriteBatch.Draw(pixel, itemPos, new Rectangle(0, 0, 1, 1),
                    glowColor * layerAlpha, globalTime * 0.2f + i * 0.5f,
                    new Vector2(0.5f), new Vector2(layerSize), SpriteEffects.None, 0f);
            }

            //旋转光芒
            int rayCount = 6;
            for (int i = 0; i < rayCount; i++) {
                float rayAngle = MathHelper.TwoPi * i / rayCount + globalTime * 0.3f;
                float rayLength = 35f + MathF.Sin(globalTime * 2f + i) * 10f;
                Color rayColor = new Color(255, 200, 100) * (alpha * 0.3f);

                spriteBatch.Draw(pixel, itemPos, new Rectangle(0, 0, 1, 1), rayColor,
                    rayAngle, new Vector2(0, 0.5f), new Vector2(rayLength, 2f), SpriteEffects.None, 0f);
            }

            //物品图标
            if (item.type > ItemID.None) {
                float itemScale = 1.2f * scale;
                VaultUtils.SimpleDrawItem(spriteBatch, item.type, itemPos,
                    item.width, itemScale, itemRotation, Color.White * alpha);
            }

            //等级徽章
            string levelText = $"Lv.{targetLevel}";
            Vector2 levelPos = itemPos + new Vector2(30f, -20f);
            Color badgeColor = new Color(60, 50, 30) * (alpha * 0.9f);
            Color badgeBorder = new Color(255, 200, 100) * alpha;

            //徽章背景
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(levelText) * 0.7f;
            Rectangle badgeRect = new((int)(levelPos.X - textSize.X / 2 - 6), (int)(levelPos.Y - textSize.Y / 2 - 3),
                (int)(textSize.X + 12), (int)(textSize.Y + 6));
            spriteBatch.Draw(pixel, badgeRect, new Rectangle(0, 0, 1, 1), badgeColor);
            DrawRoundedRectBorder(spriteBatch, badgeRect, badgeBorder, 3f, 1);

            Utils.DrawBorderString(spriteBatch, levelText, levelPos - textSize / 2 + new Vector2(0, 2),
                new Color(255, 220, 150) * alpha, 0.7f);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            float hoverAnim, float pressAnim, float alpha, bool isConfirm, float scale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //按压效果
            Rectangle drawRect = rect;
            if (pressAnim > 0.01f) {
                int pressOffset = (int)(pressAnim * 2f);
                drawRect.Y += pressOffset;
            }

            //悬停膨胀效果
            int expandAmount = (int)(hoverAnim * 3f);
            drawRect.Inflate(expandAmount, expandAmount / 2);

            //背景渐变
            Color bgTop, bgBottom;
            if (isConfirm) {
                bgTop = Color.Lerp(new Color(50, 45, 25), new Color(70, 60, 30), hoverAnim);
                bgBottom = Color.Lerp(new Color(35, 30, 18), new Color(50, 42, 22), hoverAnim);
            }
            else {
                bgTop = Color.Lerp(new Color(50, 35, 35), new Color(70, 45, 45), hoverAnim);
                bgBottom = Color.Lerp(new Color(35, 25, 25), new Color(50, 32, 32), hoverAnim);
            }
            DrawGradientRoundedRect(spriteBatch, drawRect, bgTop * (alpha * 0.95f), bgBottom * (alpha * 0.95f), 6f);

            //边框
            Color borderColor = isConfirm
                ? Color.Lerp(new Color(150, 130, 70), new Color(255, 200, 100), hoverAnim)
                : Color.Lerp(new Color(150, 100, 100), new Color(220, 150, 150), hoverAnim);
            DrawRoundedRectBorder(spriteBatch, drawRect, borderColor * alpha, 6f, 1 + (int)(hoverAnim));

            //悬停时的内发光
            if (hoverAnim > 0.01f) {
                Color innerGlow = (isConfirm ? new Color(255, 200, 100) : new Color(255, 150, 150)) * (alpha * hoverAnim * 0.15f);
                DrawInnerGlow(spriteBatch, drawRect, innerGlow, 6f, 10);
            }

            //按钮文字
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.85f * scale;
            Vector2 textPos = drawRect.Center.ToVector2() - textSize / 2f + new Vector2(0, 2);

            //文字阴影
            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 2),
                Color.Black * (alpha * 0.5f), 0.85f * scale);

            //文字主体
            Color textColor = Color.Lerp(new Color(200, 190, 170), Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.85f * scale);

            //悬停时的文字光晕
            if (hoverAnim > 0.3f) {
                Color textGlow = (isConfirm ? new Color(255, 200, 100) : new Color(255, 180, 180)) * (alpha * (hoverAnim - 0.3f) * 0.5f);
                Utils.DrawBorderString(spriteBatch, text, textPos, textGlow, 0.85f * scale);
            }
        }

        #region 绘制辅助方法

        private static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, float radius) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //简化的圆角矩形(用多个矩形近似)
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            //中心区域
            Rectangle center = new(rect.X + r, rect.Y, rect.Width - r * 2, rect.Height);
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color);

            //左右区域
            Rectangle left = new(rect.X, rect.Y + r, r, rect.Height - r * 2);
            Rectangle right = new(rect.Right - r, rect.Y + r, r, rect.Height - r * 2);
            sb.Draw(pixel, left, new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, right, new Rectangle(0, 0, 1, 1), color);

            //四个角(用小矩形填充)
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

                //计算圆角内缩
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

            //上下边
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Bottom - thickness, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);

            //左右边
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);

            //四个角弧线
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
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < glowSize; i++) {
                float t = i / (float)glowSize;
                float alpha = (1f - t) * (1f - t);
                Color glowColor = color * alpha;

                Rectangle glowRect = rect;
                glowRect.Inflate(-i, -i);

                if (glowRect.Width > 0 && glowRect.Height > 0) {
                    DrawRoundedRectBorder(sb, glowRect, glowColor, Math.Max(0, radius - i), 1);
                }
            }
        }

        private static void DrawHorizontalGradient(SpriteBatch sb, Rectangle rect, Color left, Color center, Color right) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int halfWidth = rect.Width / 2;

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
