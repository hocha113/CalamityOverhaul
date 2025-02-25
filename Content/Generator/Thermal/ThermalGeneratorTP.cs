using InnoVault.UIHandles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Generator.Thermal
{
    internal class ThermalGeneratorTP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<ThermalGeneratorTile>();
        internal int frame;
        internal bool oldMouseLeft;
        internal ThermalData ThermalData => GeneratorData as ThermalData;
        public override GeneratorData GetGeneratorDataInds() => new ThermalData();
        public override void GeneratorUpdate() {
            if (PosInWorld.Distance(Main.LocalPlayer.Center) > maxFindMode) {
                if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this 
                    && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                    UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }
            else {
                Rectangle rectangle = PosInWorld.GetRectangle(32);
                bool newLeft = !oldMouseLeft && Main.mouseLeft;
                oldMouseLeft = Main.mouseLeft;
                if (rectangle.Intersects(Main.MouseWorld.GetRectangle(1)) && newLeft) {
                    if (Main.mouseItem.type != ItemID.None) {
                        Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.FromObjectGetParent(), ThermalData.FuelItem, ThermalData.FuelItem.stack);
                        ThermalData.FuelItem = Main.mouseItem.Clone();
                        Main.LocalPlayer.itemAnimation = 0;
                        Main.mouseItem.TurnToAir();
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                }
            }

            if (ThermalData.FuelItem != null && ThermalData.FuelItem.type != ItemID.None && ThermalData.Temperature <= 900) {
                if (++ThermalData.ChargeCool > 6) {
                    ThermalData.FuelItem.stack--;
                    ThermalData.Temperature += 50;
                    if (ThermalData.Temperature > 1000) {
                        ThermalData.Temperature = 1000;
                    }
                    if (ThermalData.FuelItem.stack <= 0) {
                        ThermalData.FuelItem.TurnToAir();
                    }
                    ThermalData.ChargeCool = 0;
                }
            }

            if (ThermalData.Temperature > 0 && GeneratorData.UEvalue <= 6000) {
                ThermalData.Temperature--;
                ThermalData.UEvalue++;
            }

            if (ThermalData.Temperature > 0) {
                CWRUtils.ClockFrame(ref frame, 5, 7, 1);
            }
            else {
                frame = 0;
            }
        }
    }
}
