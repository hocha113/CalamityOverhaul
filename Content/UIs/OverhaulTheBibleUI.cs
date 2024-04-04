using CalamityMod;
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

namespace CalamityOverhaul.Content.UIs
{
    internal class OverhaulTheBibleUI : CWRUIPanel
    {
        internal static OverhaulTheBibleUI Instance { get; private set; }
        public override void Load() => Instance = this;
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/BookPans");

        private Texture2D MeleeImage {
            get {
                Main.instance.LoadItem(4);
                return TextureAssets.Item[4].Value;
            }
        }
        private Texture2D RangedImage {
            get {
                Main.instance.LoadItem(99);
                return TextureAssets.Item[99].Value;
            }
        }
        private Texture2D MagicImage {
            get {
                Main.instance.LoadItem(114);
                return TextureAssets.Item[114].Value;
            }
        }
        private Texture2D SummonImage {
            get {
                Main.instance.LoadItem(4281);
                return TextureAssets.Item[4281].Value;
            }
        }
        private Texture2D RogueImage {
            get {
                Main.instance.LoadItem(55);
                return TextureAssets.Item[55].Value;
            }
        }

        private bool melee;
        private bool ranged;
        private bool magic;
        private bool summon;
        private bool rogue;

        private bool onMeleeP;
        private bool onRangedP;
        private bool onMagicP;
        private bool onSummonP;
        private bool onRogueP;

        private Rectangle meleeRec;
        private Rectangle rangedRec;
        private Rectangle magicRec;
        private Rectangle summonRec;
        private Rectangle rogueRec;

        private Vector2 meleePos;
        private Vector2 rangedPos;
        private Vector2 magicPos;
        private Vector2 summonPos;
        private Vector2 roguePos;

        private float SlideroutVlue;

        private bool _active;
        public bool Active {
            get => _active;
            set {
                if (!value) {
                    SlideroutVlue = 0f;
                }
                _active = value;
            }
        }
        public Rectangle CloseRec;
        public bool OnCloseP;
        public Vector2 InCellPos => DrawPos + new Vector2(35, 32);
        public Vector2 CellSlpSize => new Vector2(55, 50);
        public int MaxInFrameXNum => 4;
        int snegValue => (ecTypeItemList.Count / 4 - 1) * 50;
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
            DrawPos = new Vector2(600, 300);

            if (SlideroutVlue > -35) {
                SlideroutVlue--;
            }

            int frmeInY = 40;

            meleePos = new Vector2(SlideroutVlue, 30 + frmeInY * 0) + DrawPos;
            rangedPos = new Vector2(SlideroutVlue + 10, 30 + frmeInY * 1) + DrawPos;
            magicPos = new Vector2(SlideroutVlue, 30 + frmeInY * 2) + DrawPos;
            summonPos = new Vector2(SlideroutVlue - 10, 30 + frmeInY * 3) + DrawPos;
            roguePos = new Vector2(SlideroutVlue + 5, 30 + frmeInY * 4) + DrawPos;

            MainRec = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, Texture.Width, Texture.Height);

            meleeRec = new Rectangle((int)meleePos.X, (int)meleePos.Y, frmeInY, frmeInY);
            rangedRec = new Rectangle((int)rangedPos.X, (int)rangedPos.Y, frmeInY, frmeInY);
            magicRec = new Rectangle((int)magicPos.X, (int)magicPos.Y, frmeInY, frmeInY);
            summonRec = new Rectangle((int)summonPos.X, (int)summonPos.Y, frmeInY, frmeInY);
            rogueRec = new Rectangle((int)roguePos.X, (int)roguePos.Y, frmeInY, frmeInY);

            Rectangle mouseRec = new Rectangle((int)MouPos.X, (int)MouPos.Y, 1, 1);

            OnMain = MainRec.Intersects(mouseRec);

            onMeleeP = meleeRec.Intersects(mouseRec);
            onRangedP = rangedRec.Intersects(mouseRec);
            onMagicP = magicRec.Intersects(mouseRec);
            onSummonP = summonRec.Intersects(mouseRec);
            onRogueP = rogueRec.Intersects(mouseRec);

            CloseRec = new Rectangle((int)(DrawPos.X + 470), (int)(DrawPos.Y + 190), 30, 30);
            OnCloseP = CloseRec.Intersects(mouseRec);

