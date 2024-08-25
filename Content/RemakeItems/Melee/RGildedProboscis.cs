using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RGildedProboscis : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<GildedProboscis>();
        public override int ProtogenesisID => ModContent.ItemType<GildedProboscisEcType>();
        public override string TargetToolTipItemName => "GildedProboscisEcType";
        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[TargetID] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }
        public override void SetDefaults(Item item) => GildedProboscisEcType.SetDefaultsFunc(item);
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                Main.projectile[proj].ai[1] = 1;
            }
            return false;
        }
    }
}
