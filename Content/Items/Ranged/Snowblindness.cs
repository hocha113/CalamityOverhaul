using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
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
            Item.noUseGraphic = true;
            Item.knockBack = 1.5f;
            Item.value = Terraria.Item.buyPrice(0, 8, 3, 5);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = null;//开火音效在HeldProj
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 28f;
            Item.crit = 10;
            Item.useAmmo = AmmoID.Snowball;
        }

        //物品使用本身不消耗雪球，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<SnowblindnessHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<SnowblindnessHeld>(player, source);

        public override void AddRecipes() {
            _ = CreateRecipe().
                AddIngredient<AvalancheM60>().
                AddIngredient(ItemID.LaserRifle).
                AddIngredient(ItemID.FragmentVortex, 6).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class SnowblindnessHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Snowblindness";
        public override int TargetID => ModContent.ItemType<Snowblindness>();
        public override SoundStyle? ShootSound => CWRSound.Gun_Snowblindness_Shoot with { Volume = 0.3f };
        public override void SetGunProperty() {
            HandIdleDistanceX = 40;
            HandIdleDistanceY = 10;
            HandFireDistanceX = 40;
            HandFireDistanceY = 2;
            RecoilRetroForceMagnitude = 6;
            RecoilOffsetRecoverValue = 0.6f;
            MuzzleForwardOffset = 20;
            MuzzleNormalOffset = -10;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            if (WantsFireLeft && FireCooldown <= 0 && HasAmmo) {
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
            SpawnGunFireDust(ShootPos, ShootVelocity, dustID1: 76, dustID2: 149, dustID3: 76);

            if (Projectile.IsOwnedByLocalPlayer()) {
                //追踪型雪球主弹
                Projectile snowball = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity.RotatedByRandom(0.1f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
                snowball.SetAllProjectilesHome(true);
                snowball.CWR().HitAttribute.SuperAttack = true;
                snowball.extraUpdates = 1;
                snowball.usesLocalNPCImmunity = true;
                snowball.localNPCHitCooldown = -1;
                snowball.netUpdate = true;

                //伴生的寒冰射弹，有三分之一概率换成高伤害的霜月射线
                int bolt = ProjectileID.IceBolt;
                bool isBeam = false;
                if (Main.rand.NextBool(3)) {
                    bolt = ProjectileID.FrostBeam;
                    isBeam = true;
                }
                Projectile frost = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity
                    , bolt, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
                frost.extraUpdates = 1;
                frost.friendly = true;
                frost.hostile = false;
                frost.DamageType = DamageClass.Ranged;
                if (isBeam) {
                    frost.damage *= 2;
                    frost.usesLocalNPCImmunity = true;
                    frost.localNPCHitCooldown = -1;
                    frost.ArmorPenetration = 50;
                }
                frost.netUpdate = true;
            }
            ConsumeAmmo();
        }
    }
}
