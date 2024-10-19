using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDragonRage : BaseRItem
    {
        private int Level;
        private int LevelAlt;
        public override int TargetID => ModContent.ItemType<DragonRage>();
        public override int ProtogenesisID => ModContent.ItemType<DragonRageEcType>();
        public override string TargetToolTipItemName => "DragonRageEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<DragonRage>()] = true;
        public override void SetDefaults(Item item) => DragonRageEcType.SetDefaultsFunc(item);

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return DragonRageEcType.ShootFunc(ref Level, ref LevelAlt, item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? UseItem(Item item, Player player) => player.ownedProjectileCounts[item.shoot] == 0;
    }
}
