using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.NeutronBows;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.RangedModify.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NeutronGun : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronGun";
        public float Charge;
        [VaultLoaden(CWRConstant.Item_Ranged + "NeutronGun2")]
        internal static Asset<Texture2D> ShootGun = null;
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 7));
        }

        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 580;
            Item.useAnimation = Item.useTime = 5;
            Item.knockBack = 1.5f;
            Item.shootSpeed = 12;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ItemRarityID.Red;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.buyPrice(13, 83, 5, 0);
            Item.crit = 2;
            Item.SetHeldProj<NeutronGunHeld>();
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronGun;
        }
    }

    internal class NeutronGunHeld : BaseGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronGun";
        public override int TargetID => ModContent.ItemType<NeutronGun>();
        private float Charge {
            get => ((NeutronGun)Item.ModItem).Charge;
            set => ((NeutronGun)Item.ModItem).Charge = value;
        }
        private int uiframe;
        private bool canattce;
        public override void SetRangedProperty() {
            CanRightClick = true;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<NeutronBullet>();
            HandIdleDistanceX = 35;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 35;
            ShootPosNorlLengValue = -2;
            ShootPosToMouLengValue = 10;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
            CanCreateSpawnGunDust = false;
        }

        public override void PostInOwner() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 6);
            VaultUtils.ClockFrame(ref uiframe, 5, 6);
            HandIdleDistanceX = onFireR ? (HandFireDistanceX = 65) : (HandFireDistanceX = 35);

            if (canattce && Charge > 0) {
                Charge--;
                if (Charge <= 0) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.6f }, Projectile.Center);
                    canattce = false;
                }
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                GunPressure = 0.1f;
                ControlForce = 0.03f;
                ShootPosToMouLengValue = 10;
                Charge = 0;
                canattce = false;
                Item.UseSound = CWRSound.Gun_AWP_Shoot with { Pitch = -0.1f, Volume = 0.25f };
            }
            else if (onFireR) {
                GunPressure = 0.16f;
                ControlForce = 0.01f;
                ShootPosToMouLengValue = -10;
                Item.UseSound = CWRSound.Gun_AWP_Shoot with { Pitch = -0.2f, Volume = 0.3f };
            }
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            _ = UpdateConsumeAmmo();
            if (++fireIndex > 2) {
                ShootCoolingValue += 7;
                fireIndex = 0;
            }
        }

        public override void FiringShootR() {
            int newdamage = (int)(WeaponDamage * (canattce ? 15.6f : 5.6f));
            Projectile.NewProjectile(Source, ShootPos, ShootVelocity, AmmoTypes, newdamage, WeaponKnockback, Owner.whoAmI, 1);
            _ = UpdateConsumeAmmo();
            ShootCoolingValue += 40;
            if (!canattce) {
                Charge += 10;
            }
            if (Charge >= 80) {
                if (!canattce) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, Projectile.Center);
                    SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.2f, Volume = 0.8f }, Projectile.Center);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Main.MouseWorld
                    , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplosionRanged>(), Projectile.damage, 0);
                }
                canattce = true;
                Charge = 80;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            if (Item.Alives() && Item.type == ModContent.ItemType<NeutronGun>()) {
                NeutronGlaiveHeldAlt.DrawBar(Owner, Charge, uiframe);
            }
            Texture2D setValue = TextureValue;
            if (onFireR) {
                setValue = NeutronGun.ShootGun.Value;
            }
            Main.EntitySpriteDraw(setValue, drawPos
                , setValue.GetRectangle(Projectile.frame, 7), lightColor
                , Projectile.rotation, VaultUtils.GetOrig(setValue, 7), Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    internal class NeutronBullet : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.Masking + "Line";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.MaxUpdates = 6;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 160;
        }

        public override void AI() {
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 5;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center
                , Vector2.Zero, ModContent.ProjectileType<NeutronExplosionRanged>(), Projectile.damage, 0);
            for (int i = 0; i < 3; i++) {
                Vector2 randVer = VaultUtils.RandVr(16, 18);
                Projectile.NewProjectile(Projectile.GetSource_FromThis()
                , target.Center + randVer * 10
                , -randVer, ModContent.ProjectileType<NeutronLaser>(), Projectile.damage, 0);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => true;

        public void DrawCustom(SpriteBatch spriteBatch) {
            Main.spriteBatch.Draw(TextureAssets.Projectile[Type].Value, Projectile.Center - Main.screenPosition, null, Color.White with { A = 0 }, Projectile.rotation, TextureAssets.Projectile[Type].Value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
        }

        public void Warp() {
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 80f,
                screenHeight: 80f,
                intensity: 0.3f,
                progress: 1f,
                rotation: Projectile.rotation,
                technique: "GravitationalLens",
                radius: 0.4f
            );
        }
    }
}
