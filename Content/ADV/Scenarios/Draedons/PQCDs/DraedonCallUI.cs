using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs
{
    /// <summary>
    /// 嘉登呼叫UI
    /// </summary>
    internal class DraedonCallUI : UIHandle, ILocalizedModType
    {
        public static DraedonCallUI Instance => UIHandleLoader.GetUIHandleOfType<DraedonCallUI>();

        //UI状态
        private bool _active;
        public override bool Active {
            get => _active || uiAlpha > 0f;
            set => _active = value;
        }

        public override float RenderPriority => 0.9f;

        public string LocalizationCategory => "UI";

        //动画参数
        private float uiAlpha = 0f;
        private float panelSlideProgress = 0f;
        private const float FadeSpeed = 0.08f;
        private const float SlideSpeed = 0.12f;

        //科技动画参数
        private float scanLineTimer = 0f;
        private float circuitPulseTimer = 0f;
        private float hologramFlicker = 0f;
        private float portraitGlowPulse = 0f;
        private float callButtonPulse = 0f;

        //UI尺寸
        private const int PanelWidth = 280;
        private const int PanelHeight = 360;
        private Vector2 panelPosition;

        //嘉登头像参数
        private const float PortraitSize = 160f;
        private Vector2 portraitPosition;
        private float portraitRotation = 0f;
        private bool isHoveringPortrait = false;
        private float portraitHoverProgress = 0f;

        //呼叫按钮参数
        private Rectangle callButtonRect;
        private bool isHoveringButton = false;
        private float buttonHoverProgress = 0f;
        private bool isCalling = false;
        private float callProgress = 0f;

        //科技粒子
        private readonly List<TechParticle> techParticles = new();
        private int particleSpawnTimer = 0;

        //状态文本
        private string statusText = "";
        private float statusTextAlpha = 0f;

        public override void Update() {
            //同步商店UI的激活状态
            _active = DraedonShopUI.Instance.Active;

            //更新动画进度
            if (_active) {
                if (uiAlpha < 1f) uiAlpha += FadeSpeed;
                if (panelSlideProgress < 1f) panelSlideProgress += SlideSpeed;
            }
            else {
                if (uiAlpha > 0f) uiAlpha -= FadeSpeed * 1.5f;
                if (panelSlideProgress > 0f) panelSlideProgress -= SlideSpeed * 1.5f;
                if (uiAlpha <= 0f) {
                    CleanupEffects();
                    return;
                }
            }

            uiAlpha = MathHelper.Clamp(uiAlpha, 0f, 1f);
            panelSlideProgress = MathHelper.Clamp(panelSlideProgress, 0f, 1f);

            //更新科技动画
            UpdateTechEffects();

            //更新粒子
            UpdateParticles();

            //计算面板位置，从左侧滑入，位于商店UI左侧
            float slideOffset = (1f - CWRUtils.EaseOutCubic(panelSlideProgress)) * PanelWidth;
            Vector2 shopPanelPos = new Vector2(Main.screenWidth - 680, (Main.screenHeight - 640) / 2f);
            panelPosition = new Vector2(shopPanelPos.X - PanelWidth - 20 + slideOffset, shopPanelPos.Y + (640 - PanelHeight) / 2f);

            //计算头像位置
            portraitPosition = panelPosition + new Vector2(PanelWidth / 2f, 100);

            //计算呼叫按钮矩形
            callButtonRect = new Rectangle(
                (int)(panelPosition.X + (PanelWidth - 180) / 2f),
                (int)(panelPosition.Y + 240),
                180,
                50
            );

            //更新UI交互
            if (_active && panelSlideProgress > 0.9f) {
                UpdateInteraction();
            }

            //更新悬停动画
            UpdateHoverAnimations();

            //更新呼叫进度
            if (isCalling) {
                callProgress += 0.015f;
                if (callProgress >= 1f) {
                    callProgress = 1f;
                    OnCallComplete();
                }
            }
        }

        private void UpdateTechEffects() {
            scanLineTimer += 0.055f;
            circuitPulseTimer += 0.03f;
            hologramFlicker += 0.1f;
            portraitGlowPulse += 0.06f;
            callButtonPulse += 0.08f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (circuitPulseTimer > MathHelper.TwoPi) circuitPulseTimer -= MathHelper.TwoPi;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (portraitGlowPulse > MathHelper.TwoPi) portraitGlowPulse -= MathHelper.TwoPi;
            if (callButtonPulse > MathHelper.TwoPi) callButtonPulse -= MathHelper.TwoPi;

            portraitRotation = 0f;
        }

        private void UpdateParticles() {
            //科技粒子刷新
            particleSpawnTimer++;
            if (_active && particleSpawnTimer >= 12 && techParticles.Count < 20) {
                particleSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(20f, PanelWidth - 20f),
                    Main.rand.NextFloat(30f, PanelHeight - 30f)
                );
                techParticles.Add(new TechParticle(spawnPos));
            }

            for (int i = techParticles.Count - 1; i >= 0; i--) {
                if (techParticles[i].Update()) {
                    techParticles.RemoveAt(i);
                }
            }
        }

        private void UpdateHoverAnimations() {
            //头像悬停动画
            float targetPortraitHover = isHoveringPortrait ? 1f : 0f;
            portraitHoverProgress = MathHelper.Lerp(portraitHoverProgress, targetPortraitHover, 0.15f);

            //按钮悬停动画
            float targetButtonHover = isHoveringButton ? 1f : 0f;
            buttonHoverProgress = MathHelper.Lerp(buttonHoverProgress, targetButtonHover, 0.15f);

            //状态文本淡入淡出
            if (!string.IsNullOrEmpty(statusText)) {
                statusTextAlpha = Math.Min(statusTextAlpha + 0.05f, 1f);
            }
            else {
                statusTextAlpha = Math.Max(statusTextAlpha - 0.05f, 0f);
            }
        }

        private void CleanupEffects() {
            techParticles.Clear();
            isHoveringPortrait = false;
            isHoveringButton = false;
            portraitHoverProgress = 0f;
            buttonHoverProgress = 0f;
            isCalling = false;
            callProgress = 0f;
            statusText = "";
            statusTextAlpha = 0f;
        }

        private void UpdateInteraction() {
            UIHitBox = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;

                //检测头像悬停
                float distToPortrait = Vector2.Distance(MousePosition, portraitPosition);
                isHoveringPortrait = distToPortrait <= PortraitSize / 2f;

                //检测按钮悬停
                isHoveringButton = callButtonRect.Contains(MousePosition.ToPoint());

                //按钮点击
                if (isHoveringButton && Main.mouseLeft && Main.mouseLeftRelease && !isCalling) {
                    StartCall();
                }
            }
            else {
                isHoveringPortrait = false;
                isHoveringButton = false;
            }
        }

        private void StartCall() {
            isCalling = true;
            callProgress = 0f;
            statusText = "CONNECTING...";

            //播放音效
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.3f });
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Custom/CodebreakerBeam") with { Volume = 0.7f });
        }

        private void OnCallComplete() {
            statusText = "CONNECTED";
            
            //播放完成音效
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.8f, Pitch = 0.5f });

            ExoMechdusaSum.SimpleMode = true;
            NPC.NewNPC(Main.LocalPlayer.FromObjectGetParent(), (int)Main.LocalPlayer.Center.X, (int)Main.LocalPlayer.Center.Y - 220, CWRID.NPC_Draedon);
            _active = false;
            DraedonShopUI.Instance.Active = false;

            //重置状态
            isCalling = false;
            callProgress = 0f;
        }

        private void DrawDarkenBackground(SpriteBatch spriteBatch) {
            Rectangle fullScreen = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, fullScreen, Color.Black * (uiAlpha * 0.7f));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (uiAlpha <= 0f) return;

            DrawDarkenBackground(spriteBatch);

            //绘制主面板
            DrawMainPanel(spriteBatch);

            //绘制嘉登头像
            DrawDraedonPortrait(spriteBatch);

            //绘制呼叫按钮
            DrawCallButton(spriteBatch);

            //绘制状态文本
            DrawStatusText(spriteBatch);

            //绘制科技粒子
            DrawTechParticles(spriteBatch);
        }

        private void DrawMainPanel(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            //深度阴影
            Rectangle shadow = panelRect;
            shadow.Offset(5, 6);
            spriteBatch.Draw(pixel, shadow, Color.Black * (uiAlpha * 0.6f));

            //主背景渐变
            int segments = 30;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle segment = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color techDark = new Color(8, 12, 22);
                Color techMid = new Color(18, 28, 42);

                float pulse = (float)Math.Sin(circuitPulseTimer * 0.6f + t * 2f) * 0.5f + 0.5f;
                Color finalColor = Color.Lerp(techDark, techMid, pulse * 0.5f) * (uiAlpha * 0.94f);

                spriteBatch.Draw(pixel, segment, finalColor);
            }

            //全息闪烁叠加
            float flicker = (float)Math.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            spriteBatch.Draw(pixel, panelRect, new Color(15, 30, 45) * (uiAlpha * 0.2f * flicker));

            //扫描线
            DrawScanLines(spriteBatch, panelRect);

            //内部脉冲发光
            float innerPulse = (float)Math.Sin(circuitPulseTimer * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-4, -4);
            spriteBatch.Draw(pixel, inner, new Color(40, 180, 255) * (uiAlpha * 0.1f * innerPulse));

            //科技边框
            DrawTechFrame(spriteBatch, panelRect, innerPulse);

            //标题
            DrawTitle(spriteBatch);
        }

        private void DrawScanLines(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = new Color(60, 180, 255) * (uiAlpha * 0.15f * intensity);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 10, (int)offsetY, rect.Width - 20, 2), scanColor);
            }
        }

        private void DrawTechFrame(SpriteBatch spriteBatch, Rectangle rect, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color borderColor = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (uiAlpha * 0.9f);

            //外边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), borderColor * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), borderColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), borderColor * 0.9f);

            //内发光边框
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(100, 200, 255) * (uiAlpha * 0.2f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 2), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 2, inner.Height), innerGlow * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), innerGlow * 0.9f);
        }

        private void DrawTitle(SpriteBatch spriteBatch) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = "CALL DRAEDON";
            Vector2 titleSize = font.MeasureString(title) * 1.1f;
            Vector2 titlePos = panelPosition + new Vector2((PanelWidth - titleSize.X) / 2f, 20);

            //标题发光效果
            Color glowColor = new Color(80, 220, 255) * (uiAlpha * 0.8f);
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 offset = angle.ToRotationVector2() * 2.5f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor * 0.4f, 1.1f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * uiAlpha, 1.1f);
        }

        private void DrawDraedonPortrait(SpriteBatch spriteBatch) {
            //获取嘉登头像纹理（使用红色版本作为默认，可以根据状态切换）
            Texture2D portraitTexture = isCalling 
                ? ADVAsset.DraedonRedADV 
                : ADVAsset.DraedonADV;

            if (portraitTexture == null) return;

            //计算缩放
            float baseScale = (PortraitSize / Math.Max(portraitTexture.Width, portraitTexture.Height));
            float hoverScale = 1f + portraitHoverProgress * 0.08f;
            float callScale = 1f + callProgress * 0.15f;
            float finalScale = baseScale * hoverScale * callScale;

            //计算透明度
            float alpha = uiAlpha * (0.9f + portraitHoverProgress * 0.1f);

            //绘制头像背景光环
            DrawPortraitGlow(spriteBatch, portraitPosition, PortraitSize * hoverScale * callScale, alpha);

            //绘制头像边框
            DrawPortraitBorder(spriteBatch, portraitPosition, PortraitSize * hoverScale * callScale, alpha);

            //绘制头像
            float rotation = portraitRotation * (1f + callProgress * 2f);
            spriteBatch.Draw(
                portraitTexture,
                portraitPosition,
                null,
                Color.White * alpha,
                rotation,
                portraitTexture.Size() / 2f,
                finalScale,
                SpriteEffects.None,
                0f
            );

            //呼叫时的额外效果
            if (isCalling) {
                DrawCallingEffect(spriteBatch, portraitPosition, PortraitSize, alpha);
            }
        }

        private void DrawPortraitGlow(SpriteBatch spriteBatch, Vector2 position, float size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(portraitGlowPulse) * 0.5f + 0.5f;

            //多层光晕
            for (int i = 0; i < 3; i++) {
                float glowSize = size * (1.15f + i * 0.12f);
                float glowAlpha = alpha * (0.2f - i * 0.05f) * pulse;
                Color glowColor = new Color(80, 200, 255) * glowAlpha;

                spriteBatch.Draw(
                    pixel,
                    position,
                    null,
                    glowColor,
                    0f,
                    new Vector2(0.5f),
                    glowSize,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawPortraitBorder(SpriteBatch spriteBatch, Vector2 position, float size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(circuitPulseTimer * 1.2f) * 0.5f + 0.5f;
            Color borderColor = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse) * (alpha * 0.8f);

            //圆形边框效果（简化版，使用方形近似）
            int segments = 32;
            float radius = size / 2f;
            for (int i = 0; i < segments; i++) {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;
                Vector2 point1 = position + angle1.ToRotationVector2() * radius;
                Vector2 point2 = position + angle2.ToRotationVector2() * radius;

                DrawLine(spriteBatch, point1, point2, borderColor, 2f);
            }
        }

        private void DrawCallingEffect(SpriteBatch spriteBatch, Vector2 position, float size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //旋转的能量环
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = size * (0.6f + ring * 0.2f) * callProgress;
                float ringRotation = portraitRotation * (2f + ring * 0.5f);
                int segments = 24;

                for (int i = 0; i < segments; i++) {
                    if (Main.rand.NextBool(2)) continue; //随机间隙

                    float angle1 = MathHelper.TwoPi * i / segments + ringRotation;
                    float angle2 = MathHelper.TwoPi * (i + 1) / segments + ringRotation;
                    Vector2 point1 = position + angle1.ToRotationVector2() * ringRadius;
                    Vector2 point2 = position + angle2.ToRotationVector2() * ringRadius;

                    Color ringColor = new Color(255, 100, 100) * (alpha * 0.6f * callProgress);
                    DrawLine(spriteBatch, point1, point2, ringColor, 2f);
                }
            }

            //中心光球
            float coreSize = size * 0.3f * callProgress;
            Color coreColor = new Color(255, 150, 150) * (alpha * 0.8f * callProgress);
            spriteBatch.Draw(
                pixel,
                position,
                null,
                coreColor,
                0f,
                new Vector2(0.5f),
                coreSize,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawCallButton(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //按钮背景
            Color buttonBg = isCalling
                ? Color.Lerp(new Color(60, 20, 20), new Color(100, 40, 40), buttonHoverProgress)
                : Color.Lerp(new Color(20, 60, 100), new Color(40, 100, 160), buttonHoverProgress);
            buttonBg *= uiAlpha * 0.7f;

            spriteBatch.Draw(pixel, callButtonRect, buttonBg);

            //按钮边框
            float pulse = (float)Math.Sin(callButtonPulse) * 0.5f + 0.5f;
            Color borderColor = isCalling
                ? Color.Lerp(new Color(255, 80, 80), new Color(255, 150, 150), pulse)
                : Color.Lerp(new Color(60, 180, 255), new Color(100, 220, 255), pulse);
            borderColor *= uiAlpha * (0.7f + buttonHoverProgress * 0.3f);

            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.X, callButtonRect.Y, callButtonRect.Width, 3), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.X, callButtonRect.Bottom - 3, callButtonRect.Width, 3), borderColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.X, callButtonRect.Y, 3, callButtonRect.Height), borderColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.Right - 3, callButtonRect.Y, 3, callButtonRect.Height), borderColor * 0.9f);

            //呼叫进度条
            if (isCalling && callProgress > 0f) {
                Rectangle progressBar = new Rectangle(
                    callButtonRect.X + 5,
                    callButtonRect.Bottom - 8,
                    (int)((callButtonRect.Width - 10) * callProgress),
                    3
                );
                spriteBatch.Draw(pixel, progressBar, new Color(255, 150, 150) * (uiAlpha * 0.9f));
            }

            //按钮文字
            string buttonText = isCalling ? "CALLING..." : "CALL NOW";
            Vector2 textSize = font.MeasureString(buttonText) * 1.0f;
            Vector2 textPos = new Vector2(
                callButtonRect.X + (callButtonRect.Width - textSize.X) / 2f,
                callButtonRect.Y + (callButtonRect.Height - textSize.Y) / 2f - 2
            );

            Color textColor = isCalling
                ? Color.Lerp(new Color(255, 200, 200), Color.White, buttonHoverProgress)
                : Color.Lerp(new Color(200, 230, 255), Color.White, buttonHoverProgress);
            textColor *= uiAlpha;

            //文字发光
            if (buttonHoverProgress > 0.01f || isCalling) {
                Color glowColor = borderColor * 0.5f;
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Vector2 offset = angle.ToRotationVector2() * 1.5f;
                    Utils.DrawBorderString(spriteBatch, buttonText, textPos + offset, glowColor * 0.4f, 1.0f);
                }
            }

            Utils.DrawBorderString(spriteBatch, buttonText, textPos, textColor, 1.0f);
        }

        private void DrawStatusText(SpriteBatch spriteBatch) {
            if (string.IsNullOrEmpty(statusText) || statusTextAlpha <= 0f) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 textSize = font.MeasureString(statusText) * 0.8f;
            Vector2 textPos = panelPosition + new Vector2((PanelWidth - textSize.X) / 2f, 310);

            Color textColor = isCalling
                ? new Color(255, 200, 100)
                : new Color(100, 255, 150);
            textColor *= uiAlpha * statusTextAlpha;

            //文字发光
            Color glowColor = textColor * 0.6f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.2f;
                Utils.DrawBorderString(spriteBatch, statusText, textPos + offset, glowColor * 0.5f, 0.8f);
            }

            Utils.DrawBorderString(spriteBatch, statusText, textPos, textColor, 0.8f);
        }

        private void DrawTechParticles(SpriteBatch spriteBatch) {
            foreach (var particle in techParticles) {
                particle.Draw(spriteBatch, uiAlpha * 0.7f);
            }
        }

        private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 0.1f) return;

            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            spriteBatch.Draw(
                pixel,
                start,
                null,
                color,
                rotation,
                new Vector2(0, 0.5f),
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f
            );
        }

        #region 科技粒子类
        private class TechParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;

            public TechParticle(Vector2 pos) {
                Position = pos;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(0.3f, 1.2f);
                Velocity = angle.ToRotationVector2() * speed;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 120f);
                Size = Main.rand.NextFloat(1.5f, 3f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.97f;
                Rotation += 0.03f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;

                Texture2D px = VaultAsset.placeholder2.Value;
                Color color = new Color(80, 200, 255) * (0.7f * fade);

                sb.Draw(
                    px,
                    Position,
                    null,
                    color,
                    Rotation,
                    new Vector2(0.5f),
                    new Vector2(Size * 2f, Size * 0.3f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
        #endregion
    }
}
