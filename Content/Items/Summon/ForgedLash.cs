using CalamityMod.Projectiles.BaseProjectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Summon
{
    internal class ForgedLash : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "ForgedLash";
        public override void SetDefaults() {
            Item.width = 16;
            Item.height = 16;
            Item.damage = 32;
            Item.useTime = Item.useAnimation = 10;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2.5f;
            Item.UseSound = SoundID.Item116;
            Item.shootSpeed = 24f;
            Item.shoot = ModContent.ProjectileType<ForgedLashProjectile>();
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 65, 0);
            Item.CWR().DeathModeItem = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI
                , 0f, (Main.rand.NextFloat() - MathHelper.PiOver4) * MathHelper.PiOver4);
            return false;
        }

        public override bool MeleePrefix() => true;
    }

    internal class ForgedLashProjectile : BaseWhipProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "ForgedLashProjectile";
        public override int ExudeDustType => DustID.RedTorch;
        public override int WhipDustType => DustID.RedTorch;
        public override int HandleHeight => 40;
        public override int BodyType1StartY => 42;
        public override int BodyType1SectionHeight => 30;
        public override int BodyType2StartY => 74;
        public override int BodyType2SectionHeight => 30;
        public override int TailStartY => 116;
        public override int TailHeight => 20;
        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
            Projectile.extraUpdates = 1;
        }
    }
}
