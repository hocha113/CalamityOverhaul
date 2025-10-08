using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class FuelElementUI : UIHandle
    {
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ThermalSlot");
        internal ThermalGeneratorUI thermalGenerator;
        private ThermalData ThermalData => thermalGenerator.GeneratorTP.MachineData as ThermalData;
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size());
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));

            if (!hoverInMainPage) {
                return;
            }

            player.mouseInterface = true;
            if (!ThermalData.FuelItem.IsAir) {
                Main.HoverItem = ThermalData.FuelItem.Clone();
                Main.hoverItemName = ThermalData.FuelItem.Name;
            }

            if (keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (!FuelItems.FuelItemToCombustion.ContainsKey(Main.mouseItem.type) && Main.mouseItem.type != ItemID.None) {
                return;
            }

            if (thermalGenerator.GeneratorTP is ThermalGeneratorTP thermal) {
                thermal.HandlerItem();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);

            if (ThermalData.TemperatureTransfer > 0) {
                Texture2D fire = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/FirePower");
                Rectangle fireRect = (DrawPosition + new Vector2(16, 52)).GetRectangle(fire.Size());
                Main.spriteBatch.Draw(fire, fireRect, Color.White * (ThermalData.TemperatureTransfer / ThermalData.MaxTemperatureTransfer));
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
        private ThermalData ThermalData => thermalGenerator.GeneratorTP.MachineData as ThermalData;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/Thermalheat");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size());
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //绘制主UI背景
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);

            //获取纹理和计算需要绘制的矩形区域
            Texture2D texture2 = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ThermalheatFull");
            float temperatureRatio = ThermalData.Temperature / ThermalData.MaxTemperature;  //计算温度的比率
            float sengs = 1 - temperatureRatio;

            //计算绘制区域的Y值和高度，避免重复计算
            Rectangle full = new Rectangle(0, (int)(texture2.Height * sengs), texture2.Width, (int)(texture2.Height * temperatureRatio));

            //绘制温度相关的图像
            Vector2 position = DrawPosition + new Vector2(8, 40 + full.Y);
            Main.spriteBatch.Draw(texture2, position, full, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            //如果鼠标在主页面中，显示温度信息
            if (hoverInMainPage) {
                string temperatureText = $"{((int)ThermalData.Temperature)}/{((int)ThermalData.MaxTemperature)}°C";
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
        private ThermalData ThermalData => thermalGenerator.GeneratorTP.MachineData as ThermalData;
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ThermalPower");
        public override void Update() {
            UIHitBox = DrawPosition.GetRectangle(Texture.Size());
            hoverInMainPage = UIHitBox.Intersects(MousePosition.GetRectangle(1));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(Texture, UIHitBox, Color.White);
            Texture2D texture2 = CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ThermalPowerFull");
            float ueRatio = ThermalData.UEvalue / ThermalData.MaxUEValue;
            float sengs = 1 - ueRatio;

            //计算绘制区域的Y值和高度，避免重复计算
            Rectangle full = new Rectangle(0, (int)(texture2.Height * sengs), texture2.Width, (int)(texture2.Height * ueRatio));

            //绘制温度相关的图像
            Vector2 position = DrawPosition + new Vector2(12, 12 + full.Y);
            Main.spriteBatch.Draw(texture2, position, full, Color.White, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            //如果鼠标在主页面中，显示温度信息
            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, (ThermalData.UEvalue + "/" + ThermalData.MaxUEValue + "UE").ToString()
                    , MousePosition.X, MousePosition.Y + 40, Color.White, Color.Black, new Vector2(0.3f), 1f);
            }
        }
    }

    internal class ThermalGeneratorUI : BaseGeneratorUI
    {
        internal FuelElementUI fuelElement = new FuelElementUI();
        internal CombustionValueUI combustionValue = new CombustionValueUI();
        internal ElectricPowerUI electricPower = new ElectricPowerUI();
        public override Texture2D Texture => CWRUtils.GetT2DValue(CWRConstant.UI + "Generator/ThermalPanel");
        public override void UpdateElement() {
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 110, Main.screenWidth - 110);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 110, Main.screenHeight - 110);

            fuelElement.thermalGenerator = this;
            fuelElement.DrawPosition = DrawPosition + new Vector2(64, 56);
            fuelElement.Update();

            combustionValue.thermalGenerator = this;
            combustionValue.DrawPosition = DrawPosition + new Vector2(172, 28);
            combustionValue.Update();

            electricPower.thermalGenerator = this;
            electricPower.DrawPosition = DrawPosition + new Vector2(0, 28);
            electricPower.Update();
        }

        public override void OnEnterWorld() => IsActive = false;

        public override void SaveUIData(TagCompound tag) {
            tag["ThermalGeneratorUI_DrawPos_X"] = DrawPosition.X;
            tag["ThermalGeneratorUI_DrawPos_Y"] = DrawPosition.Y;
        }

        public override void LoadUIData(TagCompound tag) {
            if (tag.TryGet("ThermalGeneratorUI_DrawPos_X", out float x)) {
                DrawPosition.X = x;
            }
            else {
                DrawPosition.X = 500;
            }

            if (tag.TryGet("ThermalGeneratorUI_DrawPos_Y", out float y)) {
                DrawPosition.Y = y;
            }
            else {
                DrawPosition.Y = 300;
            }
        }

        public override void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();
            if ((!item.IsAir) && FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return;
            }

            if (!Main.keyState.PressingShift()) {
                if (!newTP) {
                    IsActive = !IsActive;
                }
                else {
                    IsActive = true;
                }
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
