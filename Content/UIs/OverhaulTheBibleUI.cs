using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.UIs.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static System.Net.Mime.MediaTypeNames;

namespace CalamityOverhaul.Content.UIs
{
    internal class OverhaulTheBibleUI : CWRUIPanel
    {
        internal static OverhaulTheBibleUI Instance { get; private set; }
        public override void Load() => Instance = this;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BookPans");

        public bool Active;
        public Rectangle closeRec;
        public bool onCloseP;
        public Vector2 inCellPos => DrawPos + new Vector2(35, 32);
        public Vector2 cellSlpV => new Vector2(55, 50);
        public int maxXNum => 4;
        int snegValue => (ecTypeItemList.Count / 4 + 1) * 42;
        float SliderValueSengs {
            get {
                if (LCCoffsetY == 0) {
                    LCCoffsetY = 0.00001f;//无论如何都不要出现零
                }
                return (LCCoffsetY / -snegValue) * 198;
            }
        }
        public int onIndex;
        /// <summary>
        /// 垂直坐标矫正值，因为是需要上下滑动的，这个值作为滑动的矫正变量使用
        /// </summary>
        private float LCCoffsetY;
        /// <summary>
        /// 上一帧的鼠标状态
        /// </summary>
        private MouseState oldMouseState;
        internal List<Item> ecTypeItemList;

        private Rectangle MainRec;
        private bool OnMain;

        public override void Initialize() {
            DrawPos = new Vector2(500, 300);
            MainRec = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, Texture.Width, Texture.Height);
            OnMain = MainRec.Intersects(new Rectangle((int)MouPos.X, (int)MouPos.Y, 1, 1));
            closeRec = new Rectangle((int)(DrawPos.X + 470), (int)(DrawPos.Y + 190), 30, 30);
            onCloseP = closeRec.Intersects(new Rectangle((int)MouPos.X, (int)MouPos.Y, 1, 1));

            Instance.ecTypeItemList = new List<Item>();
            foreach (BaseRItem baseRItem in CWRMod.RItemInstances) {
                Item item = new Item(baseRItem.TargetID);
                if (item != null) {
                    if (item.type != ItemID.None) {
                        Instance.ecTypeItemList.Add(item);
                    }
                }
            }
        }
        public override void Update(GameTime gameTime) {
            Initialize();

            int museS = DownStartL();

            if (onCloseP) {
                player.mouseInterface = true;
                if (museS == 1) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    Active = false;
                }
            }

            if (OnMain) {
                player.mouseInterface = true;
                MouseState currentMouseState = Mouse.GetState();
                int scrollWheelDelta = currentMouseState.ScrollWheelValue - oldMouseState.ScrollWheelValue;
                //更具滚轮的变动量来更新矫正值
                LCCoffsetY += scrollWheelDelta * 0.2f;
                if (LCCoffsetY < -snegValue) {
                    LCCoffsetY = -snegValue;
                }
                if (LCCoffsetY > 0) {
                    LCCoffsetY = 0;
                }
                //更新上一帧的鼠标状态
                oldMouseState = currentMouseState;
                //计算鼠标位置相对于滑轮矩阵左上角的真实位置
                Vector2 mouseInCellPos = MouPos - DrawPos - new Vector2(16, 16);

                if (mouseInCellPos.X > 198) {
                    mouseInCellPos.X = 198;
                }
                if (mouseInCellPos.Y > 198) {
                    mouseInCellPos.Y = 198;
                }
                int mouseCellX = (int)(mouseInCellPos.X / 50);
                int mouseCellY = (int)((mouseInCellPos.Y - LCCoffsetY) / 50);
                onIndex = (mouseCellY) * maxXNum + mouseCellX;
            }
            time++;
        }
        private Vector2 inIndexGetPos(int index) {
            int x = index % maxXNum;
            int y = index / maxXNum;
            return new Vector2(x, y);
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (DrawPos == Vector2.Zero) {
                DrawPos = new Vector2(500, 300);
            }
            //绘制UI主体
            spriteBatch.Draw(Texture, DrawPos, null, Color.White, 0f, Vector2.Zero, new Vector2(2, 1), SpriteEffects.None, 0);
            //进行矩形画布裁剪绘制
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);
            Rectangle originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle newScissorRect = CWRUtils.GetClippingRectangle(spriteBatch, new((int)DrawPos.X + 9, (int)DrawPos.Y + 9, 239, 213));
            spriteBatch.GraphicsDevice.ScissorRectangle = newScissorRect;
            //遍历绘制索引目标的实例
            for (int i = 0; i < ecTypeItemList.Count; i++) {
                Item item = ecTypeItemList[i];
                Main.instance.LoadItem(item.type);
                Vector2 drawPos = inCellPos + new Vector2(0, LCCoffsetY) + inIndexGetPos(i) * cellSlpV;
                //if (item.type == ModContent.ItemType<Murasama>()) {
                //    Texture2D value = TextureAssets.Item[item.type].Value;
                //    spriteBatch.Draw(value, drawPos, CWRUtils.GetRec(value, (time / 5) % 12, 13), Color.White, 0f, CWRUtils.GetOrig(value, 13), 0.35f, SpriteEffects.None, 0);
                //}
                //else {
                //    SupertableUI.DrawItemIcons(spriteBatch, item, drawPos);
                //}
                SupertableUI.DrawItemIcons(spriteBatch, item, drawPos);
            }
            //恢复画布
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            spriteBatch.ResetUICanvasState();
            if (onIndex >= 0 && onIndex < ecTypeItemList.Count && OnMain) {
                if (ecTypeItemList[onIndex]?.type != ItemID.None) {
                    Item previewItem = ecTypeItemList[onIndex];
                    Main.HoverItem = previewItem.Clone();
                    Main.hoverItemName = previewItem.Name;
                }
            }

            Texture2D value2 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SliderBar");
            spriteBatch.Draw(value2, DrawPos + new Vector2(5, 5), null, Color.White, 0f, Vector2.Zero, new Vector2(2, 0.88f), SpriteEffects.None, 0);
            Texture2D value3 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/ScrollbarInner");
            spriteBatch.Draw(value3, DrawPos + new Vector2(5, 5) + new Vector2(0, SliderValueSengs), null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

            string text = CWRLocText.GetTextValue("OverhaulTheBibleUI_Text");
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRUtils.GetSafeText(text, FontAssets.MouseText.Value.MeasureString(text), 110)
                , DrawPos.X + 250, DrawPos.Y + 16, Color.Black, Color.White, new Vector2(0.3f), 1);

            spriteBatch.Draw(CWRUtils.GetT2DValue("CalamityMod/UI/DraedonSummoning/DecryptCancelIcon")
                , DrawPos + new Vector2(470, 190), null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);//绘制出关闭按键
            if (onCloseP) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRLocText.GetTextValue("SupertableUI_Text1")
                    , DrawPos.X + 470, DrawPos.Y + 190, Color.Gold, Color.Black, new Vector2(0.3f), 1.1f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.1f));
            }
        }
    }
}
