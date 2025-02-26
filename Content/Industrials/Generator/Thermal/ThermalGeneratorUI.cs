using CalamityOverhaul.Content.Industrials.Generator;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class FuelElementUI : UIHandle
    {
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/PlaceItem");
        internal ThermalGeneratorUI thermalGenerator;
        private ThermalData ThermalData => thermalGenerator.GeneratorTP.GeneratorData as ThermalData;
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size());
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));

            if (hoverInMainPage) {
                player.mouseInterface = true;

                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (FuelItems.FuelItemToCombustion.ContainsKey(Main.mouseItem.type) || Main.mouseItem.type == ItemID.None) {
                        SoundEngine.PlaySound(SoundID.Grab);
                        if (ThermalData.FuelItem.type == ItemID.None) {
                            ThermalData.FuelItem = Main.mouseItem.Clone();
                            Main.mouseItem.TurnToAir();
                        }
                        else {
                            if (Main.mouseItem.IsAir) {
                                Main.mouseItem = ThermalData.FuelItem.Clone();
                                ThermalData.FuelItem.TurnToAir();
                            }
                            else {
                                if (Main.mouseItem.type == ThermalData.FuelItem.type) {
                                    ThermalData.FuelItem.stack += Main.mouseItem.stack;
                                    Main.mouseItem.TurnToAir();
                                }
                                else if (Main.mouseItem.type != ItemID.None) {
                                    ThermalData.FuelItem = Main.mouseItem.Clone();
                                    Main.mouseItem = ThermalData.FuelItem.Clone();
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);

            if (ThermalData.Temperature > 0) {
                Texture2D fire = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/FirePower");
                Rectangle fireRect = (DrawPosition + new Vector2(16, 56)).GetRectangle(fire.Size());
                Main.spriteBatch.Draw(fire, fireRect, Color.White * (ThermalData.Temperature / 1000f));
            }

            if (ThermalData.FuelItem != null && ThermalData.FuelItem.type != ItemID.None) {
                Main.instance.LoadItem(ThermalData.FuelItem.type);
                VaultUtils.SimpleDrawItem(spriteBatch, ThermalData.FuelItem.type, DrawPosition + Texture.Size() / 2, 32);
                if (ThermalData.FuelItem.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, ThermalData.FuelItem.stack.ToString()
                        , DrawPosition.X, DrawPosition.Y + 40, Color.White, Color.Black, new Vector2(0.3f), 1f);
                }
            }
        }
    }

    internal class CombustionValueUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal ThermalGeneratorUI thermalGenerator;
        private ThermalData ThermalData => thermalGenerator.GeneratorTP.GeneratorData as ThermalData;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/CombustionValue");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size());
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            // 绘制主UI背景
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);

            // 获取纹理和计算需要绘制的矩形区域
            Texture2D texture2 = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/CombustionValueFull");
            float temperatureRatio = ThermalData.Temperature / 1000f;  // 计算温度的比率
            float sengs = 1 - temperatureRatio;

            // 计算绘制区域的Y值和高度，避免重复计算
            Rectangle full = new Rectangle(0, (int)(texture2.Height * sengs), texture2.Width, (int)(texture2.Height * temperatureRatio));

            // 绘制温度相关的图像
            Vector2 position = DrawPosition + new Vector2(8, 10 + full.Y);
            Main.spriteBatch.Draw(texture2, position, full, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            Main.spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/CombustionValueGlow"), UIHitBox, Color.White);

            // 如果鼠标在主页面中，显示温度信息
            if (hoverInMainPage) {
                string temperatureText = $"{ThermalData.Temperature}/1000°C";
                Vector2 textPosition = new Vector2(MousePosition.X, MousePosition.Y + 40);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, temperatureText
                    , textPosition.X, textPosition.Y, Color.White, Color.Black, new Vector2(0.3f), 1f);
            }
        }
    }

    internal class ElectricPowerUI : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        internal ThermalGeneratorUI thermalGenerator;
        private ThermalData ThermalData => thermalGenerator.GeneratorTP.GeneratorData as ThermalData;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ElectricPower");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size());
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));

            if (hoverInMainPage) {
                if (keyRightPressState == KeyPressState.Pressed) {
                    ThermalData.UEvalue = 0;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);
            Texture2D texture2 = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ElectricPowerFull");
            float ueRatio = ThermalData.UEvalue / 6000f;
            float sengs = 1 - ueRatio;

            // 计算绘制区域的Y值和高度，避免重复计算
            Rectangle full = new Rectangle(0, (int)(texture2.Height * sengs), texture2.Width, (int)(texture2.Height * ueRatio));

            // 绘制温度相关的图像
            Vector2 position = DrawPosition + new Vector2(8, 36 + full.Y);
            Main.spriteBatch.Draw(texture2, position, full, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            Main.spriteBatch.Draw(CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ElectricPowerGlow"), UIHitBox, Color.White);

            // 如果鼠标在主页面中，显示温度信息
            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, (ThermalData.UEvalue + "/6000UE").ToString()
                    , MousePosition.X, MousePosition.Y + 40, Color.White, Color.Black, new Vector2(0.3f), 1f);
            }
        }
    }

    internal class ThermalGeneratorUI : BaseGeneratorUI
    {
        internal FuelElementUI fuelElement = new FuelElementUI();
        internal CombustionValueUI combustionValue = new CombustionValueUI();
        internal ElectricPowerUI electricPower = new ElectricPowerUI();
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/GeneratorPanel");
        public override void UpdateElement() {
            fuelElement.thermalGenerator = this;
            fuelElement.DrawPosition = DrawPosition + new Vector2(100, 50);
            fuelElement.Update();

            combustionValue.thermalGenerator = this;
            combustionValue.DrawPosition = DrawPosition + new Vector2(20, 20);
            combustionValue.Update();

            electricPower.thermalGenerator = this;
            electricPower.DrawPosition = DrawPosition + new Vector2(200, 20);
            electricPower.Update();
        }

        public override void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();
            if ((!item.IsAir || Main.keyState.PressingShift()) && FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return;
            }
            if (!newTP) {
                IsActive = !IsActive;
            }
            else {
                IsActive = true;
            }
            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(Texture, UIHitBox, Color.White);
            fuelElement.Draw(spriteBatch);
            combustionValue.Draw(spriteBatch);
            electricPower.Draw(spriteBatch);
        }
    }
}
