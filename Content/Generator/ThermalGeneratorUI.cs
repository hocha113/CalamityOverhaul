using CalamityOverhaul.Content.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Generator
{
    internal class FuelElementUI : ItemContainer
    {
        internal int chargeCool;
        internal int activeTime;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/PlaceItem");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size() * 2);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));

            if (hoverInMainPage) {
                player.mouseInterface = true;

                if (keyLeftPressState == KeyPressState.Pressed) {
                    Item mouseItem = Main.mouseItem.Clone();
                    Item uiItem = Item.Clone();

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

            if (activeTime > 0) {
                activeTime--;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);

            if (activeTime > 0) {
                Texture2D fire = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/FirePower");
                Rectangle fireRect = (DrawPosition + new Vector2(8, 28) * 2).GetRectangle(fire.Size() * 2);
                Main.spriteBatch.Draw(fire, fireRect, Color.White * (activeTime / 60f));
            }

            if (Item != null && Item.type != ItemID.None) {
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + Texture.Size(), 32);
                if (Item.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, Item.stack.ToString()
                        , DrawPosition.X, DrawPosition.Y + 40, Color.White, Color.Black, new Vector2(0.3f), 1f);
                }
            }
        }
    }

    internal class CombustionValueUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal float FEvalue;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/CombustionValue");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size() * 2);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);
            Texture2D texture2 = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/CombustionValueFull");
            Rectangle full = (DrawPosition + new Vector2(4, 8) * 2).GetRectangle(texture2.Size() * 2);
            float sengs = FEvalue / 1000f;
            full.Height = (int)(full.Height * sengs);
            Main.spriteBatch.Draw(texture2, full, Color.White);

            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, (FEvalue + "/1000FE").ToString()
                    , MousePosition.X, MousePosition.Y + 40, Color.White, Color.Black, new Vector2(0.3f), 1f);
            }
        }
    }

    internal class ElectricPowerUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal float UEvalue;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ElectricPower");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size() * 2);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));

            if (hoverInMainPage) {
                if (keyRightPressState == KeyPressState.Pressed) {
                    UEvalue = 0;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);
            Texture2D texture2 = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ElectricPowerFull");
            
            Rectangle full = (DrawPosition + new Vector2(4, 20) * 2).GetRectangle(texture2.Size() * 2);
            float sengs = UEvalue / 6000f;
            full.Height = (int)(full.Height * sengs);
            Main.spriteBatch.Draw(texture2, full, Color.White);

            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, (UEvalue + "/6000UE").ToString()
                    , MousePosition.X, MousePosition.Y + 40, Color.White, Color.Black, new Vector2(0.3f), 1f);
            }
        }
    }

    internal class ThermalGeneratorUI : UIHandle
    {
        internal static bool IsActive;
        public override bool Active => IsActive;
        private bool onDrag;
        private Vector2 dragOffset;
        internal FuelElementUI fuelElement = new FuelElementUI();
        internal CombustionValueUI combustionValue = new CombustionValueUI();
        internal ElectricPowerUI electricPower = new ElectricPowerUI();
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/GeneratorPanel");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size() * 2);
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));

            if (hoverInMainPage) {
                if (keyRightPressState == KeyPressState.Held) {
                    if (!onDrag) {
                        dragOffset = MousePosition.To(DrawPosition);
                    }
                    onDrag = true;
                }
            }
            if (onDrag) {
                DrawPosition = MousePosition + dragOffset;
                if (keyRightPressState == KeyPressState.Released) {
                    onDrag = false;
                }
            }

            fuelElement.DrawPosition = DrawPosition + new Vector2(100, 50);
            fuelElement.Update();
            combustionValue.DrawPosition = DrawPosition + new Vector2(20, 20);
            combustionValue.Update();
            electricPower.DrawPosition = DrawPosition + new Vector2(200, 20);
            electricPower.Update();

            if (fuelElement.Item != null && fuelElement.Item.type != ItemID.None && combustionValue.FEvalue <= 900) {
                if (fuelElement.activeTime < 60) {
                    fuelElement.activeTime += 60;
                    if (fuelElement.activeTime > 60) {
                        fuelElement.activeTime = 60;
                    }
                }
                
                if (++fuelElement.chargeCool > 6) {
                    fuelElement.Item.stack--;
                    combustionValue.FEvalue += 50;
                    if (combustionValue.FEvalue > 1000) {
                        combustionValue.FEvalue = 1000;
                    }
                    if (fuelElement.Item.stack <= 0) {
                        fuelElement.Item.TurnToAir();
                    }
                    fuelElement.chargeCool = 0;
                }
            }

            if (combustionValue.FEvalue > 0 && electricPower.UEvalue <= 6000) {
                combustionValue.FEvalue--;
                electricPower.UEvalue++;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);
            fuelElement.Draw(spriteBatch);
            combustionValue.Draw(spriteBatch);
            electricPower.Draw(spriteBatch);
        }
    }
}
