using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using System.Buffers.Text;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.Core;
using Terraria.Audio;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;

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
            Item.useAnimation = 60;
            Item.useTime = 2;
            Item.useLimitPerAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.rare = ItemRarityID.Orange;
            Item.value = 600;
            Item.mana = 12;
            Item.shootSpeed = 12;
            Item.shoot = ModContent.ProjectileType<SporeBoboMagic>();
            Item.UseSound = CWRSound.SporeBubble;
            Item.SetHeldProj<TomeofFungalDecayHeld>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            for (int i = 0; i < 8; i++) {
                velocity = velocity.RotatedBy(MathHelper.TwoPi / 8f * i);
                Vector2 targetPos = position + velocity * 300;
                if (Main.dayTime) {
                    damage /= 2;//在白天只造成一半的伤害
                }
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, targetPos.X, targetPos.Y);
            }
            
            return false;
        }
    }

    internal class TomeofFungalDecayHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "TomeofFungalDecay";
        public override int TargetID => ModContent.ItemType<TomeofFungalDecay>();
        private bool OnFire;
        public override void SetMagicProperty() {
            FiringDefaultSound = false;
            CanCreateSpawnGunDust = false;
            Onehanded = true;
            CanCreateCaseEjection = false;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }
        public override void PostInOwner() {
            if (CanFire) {
                return;
            }
            OnFire = true;
        }
        public override bool PreFiringShoot() {
            if (fireIndex == 0 || OnFire) {
                SoundEngine.PlaySound(Item.UseSound, ShootPos);
                OnFire = false;
            }
            if (++fireIndex < 3) {
                Item.useTime = 6;
            }
            else {
                fireIndex = 0;
                Item.useTime = 32;
            }
            return true;
        }
        public override void FiringShoot() {
            OrigItemShoot();
        }
        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
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
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
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
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * 0.9f;
            return false;
        }
    }
}
