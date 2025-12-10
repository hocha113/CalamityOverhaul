using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs
{
    public class QuestNotificationSystem : UIHandle, ILocalizedModType
    {
        private class NotificationInfo
        {
            public string Title;
            public Texture2D Icon;
            public Rectangle? IconFrame;

            public NotificationInfo(string title, Texture2D icon, Rectangle? iconFrame) {
                Title = title;
                Icon = icon;
                IconFrame = iconFrame;
            }
        }

        public override bool Active => VaultLoad.LoadenContent;

        public string LocalizationCategory => "UI";

        private static Queue<NotificationInfo> _notifications = new();
        private static NotificationInfo _currentNotification;
        private static int _timer;
        private const int SlideTime = 20;
        private const int DisplayTime = 180;

        public static LocalizedText Text1;

        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "任务完成");
        }

        public static void AddNotification(string title, Texture2D icon = null, Rectangle? iconFrame = null) {
            _notifications.Enqueue(new NotificationInfo(title, icon, iconFrame));
        }

        public override void LogicUpdate() {
            if (_currentNotification == null && _notifications.Count > 0) {
                _currentNotification = _notifications.Dequeue();
                _timer = 0;
            }

            if (_currentNotification != null) {
                _timer++;
                if (_timer >= SlideTime * 2 + DisplayTime) {
                    _currentNotification = null;
                    _timer = 0;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (_currentNotification == null) return;

            float progress = 0f;
            if (_timer < SlideTime) {
                progress = _timer / (float)SlideTime;
                //平滑插值
                progress = 1f - (1f - progress) * (1f - progress);
            }
            else if (_timer > SlideTime + DisplayTime) {
                progress = 1f - (_timer - SlideTime - DisplayTime) / (float)SlideTime;
                progress = 1f - (1f - progress) * (1f - progress);
            }
            else {
                progress = 1f;
            }

            string titleText = Text1.Value;
            string nameText = _currentNotification.Title;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 titleSize = font.MeasureString(titleText) * 0.8f;
            Vector2 nameSize = font.MeasureString(nameText);

            float width = 260;
            float height = 60;
            float padding = 10;

            //计算位置(屏幕右侧)
            float x = Main.screenWidth - width * progress;
            float y = Main.screenHeight * 0.4f; //屏幕高度的40%处

            Rectangle panelRect = new Rectangle((int)x, (int)y, (int)width, (int)height);

            SpriteBatch sb = Main.spriteBatch;

            //绘制背景
            sb.Draw(TextureAssets.MagicPixel.Value, panelRect, Color.Black * 0.7f);
            //简单的边框线条
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), Color.Gold);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y + panelRect.Height - 2, panelRect.Width, 2), Color.Gold);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y, 2, panelRect.Height), Color.Gold);

            //绘制图标
            float iconSize = height - padding * 2;
            Vector2 iconPos = new Vector2(x + padding, y + padding);
            if (_currentNotification.Icon != null) {
                Rectangle src = _currentNotification.IconFrame ?? _currentNotification.Icon.Frame();
                float scale = iconSize / MathHelper.Max(src.Width, src.Height);
                sb.Draw(_currentNotification.Icon, iconPos + new Vector2(iconSize / 2, iconSize / 2), src, Color.White, 0f, src.Size() / 2, scale, SpriteEffects.None, 0f);
            }

            //绘制文字
            float textX = x + padding * 2 + iconSize;
            Utils.DrawBorderString(sb, titleText, new Vector2(textX, y + 8), Color.Gold, 0.8f);
            Utils.DrawBorderString(sb, nameText, new Vector2(textX, y + 30), Color.White, 0.9f);
        }
    }
}
