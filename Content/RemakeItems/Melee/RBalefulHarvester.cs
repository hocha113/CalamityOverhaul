using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBalefulHarvester : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BalefulHarvester>();
        public override int ProtogenesisID => ModContent.ItemType<BalefulHarvesterEcType>();
        public override string TargetToolTipItemName => "BalefulHarvesterEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => BalefulHarvesterEcType.SetDefaultsFunc(item);

        public override bool? CanUseItem(Item item, Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<BalefulHarvesterHeldThrow>()] == 0;

        public override bool? AltFunctionUse(Item item, Player player) {
            item.initialize();
            return item.CWR().ai[0] <= 0;
        }

        public override void HoldItem(Item item, Player player) {
            item.initialize();
            if (item.CWR().ai[0] > 0) {
                item.CWR().ai[0]--;
            }
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
            => BalefulHarvesterEcType.PostDrawInInventoryFunc(item, spriteBatch, position, frame, scale);

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BalefulHarvesterEcType.ShootFunc(item, player, source, position, velocity, type, damage, knockback);
    }
}
