using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    /// <summary>
    /// 菌泡囊
    /// </summary>
    internal class SporeburstPouch : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "SporeburstPouch";
        public override void SetDefaults() {
            Item.width = Item.height = 22;
            Item.damage = 12;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<SporeburstPouchThrow>();
            Item.shootSpeed = 17f;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(0, 0, 25, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, player.Calamity().StealthStrikeAvailable() ? 1 : 0);
            return false;
        }
    }

    internal class SporeburstPouchThrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "SporeburstPouch";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 6;
            Projectile.timeLeft = 900;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 50;
            Projectile.extraUpdates = 1;
            Projectile.CWR().Viscosity = true;
        }
        public override void AI() {
            //旋转
            Projectile.rotation += 0.3f * Math.Sign(Projectile.velocity.X);
            if (++Projectile.ai[0] > 30) {
                Projectile.velocity.X *= 0.99f;
                Projectile.velocity.Y += 0.1f;
            }
            
            //生成蘑菇尘埃
            if (Main.rand.NextBool(3)) {
                int dustType = Main.rand.NextBool() ? DustID.MushroomTorch : DustID.BlueFairy;
                var dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType);
                dust.scale = 1.2f;
                dust.velocity *= 0.3f;
                dust.noGravity = true;
            }
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //造成剧毒效果
            target.AddBuff(BuffID.Poisoned, 300);
        }
        public override void OnKill(int timeLeft) {
            //爆炸特效
            for (int i = 0; i < 20; i++) {
                int dustType = Main.rand.NextBool() ? DustID.MushroomTorch : DustID.BlueFairy;
                var dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType);
                dust.scale = 1.5f;
                dust.velocity *= 2f;
                dust.noGravity = true;
            }
            //造成范围伤害
            int explosionRadius = 100;//爆炸半径
            Projectile.Explode(explosionRadius);
        }
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                Color color = Color.White * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, texture.Size() / 2, Projectile.scale, SpriteEffects.None);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation , texture.Size() / 2, Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
