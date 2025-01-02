using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.RemakeItems.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class DeathFlySickle : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/DeathFlySickle";
        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.DeathSickle);
            Item.damage = 40;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.shoot = ModContent.ProjectileType<DeathSickleThrowableRogue>();
            Item.CWR().GetMeleePrefix = Item.CWR().GetRangedPrefix = true;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 9;

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            Recipe.Create(Type)
                    .AddIngredient(ItemID.DeathSickle)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
            Recipe.Create(ItemID.DeathSickle)
                    .AddIngredient(Type)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
        }
    }
}
