using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DeathsAscensionEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DeathsAscension";

        private int swingIndex = 0;
        public override void SetDefaults() {
            Item.SetCalamitySD<DeathsAscension>();
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.UseSound = null;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<DeathsAscensionHeld>();
            Item.CWR().GetAllowPrefix = true;
            Item.CWR().GetMeleePrefix = true;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DeathsAscensionThrowable>()] <= 0;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (++swingIndex > 6) {
                type = ModContent.ProjectileType<DeathsAscensionThrowable>();
                swingIndex = 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, swingIndex % 2 == 0 ? 0 : 1, swingIndex + 1);
            return false;
        }
    }
}