            Instance.ecTypeItemList = new List<Item>();
            foreach (BaseRItem baseRItem in CWRMod.RItemInstances) {
                Item item = new Item(baseRItem.TargetID);
                if (item != null) {
                    if (item.type != ItemID.None) {
                        if (melee) {
                            if (item.DamageType == DamageClass.Melee) {
                                Instance.ecTypeItemList.Add(item);
                            }
                        }
                        if (ranged) {
                            if (item.DamageType == DamageClass.Ranged) {
                                Instance.ecTypeItemList.Add(item);
                            }
                        }
                        if (summon) {
                            if (item.DamageType == DamageClass.Summon) {
                                Instance.ecTypeItemList.Add(item);
                            }
                        }
                        if (rogue) {
                            if (item.DamageType == ModContent.GetInstance<RogueDamageClass>()) {
                                Instance.ecTypeItemList.Add(item);
                            }
                        }
                        if (magic) {
                            if (item.DamageType == DamageClass.Magic) {
                                Instance.ecTypeItemList.Add(item);
                            }
                        }
                    }
                }
            }
        }
        private void setSouldKey(ref bool keyValue, bool onP, int mouseS) {
            if (onP) {
                player.mouseInterface = true;
                if (mouseS == 1) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    LCCoffsetY = 0;
                    keyValue = !keyValue;
                }
            }
        }
        public override void Update(GameTime gameTime) {
            Initialize();

            int museS = DownStartL();

            if (OnCloseP) {
                player.mouseInterface = true;
                if (museS == 1) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    Active = false;
                }
            }

            setSouldKey(ref melee, onMeleeP, museS);
            setSouldKey(ref ranged, onRangedP, museS);
            setSouldKey(ref magic, onMagicP, museS);
            setSouldKey(ref summon, onSummonP, museS);
            setSouldKey(ref rogue, onRogueP, museS);

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
                int mouseCellX = (int)(mouseInCellPos.X / 50f);
                int mouseCellY = (int)((mouseInCellPos.Y - LCCoffsetY) / 50f);
                onIndex = (mouseCellY) * MaxInFrameXNum + mouseCellX;
            }
            
            time++;
        }
        private Vector2 inIndexGetPos(int index) {
            int x = index % MaxInFrameXNum;
            int y = index / MaxInFrameXNum;
            return new Vector2(x, y);
        }
        private Color getSouldColor(bool keyValue, bool onP) {
            Color color;
            if (keyValue) {
                color = new Color(255, 255, 255, 255);
            } else {
                color = new Color(0, 0, 0, 155);
            }
            if (onP) {
                color = new Color(255, 55, 255, 255);
            }
            if (keyValue) {
                color = new Color(255, 255, 255, 255);
            }
            return color;
        }
        private void inMouseDrawText(SpriteBatch spriteBatch, string text) {
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, text, Main.MouseScreen.X, Main.MouseScreen.Y + 30, Color.White, Color.Black, new Vector2(0.3f), 1);
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (DrawPos == Vector2.Zero) {
                DrawPos = new Vector2(500, 300);
            }

            spriteBatch.Draw(MeleeImage, meleePos, null, getSouldColor(melee, onMeleeP), 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(RangedImage, rangedPos, null, getSouldColor(ranged, onRangedP), 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(MagicImage, magicPos, null, getSouldColor(magic, onMagicP), 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(SummonImage, summonPos, null, getSouldColor(summon, onSummonP), 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(RogueImage, roguePos, null, getSouldColor(rogue, onRogueP), 0f, Vector2.Zero, 1, SpriteEffects.None, 0);

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
                Vector2 drawPos = InCellPos + new Vector2(0, LCCoffsetY) + inIndexGetPos(i) * CellSlpSize;
                SupertableUI.DrawItemIcons(spriteBatch, item, drawPos, new Vector2(0.001f, 0.001f));
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

            if (OnCloseP) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRLocText.GetTextValue("SupertableUI_Text1")
                    , DrawPos.X + 470, DrawPos.Y + 190, Color.Gold, Color.Black, new Vector2(0.3f), 1.1f + Math.Abs(MathF.Sin(Main.GameUpdateCount * 0.05f) * 0.1f));
            }

            if (onMeleeP) {
                inMouseDrawText(spriteBatch, CWRLocText.GetTextValue("OverhaulTheBibleUI_Text1"));
            }
            if (onRangedP) {
                inMouseDrawText(spriteBatch, CWRLocText.GetTextValue("OverhaulTheBibleUI_Text2"));
            }
            if (onMagicP) {
                inMouseDrawText(spriteBatch, CWRLocText.GetTextValue("OverhaulTheBibleUI_Text3"));
            }
            if (onSummonP) {
                inMouseDrawText(spriteBatch, CWRLocText.GetTextValue("OverhaulTheBibleUI_Text4"));
            }
            if (onRogueP) {
                inMouseDrawText(spriteBatch, CWRLocText.GetTextValue("OverhaulTheBibleUI_Text5"));
            }
        }
    }
}
