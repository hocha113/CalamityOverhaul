using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 压缩水弹：尾扇齐射用。原版水矢贴图作本体（有遮挡剪影），
    /// 拖尾用同素材缩放重绘（同材质、横径≥本体一半），前段直飞后段带轻微下坠
    /// </summary>
    internal class SeaShrimpWaterBolt : SeaShrimpModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WaterBolt}";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            //前 22 帧直飞（读得准），此后轻微下坠成弧
            if (Projectile.timeLeft < 218) {
                Projectile.velocity.Y += 0.09f;
                if (Projectile.velocity.Y > 14f) {
                    Projectile.velocity.Y = 14f;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.06f, 0.16f, 0.32f);

            if (!Main.dedServ && Main.GameUpdateCount % 4 == 0) {
                PRTLoader.NewParticle<PRT_SHPCCoralBubble>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity * 0.06f, Color.White * 0.55f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //碎裂水花：飞沫在弹体死后继续存在
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f) - Projectile.velocity * 0.12f,
                    Color.Lerp(new Color(90, 150, 235), Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.35f, 0.65f))?.Configure(true, Main.rand.Next(8, 15));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //拖尾：同素材递缩重绘（0.55×/0.35α 量级，契约5）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                float t = i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color col = Color.Lerp(new Color(60, 110, 200), SeaShrimpRenderer.CrystalBlue, 1f - t)
                    * (0.4f * (1f - t));
                Main.spriteBatch.Draw(tex, pos, null, col, Projectile.oldRot[i],
                    origin, MathHelper.Lerp(0.9f, 0.5f, t), SpriteEffects.None, 0f);
            }

            //本体：光照色主体 + 白亮芯
            Vector2 center = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, center, null, lightColor, Projectile.rotation,
                origin, 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, center, null,
                new Color(200, 235, 255, 90) * 0.7f, Projectile.rotation,
                origin, 0.62f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
