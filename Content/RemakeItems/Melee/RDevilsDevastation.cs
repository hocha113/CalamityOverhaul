using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDevilsDevastation : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<DevilsDevastation>();
        private int Level;
        public override void SetDefaults(Item item) => DevilsDevastationEcType.SetDefaultsFunc(item);
        public override bool? CanUseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] <= 0;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            return DevilsDevastationEcType.ShootFunc(ref Level, item
                , player, source, position, velocity, type, damage, knockback);
        }
    }
}
