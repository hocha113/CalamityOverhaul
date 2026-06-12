using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged.NeutronBows;
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
        /// <summary>右键射击积累的充能，存放在物品实例上以便跨弹幕生命周期保留</summary>
        public float Charge;
        /// <summary>充能打满后的过载状态，期间右键伤害大幅提升且充能持续衰减</summary>
        public bool Overcharged;
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
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1.5f;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12;
            Item.useAmmo = AmmoID.Bullet;
            Item.rare = ItemRarityID.Red;
            Item.DamageType = DamageClass.Ranged;
            Item.value = Item.buyPrice(13, 83, 5, 0);
            Item.crit = 2;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronGun;
        }

        //右键用于发射蓄能重击
        public override bool AltFunctionUse(Player player) => true;

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<NeutronGunHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<NeutronGunHeld>(player, source);
    }

    internal class NeutronGunHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronGun";
        public override int TargetID => ModContent.ItemType<NeutronGun>();
        public override bool CanRightClick => true;
        private NeutronGun GunItem => (NeutronGun)Item.ModItem;
        private float Charge {
            get => GunItem.Charge;
            set => GunItem.Charge = value;
        }
        private bool Overcharged {
            get => GunItem.Overcharged;
            set => GunItem.Overcharged = value;
        }
        private int uiframe;
        private bool rightHolding;
        //过载状态衰减期间枪体不要消失，保持充能条与衰减逻辑可见
        public override bool StayAlive() => Overcharged && Charge > 0;
        public override void SetGunProperty() {
            HandIdleDistanceX = 35;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 35;
            MuzzleForwardOffset = 10;
            MuzzleNormalOffset = -2;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 6);
            VaultUtils.ClockFrame(ref uiframe, 5, 6);

            rightHolding = WantsFireRight && HasAmmo;
            HandIdleDistanceX = HandFireDistanceX = rightHolding ? 65 : 35;

            //过载状态下充能持续衰减
            if (Overcharged && Charge > 0) {
                Charge--;
                if (Charge <= 0) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.6f }, Projectile.Center);
                    Overcharged = false;
                }
            }

            UpdateHeldPose(CanFire);

            if (FireCooldown <= 0 && HasAmmo) {
                if (WantsFireLeft) {
                    FireLeft();
                }
                else if (rightHolding) {
                    FireRight();
                }
            }
            Time++;
        }

        private void FireLeft() {
            //左键速射会清空充能
            Charge = 0;
            Overcharged = false;
            GunPressure = 0.1f;
            ControlForce = 0.03f;
            MuzzleForwardOffset = 10;

            SnapToAimPose();
            SoundEngine.PlaySound(CWRSound.Gun_AWP_Shoot with { Pitch = -0.1f, Volume = 0.25f }, Projectile.Center);
            CreateRecoil();
            CreateFireLight();

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<NeutronBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
            ConsumeAmmo();

            SetFireCooldown();
            if (++fireIndex > 2) {
                FireCooldown += 7;
                fireIndex = 0;
            }
        }

        private void FireRight() {
            GunPressure = 0.16f;
            ControlForce = 0.01f;
            MuzzleForwardOffset = -10;

            SnapToAimPose();
            SoundEngine.PlaySound(CWRSound.Gun_AWP_Shoot with { Pitch = -0.2f, Volume = 0.3f }, Projectile.Center);
            CreateRecoil();
            CreateFireLight();

            if (Projectile.IsOwnedByLocalPlayer()) {
                int newdamage = (int)(WeaponDamage * (Overcharged ? 15.6f : 5.6f));
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<NeutronBullet>(), newdamage, WeaponKnockback, Owner.whoAmI, 1);
            }
            ConsumeAmmo();
            FireCooldown += 40;

            if (!Overcharged) {
                Charge += 10;
            }
            if (Charge >= 80) {
                if (!Overcharged) {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.2f }, Projectile.Center);
                    SoundEngine.PlaySound(CWRSound.Pecharge with { Pitch = -0.2f, Volume = 0.8f }, Projectile.Center);
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), InMousePos
                        , Vector2.Zero, ModContent.ProjectileType<EXNeutronExplosionRanged>(), WeaponDamage, 0, Owner.whoAmI);
                    }
                }
                Overcharged = true;
                Charge = 80;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            NeutronGlaiveHeldAlt.DrawBar(Owner, Charge, uiframe);

            Texture2D setValue = rightHolding ? NeutronGun.ShootGun.Value : TextureValue;
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
