using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.CompressorUIs
{
    internal class ItemAccept : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal ItemConversion FaterConversion;
        public Item Item { get; set; } = new Item();
        public int TargetID { get; set; }
        internal bool IsRight;
        public override void Update() {
            if (!IsRight) {
                if (FaterConversion != null && FaterConversion.NextConversion != null) {
                    FaterConversion.NextConversion.ContainerLeft.TargetID = FaterConversion.TargetItem.type;
                }
            }
            else {
                if (FaterConversion != null) {
                    TargetID = FaterConversion.TargetItem.type;
                }
            }

            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, ItemConversion.Weith, ItemConversion.Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            if (hoverInMainPage) {
                player.mouseInterface = true;

                if (keyLeftPressState == KeyPressState.Pressed) {
                    Item mouseItem = Main.mouseItem.Clone();
                    Item uiItem = Item.Clone();

                    if (!IsRight) {
                        if (TargetID > 0 && TargetID != mouseItem.type) {
                            if (Item.type > ItemID.None) {
                                Main.mouseItem = uiItem;
                                Item.TurnToAir();
                                SoundEngine.PlaySound(SoundID.Grab);
                                return;
                            }
                            SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.4f });
                            return;
                        }
                    }
                    else {
                        if (mouseItem.type != TargetID && uiItem.type != TargetID) {
                            SoundEngine.PlaySound(SoundID.Grab with { Pitch = -0.4f });
                            return;
                        }
                    }

                    if (mouseItem.type > ItemID.None || uiItem.type > ItemID.None) {
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    if (mouseItem.type != uiItem.type) {
                        Main.mouseItem = uiItem;
                        Item = mouseItem;
                    }
                    else if (mouseItem.type == uiItem.type) {
                        Item.stack += Main.mouseItem.stack;
                        Main.mouseItem.TurnToAir();
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value
                , 4, DrawPosition, ItemConversion.Weith, ItemConversion.Height
                , Color.AliceBlue * 0.8f, Color.Azure * 0.2f, 1);

            if (!IsRight) {
                if (TargetID > ItemID.None) {
                    Vector2 drawPos = DrawPosition + new Vector2(ItemConversion.Weith, ItemConversion.Height) / 2;

                    Texture2D aim = CWRUtils.GetT2DValue(CWRConstant.Other + "AimTarget");
                    spriteBatch.Draw(aim, drawPos, null, Color.White * 0.4f, 0, aim.Size() / 2, 0.1f, SpriteEffects.None, 0);

                    float drawSize = VaultUtils.GetDrawItemSize(new Item(TargetID), (int)(ItemConversion.Weith * 0.6f)) * 1;
                    VaultUtils.SimpleDrawItem(spriteBatch, TargetID, drawPos, drawSize, 0, Color.White * 0.6f);
                }
            }
            else if (TargetID > ItemID.None) {
                float drawSize = VaultUtils.GetDrawItemSize(FaterConversion.TargetItem, (int)(ItemConversion.Weith * 0.6f)) * 1;
                Vector2 drawPos = DrawPosition + new Vector2(ItemConversion.Weith, ItemConversion.Height) / 2;
                VaultUtils.SimpleDrawItem(spriteBatch, TargetID, drawPos, drawSize, 0, Color.White * 0.6f);
            }

            if (Item.type > ItemID.None) {
                float drawSize = VaultUtils.GetDrawItemSize(Item, ItemConversion.Weith) * 1;
                Vector2 drawPos = DrawPosition + new Vector2(ItemConversion.Weith, ItemConversion.Height) / 2;
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, drawPos, drawSize, 0, Color.White);
                if (Item.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, Item.stack.ToString()
                        , drawPos.X, drawPos.Y + 20, Color.White, Color.Black, new Vector2(0.3f), 1f);
                }
            }
        }
    }
}
