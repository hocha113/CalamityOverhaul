using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class NeutronGlaive : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "NeutronGlaive";
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            //return false;//暂时不要在这个版本中出现
            return base.IsLoadingEnabled(mod);
        }

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 6));
        }

        public override void SetDefaults() {
            Item.height = 154;
            Item.width = 154;
            Item.damage = 855;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.scale = 1;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item60;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(13, 53, 75, 0);
            Item.rare = ItemRarityID.Red;
            Item.crit = 8;
            Item.shoot = ModContent.ProjectileType<NeutronGlaiveBeam>();
            Item.shootSpeed = 18f;
        }

        public override bool CanUseItem(Player player) {
            Item.noMelee = true;
            Item.noUseGraphic = false;
            Item.UseSound = SoundID.Item60;
            if (player.altFunctionUse == 2) {
                Item.noMelee = false;
                Item.noUseGraphic = true;
                Item.UseSound = SoundID.AbigailAttack;
            }
            return player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGlaiveHeld>()] == 0;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<NeutronGlaiveHeld>(), damage, knockback, player.whoAmI);
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }
}
