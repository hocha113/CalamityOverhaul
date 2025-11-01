using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Common
{
    /// <summary>
    /// ����׷��UI��ͨ�û��࣬������ʾս���е��������
    /// </summary>
    internal abstract class BaseQuestTrackerUI : UIHandle, ILocalizedModType
    {
        public abstract string LocalizationCategory { get; }

        //���ػ��ı�
        protected LocalizedText QuestTitle { get; set; }
        protected LocalizedText DamageContribution { get; set; }
        protected LocalizedText RequiredContribution { get; set; }

        //UI����
        protected const float PanelWidth = 220f;
        protected const float PanelHeight = 90f;
        protected virtual float ScreenX => 0f;
        protected virtual float ScreenY => Main.screenHeight / 2f - PanelHeight / 2f;

        //��������
        protected float slideProgress = 0f;
        protected float pulseTimer = 0f;
        protected float borderGlow = 1f;
        protected float warningPulse = 0f;

        //�˺�����
        protected float cachedContribution = 0f;
        protected const float UpdateInterval = 0.5f;
        protected float updateTimer = 0f;

        /// <summary>
        /// ��ȡ��ǰ�˺�׷������
        /// </summary>
        protected abstract (float current, float total, bool isActive) GetTrackingData();

        /// <summary>
        /// ��ȡ������˺����׶���ֵ
        /// </summary>
        protected abstract float GetRequiredContribution();

        public override void SetStaticDefaults() {
            SetupLocalizedTexts();
        }

        protected abstract void SetupLocalizedTexts();

        public override void Update() {
            //չ��/���𶯻�
            float targetSlide = Active ? 1f : 0f;
            slideProgress = MathHelper.Lerp(slideProgress, targetSlide, 0.15f);

            if (slideProgress < 0.01f) {
                return;
            }

            //��������
            pulseTimer += 0.03f;
            borderGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.3f + 0.7f;

            //�����˺�������ʾ
            updateTimer += 0.016f;
            if (updateTimer >= UpdateInterval) {
                updateTimer = 0f;
                var trackingData = GetTrackingData();
                if (trackingData.total > 0) {
                    cachedContribution = trackingData.current / trackingData.total;
                }
            }

            //������׶ȵͣ���˸����
            float requiredContribution = GetRequiredContribution();
            if (cachedContribution < requiredContribution * 0.5f) {
                warningPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f;
            }
            else {
                warningPulse = 0f;
            }

            //����UIλ��
            float offsetX = MathHelper.Lerp(-PanelWidth - 50f, ScreenX, CWRUtils.EaseOutCubic(slideProgress));
            DrawPosition = new Vector2(offsetX, ScreenY);
            Size = new Vector2(PanelWidth, PanelHeight);
            UIHitBox = DrawPosition.GetRectangle((int)PanelWidth, (int)PanelHeight);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (slideProgress < 0.01f) {
                return;
            }

            float alpha = Math.Min(slideProgress * 2f, 1f);
            DrawPanel(spriteBatch, alpha);
            DrawContent(spriteBatch, alpha);
        }

        protected virtual void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //��Ӱ
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.5f));

            //�������� (��ǻ���)
            int segments = 15;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = (int)(DrawPosition.Y + t * PanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * PanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));

                Color deep = new Color(30, 15, 15);
                Color mid = new Color(70, 30, 25);
                Color hot = new Color(120, 50, 35);

                float wave = (float)Math.Sin(pulseTimer * 1.2f + t * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, wave), hot, t * 0.5f);
                c *= alpha;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //��������Ч��
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = new Color(140, 40, 25) * (alpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), pulseColor);

            //�߿�
            DrawBrimstoneFrame(spriteBatch, UIHitBox, alpha, borderGlow);
        }

        protected virtual void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;

            //����
            Vector2 titlePos = DrawPosition + new Vector2(10, 8);
            Color titleColor = new Color(255, 220, 180) * alpha;
            Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos, titleColor, 0.75f);

            //�ָ���
            Vector2 dividerStart = titlePos + new Vector2(0, 22);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 20, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                Color.OrangeRed * alpha * 0.8f, Color.OrangeRed * alpha * 0.1f, 1.2f);

            //�˺����׶��ı�
            Vector2 contributionTextPos = dividerStart + new Vector2(0, 10);
            string contributionText = $"{DamageContribution.Value}: ";
            Utils.DrawBorderString(spriteBatch, contributionText, contributionTextPos,
                Color.White * alpha, 0.65f);

            //�ٷֱ���ʾ
            Vector2 percentPos = contributionTextPos + new Vector2(font.MeasureString(contributionText).X * 0.65f, 0);
            string percentText = $"{cachedContribution:P1}";

            //���ݽ��ȸı���ɫ
            float requiredContribution = GetRequiredContribution();
            Color percentColor;
            if (cachedContribution >= requiredContribution) {
                percentColor = Color.Lerp(Color.Yellow, Color.LimeGreen, (cachedContribution - requiredContribution) / (1f - requiredContribution));
            }
            else {
                percentColor = Color.Lerp(Color.Red, Color.Orange, cachedContribution / requiredContribution);
                //����Ҫ��ʱ��˸����
                percentColor = Color.Lerp(percentColor, Color.Red, warningPulse * 0.5f);
            }

            Utils.DrawBorderString(spriteBatch, percentText, percentPos, percentColor * alpha, 0.75f); //��0.85��С��0.75

            //�����ı� - ��С����
            Vector2 requirementPos = contributionTextPos + new Vector2(0, 18);
            Utils.DrawBorderString(spriteBatch, RequiredContribution.Value, requirementPos,
                Color.Gray * alpha, 0.6f); //��0.7��С��0.6

            //������ - ��С�߶�
            DrawProgressBar(spriteBatch, requirementPos + new Vector2(0, 14), alpha);
        }

        protected virtual void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = PanelWidth - 20;
            float barHeight = 6; //��8��С��6

            //����
            Rectangle barBg = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //������
            float fillWidth = barWidth * Math.Min(cachedContribution, 1f);
            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle((int)position.X, (int)position.Y, (int)fillWidth, (int)barHeight);

                float requiredContribution = GetRequiredContribution();
                Color fillColor;
                if (cachedContribution >= requiredContribution) {
                    fillColor = Color.Lerp(new Color(255, 180, 80), new Color(80, 255, 120), (cachedContribution - requiredContribution) / (1f - requiredContribution));
                }
                else {
                    fillColor = Color.Lerp(new Color(180, 50, 50), new Color(255, 140, 60), cachedContribution / requiredContribution);
                }

                spriteBatch.Draw(pixel, barFill, new Rectangle(0, 0, 1, 1), fillColor * alpha);
            }

            //��������
            float requiredX = position.X + barWidth * GetRequiredContribution();
            Rectangle requirementLine = new Rectangle((int)requiredX - 1, (int)position.Y, 2, (int)barHeight);
            spriteBatch.Draw(pixel, requirementLine, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.8f));

            //�߿�
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
        }

        protected static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //���
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //�ڿ򷢹�
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
        }

        protected static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
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
