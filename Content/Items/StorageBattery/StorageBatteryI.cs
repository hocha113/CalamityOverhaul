using CalamityMod.Items;
using CalamityMod.Items.DraedonMisc;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;

namespace CalamityOverhaul.Content.Items.StorageBattery
{
    internal class StorageBatteryI : BaseStorageBattery
    {
        public override string Texture => CWRConstant.Item + "StorageBattery/StorageBatteryI";
        public override void SetStorageBattery(CalamityGlobalItem calStb, CWRItems cwrStb) {
            calStb.MaxCharge = 555;
            SingleChargePower = 10;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<DraedonPowerCell>(5)
                .AddIngredient<DubiousPlating>(5)
                .AddIngredient<EnergyCore>(1)
                .Register();
        }
    }
}
