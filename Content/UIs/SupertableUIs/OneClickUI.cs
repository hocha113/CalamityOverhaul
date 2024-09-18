using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class OneClickUI : CWRUIPanel
    {
        public static OneClickUI Instance;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/OneClick");
        protected SupertableUI mainUI => SupertableUI.Instance;
        protected virtual Vector2 offsetDraw => new Vector2(578, 330);
        private Rectangle mainRec;
        private int useTimeCoolding;
        private int useMuse3AddCount;
        private bool onMainP;
        private bool checkSetO => GetType() != typeof(OneClickUI);
        public override void Load() => Instance = this;
        public override void Update(GameTime gameTime) {
            // 更新当前绘制位置和矩形
            DrawPosition = mainUI.DrawPosition + offsetDraw;
            mainRec = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 30, 30);
            // 判断鼠标是否在主矩形内
            bool isMouseOverMainRec = mainRec.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            int mouseState = DownStartL();
            // 重置鼠标点击计数器
            if (mouseState != 1 && mouseState != 3) {
                useMuse3AddCount = 30;
            }
            // 当鼠标在主区域内时，处理点击事件
            if (isMouseOverMainRec) {
                if (mouseState == 1 || mouseState == 3) {
                    HandleClickEvents(mouseState);
                }
            }
            // 冷却时间递减
            if (useTimeCoolding > 0) {
                useTimeCoolding--;
            }
            onMainP = isMouseOverMainRec;
        }
        private void HandleClickEvents(int mouseState) {
            if (checkSetO) {
                // 如果有物品在使用，执行点击事件
                bool isItemInUse = mainUI.items.Any(item => item.type != ItemID.None);
                if (isItemInUse) {
                    ClickEvent();
                }
            }
            else if (useTimeCoolding <= 0 || mouseState == 1) {
                // 执行点击事件并处理冷却
                ClickEvent();
                useTimeCoolding = useMuse3AddCount;
                AdjustMouseClickSpeed();
            }
        }
        private void AdjustMouseClickSpeed() {
            // 调整鼠标点击速度
            if (useMuse3AddCount == 30) {
                useMuse3AddCount = 12;
            }
            else {
                useMuse3AddCount = Math.Max(useMuse3AddCount - 1, 1);
            }
        }
        protected virtual void ClickEvent() {
            mainUI.OneClickPFunc();
            mainUI.OutItem();
        }
        public override void Draw(SpriteBatch spriteBatch) {
            Color color = Color.White;
            if (onMainP) {
                color = Color.Gold;
            }
            spriteBatch.Draw(Texture, DrawPosition, null, color, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (onMainP) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                , checkSetO ? CWRLocText.GetTextValue("SupMUI_OneClick_Text2") : CWRLocText.GetTextValue("SupMUI_OneClick_Text1")
                , DrawPosition.X - 30, DrawPosition.Y + 30, Color.White, Color.Black, new Vector2(0.3f), 0.8f);
            }
        }
    }
}
