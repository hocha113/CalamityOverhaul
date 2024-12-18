using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    /// <summary>
    /// 沙之下
    /// </summary>
    internal class UnderTheSand : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "UnderTheSand";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 9;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.mana = 6;
            Item.shoot = ModContent.ProjectileType<SaltationSend>();
            Item.shootSpeed = 6;
            Item.UseSound = SoundID.Item20;
            Item.SetHeldProj<UnderTheSandHeld>();
        }
    }

    internal class UnderTheSandHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "UnderTheSand";
        public override int targetCayItem => ModContent.ItemType<UnderTheSand>();
        public override int targetCWRItem => ModContent.ItemType<UnderTheSand>();
        public override void SetMagicProperty() {
            Recoil = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void PreInOwnerUpdate() {
            base.PreInOwnerUpdate();
        }

        public override void FiringShoot() {
            float maxDustNum = 20f;
            for (int i = 0; i < maxDustNum; i++) {
                Vector2 vr = (MathHelper.TwoPi / maxDustNum * i).ToRotationVector2() * 6;
                int dust = Dust.NewDust(GunShootPos, 1, 1, DustID.Gold, vr.X, vr.Y, 0, default(Color), .8f);
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source
                , GunShootPos + CWRUtils.randVr(164) - ShootVelocity.UnitVector() * 80
                , ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
            }
        }
    }

    internal class SaltationSend : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj + "Boss/DesertScourgeSpit";
        bool Moved;
        Vector2 StartVelocity;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 700;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }
        public override void AI() {
            Projectile.ai[1]++;
            if (!Moved && Projectile.ai[1] >= 0) {
                Moved = true;
            }
            if (Projectile.ai[1] == 1) {
                StartVelocity = Projectile.velocity;
            }
            if (Projectile.ai[1] == 2) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                Projectile.velocity = -Projectile.velocity;
            }
            if (Projectile.ai[1] >= 0 && Projectile.ai[1] <= 20) {
                Projectile.velocity *= 0.96f;
            }
            if (Projectile.ai[1] == 20) {
                Projectile.velocity = -Projectile.velocity;
            }
            if (Projectile.ai[1] >= 21 && Projectile.ai[1] <= 60) {
                Projectile.velocity /= 0.90f;

            }
            if (Projectile.ai[1] == 60) {
                Projectile.tileCollide = true;
            }
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            if (Main.rand.NextBool(5)) {
                int dustnumber = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Gold, 0f, 0f, 150, Color.Gold, 1f);
                Main.dust[dustnumber].velocity *= 0.3f;
                Main.dust[dustnumber].noGravity = true;
            }
        }
        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            for (int i = 0; i < 20; i++) {
                int num1 = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Sand, 0f, -2f, 0, default(Color), .8f);
                Main.dust[num1].noGravity = true;
                Main.dust[num1].position.X += Main.rand.Next(-50, 51) * .05f - 1.5f;
                Main.dust[num1].position.Y += Main.rand.Next(-50, 51) * .05f - 1.5f;
                if (Main.dust[num1].position != Projectile.Center)
                    Main.dust[num1].velocity = Projectile.DirectionTo(Main.dust[num1].position) * 6f;
                int num = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Gold, 0f, -2f, 0, default(Color), .8f);
                Main.dust[num].noGravity = true;
                Main.dust[num].position.X += Main.rand.Next(-50, 51) * .05f - 1.5f;
                Main.dust[num].position.Y += Main.rand.Next(-50, 51) * .05f - 1.5f;
                if (Main.dust[num].position != Projectile.Center)
                    Main.dust[num].velocity = Projectile.DirectionTo(Main.dust[num].position) * 6f;
            }
        }
        public override Color? GetAlpha(Color lightColor) {
            return Color.White;
        }
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = CWRUtils.GetRec(texture, Projectile.frame, 4);
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + Projectile.Size / 2;
                Color color = Projectile.GetAlpha(Color.Gold) * (float)(((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
        public override void PostDraw(Color lightColor) {
            Lighting.AddLight(Projectile.Center, Color.PaleGoldenrod.ToVector3() * 1.75f * Main.essScale);
        }
    }
}
