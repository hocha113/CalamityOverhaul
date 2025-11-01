using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.DoGQuest
{
    /// <summary>
    /// ��������������UI
    /// </summary>
    internal class DoGQuestUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static DoGQuestUI Instance => UIHandleLoader.GetUIHandleOfType<DoGQuestUI>();

        //���ػ��ı�
        public static LocalizedText QuestTitle { get; private set; }
        public static LocalizedText QuestDesc { get; private set; }
        public static LocalizedText AcceptText { get; private set; }
        public static LocalizedText DeclineText { get; private set; }

        //UI����
        private float sengs;
        private float contentFadeProgress;
        private bool showingQuest;
        private bool questAccepted;
        private bool questDeclined;

        //����
        private float panelPulse;
        private float borderGlow;

        //���ֳ���
        private const float PanelWidth = 340f;
        private const float PanelHeight = 240f;
        private const float Padding = 16f;
        private const float ButtonHeight = 32f;

        //��ť
        private Rectangle acceptButtonRect;
        private Rectangle declineButtonRect;
        private bool hoveringAccept;
        private bool hoveringDecline;

        public override void SetStaticDefaults() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "ί�У�����������");
            QuestDesc = this.GetLocalization(nameof(QuestDesc), () => "ʹ�ÿ����߻�ɱ����������");
            AcceptText = this.GetLocalization(nameof(AcceptText), () => "����");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "�ܾ�");
        }

        public override bool Active {
            get {
                if (questAccepted || questDeclined) {
                    if (sengs > 0) {
                        return true;
                    }
                    return false;
                }

                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }

                //�������Ѿ�����/�ܾ�/��������񣬾Ͳ�����ʾUI
                if (halibutPlayer.ADCSave.SupCalDoGQuestReward || halibutPlayer.ADCSave.SupCalDoGQuestDeclined) {
                    return false;
                }

                //ǰ������������
                if (!halibutPlayer.ADCSave.SupCalQuestReward) {
                    return false;
                }

                Item heldItem = Main.LocalPlayer.GetItem();
                showingQuest = heldItem.type == ModContent.ItemType<Heartcarver>()
                    && halibutPlayer.ADCSave.SupCalQuestReward
                    && !halibutPlayer.ADCSave.SupCalDoGQuestReward
                    && !halibutPlayer.ADCSave.SupCalDoGQuestDeclined;

                return showingQuest || sengs > 0;
            }
        }

        public override void Update() {
            //չ��/���𶯻�
            float targetSengs = showingQuest ? 1f : 0f;
            sengs = MathHelper.Lerp(sengs, targetSengs, 0.15f);

            if (Math.Abs(sengs - targetSengs) < 0.01f) {
                sengs = targetSengs;
            }

            if (sengs < 0.01f) {
                if (questAccepted) {
                    questAccepted = false;
                    //�����ҽ���������
                    if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                        halibutPlayer.ADCSave.SupCalDoGQuestReward = true;
                    }
                }
                if (questDeclined) {
                    questDeclined = false;
                    //�����Ҿܾ�������
                    if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                        halibutPlayer.ADCSave.SupCalDoGQuestDeclined = true;
                    }
                }
                return;
            }

            //���ݵ���
            if (sengs > 0.4f && contentFadeProgress < 1f) {
                float adjustedProgress = (sengs - 0.4f) / 0.6f;
                contentFadeProgress = Math.Min(contentFadeProgress + 0.1f, adjustedProgress);
            }
            else if (sengs <= 0.4f && contentFadeProgress > 0f) {
                contentFadeProgress -= 0.15f;
                contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);
            }

            //��������
            panelPulse += 0.03f;
            borderGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.3f + 0.7f;

            //λ�úͳߴ�
            Vector2 panelSize = new Vector2(PanelWidth, PanelHeight);
            DrawPosition = new Vector2(Main.screenWidth / 2 - PanelWidth / 2, Main.screenHeight / 2 - PanelHeight / 2);
            Size = panelSize;
            UIHitBox = DrawPosition.GetRectangle(panelSize);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //��ťλ��
            float buttonY = DrawPosition.Y + PanelHeight - Padding - ButtonHeight;
            float buttonWidth = (PanelWidth - Padding * 3) / 2;

            acceptButtonRect = new Rectangle(
                (int)(DrawPosition.X + Padding),
                (int)buttonY,
                (int)buttonWidth,
                (int)ButtonHeight
            );

            declineButtonRect = new Rectangle(
                (int)(DrawPosition.X + Padding * 2 + buttonWidth),
                (int)buttonY,
                (int)buttonWidth,
                (int)ButtonHeight
            );

            //��ͣ���
            hoveringAccept = acceptButtonRect.Contains(MouseHitBox);
            hoveringDecline = declineButtonRect.Contains(MouseHitBox);

            //�������
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (hoveringAccept) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                    questAccepted = true;
                    showingQuest = false;
                }
                else if (hoveringDecline) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    questDeclined = true;
                    showingQuest = false;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (sengs < 0.01f) {
                return;
            }

            float alpha = Math.Min(sengs * 2f, 1f);
            DrawPanel(spriteBatch, alpha);

            if (contentFadeProgress > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFadeProgress);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //��Ӱ
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(5, 5);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //��������
            Color bgTop = new Color(30, 20, 20) * (alpha * 0.95f);
            Color bgBottom = new Color(50, 35, 35) * (alpha * 0.95f);
            int segments = 20;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = (int)(DrawPosition.Y + t * PanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * PanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));
                Color segColor = Color.Lerp(bgTop, bgBottom, t);
                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), segColor);
            }

            //�������
            float pulse = (float)Math.Sin(panelPulse * 1.5f) * 0.5f + 0.5f;
            Color pulseColor = new Color(140, 50, 50) * (alpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), pulseColor);

            //�߿�
            DrawBrimstoneFrame(spriteBatch, UIHitBox, alpha, borderGlow);
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            //����
            Vector2 titlePos = DrawPosition + new Vector2(Padding, Padding);
            string title = QuestTitle.Value;

            //���ⷢ��
            Color titleGlow = Color.Gold * alpha * 0.6f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.8f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow, 0.95f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 0.95f);

            //�ָ���
            Vector2 dividerStart = titlePos + new Vector2(0, 32);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - Padding * 2, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, Color.Gold * alpha * 0.8f, Color.Gold * alpha * 0.1f, 1.5f);

            //�����ı�
            Vector2 descPos = dividerStart + new Vector2(2, 12);
            string desc = QuestDesc.Value;
            string[] lines = desc.Split('\n');

            float lineHeight = FontAssets.MouseText.Value.MeasureString("A").Y * 0.75f;
            Color textColor = Color.White * alpha;

            for (int i = 0; i < lines.Length; i++) {
                Vector2 linePos = descPos + new Vector2(0, i * lineHeight);
                Utils.DrawBorderString(spriteBatch, lines[i], linePos + new Vector2(1, 1), Color.Black * alpha * 0.5f, 0.75f);
                Utils.DrawBorderString(spriteBatch, lines[i], linePos, textColor, 0.75f);
            }

            //���ư�ť
            DrawButton(spriteBatch, acceptButtonRect, AcceptText.Value, hoveringAccept, alpha, true);
            DrawButton(spriteBatch, declineButtonRect, DeclineText.Value, hoveringDecline, alpha, false);
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle buttonRect, string text, bool hovering, float alpha, bool isAccept) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //��ť����
            Color bgColor = isAccept
                ? hovering ? new Color(80, 100, 60) : new Color(60, 80, 45)
                : hovering ? new Color(100, 60, 60) : new Color(80, 45, 45);
            bgColor *= alpha * 0.9f;

            spriteBatch.Draw(pixel, buttonRect, new Rectangle(0, 0, 1, 1), bgColor);

            //��ť�߿�
            Color borderColor = isAccept ? Color.Green : Color.Red;
            borderColor *= alpha * (hovering ? 1f : 0.6f);
            int borderWidth = hovering ? 2 : 1;

            Rectangle topBorder = new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, borderWidth);
            Rectangle bottomBorder = new Rectangle(buttonRect.X, buttonRect.Bottom - borderWidth, buttonRect.Width, borderWidth);
            Rectangle leftBorder = new Rectangle(buttonRect.X, buttonRect.Y, borderWidth, buttonRect.Height);
            Rectangle rightBorder = new Rectangle(buttonRect.Right - borderWidth, buttonRect.Y, borderWidth, buttonRect.Height);

            spriteBatch.Draw(pixel, topBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, bottomBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, leftBorder, new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, rightBorder, new Rectangle(0, 0, 1, 1), borderColor);

            //��ť����
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);
            Vector2 textPos = buttonRect.Center.ToVector2() - textSize / 2;
            Color textColor = Color.White * alpha * (hovering ? 1.1f : 1f);

            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 1), Color.Black * alpha * 0.6f, 1f);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 1f);
        }

        private static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //���
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //�ڿ򷢹�
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
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
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
    }
}
