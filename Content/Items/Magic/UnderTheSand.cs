using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
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
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 50, 15);
            Item.SetHeldProj<UnderTheSandHeld>();
        }
    }

    internal class UnderTheSandHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "UnderTheSand";
        public override int TargetID => ModContent.ItemType<UnderTheSand>();
        public override void SetMagicProperty() {
            Recoil = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void FiringShoot() {
            float maxDustNum = 20f;
            for (int i = 0; i < maxDustNum; i++) {
                Vector2 vr = (MathHelper.TwoPi / maxDustNum * i).ToRotationVector2() * 6;
                int dust = Dust.NewDust(ShootPos, 1, 1, DustID.Gold, vr.X, vr.Y, 0, default, .8f);
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 2; i++) {
                int proj = Projectile.NewProjectile(Source
                , ShootPos + CWRUtils.randVr(84) - ShootVelocity.UnitVector() * 80
                , ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
            }
        }
    }

    internal class SaltationSend : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj + "Boss/DesertScourgeSpit";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
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
            if (Projectile.ai[1] == 2) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                Projectile.velocity = -Projectile.velocity;
            }
            if (Projectile.ai[1] >= 0 && Projectile.ai[1] <= 20) {
                Projectile.velocity *= 0.9f;
            }
            if (Projectile.ai[1] == 20) {
                Projectile.velocity = -Projectile.velocity;
            }
            if (Projectile.ai[1] >= 21 && Projectile.ai[1] <= 50) {
                Projectile.velocity *= 1.1f;
            }
            if (Projectile.ai[1] == 60) {
                Projectile.tileCollide = true;
            }

            if (!Main.dedServ) {
                VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);
                if (Main.rand.NextBool(5)) {
                    int dustnumber = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Gold, 0f, 0f, 150, Color.Gold, 1f);
                    Main.dust[dustnumber].velocity *= 0.3f;
                    Main.dust[dustnumber].noGravity = true;
                }
            }

            Projectile.ai[1]++;
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();
            CreateDustEffect(DustID.Sand, 20);
            CreateDustEffect(DustID.Gold, 20);
        }

        private void CreateDustEffect(int dustType, int amount) {
            for (int i = 0; i < amount; i++) {
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType, 0f, -2f, 0, default, 0.8f);
                Dust dust = Main.dust[dustIndex];
                dust.noGravity = true;
                dust.position.X += Main.rand.Next(-50, 51) * 0.05f - 1.5f;
                dust.position.Y += Main.rand.Next(-50, 51) * 0.05f - 1.5f;

                if (dust.position != Projectile.Center) {
                    dust.velocity = Projectile.DirectionTo(dust.position) * 6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = texture.GetRectangle(Projectile.frame, 4);
            Vector2 drawOrigin = rectangle.Size() / 2;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, rectangle, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
        public override void PostDraw(Color lightColor) => Lighting.AddLight(Projectile.Center, Color.PaleGoldenrod.ToVector3() * 1.75f * Main.essScale);
    }
}
