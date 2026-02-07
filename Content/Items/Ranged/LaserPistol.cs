using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class LaserPistol : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "LaserPistol";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 22;
            Item.height = 22;
            Item.damage = 16;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.shootSpeed = 10;
            Item.UseSound = SoundID.Item157 with { Pitch = -0.2f };
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 75, 5);
            Item.shoot = ProjectileID.MiniRetinaLaser;
            Item.SetHeldProj<LaserPistolHeld>();
            Item.SetItemUsesCharge(true);
            Item.RefItemMaxCharge() = 40;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 2).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
                return;
            }
            CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 4).
                AddRecipeGroup(RecipeGroupID.IronBar, 2).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class LaserPistolHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "LaserPistol";
        public override int TargetID => ModContent.ItemType<LaserPistol>();
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = -14;
            ShootPosNorlLengValue = -2;
            HandFireDistanceX = 18;
            HandFireDistanceY = -4;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
            Onehanded = true;
            EnableRecoilRetroEffect = true;
            CanCreateSpawnGunDust = false;
            CanCreateCaseEjection = false;
            RecoilRetroForceMagnitude = 6;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override bool CanSpanProj() {
            if (Item.RefItemCharge() <= 0) {
                return false;
            }
            return base.CanSpanProj();
        }

        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].DamageType = DamageClass.Ranged;
            Main.projectile[proj].penetrate = 3;
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;
        }

        public override void PostShootEverthing() {
            Item.RefItemCharge() -= 0.12f;
            Item.RefItemCharge() = MathHelper.Clamp(Item.RefItemCharge(), 0, Item.GetItemMaxCharge());
        }
    }
}
