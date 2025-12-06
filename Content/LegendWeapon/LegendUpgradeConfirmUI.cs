using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
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

        //动画
        private float panelPulse;
        private float borderGlow;
        private float titleGlowPhase;

        //布局常量
        private const float PanelWidth = 380f;
        private const float PanelHeight = 220f;
        private const float Padding = 18f;
        private const float ButtonHeight = 36f;
        private const float ButtonWidth = 140f;

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
        }

        public override void SaveUIData(TagCompound tag) {
            CancelPending();
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
            //展开/收起动画
            float targetShow = isPending && !closing ? 1f : 0f;
            showProgress = MathHelper.Lerp(showProgress, targetShow, 0.15f);

            if (Math.Abs(showProgress - targetShow) < 0.01f) {
                showProgress = targetShow;
            }

            if (showProgress <= 0.01f && !isPending) {
                return;
            }

            //内容淡入
            if (showProgress > 0.4f && contentFade < 1f) {
                float adjustedProgress = (showProgress - 0.4f) / 0.6f;
                contentFade = Math.Min(contentFade + 0.1f, adjustedProgress);
            }
            else if (showProgress <= 0.4f && contentFade > 0f) {
                contentFade -= 0.15f;
                contentFade = Math.Clamp(contentFade, 0f, 1f);
            }

            //动画更新
            panelPulse += 0.03f;
            borderGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.3f + 0.7f;
            titleGlowPhase += 0.05f;

            //关闭动画
            if (closing) {
                hideProgress += 0.08f;
                if (hideProgress >= 1f) {
                    hideProgress = 1f;
                    closing = false;
                    CancelPending();
                }
            }

            //位置和尺寸
            Vector2 panelSize = new Vector2(PanelWidth, PanelHeight);
            DrawPosition = new Vector2(Main.screenWidth / 2 - PanelWidth / 2, Main.screenHeight - PanelHeight);
            Size = panelSize;
            UIHitBox = DrawPosition.GetRectangle(panelSize);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //按钮位置
            float buttonY = DrawPosition.Y + PanelHeight - Padding - ButtonHeight;
            float centerX = DrawPosition.X + PanelWidth / 2;
            float buttonSpacing = 20f;

            confirmButtonRect = new Rectangle(
                (int)(centerX - ButtonWidth - buttonSpacing / 2),
                (int)buttonY,
                (int)ButtonWidth,
                (int)ButtonHeight
            );

            cancelButtonRect = new Rectangle(
                (int)(centerX + buttonSpacing / 2),
                (int)buttonY,
                (int)ButtonWidth,
                (int)ButtonHeight
            );

            //悬停检测
            hoveringConfirm = confirmButtonRect.Contains(MouseHitBox);
            hoveringCancel = cancelButtonRect.Contains(MouseHitBox);

            //点击处理
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (hoveringConfirm) {
                    OnConfirm();
                }
                else if (hoveringCancel) {
                    OnCancel();
                }
            }
        }

        private void OnConfirm() {
            if (pendingLegendData != null && pendingItem != null) {
                //执行升级
                pendingLegendData.UpgradeWorldName = Main.worldName;
                pendingLegendData.UpgradeWorldFullName = SaveWorld.WorldFullName;
                pendingLegendData.Level = targetLevel;

                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.3f });

                //升级成功提示
                string message = Success.Value.Replace("[ITEM]", pendingItem.Name).Replace("[LEVEL]", targetLevel.ToString());
                CombatText.NewText(player.Hitbox, Color.Gold, message, true);
            }

            BeginClose();
        }

        private void OnCancel() {
            SoundEngine.PlaySound(SoundID.MenuClose);
            pendingLegendData.DontUpgradeName = SaveWorld.WorldFullName;
            BeginClose();
        }

        private void BeginClose() {
            if (closing) {
                return;
            }
            closing = true;
            hideProgress = 0f;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f) {
                return;
            }

            float alpha = Math.Min(showProgress * 2f, 1f);
            if (closing) {
                alpha *= (1f - hideProgress);
            }

            DrawPanel(spriteBatch, alpha);

            if (contentFade > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFade);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(5, 5);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //背景渐变 - 传奇金色主题
            Color bgTop = new Color(40, 35, 20) * (alpha * 0.95f);
            Color bgMid = new Color(60, 50, 25) * (alpha * 0.95f);
            Color bgBottom = new Color(80, 65, 30) * (alpha * 0.95f);
            int segments = 20;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = (int)(DrawPosition.Y + t * PanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * PanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));
                Color segColor = t < 0.5f
                    ? Color.Lerp(bgTop, bgMid, t * 2f)
                    : Color.Lerp(bgMid, bgBottom, (t - 0.5f) * 2f);
                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), segColor);
            }

            //脉冲叠加
            float pulse = (float)Math.Sin(panelPulse * 1.5f) * 0.5f + 0.5f;
            Color pulseColor = new Color(180, 140, 60) * (alpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), pulseColor);

            //边框
            DrawLegendFrame(spriteBatch, UIHitBox, alpha, borderGlow);
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding, Padding);
            string title = TitleText.Value;

            //标题发光
            float titleGlow = (float)Math.Sin(titleGlowPhase) * 0.3f + 0.7f;
            Color glowColor = Color.Gold * alpha * titleGlow * 0.8f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 2f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor, 1f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 1f);

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, 36);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - Padding * 2, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                Color.Gold * alpha * 0.9f, Color.Gold * alpha * 0.1f, 2f);

            //描述文本
            if (pendingItem != null) {
                Vector2 descPos = dividerStart + new Vector2(4, 16);
                string itemName = pendingItem.Name;
                string desc = string.Format(DescText.Value, itemName, targetLevel);
                string[] lines = desc.Split('\n');

                float lineHeight = FontAssets.MouseText.Value.MeasureString("A").Y * 0.85f;
                Color textColor = new Color(255, 245, 200) * alpha;

                for (int i = 0; i < lines.Length; i++) {
                    Vector2 linePos = descPos + new Vector2(0, i * lineHeight);
                    Utils.DrawBorderString(spriteBatch, lines[i], linePos + new Vector2(1, 1),
                        Color.Black * alpha * 0.6f, 0.85f);
                    Utils.DrawBorderString(spriteBatch, lines[i], linePos, textColor, 0.85f);
                }

                //物品图标
                if (pendingItem.type > ItemID.None) {
                    Vector2 iconPos = new Vector2(DrawPosition.X + PanelWidth / 2, descPos.Y + lineHeight * lines.Length + 24);
                    float iconScale = 1f;
                    VaultUtils.SimpleDrawItem(spriteBatch, pendingItem.type, iconPos,
                        pendingItem.width, iconScale, 0, Color.White * alpha);
                }
            }

            //绘制按钮
            DrawButton(spriteBatch, confirmButtonRect, ConfirmText.Value, hoveringConfirm, alpha, true);
            DrawButton(spriteBatch, cancelButtonRect, CancelText.Value, hoveringCancel, alpha, false);
        }

        private static void DrawButton(SpriteBatch spriteBatch, Rectangle buttonRect, string text,
            bool hovering, float alpha, bool isConfirm) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //按钮背景
            Color bgColor = isConfirm
                ? hovering ? new Color(100, 80, 30) : new Color(70, 55, 20)
                : hovering ? new Color(80, 40, 40) : new Color(60, 30, 30);
            bgColor *= alpha * 0.9f;

            spriteBatch.Draw(pixel, buttonRect, new Rectangle(0, 0, 1, 1), bgColor);

            //按钮边框
            Color borderColor = isConfirm ? Color.Gold : new Color(200, 100, 100);
            borderColor *= alpha * (hovering ? 1f : 0.7f);
            int borderWidth = hovering ? 2 : 1;

            Rectangle topBorder = new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, borderWidth);
            Rectangle bottomBorder = new Rectangle(buttonRect.X, buttonRect.Bottom - borderWidth, buttonRect.Width, borderWidth);
            Rectangle leftBorder = new Rectangle(buttonRect.X, buttonRect.Y, borderWidth, buttonRect.Height);
            Rectangle rightBorder = new Rectangle(buttonRect.Right - borderWidth, buttonRect.Y, borderWidth, buttonRect.Height);

            spriteBatch.Draw(pixel, topBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, bottomBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, leftBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, rightBorder, new Rectangle(0, 0, 1, 1), borderColor);

            //按钮文字
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.9f;
            Vector2 textPos = buttonRect.Center.ToVector2() - textSize / 2;
            Color textColor = Color.White * alpha * (hovering ? 1.2f : 1f);

            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 1),
                Color.Black * alpha * 0.7f, 0.9f);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 0.9f);

            //悬停发光
            if (hovering) {
                float hoverGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.3f + 0.7f;
                Color glowColor = (isConfirm ? Color.Gold : new Color(255, 150, 150)) * alpha * hoverGlow * 0.3f;
                spriteBatch.Draw(pixel, buttonRect, new Rectangle(0, 0, 1, 1), glowColor);
            }
        }

        private static void DrawLegendFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外框
            Color outerEdge = Color.Lerp(new Color(180, 140, 60), new Color(255, 215, 100), pulse) * (alpha * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.8f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.95f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.95f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-7, -7);
            Color innerGlow = new Color(255, 215, 100) * (alpha * 0.25f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.9f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.9f);

            //四角星光装饰
            DrawCornerStar(sb, new Vector2(rect.X + 12, rect.Y + 12), alpha * pulse);
            DrawCornerStar(sb, new Vector2(rect.Right - 12, rect.Y + 12), alpha * pulse);
            DrawCornerStar(sb, new Vector2(rect.X + 12, rect.Bottom - 12), alpha * pulse * 0.8f);
            DrawCornerStar(sb, new Vector2(rect.Right - 12, rect.Bottom - 12), alpha * pulse * 0.8f);
        }

        private static void DrawCornerStar(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 6f;
            Color color = Color.Gold * alpha;
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.9f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.3f), SpriteEffects.None, 0f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
    }
}
