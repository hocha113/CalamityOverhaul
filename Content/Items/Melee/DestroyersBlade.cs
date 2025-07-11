using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DestroyersBlade : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBlade";
        [VaultLoaden(CWRConstant.Item_Melee + "DestroyersBladeGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.width = Item.height = 120;
            Item.damage = 90;
            Item.knockBack = 6;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.useTime = Item.useAnimation = 16;
            Item.DamageType = DamageClass.Melee;
            Item.useTurn = true;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 1, 60, 5);
            Item.shoot = ModContent.ProjectileType<DestroyersBeam>();
            Item.shootSpeed = 15;
            Item.CWR().DeathModeItem = true;
            Item.SetKnifeHeld<DestroyersBladeHeld>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class DestroyersBladeEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        [VaultLoaden(CWRConstant.Item_Melee + "DestroyersBladeEXGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.height = 132;
            Item.width = 134;
            Item.damage = 890;
            Item.knockBack = 8;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.useTime = Item.useAnimation = 12;
            Item.DamageType = DamageClass.Melee;
            Item.useTurn = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 5);
            Item.shoot = ModContent.ProjectileType<DestroyersBeam>();
            Item.shootSpeed = 15;
            Item.SetKnifeHeld<DestroyersBladeEXHeld>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
           , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition, null, Color.White
                , rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<DestroyersBlade>().
                AddIngredient<SoulofMightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class DestroyersBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DestroyersBlade>();
        public override string GlowTexturePath => CWRConstant.Item_Melee + "DestroyersBladeGlow";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 112;
            canDrawSlashTrail = true;
            drawTrailCount = 34;
            distanceToOwner = -20;
            drawTrailTopWidth = 86;
            ownerOrientationLock = true;
            SwingData.starArg = 50;
            SwingData.baseSwingSpeed = 4f;
            Length = 124;
            autoSetShoot = true;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 1.2f
                , phase1SwingSpeed: 8f, phase2SwingSpeed: 4f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0, drawSlash: true);
            return base.PreInOwner();
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ShootID
                , Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
        }
    }

    internal class DestroyersBladeEXHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DestroyersBladeEX>();
        public override string GlowTexturePath => CWRConstant.Item_Melee + "DestroyersBladeEXGlow";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 112;
            canDrawSlashTrail = true;
            drawTrailHighlight = true;
            drawTrailCount = 34;
            distanceToOwner = -20;
            drawTrailTopWidth = 86;
            ownerOrientationLock = true;
            SwingData.starArg = 50;
            SwingData.baseSwingSpeed = 4.65f;
            Length = 124;
            autoSetShoot = true;
        }

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 1.6f
                , phase1SwingSpeed: 8.2f, phase2SwingSpeed: 6f
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0, drawSlash: true);
            return base.PreInOwner();
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ShootID
                , (int)(Projectile.damage * 0.75f), Projectile.knockBack, Owner.whoAmI);
        }
    }

    internal class DestroyersBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "DestroyersBeam";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.extraUpdates = 2;
        }
        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
        }

        public override void OnKill(int timeLeft) {
            CWRUtils.BlastingSputteringDust(Projectile, DustID.LavaMoss, DustID.LavaMoss, DustID.LavaMoss, DustID.LavaMoss, DustID.LavaMoss);
            Projectile.Explode();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle();
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
        public override void PostDraw(Color lightColor) => Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 1.75f * Main.essScale);
    }
}
