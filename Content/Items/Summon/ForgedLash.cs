using CalamityMod;
using CalamityMod.Projectiles.BaseProjectiles;
using CalamityOverhaul.Content.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
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

    internal class ForgedLashEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Summon + "ForgedLashEX";
        public override void SetDefaults() {
            Item.width = 16;
            Item.height = 16;
            Item.damage = 182;
            Item.useTime = Item.useAnimation = 10;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4.5f;
            Item.UseSound = SoundID.Item116;
            Item.shootSpeed = 24f;
            Item.shoot = ModContent.ProjectileType<ForgedLasEXhProjectile>();
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 65, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI
                , 0f, (Main.rand.NextFloat() - MathHelper.PiOver4) * MathHelper.PiOver4);
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI
                , 0f, (Main.rand.NextFloat() + MathHelper.PiOver4) * MathHelper.PiOver4);
            return false;
        }

        public override bool MeleePrefix() => true;

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<ForgedLash>().
                AddIngredient<SoulofMightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
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
            Projectile.localNPCHitCooldown = -1;
            Projectile.extraUpdates = 1;
        }
    }

    internal class ForgedLasEXhProjectile : BaseWhipProjectile
    {
        public override string Texture => CWRConstant.Projectile_Summon + "ForgedLashEXProjectile";
        public override int ExudeDustType => DustID.RedTorch;
        public override int WhipDustType => DustID.RedTorch;
        public override int HandleHeight => 50;
        public override int BodyType1StartY => 50;
        public override int BodyType1SectionHeight => 34;
        public override int BodyType2StartY => 84;
        public override int BodyType2SectionHeight => 30;
        public override int TailStartY => 120;
        public override int TailHeight => 58;
        private int Time;
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
            Projectile.localNPCHitCooldown = -1;
            Projectile.extraUpdates = 1;
        }

        public override void ExtraBehavior() {
            if (++Time > 6) {
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center + Projectile.velocity * 2, Vector2.Zero
                    , ModContent.ProjectileType<ForgedLasOrb>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
                Time = 0;
            }
        }
    }

    internal class ForgedLasOrb : ModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BTD/Probe";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 120;
            Projectile.friendly = true;
            Projectile.scale = 0.1f;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            if (Projectile.scale < 1f) {
                Projectile.scale += 0.04f;
            }
            Projectile.rotation += 0.4f;
            if (Projectile.timeLeft < 80) {
                CalamityUtils.HomeInOnNPC(Projectile, true, 360, 16, 10);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.Red
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
