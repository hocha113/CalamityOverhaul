using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheDarkMaster : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<TheDarkMaster>();
        public override int ProtogenesisID => ModContent.ItemType<TheDarkMasterEcType>();
        public override string TargetToolTipItemName => "TheDarkMasterEcType";
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<TheDarkMasterRapier>();
            item.useTime = 45;
            item.useAnimation = 45;
            item.autoReuse = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3.5f;
            item.shootSpeed = 5f;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.channel = true;
        }
        public override bool? On_AltFunctionUse(Item item, Player player) => false;
        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame
            , Color drawColor, Color itemColor, Vector2 origin, float scale) {
            TheDarkMasterEcType.PostDrawInInventoryFunc(spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }
        public override void HoldItem(Item item, Player player) => TheDarkMasterEcType.HoldItemFunc(player);
        public override bool? On_CanUseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] <= 0;
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
