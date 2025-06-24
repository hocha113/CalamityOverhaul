using CalamityMod;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.UIs.OverhaulTheBible
{
    internal class OverhaulTheBibleUI : UIHandle, ICWRLoader
    {
        internal static OverhaulTheBibleUI Instance => UIHandleLoader.GetUIHandleOfType<OverhaulTheBibleUI>();
        public override bool Active { get => _active || _sengs > 0; set => _active = value; }
        private bool onDrag;
        private bool onTopP;
        private bool onTopDarg;
        private bool _active;
        internal float _sengs;
        private Vector2 dragOffset;
        internal int boxWeith = 400;
        internal int boxHeight = 300;
        internal List<ItemVidous> itemVidousList;
        internal MouseState oldMouseState;
        internal float rollerValue;
        internal float rollerSengs;
        private SliderUI sliderUI;
        private TabControl tabControlMelee;
        private TabControl tabControlRanged;
        private TabControl tabControlMagic;
        private TabControl tabControlRogue;
        private TabControl tabControlSummon;
        private TabControl tabControlClose;
        internal int elementsPerRow;
        internal int elementsPerColumn;
        public override void SaveUIData(TagCompound tag) {
            tag["OverhaulTheBibleUI_DrawPos_X"] = DrawPosition.X;
            tag["OverhaulTheBibleUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("OverhaulTheBibleUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = 500;
            }

            if (tag.TryGet("OverhaulTheBibleUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = 300;
            }
        }

        private void InitializeElement() {
            if (sliderUI == null) {
                sliderUI = new SliderUI();
            }
            if (tabControlMelee == null) {
                tabControlMelee = new TabControl();
                tabControlMelee.DamageClass = DamageClass.Melee;
            }
            if (tabControlRanged == null) {
                tabControlRanged = new TabControl();
                tabControlRanged.DamageClass = DamageClass.Ranged;
            }
            if (tabControlMagic == null) {
                tabControlMagic = new TabControl();
                tabControlMagic.DamageClass = DamageClass.Magic;
            }
            if (tabControlRogue == null) {
                tabControlRogue = new TabControl();
                tabControlRogue.DamageClass = CWRLoad.RogueDamageClass;
            }
            if (tabControlSummon == null) {
                tabControlSummon = new TabControl();
                tabControlSummon.DamageClass = DamageClass.Summon;
            }
            if (tabControlClose == null) {
                tabControlClose = new TabControl();
                tabControlClose.DamageClass = DamageClass.Default;
            }
            if (itemVidousList == null) {
                SetItemVidousList();
            }

            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, boxWeith, boxHeight);
            Rectangle topHit = new Rectangle((int)DrawPosition.X + boxWeith - 20, (int)DrawPosition.Y + boxHeight - 20, 20, 20);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            onTopP = topHit.Intersects(mouseHit);
        }

        public override void Update() {

            InitializeElement();

            int itemVidousListCount = itemVidousList.Count > 0 ? itemVidousList.Count : 1;
            elementsPerRow = boxWeith / ItemVidous.Width; // 每行最多元素数
            if (elementsPerRow <= 0) {
                elementsPerRow = 1; // 确保至少有一列
            }
            elementsPerColumn = itemVidousListCount / elementsPerRow;
            if (elementsPerColumn <= 0) {
                elementsPerColumn = 1; // 确保至少有一行
            }

            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Held && !onTopDarg && !UIHandleLoader.GetUIHandleOfType<SliderUI>().onDrag) {
                    if (!onDrag) {
                        dragOffset = DrawPosition - MousePosition;
                    }
                    onDrag = true;
                }
            }

            MouseState currentMouseState = Mouse.GetState();
            int scrollWheelDelta = currentMouseState.ScrollWheelValue - oldMouseState.ScrollWheelValue;
            rollerValue -= scrollWheelDelta;
            rollerValue = MathHelper.Clamp(rollerValue, 0, elementsPerColumn * ItemVidous.Height);
            oldMouseState = currentMouseState;
            rollerSengs = (rollerValue / (elementsPerColumn * ItemVidous.Height)) * boxHeight;

            if (onDrag) {
                player.mouseInterface = true;
                DrawPosition = MousePosition + dragOffset;
                if (keyLeftPressState == KeyPressState.Released || onTopDarg) {
                    onDrag = false;
                }
            }

            if (onTopP) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Held) {
                    onTopDarg = true;
                }
            }

            if (onTopDarg) {
                boxWeith = Math.Max((int)Math.Abs(DrawPosition.X - MousePosition.X - 5), 10);
                if (MousePosition.X < DrawPosition.X) {
                    DrawPosition.X = MousePosition.X;
                }
                boxHeight = Math.Max((int)Math.Abs(DrawPosition.Y - MousePosition.Y - 5), 10);
                if (MousePosition.Y < DrawPosition.Y) {
                    DrawPosition.Y = MousePosition.Y;
                }
                if (keyLeftPressState == KeyPressState.Released) {
                    onTopDarg = false;
                }
            }

            sliderUI.Update();
            UpdateTabControl();

            for (int i = 0; i < itemVidousList.Count; i++) {
                ItemVidous itemV = itemVidousList[i];
                int width = ItemVidous.Width;
                int height = ItemVidous.Height;

                // 计算当前元素的行列位置
                int row = i / elementsPerRow;
                int column = i % elementsPerRow;
                Vector2 offset = new Vector2(column * width, row * height - rollerValue);
                // 根据行列位置计算 DrawPosition
                itemV.DrawPosition = DrawPosition + offset + ItemVidous.handerOffsetTopL;
                itemV.Update();
            }

            if (_active) {
                if (_sengs < 1) {
                    _sengs += 0.1f;
                }
            }
            else {
                if (_sengs > 0) {
                    _sengs -= 0.1f;
                }
                if (_sengs < 0) {
                    _sengs = 0;
                }
            }
        }

        internal void SetItemVidousList() {
            itemVidousList = [];
            foreach (var rItem in ItemOverride.Instances) {
                if (rItem.Mod != CWRMod.Instance || !rItem.DrawingInfo || rItem.TargetID <= 0) {
                    continue;
                }

                Item ccItem = new Item(rItem.TargetID);

                if (!tabControlMelee.Tab && (ccItem.DamageType == DamageClass.Melee
                            || ccItem.DamageType == ModContent.GetInstance<MeleeNoSpeedDamageClass>()
                            || ccItem.DamageType == ModContent.GetInstance<TrueMeleeDamageClass>()
                            || ccItem.DamageType == ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>())) {
                    continue;
                }

                if (!tabControlRanged.Tab && ccItem.DamageType == DamageClass.Ranged) {
                    continue;
                }

                if (!tabControlMagic.Tab && ccItem.DamageType == DamageClass.Magic) {
                    continue;
                }

                if (!tabControlRogue.Tab && ccItem.DamageType == CWRLoad.RogueDamageClass) {
                    continue;
                }

                var itemVidous = new ItemVidous();
                itemVidous.BaseRItem = rItem;
                itemVidousList.Add(itemVidous);
            }
        }

        private void UpdateTabControl() {
            tabControlMelee.DrawPosition = DrawPosition + new Vector2(-68, 0);
            tabControlMelee.Update();
            tabControlRanged.DrawPosition = DrawPosition + new Vector2(-68, 68);
            tabControlRanged.Update();
            tabControlMagic.DrawPosition = DrawPosition + new Vector2(-68, 68 * 2);
            tabControlMagic.Update();
            tabControlRogue.DrawPosition = DrawPosition + new Vector2(-68, 68 * 3);
            tabControlRogue.Update();
            tabControlSummon.DrawPosition = DrawPosition + new Vector2(-68, 68 * 4);
            tabControlSummon.Update();
            tabControlClose.DrawPosition = DrawPosition + new Vector2(-68, 68 * 5);
            tabControlClose.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_White.Value
                , 4, DrawPosition, (int)(boxWeith * _sengs), (int)(boxHeight * _sengs), Color.White, Color.GhostWhite, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRUtils.GetT2DValue(CWRConstant.UI + "JAR")
                , 4, DrawPosition, (int)(boxWeith * _sengs), (int)(boxHeight * _sengs), Color.White, Color.White * 0, 1);

            Rectangle viedutRect = new((int)(DrawPosition.X + ItemVidous.handerOffsetTopL.X), (int)(DrawPosition.Y + ItemVidous.handerOffsetTopL.Y)
                , (int)(boxWeith - ItemVidous.handerOffsetTopL.X), (int)(boxHeight - ItemVidous.handerOffsetTopL.Y * 2));

            //进行矩形画布裁剪绘制
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);
            Rectangle originalScissorRect = spriteBatch.GraphicsDevice.ScissorRectangle;
            Rectangle newScissorRect = VaultUtils.GetClippingRectangle(spriteBatch, viedutRect);
            spriteBatch.GraphicsDevice.ScissorRectangle = newScissorRect;

            foreach (var itemV in itemVidousList) {
                itemV.Draw(spriteBatch);
            }

            //恢复画布
            spriteBatch.GraphicsDevice.ScissorRectangle = originalScissorRect;
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value
                , 4, viedutRect.TopLeft(), (int)(viedutRect.Width * _sengs), (int)(viedutRect.Height * _sengs), Color.White, Color.White * 0, 1);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value
                , 4, DrawPosition + new Vector2(boxWeith, 0) * _sengs, 10, (int)(boxHeight * _sengs), Color.Blue, Color.White * 0, 1);

            sliderUI.Draw(spriteBatch);

            tabControlMelee.Draw(spriteBatch);
            tabControlRanged.Draw(spriteBatch);
            tabControlMagic.Draw(spriteBatch);
            tabControlRogue.Draw(spriteBatch);
            tabControlSummon.Draw(spriteBatch);
            tabControlClose.Draw(spriteBatch);

            if (onTopP) {
                Texture2D value = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/TexturePackButtons");
                Rectangle r1 = new Rectangle(0, 0, 32, 32);
                spriteBatch.Draw(value, MousePosition, r1, Color.White, MathHelper.PiOver4 + MathHelper.PiOver2, r1.Size() / 2, 1, SpriteEffects.None, 0);
            }
        }
    }
}
