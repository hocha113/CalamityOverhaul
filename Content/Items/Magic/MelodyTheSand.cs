using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 沙中曲
    /// </summary>
    internal class MelodyTheSand : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "MelodyTheSand";
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 32;
            Item.height = 32;
            Item.damage = 29;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.mana = 6;
            Item.shoot = ModContent.ProjectileType<SaltationSend>();
            Item.shootSpeed = 6;
            Item.UseSound = SoundID.Item20;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(0, 0, 80, 15);
            Item.SetHeldProj<MelodyTheSandHeld>();
        }
    }

    internal class MelodyTheSandHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "MelodyTheSand";
        public override int TargetID => ModContent.ItemType<MelodyTheSand>();
        private bool oldOnFire;
        private int chargeIndex;
        public override void SetMagicProperty() {
            HandFireDistanceX = 18;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 10;
            GunPressure = 0.3f;
            ControlForce = 0.05f;
            Recoil = 1.2f;
            RangeOfStress = 25;
            RecoilRetroForceMagnitude = 26;
            RecoilOffsetRecoverValue = 0.65f;
            EnableRecoilRetroEffect = false;
            FiringDefaultSound = false;
            CanCreateRecoilBool = false;
            CanCreateSpawnGunDust = CanCreateCaseEjection = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void PostInOwnerUpdate() {
            if (onFire != oldOnFire && onFire) {
                chargeIndex = 0;
            }
            if (onFire) {
                chargeIndex++;
                if (chargeIndex > 11) {
                    RecoilOffsetRecoverValue = 0.65f;
                    OffsetPos += CWRUtils.randVr(0.1f + chargeIndex * 0.05f);
                }
            }
            else {
                chargeIndex = 0;
            }
            oldOnFire = onFire;
        }

        public override void FiringShoot() {
            if (chargeIndex > 60) {
                SoundStyle sound = SoundID.Item20;
                SoundEngine.PlaySound(sound with { Pitch = 0.2f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Vector2 ver = new Vector2(ShootVelocity.X * (0.6f + i * 0.12f), ShootVelocity.Y * Main.rand.NextFloat(0.6f, 1.2f));
                    Projectile.NewProjectile(Source, ShootPos, ver, ModContent.ProjectileType<SandThorn>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, UseAmmoItemType);
                }

                chargeIndex = 0;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Color color = Color.Gold;
            color.A = 0;
            float slp = 1 + 0.004f * chargeIndex;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, color
                , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale * slp
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            base.GunDraw(drawPos, ref lightColor);
        }
    }

    internal class SandThorn : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "SandThorn";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 13;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 700;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.extraUpdates = 2;
        }
        public override void AI() {
            Projectile.velocity.Y += 0.01f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ) {
                if (Main.rand.NextBool(5)) {
                    int dustnumber = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Gold, 0f, 0f, 150, Color.Gold, 1f);
                    Main.dust[dustnumber].velocity *= 0.3f;
                    Main.dust[dustnumber].noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, Color.PaleGoldenrod.ToVector3() * 1.75f * Main.essScale);
            }
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
    }
}
