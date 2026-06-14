using CalamityOverhaul.Content.RangedModify.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class FocusingGrimoire : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Magic + "FocusingGrimoire";
        [VaultLoaden(CWRConstant.Item_Magic + "FocusingGrimoireGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Magic;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 40;
            Item.height = 44;
            Item.damage = 52;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.mana = 8;
            Item.shoot = ModContent.ProjectileType<PowerCoil>();
            Item.shootSpeed = 8;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.CWR().DeathModeItem = true;
        }

        //右键：发射速射激光
        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<FocusingGrimoireHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<FocusingGrimoireHeld>(player, source);

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class FocusingGrimoireHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "FocusingGrimoire";
        public override Asset<Texture2D> GlowAsset => FocusingGrimoire.Glow;
        public override int TargetID => ModContent.ItemType<FocusingGrimoire>();
        public override bool CanRightClick => true;
        //左键能量线圈与右键速射激光各自的节奏与魔力开销
        private const int CoilUseTime = 18;
        private const int CoilMana = 8;
        private const int LaserUseTime = 5;
        private const int LaserMana = 2;
        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(CanFire);

            if (CanFire) {
                HoldManaRegenDelay();
            }

            if (FireCooldown <= 0) {
                if (WantsFireLeft && PayMana(CoilMana)) {
                    FireCoil();
                    FireCooldown += MathF.Max(CoilUseTime / AttackSpeed, 1f);
                }
                else if (WantsFireRight && PayMana(LaserMana)) {
                    FireLaser();
                    FireCooldown += MathF.Max(LaserUseTime / AttackSpeed, 1f);
                }
            }
            Time++;
        }

        private void FireCoil() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.Item84, Projectile.Center);
            CreateFireLight();
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                    , ModContent.ProjectileType<PowerCoil>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        private void FireLaser() {
            SnapToAimPose();
            SoundEngine.PlaySound(SoundID.Item12, Projectile.Center);
            CreateFireLight();
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile laser = Projectile.NewProjectileDirect(Source, ShootPos, ShootVelocity
                    , ProjectileID.MiniRetinaLaser, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
                laser.DamageType = DamageClass.Magic;
                laser.netUpdate = true;
            }
        }
    }

    internal class PowerCoil : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "PowerCoil";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 6;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.timeLeft = 460;
            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            if (Projectile.timeLeft > 400) {
                return;
            }
            Projectile.rotation += Math.Sign(Projectile.velocity.X) * 0.22f;
            NPC target = Projectile.Center.FindClosestNPC(600);
            if (target != null) {
                Projectile.SmoothHomingBehavior(target.Center, 1, 0.1f);
            }
            Projectile.scale = 1 + Math.Abs(MathF.Sin(Projectile.ai[0] * 0.04f)) * 0.2f;
            Projectile.ai[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.9f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.9f;
            }
            return false;
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
        public override void PostDraw(Color lightColor) => Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 1.15f * Main.essScale);
    }
}
