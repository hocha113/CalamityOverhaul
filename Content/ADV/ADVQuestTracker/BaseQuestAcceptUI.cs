using CalamityOverhaul.Common;
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

namespace CalamityOverhaul.Content.ADV.ADVQuestTracker
{
    /// <summary>
    /// 任务接受UI的通用基类，用于显示任务详情和接受/拒绝选项
    /// </summary>
    internal abstract class BaseQuestAcceptUI : UIHandle, ILocalizedModType
    {
        public abstract string LocalizationCategory { get; }

        //本地化文本
        protected LocalizedText QuestTitle { get; set; }
        protected LocalizedText QuestDesc { get; set; }
        protected LocalizedText AcceptText { get; set; }
        protected LocalizedText DeclineText { get; set; }

        //UI控制
        protected float sengs;
        protected float contentFadeProgress;
        protected bool showingQuest;
        protected bool questAccepted;
        protected bool questDeclined;
        protected bool closing;
        protected float hideProgress;

        //动画时间轴
        protected float globalTime;
        protected float panelSlideOffset;
        protected float panelScaleAnim;
        protected float breatheAnim;
        protected float shimmerPhase;

        //按钮动画
        protected float acceptHoverAnim;
        protected float declineHoverAnim;
        protected float acceptPressAnim;
        protected float declinePressAnim;

        //粒子系统
        protected readonly List<EmberParticle> embers = [];
        protected float emberSpawnTimer;

        //布局常量
        protected const float PanelWidth = 380f;
        protected const float PanelHeight = 260f;
        protected const float Padding = 20f;
        protected const float ButtonHeight = 38f;
        protected const float ButtonWidth = 120f;
        protected const float CornerRadius = 10f;

        //按钮
        protected Rectangle acceptButtonRect;
        protected Rectangle declineButtonRect;
        protected bool hoveringAccept;
        protected bool hoveringDecline;

        //浮游粒子结构
        protected struct EmberParticle
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

        /// <summary>
        /// 设置本地化文本，子类需要实现
        /// </summary>
        protected abstract void SetupLocalizedTexts();

        /// <summary>
        /// 检查是否应该显示任务UI，子类需要实现
        /// </summary>
        protected abstract bool ShouldShowQuest();

        /// <summary>
        /// 当玩家接受任务时调用，子类需要实现
        /// </summary>
        protected abstract void OnQuestAccepted();

        /// <summary>
        /// 当玩家拒绝任务时调用，子类需要实现
        /// </summary>
        protected abstract void OnQuestDeclined();

        public override void SetStaticDefaults() {
            SetupLocalizedTexts();
        }

        public override bool Active {
            get {
                if (questAccepted || questDeclined) {
                    return sengs > 0;
                }
                showingQuest = ShouldShowQuest();
                return showingQuest || sengs > 0;
            }
        }

        protected void ResetAnimations() {
            sengs = 0f;
            contentFadeProgress = 0f;
            panelSlideOffset = 40f;
            panelScaleAnim = 0.85f;
            acceptHoverAnim = 0f;
            declineHoverAnim = 0f;
            acceptPressAnim = 0f;
            declinePressAnim = 0f;
            embers.Clear();
        }

        protected void BeginClose() {
            if (closing) return;
            closing = true;
            hideProgress = 0f;
        }

        public override void Update() {
            globalTime += 0.016f;

            //主面板展开动画(带弹性缓动)
            float targetSengs = showingQuest && !closing ? 1f : 0f;
            float animSpeed = closing ? 0.22f : 0.18f;
            sengs += (targetSengs - sengs) * animSpeed;
            if (Math.Abs(sengs - targetSengs) < 0.001f) {
                sengs = targetSengs;
            }

            if (sengs <= 0.001f && !showingQuest) {
                if (questAccepted) {
                    questAccepted = false;
                    OnQuestAccepted();
                }
                if (questDeclined) {
                    questDeclined = false;
                    OnQuestDeclined();
                }
                ResetAnimations();
                return;
            }

            //面板滑入偏移
            float targetSlide = showingQuest && !closing ? 0f : 50f;
            panelSlideOffset += (targetSlide - panelSlideOffset) * 0.14f;

            //面板缩放(带微弱过冲)
            float targetScale = showingQuest && !closing ? 1f : 0.88f;
            panelScaleAnim += (targetScale - panelScaleAnim) * 0.1f;
            if (panelScaleAnim > 0.97f && targetScale == 1f) {
                panelScaleAnim += (1.015f - panelScaleAnim) * 0.25f;
            }

            //内容延迟淡入
            if (sengs > 0.5f && !closing) {
                contentFadeProgress += (1f - contentFadeProgress) * 0.12f;
            }
            else {
                contentFadeProgress *= 0.85f;
            }
            contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);

