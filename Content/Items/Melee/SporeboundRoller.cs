using CalamityMod;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 牵滚菌
    /// </summary>
    internal class SporeboundRoller : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "SporeboundRoller";
        [VaultLoaden(CWRConstant.Item_Melee + "SporeboundRollerGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetStaticDefaults() {
            ItemID.Sets.Yoyo[Item.type] = true;
            ItemID.Sets.GamepadExtraRange[Item.type] = 8;
        }

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 40;
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.damage = 12;
            Item.knockBack = 2f;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item1;
            Item.channel = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<SporeboundRollerYoyo>();
            Item.shootSpeed = 15f;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 0, 50, 5);
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class SporeboundRollerYoyo : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "SporeboundRollerYoyo";
        [VaultLoaden(CWRConstant.Item_Melee + "SporeboundRollerYoyoGlow")]
        public static Asset<Texture2D> Glow = null;
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<SporeboundRoller>()).DisplayName;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Type] = 20f * 2;
            ProjectileID.Sets.YoyosMaximumRange[Type] = 230f;
            ProjectileID.Sets.YoyosTopSpeed[Type] = 10f;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults() {
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            if (Projectile.Distance(Owner.Center) > 3200f) {
                Projectile.Kill();
            }

            //随机生成一些发光的孢子尘埃，增强视觉效果
            if (Main.rand.NextBool(3)) {
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width
                    , Projectile.height, DustID.BlueFairy, 0f, 0f, 100, default, 1.2f);
                Dust dust = Main.dust[dustIndex];
                dust.velocity *= 0.6f;
                dust.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int mushroomType = ModContent.ProjectileType<Glomushroom>();
            int mushroomDamage = damageDone / 2;
            //给予每个蘑菇一个随机的初始速度，让它们向不同方向散开
            Vector2 velocity = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-1f, -1f));
            Projectile.NewProjectile(Projectile.GetSource_OnHit(target)
                , target.Center, velocity, mushroomType, mushroomDamage, 0, Owner.whoAmI, Main.rand.NextFloat(-0.1f, 0.1f));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor
            , Projectile.rotation, value.GetOrig(), Projectile.scale, SpriteEffects.None, 0);
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Type], lightColor, 1);
            value = Glow.Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White
            , Projectile.rotation, value.GetOrig(), Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    internal class Glomushroom : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Glomushroom";
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180; //弹幕持续3秒
            Projectile.aiStyle = -1; //使用自定义AI
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.tileCollide = false; //不与物块碰撞，可以自由飘浮
        }

        public override void AI() {
            Projectile.velocity *= 0.97f;
            Projectile.velocity.Y += Projectile.ai[0];

            Projectile.velocity.X += (float)Math.Sin(Projectile.timeLeft * 0.04f + Projectile.whoAmI) * 0.15f;

            Projectile.alpha += 2;
            if (Projectile.alpha > 255) {
                Projectile.Kill();
            }

            Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.1f, -0.3f, 0.3f);

            if (Main.rand.NextBool(6)) {
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width,
                    Projectile.height, DustID.BlueFairy, 0f, 0f, 100, default, 1.2f);
                Dust dust = Main.dust[dustIndex];
                dust.velocity *= 0.3f;
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //自定义绘制，让蘑菇弹幕在光照不足的地方也能发光
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 origin = texture.Size() / 2f;
            //弹幕本身的颜色会受到光照和自身透明度的影响
            Color drawColor = Projectile.GetAlpha(lightColor);
            //绘制弹幕主体
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null
                , drawColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            //额外绘制一层白色来模拟发光，该层不受光照影响，只受弹幕自身透明度影响
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null
                , Projectile.GetAlpha(Color.White) * 0.5f, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false; //阻止原版绘制逻辑
        }
    }
}