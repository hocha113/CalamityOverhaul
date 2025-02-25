using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DestroyersBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBlade";
        public override void SetDefaults() {
            Item.width = Item.height = 88;
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
    }

    internal class DestroyersBladeEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        public override void SetDefaults() {
            Item.width = Item.height = 108;
            Item.damage = 890;
            Item.knockBack = 8;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = null;
            Item.useTime = Item.useAnimation = 14;
            Item.DamageType = DamageClass.Melee;
            Item.useTurn = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 5);
            Item.shoot = ModContent.ProjectileType<DestroyersBeam>();
            Item.shootSpeed = 15;
            Item.CWR().DeathModeItem = true;
            Item.SetKnifeHeld<DestroyersBladeEXHeld>();
        }
    }

    internal class DestroyersBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DestroyersBlade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 112;
            canDrawSlashTrail = false;
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
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0, drawSlash: false);
            return base.PreInOwner();
        }

        public override void Shoot() => OrigItemShoot();
    }

    internal class DestroyersBladeEXHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DestroyersBladeEX>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 112;
            canDrawSlashTrail = false;
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
                , phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0, drawSlash: false);
            return base.PreInOwner();
        }

        public override void Shoot() => OrigItemShoot();
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
            CWRDust.BlastingSputteringDust(Projectile, DustID.LavaMoss, DustID.LavaMoss, DustID.LavaMoss, DustID.LavaMoss, DustID.LavaMoss);
            Projectile.Explode();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = CWRUtils.GetRec(texture);
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