            //呼吸律动
            breatheAnim = MathF.Sin(globalTime * 1.8f) * 0.5f + 0.5f;
            shimmerPhase = globalTime * 2.2f;

            //按钮悬停平滑过渡
            float hoverSpeed = 0.14f;
            acceptHoverAnim += ((hoveringAccept ? 1f : 0f) - acceptHoverAnim) * hoverSpeed;
            declineHoverAnim += ((hoveringDecline ? 1f : 0f) - declineHoverAnim) * hoverSpeed;

            //按钮按压衰减
            acceptPressAnim *= 0.85f;
            declinePressAnim *= 0.85f;

            //关闭动画
            if (closing) {
                hideProgress += 0.06f;
                if (hideProgress >= 1f) {
                    hideProgress = 1f;
                    closing = false;
                    showingQuest = false;
                }
            }

            //更新粒子
            UpdateEmbers();

            //生成余烬粒子
            if (sengs > 0.3f && !closing) {
                emberSpawnTimer += 1f;
                if (emberSpawnTimer > 4f) {
                    SpawnEmber();
                    emberSpawnTimer = 0f;
                }
            }

            //计算面板位置(屏幕居中，附带滑动偏移)
            float scaledW = PanelWidth * panelScaleAnim;
            float scaledH = PanelHeight * panelScaleAnim;
            Vector2 panelCenter = new(Main.screenWidth / 2f, Main.screenHeight / 2f + panelSlideOffset);
            DrawPosition = panelCenter - new Vector2(scaledW, scaledH) / 2f;
            Size = new Vector2(scaledW, scaledH);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)scaledW, (int)scaledH);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage && sengs > 0.5f) {
                player.mouseInterface = true;
            }

            //悬停检测
            hoveringAccept = acceptButtonRect.Contains(MouseHitBox) && contentFadeProgress > 0.5f;
            hoveringDecline = declineButtonRect.Contains(MouseHitBox) && contentFadeProgress > 0.5f;

            //点击处理
            if (keyLeftPressState == KeyPressState.Pressed && contentFadeProgress > 0.8f) {
                if (hoveringAccept) {
                    acceptPressAnim = 1f;
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                    questAccepted = true;
                    BeginClose();
                }
                else if (hoveringDecline) {
                    declinePressAnim = 1f;
                    SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
                    questDeclined = true;
                    BeginClose();
                }
            }
        }

        protected void UpdateEmbers() {
            for (int i = embers.Count - 1; i >= 0; i--) {
                var e = embers[i];
                e.Life -= 0.016f;
                e.Position += e.Velocity;
                e.Velocity *= 0.97f;
                e.Velocity.Y -= 0.015f;
                e.Rotation += e.RotationSpeed;
                embers[i] = e;
                if (e.Life <= 0f) {
                    embers.RemoveAt(i);
                }
            }
        }

        protected void SpawnEmber() {
            if (embers.Count > 25) return;
            var e = new EmberParticle {
                Position = new Vector2(
                    DrawPosition.X + Main.rand.NextFloat(Size.X),
                    DrawPosition.Y + Size.Y
                ),
                Velocity = new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-1.2f, -0.4f)),
                Life = Main.rand.NextFloat(1.5f, 2.8f),
                MaxLife = 0f,
                Size = Main.rand.NextFloat(2f, 4.5f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.04f, 0.04f),
                BaseColor = Color.Lerp(new Color(220, 80, 30), new Color(255, 160, 60), Main.rand.NextFloat())
            };
            e.MaxLife = e.Life;
            embers.Add(e);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (sengs <= 0.001f) return;

            float alpha = Math.Min(sengs * 1.5f, 1f);
            if (closing) {
                alpha *= 1f - hideProgress * hideProgress;
            }

            //背景遮罩
            DrawBackdrop(spriteBatch, alpha * 0.35f);

            //主面板
            DrawPanel(spriteBatch, alpha);

            //余烬粒子
            DrawEmbers(spriteBatch, alpha);

            //内容
            if (contentFadeProgress > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFadeProgress);
            }
        }

        protected virtual void DrawBackdrop(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            //从面板中心向外辐射的暗色遮罩
            Vector2 center = DrawPosition + Size / 2f;
            int radius = (int)(Math.Max(Main.screenWidth, Main.screenHeight) * 0.6f);
            for (int i = 0; i < 60; i++) {
                float t = i / 60f;
                float ringAlpha = (1f - t) * (1f - t) * alpha;
                int ringSize = (int)(radius * t);
                Rectangle ring = new(
                    (int)(center.X - ringSize),
                    (int)(center.Y - ringSize),
                    ringSize * 2, ringSize * 2
                );
                //用简化方式逐行绘制暗色叠加
                spriteBatch.Draw(pixel, ring, new Rectangle(0, 0, 1, 1), Color.Black * ringAlpha * 0.04f);
            }
        }

        protected virtual void DrawEmbers(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            foreach (var e in embers) {
                float lifeRatio = e.Life / e.MaxLife;
                float eAlpha = lifeRatio * alpha;
                float size = e.Size * (0.5f + lifeRatio * 0.5f);

                Color color = e.BaseColor * eAlpha;
                spriteBatch.Draw(pixel, e.Position, new Rectangle(0, 0, 1, 1), color, e.Rotation,
                    new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);

                //外层光晕
                Color glow = color * 0.25f;
                spriteBatch.Draw(pixel, e.Position, new Rectangle(0, 0, 1, 1), glow, e.Rotation,
                    new Vector2(0.5f), new Vector2(size * 2.5f), SpriteEffects.None, 0f);
            }
        }

        protected virtual void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Rectangle panelRect = UIHitBox;

            //多层柔和投影
            for (int i = 4; i >= 1; i--) {
                Rectangle shadow = panelRect;
                shadow.Offset(i * 2, i * 3);
                float shadowAlpha = alpha * 0.12f * (5 - i) / 4f;
                DrawRoundedRect(spriteBatch, shadow, Color.Black * shadowAlpha, CornerRadius + i);
            }

            //背景渐变(深红暗色调)
            Color bgTop = new Color(28, 18, 18);
            Color bgBottom = new Color(50, 28, 22);
            DrawGradientRoundedRect(spriteBatch, panelRect, bgTop * (alpha * 0.96f), bgBottom * (alpha * 0.96f), CornerRadius);

            //脉冲叠加层
            float pulse = MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f;
            Color pulseColor = new Color(140, 50, 40) * (alpha * 0.08f * pulse);
            DrawRoundedRect(spriteBatch, panelRect, pulseColor, CornerRadius);

            //内发光
            float innerIntensity = 0.12f + breatheAnim * 0.08f;
            DrawInnerGlow(spriteBatch, panelRect, new Color(200, 80, 40) * (alpha * innerIntensity), CornerRadius, 16);

            //流光边框
            DrawShimmerBorder(spriteBatch, panelRect, alpha);

            //顶部高光条
            Rectangle highlight = new(panelRect.X + 16, panelRect.Y + 2, panelRect.Width - 32, 2);
            float hlAlpha = 0.35f + breatheAnim * 0.2f;
            DrawHorizontalGradient(spriteBatch, highlight,
                Color.Transparent, new Color(255, 140, 80) * (alpha * hlAlpha), Color.Transparent);

            //角落装饰
            DrawCornerOrnaments(spriteBatch, panelRect, alpha);
        }

        protected void DrawShimmerBorder(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //基础边框
            Color baseColor = new Color(140, 60, 30) * (alpha * 0.75f);
            DrawRoundedRectBorder(spriteBatch, rect, baseColor, CornerRadius, 2);

            //沿边框运动的流光
            float shimmerPos = (shimmerPhase % 4f) / 4f;
            for (int i = 0; i < 2; i++) {
                float offset = (shimmerPos + i * 0.5f) % 1f;
                Vector2 pos = GetPointOnRectPerimeter(rect, offset);
                float intensity = MathF.Sin(offset * MathHelper.Pi) * 0.75f;
                Color shimmerColor = new Color(255, 140, 60) * (alpha * intensity);

                //流光主体
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), shimmerColor,
                    0f, new Vector2(0.5f), new Vector2(7f, 3.5f), SpriteEffects.None, 0f);

                //拖尾
                for (int j = 1; j <= 4; j++) {
                    float trailOff = (offset - j * 0.012f + 1f) % 1f;
                    Vector2 trailPos = GetPointOnRectPerimeter(rect, trailOff);
                    float trailFade = intensity * (1f - j / 5f);
                    spriteBatch.Draw(pixel, trailPos, new Rectangle(0, 0, 1, 1),
                        shimmerColor * trailFade * 0.45f, 0f, new Vector2(0.5f),
                        new Vector2(5f - j, 2.5f - j * 0.4f), SpriteEffects.None, 0f);
                }
            }
        }

        protected void DrawCornerOrnaments(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float ornAlpha = alpha * (0.55f + breatheAnim * 0.45f);
            Color ornColor = new Color(255, 130, 60) * ornAlpha;

            Vector2[] corners = [
                new(rect.X + 7, rect.Y + 7),
                new(rect.Right - 7, rect.Y + 7),
                new(rect.X + 7, rect.Bottom - 7),
                new(rect.Right - 7, rect.Bottom - 7)
            ];

            for (int i = 0; i < 4; i++) {
                //菱形核心
                spriteBatch.Draw(pixel, corners[i], new Rectangle(0, 0, 1, 1), ornColor,
                    MathHelper.PiOver4 + globalTime * 0.08f, new Vector2(0.5f),
                    new Vector2(5f, 5f), SpriteEffects.None, 0f);

                //四向光芒
                for (int j = 0; j < 4; j++) {
                    float rayRot = j * MathHelper.PiOver2 + globalTime * 0.25f;
                    Vector2 rayDir = rayRot.ToRotationVector2();
                    spriteBatch.Draw(pixel, corners[i] + rayDir * 3.5f, new Rectangle(0, 0, 1, 1),
                        ornColor * 0.45f, rayRot, new Vector2(0f, 0.5f),
                        new Vector2(7f, 1.2f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>
        /// 将文本按宽度自动换行
        /// </summary>
        protected static List<string> WrapText(string text, DynamicSpriteFont font, float maxWidth, float scale = 1f) {
            List<string> lines = [];

            if (string.IsNullOrEmpty(text)) {
                return lines;
            }

            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words) {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                Vector2 testSize = font.MeasureString(testLine) * scale;

                if (testSize.X > maxWidth && !string.IsNullOrEmpty(currentLine)) {
                    //当前行已满，保存并开始新行
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else {
                    currentLine = testLine;
                }
            }

            //添加最后一行
            if (!string.IsNullOrEmpty(currentLine)) {
                lines.Add(currentLine);
            }

            return lines;
        }

        protected virtual void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;
            float scale = panelScaleAnim;
            float titleScale = 0.95f * scale;
            float descScale = 0.78f * scale;
            float maxTextWidth = Size.X - Padding * scale * 2;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding * scale, Padding * scale);
            List<string> titleLines = WrapText(QuestTitle.Value, font, maxTextWidth, titleScale);

            //标题光晕(呼吸律动)
            float titleGlowStr = 0.4f + breatheAnim * 0.5f;
            Color titleGlowColor = new Color(255, 120, 50) * (alpha * titleGlowStr * 0.35f);
            float currentY = titlePos.Y;

            foreach (string line in titleLines) {
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f + globalTime * 0.4f;
                    Vector2 offset = angle.ToRotationVector2() * (2.5f + breatheAnim * 1.5f);
                    Utils.DrawBorderString(spriteBatch, line, new Vector2(titlePos.X, currentY) + offset, titleGlowColor, titleScale);
                }
                //标题主体
                Color titleColor = Color.Lerp(new Color(255, 230, 200), new Color(255, 160, 80), breatheAnim * 0.3f);
                Utils.DrawBorderString(spriteBatch, line, new Vector2(titlePos.X, currentY), titleColor * alpha, titleScale);
                currentY += font.MeasureString(line).Y * titleScale * 0.9f;
            }

            //带流光的分割线
            float titleHeight = currentY - titlePos.Y;
            Vector2 divStart = titlePos + new Vector2(0, titleHeight + 6 * scale);
            Vector2 divEnd = divStart + new Vector2(maxTextWidth, 0);
            DrawAnimatedDivider(spriteBatch, divStart, divEnd, alpha);

            //描述文本
            Vector2 descPos = divStart + new Vector2(2 * scale, 14 * scale);
            string desc = QuestDesc.Value;
            string[] paragraphs = desc.Split('\n');
            currentY = descPos.Y;
            Color textColor = new Color(220, 200, 185) * alpha;

            foreach (string paragraph in paragraphs) {
                List<string> wrappedLines = WrapText(paragraph, font, maxTextWidth, descScale);
                foreach (string line in wrappedLines) {
                    Vector2 linePos = new Vector2(descPos.X, currentY);
                    //柔和阴影
                    Utils.DrawBorderString(spriteBatch, line, linePos + new Vector2(1, 1.5f), Color.Black * alpha * 0.4f, descScale);
                    Utils.DrawBorderString(spriteBatch, line, linePos, textColor, descScale);
                    currentY += font.MeasureString(line).Y * descScale * 0.9f;
                }
            }

            //按钮区域
            float buttonY = DrawPosition.Y + Size.Y - Padding * scale - ButtonHeight * scale;
            float buttonCenterX = DrawPosition.X + Size.X / 2f;
            float buttonSpacing = 14f * scale;
            float scaledBtnW = ButtonWidth * scale;
            float scaledBtnH = ButtonHeight * scale;

            acceptButtonRect = new Rectangle(
                (int)(buttonCenterX - scaledBtnW - buttonSpacing / 2f),
                (int)buttonY,
                (int)scaledBtnW,
                (int)scaledBtnH
            );

            declineButtonRect = new Rectangle(
                (int)(buttonCenterX + buttonSpacing / 2f),
                (int)buttonY,
                (int)scaledBtnW,
                (int)scaledBtnH
            );

            DrawStyledButton(spriteBatch, acceptButtonRect, AcceptText.Value, acceptHoverAnim, acceptPressAnim, alpha, true, scale);
            DrawStyledButton(spriteBatch, declineButtonRect, DeclineText.Value, declineHoverAnim, declinePressAnim, alpha, false, scale);
        }

        protected void DrawAnimatedDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float length = (end - start).Length();
            if (length < 1f) return;

            //底层线条
            Color baseColor = new Color(100, 50, 30) * (alpha * 0.55f);
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), baseColor, 0f,
                Vector2.Zero, new Vector2(length, 1f), SpriteEffects.None, 0f);

            //流光
            float shimmerT = (globalTime * 0.5f) % 1f;
            Vector2 shimmerPos = Vector2.Lerp(start, end, shimmerT);
            Color shimmerColor = new Color(255, 140, 60, 0) * (alpha * 0.7f);

            Texture2D softGlow = CWRAsset.SoftGlow.Value;
            spriteBatch.Draw(softGlow, shimmerPos, null, shimmerColor * 0.5f, 0f,
                softGlow.Size() / 2f, new Vector2(0.25f, 0.08f), SpriteEffects.None, 0f);
        }

        protected void DrawStyledButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            float hoverAnim, float pressAnim, float alpha, bool isAccept, float scale) {
            //按压偏移
            Rectangle drawRect = rect;
            if (pressAnim > 0.01f) {
                drawRect.Y += (int)(pressAnim * 2f);
            }

            //悬停膨胀
            int expand = (int)(hoverAnim * 3f);
            drawRect.Inflate(expand, expand / 2);

            //背景渐变
            Color bgTop, bgBottom;
            if (isAccept) {
                bgTop = Color.Lerp(new Color(40, 55, 30), new Color(55, 75, 35), hoverAnim);
                bgBottom = Color.Lerp(new Color(28, 38, 18), new Color(40, 55, 25), hoverAnim);
            }
            else {
                bgTop = Color.Lerp(new Color(55, 30, 30), new Color(75, 40, 40), hoverAnim);
                bgBottom = Color.Lerp(new Color(38, 22, 22), new Color(55, 30, 30), hoverAnim);
            }
            DrawGradientRoundedRect(spriteBatch, drawRect, bgTop * (alpha * 0.95f), bgBottom * (alpha * 0.95f), 6f);

            //边框
            Color borderColor = isAccept
                ? Color.Lerp(new Color(80, 140, 60), new Color(140, 220, 100), hoverAnim)
                : Color.Lerp(new Color(140, 70, 60), new Color(220, 120, 100), hoverAnim);
            DrawRoundedRectBorder(spriteBatch, drawRect, borderColor * alpha, 6f, 1 + (int)hoverAnim);

            //悬停内发光
            if (hoverAnim > 0.01f) {
                Color innerGlow = (isAccept ? new Color(120, 200, 80) : new Color(200, 100, 80)) * (alpha * hoverAnim * 0.12f);
                DrawInnerGlow(spriteBatch, drawRect, innerGlow, 6f, 8);
            }

            //文字
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.85f * scale;
            Vector2 textPos = drawRect.Center.ToVector2() - textSize / 2f + new Vector2(0, 2);

            //文字阴影
            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 2),
                Color.Black * (alpha * 0.45f), 0.85f * scale);

            //文字主体
            Color textColor = Color.Lerp(new Color(200, 190, 175), Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.85f * scale);

            //悬停文字光晕
            if (hoverAnim > 0.3f) {
                Color textGlow = (isAccept ? new Color(140, 220, 100) : new Color(220, 140, 120)) * (alpha * (hoverAnim - 0.3f) * 0.4f);
                Utils.DrawBorderString(spriteBatch, text, textPos, textGlow, 0.85f * scale);
            }
        }

        #region 绘制辅助方法

        protected static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, float radius) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            //中心
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - r * 2, rect.Height), new Rectangle(0, 0, 1, 1), color);
            //左右
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + r, r, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Y + r, r, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);

            //四角
            for (int i = 0; i < r; i++) {
                float t = i / (float)r;
                int cw = (int)(r * MathF.Sqrt(1f - (1f - t) * (1f - t)));
                sb.Draw(pixel, new Rectangle(rect.X + r - cw, rect.Y + i, cw, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Y + i, cw, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.X + r - cw, rect.Bottom - 1 - i, cw, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Bottom - 1 - i, cw, 1), new Rectangle(0, 0, 1, 1), color);
            }
        }

        protected static void DrawGradientRoundedRect(SpriteBatch sb, Rectangle rect, Color topColor, Color bottomColor, float radius) {
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
            for (int i = 0; i < rect.Height; i++) {
                float t = i / (float)rect.Height;
                Color color = Color.Lerp(topColor, bottomColor, t);
                int inset = 0;
                if (i < r) {
                    float ct = i / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - ct) * (1f - ct))));
                }
                else if (i > rect.Height - r) {
                    float ct = (rect.Height - i) / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - ct) * (1f - ct))));
                }
                sb.Draw(VaultAsset.placeholder2.Value,
                    new Rectangle(rect.X + inset, rect.Y + i, rect.Width - inset * 2, 1),
                    new Rectangle(0, 0, 1, 1), color);
            }
        }

        protected static void DrawRoundedRectBorder(SpriteBatch sb, Rectangle rect, Color color, float radius, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            //上下
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Bottom - thickness, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            //左右
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);

            //四角弧线
            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Y + r), r, MathHelper.Pi, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Y + r), r, -MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Bottom - r), r, MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Bottom - r), r, 0, MathHelper.PiOver2, color, thickness);
        }

        protected static void DrawCornerArc(SpriteBatch sb, Vector2 center, float radius, float startAngle, float sweep, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = Math.Max(4, (int)(radius * sweep / 2f));
            for (int i = 0; i <= segments; i++) {
                float angle = startAngle + sweep * i / segments;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
            }
        }

        protected static void DrawInnerGlow(SpriteBatch sb, Rectangle rect, Color color, float radius, int glowSize) {
            for (int i = 0; i < glowSize; i++) {
                float t = i / (float)glowSize;
                float a = (1f - t) * (1f - t);
                Rectangle glowRect = rect;
                glowRect.Inflate(-i, -i);
                if (glowRect.Width > 0 && glowRect.Height > 0) {
                    DrawRoundedRectBorder(sb, glowRect, color * a, Math.Max(0, radius - i), 1);
                }
            }
        }

        protected static void DrawHorizontalGradient(SpriteBatch sb, Rectangle rect, Color left, Color center, Color right) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            for (int i = 0; i < rect.Width; i++) {
                float t = i / (float)rect.Width;
                Color color = t < 0.5f
                    ? Color.Lerp(left, center, t * 2f)
                    : Color.Lerp(center, right, (t - 0.5f) * 2f);
                sb.Draw(pixel, new Rectangle(rect.X + i, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
            }
        }

        protected static Vector2 GetPointOnRectPerimeter(Rectangle rect, float t) {
            float perimeter = (rect.Width + rect.Height) * 2f;
            float dist = t * perimeter;

            if (dist < rect.Width)
                return new Vector2(rect.X + dist, rect.Y);
            dist -= rect.Width;
            if (dist < rect.Height)
                return new Vector2(rect.Right, rect.Y + dist);
            dist -= rect.Height;
            if (dist < rect.Width)
                return new Vector2(rect.Right - dist, rect.Bottom);
            dist -= rect.Width;
            return new Vector2(rect.X, rect.Bottom - dist);
        }

        #endregion
    }
}