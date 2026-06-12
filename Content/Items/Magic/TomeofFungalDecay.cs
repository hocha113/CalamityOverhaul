using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 腐菌秘典
    /// </summary>
    internal class TomeofFungalDecay : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "TomeofFungalDecay";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 20;
            Item.DamageType = DamageClass.Magic;
            Item.useAnimation = Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.rare = ItemRarityID.Orange;
            Item.value = 600;
            Item.mana = 12;
            Item.shootSpeed = 12;
            Item.shoot = ModContent.ProjectileType<SporeBoboMagic>();
            Item.UseSound = null;//开火音效由手持弹幕负责
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<TomeofFungalDecayHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<TomeofFungalDecayHeld>(player, source);
    }

    internal class TomeofFungalDecayHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "TomeofFungalDecay";
        public override int TargetID => ModContent.ItemType<TomeofFungalDecay>();
        public override SoundStyle? ShootSound => CWRSound.SporeBubble;
        //每轮点射的发数与节奏
        private const int BurstCount = 3;
        private const int ShotInterval = 10;
        private const int BurstCooldown = 60;
        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            Onehanded = true;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            //点射进行中书页震颤
            if (WantsFireLeft && fireIndex != 0) {
                RecoilOffset = Vector2.Lerp(RecoilOffset, VaultUtils.RandVr(16f), 0.2f);
            }
            else {
                RecoilOffset = Vector2.Zero;
            }

            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (WantsFireLeft && FireCooldown <= 0 && PayMana()) {
                Fire();
            }
            Time++;
        }

        private void Fire() {
            //每轮点射开始时播放一次音效
            if (fireIndex == 0) {
                PlayShootSound();
            }

            SnapToAimPose();

            if (Projectile.IsOwnedByLocalPlayer()) {
                //向八个方向喷出孢子泡泡，各自奔向远处的标记点
                Vector2 velocity = ShootVelocity;
                for (int i = 0; i < 8; i++) {
                    velocity = velocity.RotatedBy(MathHelper.TwoPi / 8f * i);
                    Vector2 targetPos = ShootPos + velocity * 300;
                    Projectile.NewProjectile(Source, ShootPos, velocity
                        , ModContent.ProjectileType<SporeBoboMagic>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, targetPos.X, targetPos.Y);
                }
            }

            //打满一轮后进入较长的吟唱冷却
            if (++fireIndex >= BurstCount) {
                fireIndex = 0;
                FireCooldown = BurstCooldown;
            }
            else {
                FireCooldown = ShotInterval;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            //吟唱蓄势时的发光胀大
            if (fireIndex == 0 && WantsFireLeft && FireCooldown > 0) {
                float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
                Color color = Color.BlueViolet;
                color.A = 0;
                float slp = 1 + 0.2f * (BurstCooldown - FireCooldown) / BurstCooldown;
                Main.EntitySpriteDraw(TextureValue, drawPos, null, color
                    , Projectile.rotation + offsetRot, TextureValue.Size() / 2, Projectile.scale * slp
                    , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
            base.GunDraw(drawPos, ref lightColor);
        }
    }

    internal class SporeBoboMagic : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.extraUpdates = 13;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (++Projectile.ai[2] > 30) {
                Projectile.SmoothHomingBehavior(new Vector2(Projectile.ai[0], Projectile.ai[1]), 1, 0.1f);
            }
            Projectile.velocity *= 0.96f;

            if (Projectile.ai[2] > 2 && Main.rand.NextBool(2) && Projectile.velocity.Length() > 1f) {
                PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center + VaultUtils.RandVr(32), Projectile.velocity / 3);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 30);
            Projectile.damage = (int)(Projectile.damage * 0.9f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * 0.9f;
            return false;
        }
    }
}
