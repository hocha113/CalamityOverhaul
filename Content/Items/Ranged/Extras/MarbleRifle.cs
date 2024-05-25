using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class MarbleRifle : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "MarbleRifle";
        public override void SetDefaults() {
            Item.SetCalamitySD<GraniteRifle>();
            Item.SetCartridgeGun<MarbleRifleHeldProj>(10);
            Item.CWR().Scope = true;
        }
    }
}
