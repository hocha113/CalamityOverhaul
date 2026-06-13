using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
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
            Item.UseSound = null;//开火音效由手持弹幕负责
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 75, 5);
            Item.shoot = ProjectileID.MiniRetinaLaser;
            Item.SetItemUsesCharge(true);
            Item.SetItemMaxCharge(40);
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<LaserPistolHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<LaserPistolHeld>(player, source);

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 4).
                AddRecipeGroup(RecipeGroupID.IronBar, 2).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 2).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    internal class LaserPistolHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "LaserPistol";
        public override int TargetID => ModContent.ItemType<LaserPistol>();
        public override SoundStyle? ShootSound => SoundID.Item157 with { Pitch = -0.2f };
        public override void SetGunProperty() {
            MuzzleForwardOffset = -14;
            MuzzleNormalOffset = -2;
            HandFireDistanceX = 18;
            HandFireDistanceY = -4;
            GunPressure = 0;
            ControlForce = 0;
            Onehanded = true;
            AlwaysAimPose = true;
            RecoilRetroForceMagnitude = 6;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            if (WantsFireLeft && FireCooldown <= 0 && Item.GetItemCharge() > 0) {
                Fire();
                SetFireCooldown();
            }
            Time++;
        }

        private void Fire() {
            SnapToAimPose();
            PlayShootSound();
            CreateRecoil();
            CreateFireLight();

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile laser = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                laser.DamageType = DamageClass.Ranged;
                laser.penetrate = 3;
                laser.usesLocalNPCImmunity = true;
                laser.localNPCHitCooldown = -1;
                laser.netUpdate = true;
            }

            //每次射击消耗一些充能
            Item.SetItemCharge(MathHelper.Clamp(Item.GetItemCharge() - 0.12f, 0, Item.GetItemMaxCharge()));
        }
    }
}
