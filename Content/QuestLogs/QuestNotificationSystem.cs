using CalamityOverhaul.Common;
using CalamityOverhaul.Content.QuestLogs.Core;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
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
            public QuestNode node;

            public NotificationInfo(string title, Texture2D icon, Rectangle? iconFrame) {
                Title = title;
                Icon = icon;
                IconFrame = iconFrame;
            }

            public NotificationInfo(QuestNode node) {
                this.node = node;
            }
        }

        private class ActiveNotification
        {
            public NotificationInfo Info;
            public int Timer;
            public float CurrentY;

            public ActiveNotification(NotificationInfo info, float startY) {
                Info = info;
                Timer = 0;
                CurrentY = startY;
            }
        }

        public override bool Active => VaultLoad.LoadenContent;

        public string LocalizationCategory => "UI";

        private static Queue<NotificationInfo> _pendingNotifications = new();
        private static List<ActiveNotification> _activeNotifications = new();

        private const int SlideTime = 20;
        private const int DisplayTime = 180;
        private const int MaxActive = 6;
        private const float PanelHeight = 60;
        private const float PanelGap = 5;

        public static LocalizedText Text1;

        public override void SetStaticDefaults() {
            Text1 = this.GetLocalization(nameof(Text1), () => "任务完成");
        }

        public static void AddNotification(string title, Texture2D icon = null, Rectangle? iconFrame = null) {
            _pendingNotifications.Enqueue(new NotificationInfo(title, icon, iconFrame));
        }

        public static void AddNotification(QuestNode node) {
            _pendingNotifications.Enqueue(new NotificationInfo(node));
        }

        public static void PlayRollout() => SoundEngine.PlaySound(CWRSound.Rollout);

        public override void LogicUpdate() {
            //添加新通知
            if (_activeNotifications.Count < MaxActive && _pendingNotifications.Count > 0) {
                var info = _pendingNotifications.Dequeue();
                float targetY = GetTargetY(_activeNotifications.Count);
                _activeNotifications.Add(new ActiveNotification(info, targetY));
                PlayRollout();
            }

            //更新现有通知
            for (int i = _activeNotifications.Count - 1; i >= 0; i--) {
                var note = _activeNotifications[i];
                note.Timer++;

                //Y轴平滑移动
                float targetY = GetTargetY(i);
                note.CurrentY = MathHelper.Lerp(note.CurrentY, targetY, 0.1f);

                //移除过期通知
                if (note.Timer >= SlideTime * 2 + DisplayTime) {
                    _activeNotifications.RemoveAt(i);
                }
            }
        }

        public override void Update() {
            //更新现有通知
            for (int i = _activeNotifications.Count - 1; i >= 0; i--) {
                var note = _activeNotifications[i];

                //检查鼠标交互
                if (CheckMouseInteraction(note)) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.6f, Pitch = -0.2f });
                    //如果点击，跳过展示阶段，直接进入滑出阶段
                    if (note.Timer < SlideTime + DisplayTime) {
                        note.Timer = SlideTime + DisplayTime;
                    }
                }
            }
        }

        private static float GetTargetY(int index) {
            return Main.screenHeight * 0.3f + index * (PanelHeight + PanelGap);
        }

        private bool CheckMouseInteraction(ActiveNotification note) {
            //计算当前绘制位置
            float progress = GetProgress(note.Timer);
            float width = 260;
            float x = Main.screenWidth - width * progress;
            Rectangle rect = new Rectangle((int)x, (int)note.CurrentY, (int)width, (int)PanelHeight);

            if (rect.Contains(Main.MouseScreen.ToPoint())) {
                Main.LocalPlayer.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Main.mouseLeftRelease = false;
                    return true;
                }
            }
            return false;
        }

        private float GetProgress(int timer) {
            if (timer < SlideTime) {
                float p = timer / (float)SlideTime;
                return 1f - (1f - p) * (1f - p);
            }
            else if (timer > SlideTime + DisplayTime) {
                float p = 1f - (timer - SlideTime - DisplayTime) / (float)SlideTime;
                return 1f - (1f - p) * (1f - p);
            }
            return 1f;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            foreach (var note in _activeNotifications) {
                DrawSingleNotification(spriteBatch, note);
            }
        }

        private void DrawSingleNotification(SpriteBatch sb, ActiveNotification note) {
            float progress = GetProgress(note.Timer);
            if (note.Info.node != null) {
                note.Info.Title = note.Info.node.DisplayName.Value;
                note.Info.Icon = note.Info.node.GetIconTexture();
                note.Info.IconFrame = note.Info.node.GetIconSourceRect(note.Info.Icon);
            }

            string titleText = Text1.Value;
            string nameText = note.Info.Title;

            float width = 260;
            float height = PanelHeight;
            float padding = 10;

            float x = Main.screenWidth - width * progress;
            float y = note.CurrentY;

            Rectangle panelRect = new Rectangle((int)x, (int)y, (int)width, (int)height);

            //绘制背景
            sb.Draw(TextureAssets.MagicPixel.Value, panelRect, Color.Black * 0.7f);
            //简单的边框线条
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), Color.Gold);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y + panelRect.Height - 2, panelRect.Width, 2), Color.Gold);
            sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(panelRect.X, panelRect.Y, 2, panelRect.Height), Color.Gold);

            //绘制图标
            float iconSize = height - padding * 2;
            Vector2 iconPos = new Vector2(x + padding, y + padding);

            if (note.Info.Icon != null) {
                Rectangle src = note.Info.IconFrame ?? note.Info.Icon.Frame();
                float scale = iconSize / MathHelper.Max(src.Width, src.Height);
                sb.Draw(note.Info.Icon, iconPos + new Vector2(iconSize / 2, iconSize / 2), src, Color.White, 0f, src.Size() / 2, scale, SpriteEffects.None, 0f);
            }

            //绘制文字
            float textX = x + padding * 2 + iconSize;
            Utils.DrawBorderString(sb, titleText, new Vector2(textX, y + 8), Color.Gold, 0.8f);
            Utils.DrawBorderString(sb, nameText, new Vector2(textX, y + 30), Color.White, 0.9f);
        }
    }
}
