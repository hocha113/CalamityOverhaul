using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
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
            Item.UseSound = SoundID.Item84;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 1, 60, 10);
            Item.SetHeldProj<FocusingGrimoireHeld>();
            Item.CWR().DeathModeItem = true;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class FocusingGrimoireHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Item_Magic + "FocusingGrimoire";
        public override string GlowTexPath => CWRConstant.Item_Magic + "FocusingGrimoireGlow";
        public override int TargetID => ModContent.ItemType<FocusingGrimoire>();
        public override void SetMagicProperty() {
            CanRightClick = true;
            Recoil = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
        }

        public override void SetShootAttribute() {
            if (onFire) {
                return;
            }
            Item.useTime = 5;
            Item.useAnimation = 5;
            Item.UseSound = SoundID.Item12;
            Item.mana = 2;
            AmmoTypes = ProjectileID.MiniRetinaLaser;
        }

        public override void FiringShootR() {
            int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity
                , AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0);
            Main.projectile[proj].DamageType = DamageClass.Magic;
        }

        public override void PostShootEverthing() {
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.UseSound = SoundID.Item84;
            Item.mana = 8;
            AmmoTypes = ModContent.ProjectileType<PowerCoil>();
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
        public override void PostDraw(Color lightColor) => Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 1.15f * Main.essScale);
    }
}
