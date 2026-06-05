using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class Snowblindness : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Snowblindness";
        public override void SetDefaults() {
            Item.damage = 30;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 84;
            Item.height = 34;
            Item.useTime = Item.useAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1.5f;
            Item.value = Terraria.Item.buyPrice(0, 8, 3, 5);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = CWRSound.Gun_Snowblindness_Shoot with { Volume = 0.3f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 28f;
            Item.crit = 10;
            Item.useAmmo = AmmoID.Snowball;
            Item.SetHeldProj<SnowblindnessHeld>();
        }

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<AvalancheM60>().
                AddIngredient(ItemID.LaserRifle).
                AddIngredient(ItemID.FragmentVortex, 6).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class SnowblindnessHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Snowblindness";
        public override int TargetID => ModContent.ItemType<Snowblindness>();
        public override void SetRangedProperty() {
            HandIdleDistanceX = 40;
            HandIdleDistanceY = 10;
            HandFireDistanceX = 40;
            HandFireDistanceY = 2;
            RecoilRetroForceMagnitude = 6;
            RecoilOffsetRecoverValue = 0.6f;
            ShootPosNorlLengValue = -10;
            ShootPosToMouLengValue = 20;
            EnableRecoilRetroEffect = true;
            SpwanGunDustData.dustID1 = 76;
            SpwanGunDustData.dustID2 = 149;
            SpwanGunDustData.dustID3 = 76;
        }
        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f), AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
            Main.projectile[proj].SetAllProjectilesHome(true);
            Main.projectile[proj].CWR().HitAttribute.SuperAttack = true;
            Main.projectile[proj].extraUpdates = 1;
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;
            int bolt = ProjectileID.IceBolt;
            bool isbeam = false;
            if (Main.rand.NextBool(3)) {
                bolt = ProjectileID.FrostBeam;
                isbeam = true;
            }
            int proj2 = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, bolt, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
            Main.projectile[proj2].extraUpdates = 1;
            Main.projectile[proj2].friendly = true;
            Main.projectile[proj2].hostile = false;
            Main.projectile[proj2].DamageType = DamageClass.Ranged;
            if (isbeam) {
                Main.projectile[proj2].damage *= 2;
                Main.projectile[proj2].usesLocalNPCImmunity = true;
                Main.projectile[proj2].localNPCHitCooldown = -1;
                Main.projectile[proj2].ArmorPenetration = 50;
            }
        }
    }
}
