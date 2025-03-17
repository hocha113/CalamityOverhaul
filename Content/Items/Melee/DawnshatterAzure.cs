using CalamityMod.NPCs.Yharon;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DawnshatterAzure : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.height = Item.width = 54;
            Item.damage = 1209;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useTime = Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(6, 23, 75, 0);
            Item.rare = ItemRarityID.Quest;
            Item.shoot = ModContent.ProjectileType<DawnshatterAzureProj>();
            Item.shootSpeed = 8f;
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_DawnshatterAzure;
            Item.CWR().isHeldItem = true;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit += 11;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                _ = Projectile.NewProjectile(source, position, velocity * 2, ModContent.ProjectileType<DawnshatterOut>(), damage * 5, knockback, player.whoAmI);
                return false;
            }
            SoundEngine.PlaySound(Yharon.OrbSound with { Pitch = 0.2f, Volume = 0.7f }, player.Center);
            _ = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterOut>()] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterSwing>()] <= 0;
        }
    }
}
