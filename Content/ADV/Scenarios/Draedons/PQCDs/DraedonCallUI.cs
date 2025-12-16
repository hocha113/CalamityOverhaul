using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.ExoMechdusaSums;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs
{
    /// <summary>
    /// 呼叫框禁用状态接口
    /// </summary>
    public interface IDraedonCallDisabledProvider
    {
        /// <summary>
        /// 是否禁用呼叫功能
        /// </summary>
        bool IsCallDisabled { get; }

        /// <summary>
        /// 禁用原因文本
        /// </summary>
        string DisabledReason { get; }
    }

    /// <summary>
    /// 嘉登呼叫禁用状态提供者示例实现
    /// </summary>
    internal class DraedonCallDisabledProvider : IDraedonCallDisabledProvider
    {
        /// <summary>
        /// 检查嘉登是否已经存在于世界中
        /// </summary>
        public bool IsCallDisabled {
            get {
                //检查是否有嘉登NPC存在
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.type == CWRID.NPC_Draedon) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 禁用原因文本
        /// </summary>
        public string DisabledReason => DraedonCallUI.DisabledReasonText?.Value ?? "UNAVAILABLE";
    }

    /// <summary>
    /// 嘉登呼叫UI
    /// </summary>
    internal class DraedonCallUI : UIHandle, ILocalizedModType
    {
        public static DraedonCallUI Instance => UIHandleLoader.GetUIHandleOfType<DraedonCallUI>();

        /// <summary>
        /// 设置禁用状态提供者
        /// </summary>
        public static IDraedonCallDisabledProvider DisabledProvider = new DraedonCallDisabledProvider();

        //UI状态
        private bool _active;
        public override bool Active {
            get => _active || uiAlpha > 0f;
            set => _active = value;
        }

        public override float RenderPriority => 0.9f;

        public string LocalizationCategory => "UI";

        //本地化文本
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText TitleTextDisabled { get; private set; }
        public static LocalizedText CallButtonText { get; private set; }
        public static LocalizedText CallingText { get; private set; }
        public static LocalizedText DisabledButtonText { get; private set; }
        public static LocalizedText ConnectingText { get; private set; }
        public static LocalizedText ConnectedText { get; private set; }
        public static LocalizedText DisabledReasonText { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "呼叫嘉登");
            TitleTextDisabled = this.GetLocalization(nameof(TitleTextDisabled), () => "呼叫禁用");
            CallButtonText = this.GetLocalization(nameof(CallButtonText), () => "启动呼叫");
            CallingText = this.GetLocalization(nameof(CallingText), () => "正在呼叫...");
            DisabledButtonText = this.GetLocalization(nameof(DisabledButtonText), () => "禁用中");
            ConnectingText = this.GetLocalization(nameof(ConnectingText), () => "正在连接...");
            ConnectedText = this.GetLocalization(nameof(ConnectedText), () => "已连接");
            DisabledReasonText = this.GetLocalization(nameof(DisabledReasonText), () => "嘉登已被呼叫");
        }

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

        //禁用状态动画参数
        private float banLineTimer = 0f;
        private float disabledPulse = 0f;
        private float warningFlash = 0f;
        private float glitchTimer = 0f;
        private bool isDisabled = false;
        private float disabledTransition = 0f;
        private readonly List<BanLine> banLines = new();
        private int banLineSpawnTimer = 0;

        //UI尺寸
        private const int PanelWidth = 280;
        private const int PanelHeight = 360;
        private Vector2 panelPosition;

        //嘉登头像参数
        private static float PortraitSize => 140f;
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

            //检查禁用状态
            bool newDisabledState = DisabledProvider?.IsCallDisabled ?? false;
            if (newDisabledState != isDisabled) {
                isDisabled = newDisabledState;
                if (isDisabled) {
                    //进入禁用状态
                    isCalling = false;
                    callProgress = 0f;
                    statusText = DisabledProvider?.DisabledReason ?? "UNAVAILABLE";
                }
                else {
                    //退出禁用状态
                    statusText = "";
                    banLines.Clear();
                }
            }

            //更新禁用状态过渡
            float targetDisabledTransition = isDisabled ? 1f : 0f;
            disabledTransition = MathHelper.Lerp(disabledTransition, targetDisabledTransition, 0.1f);

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

            //更新禁用动画
            if (isDisabled) {
                UpdateDisabledEffects();
            }

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
            if (isCalling && !isDisabled) {
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

        private void UpdateDisabledEffects() {
            banLineTimer += 0.04f;
            disabledPulse += 0.05f;
            warningFlash += 0.15f;
            glitchTimer += 0.08f;

            if (banLineTimer > MathHelper.TwoPi) banLineTimer -= MathHelper.TwoPi;
            if (disabledPulse > MathHelper.TwoPi) disabledPulse -= MathHelper.TwoPi;
            if (warningFlash > MathHelper.TwoPi) warningFlash -= MathHelper.TwoPi;
            if (glitchTimer > MathHelper.TwoPi) glitchTimer -= MathHelper.TwoPi;

            //生成封禁线
            banLineSpawnTimer++;
            if (banLineSpawnTimer >= 8 && banLines.Count < 15) {
                banLineSpawnTimer = 0;
                Vector2 spawnPos = panelPosition + new Vector2(
                    Main.rand.NextFloat(0, PanelWidth),
                    Main.rand.NextFloat(0, PanelHeight)
                );
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                banLines.Add(new BanLine(spawnPos, angle));
            }

            //更新封禁线
            for (int i = banLines.Count - 1; i >= 0; i--) {
                if (banLines[i].Update()) {
                    banLines.RemoveAt(i);
                }
            }
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
            banLines.Clear();
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

                //按钮点击（禁用时无法点击）
                if (isHoveringButton && Main.mouseLeft && Main.mouseLeftRelease && !isCalling && !isDisabled) {
                    StartCall();
                }
                else if (isHoveringButton && Main.mouseLeft && Main.mouseLeftRelease && isDisabled) {
                    //点击禁用按钮时播放错误音效
                    SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f, Pitch = -0.5f });
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
            statusText = ConnectingText.Value;

            //播放音效
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = 0.3f });
            SoundEngine.PlaySound("CalamityMod/Sounds/Custom/CodebreakerBeam".GetSound() with { Volume = 0.7f });
        }

        private void OnCallComplete() {
            statusText = ConnectedText.Value;

            //播放完成音效
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.8f, Pitch = 0.5f });

            ExoMechdusaSum.SimpleMode = true;
            NPC.NewNPC(Main.LocalPlayer.FromObjectGetParent(), (int)Main.LocalPlayer.Center.X, (int)Main.LocalPlayer.Center.Y - 260, CWRID.NPC_Draedon);
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

            //标题
            DrawTitle(spriteBatch);

            //绘制呼叫按钮
            DrawCallButton(spriteBatch);

            //绘制状态文本
            DrawStatusText(spriteBatch);

            //绘制科技粒子
            DrawTechParticles(spriteBatch);

            //绘制禁用状态效果
            if (disabledTransition > 0.01f) {
                DrawDisabledEffects(spriteBatch);
            }
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
                Color disabledDark = new Color(18, 8, 8);
                Color disabledMid = new Color(30, 12, 12);

                //根据禁用状态混合颜色
                Color baseDark = Color.Lerp(techDark, disabledDark, disabledTransition);
                Color baseMid = Color.Lerp(techMid, disabledMid, disabledTransition);

                float pulse = (float)Math.Sin(circuitPulseTimer * 0.6f + t * 2f) * 0.5f + 0.5f;
                Color finalColor = Color.Lerp(baseDark, baseMid, pulse * 0.5f) * (uiAlpha * 0.94f);

                spriteBatch.Draw(pixel, segment, finalColor);
            }

            //全息闪烁叠加
            float flicker = (float)Math.Sin(hologramFlicker * 1.5f) * 0.5f + 0.5f;
            Color hologramColor = Color.Lerp(
                new Color(15, 30, 45),
                new Color(30, 15, 15),
                disabledTransition
            );
            spriteBatch.Draw(pixel, panelRect, hologramColor * (uiAlpha * 0.2f * flicker));

            //扫描线
            DrawScanLines(spriteBatch, panelRect);

            //内部脉冲发光
            float innerPulse = (float)Math.Sin(circuitPulseTimer * 1.3f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-4, -4);
            Color innerGlowColor = Color.Lerp(
                new Color(40, 180, 255),
                new Color(255, 80, 80),
                disabledTransition
            );
            spriteBatch.Draw(pixel, inner, innerGlowColor * (uiAlpha * 0.1f * innerPulse));

            //科技边框
            DrawTechFrame(spriteBatch, panelRect, innerPulse);
        }

        private void DrawScanLines(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scanY = rect.Y + (float)Math.Sin(scanLineTimer) * 0.5f * rect.Height + rect.Height * 0.5f;

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 3f;
                if (offsetY < rect.Y || offsetY > rect.Bottom) continue;

                float intensity = 1f - Math.Abs(i) * 0.3f;
                Color scanColor = Color.Lerp(
                    new Color(60, 180, 255),
                    new Color(255, 100, 100),
                    disabledTransition
                ) * (uiAlpha * 0.15f * intensity);
                spriteBatch.Draw(pixel, new Rectangle(rect.X + 10, (int)offsetY, rect.Width - 20, 2), scanColor);
            }
        }

        private void DrawTechFrame(SpriteBatch spriteBatch, Rectangle rect, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color normalBorder = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse);
            Color disabledBorder = Color.Lerp(new Color(200, 40, 40), new Color(255, 80, 80), pulse);
            Color borderColor = Color.Lerp(normalBorder, disabledBorder, disabledTransition) * (uiAlpha * 0.9f);

            //外边框
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), borderColor * 0.75f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), borderColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), borderColor * 0.9f);

            //内发光边框
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color normalGlow = new Color(100, 200, 255);
            Color disabledGlow = new Color(255, 100, 100);
            Color innerGlow = Color.Lerp(normalGlow, disabledGlow, disabledTransition) * (uiAlpha * 0.2f * pulse);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 2), innerGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 2, inner.Width, 2), innerGlow * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 2, inner.Height), innerGlow * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 2, inner.Y, 2, inner.Height), innerGlow * 0.9f);
        }

        private void DrawTitle(SpriteBatch spriteBatch) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string title = isDisabled ? TitleTextDisabled.Value : TitleText.Value;
            Vector2 titleSize = font.MeasureString(title) * 1.1f;
            Vector2 titlePos = panelPosition + new Vector2((PanelWidth - titleSize.X) / 2f, 20);

            //标题发光效果
            Color normalGlow = new Color(80, 220, 255);
            Color disabledGlow = new Color(255, 100, 100);
            Color glowColor = Color.Lerp(normalGlow, disabledGlow, disabledTransition) * (uiAlpha * 0.8f);

            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 offset = angle.ToRotationVector2() * 2.5f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor * 0.4f, 1.1f);
            }

            Color titleColor = Color.Lerp(Color.White, new Color(255, 180, 180), disabledTransition) * uiAlpha;
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor, 1.1f);
        }

        private void DrawDraedonPortrait(SpriteBatch spriteBatch) {
            //获取嘉登头像纹理
            Texture2D portraitTexture = (isCalling || isDisabled)
                ? ADVAsset.DraedonRedADV
                : ADVAsset.DraedonADV;

            if (portraitTexture == null) return;

            //计算缩放
            float baseScale = (PortraitSize / Math.Max(portraitTexture.Width, portraitTexture.Height));
            float hoverScale = 1f + portraitHoverProgress * 0.08f;
            float callScale = 1f + callProgress * 0.15f;
            float disabledScale = 1f - disabledTransition * 0.1f;
            float finalScale = baseScale * hoverScale * callScale * disabledScale;

            //计算透明度
            float alpha = uiAlpha * (0.9f + portraitHoverProgress * 0.1f);
            float disabledAlpha = alpha * (0.5f + 0.5f * (1f - disabledTransition));

            //绘制头像背景光环
            DrawPortraitGlow(spriteBatch, portraitPosition, PortraitSize * hoverScale * callScale * disabledScale, disabledAlpha);

            //绘制头像边框
            DrawPortraitBorder(spriteBatch, portraitPosition, PortraitSize * hoverScale * callScale * disabledScale, disabledAlpha);

            //绘制头像
            float rotation = portraitRotation * (1f + callProgress * 2f);

            //禁用时的颜色调制
            Color portraitColor = Color.Lerp(Color.White, new Color(255, 150, 150), disabledTransition) * disabledAlpha;

            spriteBatch.Draw(
                portraitTexture,
                portraitPosition,
                null,
                portraitColor,
                rotation,
                portraitTexture.Size() / 2f,
                finalScale,
                SpriteEffects.None,
                0f
            );

            //呼叫时的额外效果
            if (isCalling && !isDisabled) {
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
                Color normalGlow = new Color(80, 200, 255);
                Color disabledGlow = new Color(255, 100, 100);
                Color glowColor = Color.Lerp(normalGlow, disabledGlow, disabledTransition) * glowAlpha;

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
            Color normalBorder = Color.Lerp(new Color(40, 160, 240), new Color(80, 200, 255), pulse);
            Color disabledBorder = Color.Lerp(new Color(200, 40, 40), new Color(255, 80, 80), pulse);
            Color borderColor = Color.Lerp(normalBorder, disabledBorder, disabledTransition) * (alpha * 0.8f);

            //圆形边框效果
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
            //旋转的能量环
            for (int ring = 0; ring < 3; ring++) {
                float ringRadius = size * (0.6f + ring * 0.2f) * callProgress;
                float ringRotation = portraitRotation * (2f + ring * 0.5f);
                int segments = 24;

                for (int i = 0; i < segments; i++) {
                    if (Main.rand.NextBool(2)) continue;

                    float angle1 = MathHelper.TwoPi * i / segments + ringRotation;
                    float angle2 = MathHelper.TwoPi * (i + 1) / segments + ringRotation;
                    Vector2 point1 = position + angle1.ToRotationVector2() * ringRadius;
                    Vector2 point2 = position + angle2.ToRotationVector2() * ringRadius;

                    Color ringColor = new Color(255, 100, 100) * (alpha * 0.6f * callProgress);
                    DrawLine(spriteBatch, point1, point2, ringColor, 2f);
                }
            }
            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            //中心光球
            float coreSize = size * callProgress * 0.1f;
            Color coreColor = new Color(255, 150, 150, 0) * (alpha * 0.8f * callProgress);
            spriteBatch.Draw(
                softGlow,
                position,
                null,
                coreColor,
                0f,
                softGlow.Size() / 2,
                coreSize,
                SpriteEffects.None,
                0f
            );
        }

        private void DrawCallButton(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //按钮背景
            Color normalBg = Color.Lerp(new Color(20, 60, 100), new Color(40, 100, 160), buttonHoverProgress);
            Color disabledBg = Color.Lerp(new Color(60, 20, 20), new Color(80, 30, 30), buttonHoverProgress * 0.5f);
            Color callingBg = Color.Lerp(new Color(60, 20, 20), new Color(100, 40, 40), buttonHoverProgress);

            Color buttonBg = isCalling ? callingBg : Color.Lerp(normalBg, disabledBg, disabledTransition);
            buttonBg *= uiAlpha * 0.7f;

            spriteBatch.Draw(pixel, callButtonRect, buttonBg);

            //按钮边框
            float pulse = (float)Math.Sin(callButtonPulse) * 0.5f + 0.5f;
            Color normalBorder = Color.Lerp(new Color(60, 180, 255), new Color(100, 220, 255), pulse);
            Color disabledBorder = Color.Lerp(new Color(150, 50, 50), new Color(200, 80, 80), pulse);
            Color callingBorder = Color.Lerp(new Color(255, 80, 80), new Color(255, 150, 150), pulse);

            Color borderColor = isCalling ? callingBorder : Color.Lerp(normalBorder, disabledBorder, disabledTransition);
            borderColor *= uiAlpha * (0.7f + buttonHoverProgress * 0.3f);

            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.X, callButtonRect.Y, callButtonRect.Width, 3), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.X, callButtonRect.Bottom - 3, callButtonRect.Width, 3), borderColor * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.X, callButtonRect.Y, 3, callButtonRect.Height), borderColor * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(callButtonRect.Right - 3, callButtonRect.Y, 3, callButtonRect.Height), borderColor * 0.9f);

            //呼叫进度条
            if (isCalling && callProgress > 0f && !isDisabled) {
                Rectangle progressBar = new Rectangle(
                    callButtonRect.X + 5,
                    callButtonRect.Bottom - 8,
                    (int)((callButtonRect.Width - 10) * callProgress),
                    3
                );
                spriteBatch.Draw(pixel, progressBar, new Color(255, 150, 150) * (uiAlpha * 0.9f));
            }

            //按钮文字
            string buttonText = isDisabled ? DisabledButtonText.Value : (isCalling ? CallingText.Value : CallButtonText.Value);
            Vector2 textSize = font.MeasureString(buttonText) * 1.0f;
            Vector2 textPos = new Vector2(
                callButtonRect.X + (callButtonRect.Width - textSize.X) / 2f,
                callButtonRect.Y + (callButtonRect.Height - textSize.Y) / 2f - 2
            );

            Color normalText = Color.Lerp(new Color(200, 230, 255), Color.White, buttonHoverProgress);
            Color disabledText = Color.Lerp(new Color(200, 150, 150), new Color(255, 180, 180), buttonHoverProgress * 0.5f);
            Color callingText = Color.Lerp(new Color(255, 200, 200), Color.White, buttonHoverProgress);

            Color textColor = isCalling ? callingText : Color.Lerp(normalText, disabledText, disabledTransition);
            textColor *= uiAlpha;

            //文字发光
            if (buttonHoverProgress > 0.01f || isCalling || isDisabled) {
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

            Color normalText = isCalling ? new Color(255, 200, 100) : new Color(100, 255, 150);
            Color disabledText = new Color(255, 100, 100);
            Color textColor = Color.Lerp(normalText, disabledText, disabledTransition);
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

        private void DrawDisabledEffects(SpriteBatch spriteBatch) {
            //绘制封禁线
            foreach (var banLine in banLines) {
                banLine.Draw(spriteBatch, uiAlpha * disabledTransition);
            }

            //绘制警告闪烁
            if (isDisabled) {
                DrawWarningFlash(spriteBatch);
            }

            //绘制X形封禁标记
            DrawBanMarks(spriteBatch);

            //绘制故障效果
            DrawGlitchEffect(spriteBatch);
        }

        private void DrawWarningFlash(SpriteBatch spriteBatch) {
            float flash = (float)Math.Sin(warningFlash * 2f);
            if (flash < 0) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            Color flashColor = new Color(255, 50, 50) * (uiAlpha * disabledTransition * flash * 0.15f);
            spriteBatch.Draw(pixel, panelRect, flashColor);
        }

        private void DrawBanMarks(SpriteBatch spriteBatch) {
            //在头像上绘制X形标记
            Vector2 center = portraitPosition;
            float size = PortraitSize * 0.7f;
            float thickness = 4f;

            Color banColor = new Color(255, 80, 80) * (uiAlpha * disabledTransition * 0.9f);

            //左上到右下
            Vector2 p1 = center + new Vector2(-size / 2f, -size / 2f);
            Vector2 p2 = center + new Vector2(size / 2f, size / 2f);
            DrawLine(spriteBatch, p1, p2, banColor, thickness);

            //右上到左下
            Vector2 p3 = center + new Vector2(size / 2f, -size / 2f);
            Vector2 p4 = center + new Vector2(-size / 2f, size / 2f);
            DrawLine(spriteBatch, p3, p4, banColor, thickness);
        }

        private void DrawGlitchEffect(SpriteBatch spriteBatch) {
            if (!isDisabled) return;

            //随机故障条纹
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float glitch = (float)Math.Sin(glitchTimer * 3f);

            if (glitch > 0.7f && Main.rand.NextBool(3)) {
                int glitchCount = Main.rand.Next(2, 5);
                for (int i = 0; i < glitchCount; i++) {
                    float y = panelPosition.Y + Main.rand.NextFloat(PanelHeight);
                    int height = Main.rand.Next(2, 8);
                    Rectangle glitchRect = new Rectangle(
                        (int)panelPosition.X,
                        (int)y,
                        PanelWidth,
                        height
                    );

                    Color glitchColor = Main.rand.NextBool()
                        ? new Color(255, 100, 100)
                        : new Color(100, 255, 255);
                    glitchColor *= uiAlpha * disabledTransition * 0.3f;

                    spriteBatch.Draw(pixel, glitchRect, glitchColor);
                }
            }
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

        #region 粒子特效类
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

        private class BanLine
        {
            public Vector2 Position;
            public float Angle;
            public float Length;
            public float Life;
            public float MaxLife;
            public float Thickness;
            public float Speed;

            public BanLine(Vector2 pos, float angle) {
                Position = pos;
                Angle = angle;
                Length = 0f;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(80f, 150f);
                Thickness = Main.rand.NextFloat(2f, 4f);
                Speed = Main.rand.NextFloat(3f, 8f);
            }

            public bool Update() {
                Life++;

                //线条生长
                if (Length < 200f) {
                    Length += Speed;
                }

                //沿方向移动
                Position += Angle.ToRotationVector2() * 1.5f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                if (Length <= 0f) return;

                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);

                Texture2D px = VaultAsset.placeholder2.Value;
                Vector2 endPos = Position + Angle.ToRotationVector2() * Length;

                //绘制主线
                Color lineColor = new Color(255, 80, 80) * (alpha * 0.8f * fade);
                DrawLine(sb, Position, endPos, lineColor, Thickness);

                //绘制发光效果
                Color glowColor = new Color(255, 150, 150) * (alpha * 0.4f * fade);
                DrawLine(sb, Position, endPos, glowColor, Thickness * 2f);
            }

            private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Vector2 edge = end - start;
                float length = edge.Length();
                if (length < 0.1f) return;

                float rotation = (float)Math.Atan2(edge.Y, edge.X);
                sb.Draw(
                    pixel,
                    start,
                    null,
                    color,
                    rotation,
                    new Vector2(0, 0.5f),
                    new Vector2(length, thickness) * 0.1f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
        #endregion
    }
}
