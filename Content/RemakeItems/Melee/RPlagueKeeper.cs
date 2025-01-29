using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RPlagueKeeper : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<PlagueKeeper>();
        public override void SetDefaults(Item item) => PlagueKeeperEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return PlagueKeeperEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
        }
    }
}
