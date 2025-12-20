using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Others
{
    internal class TransferItemProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 120;
            Projectile.alpha = 255; //初始不可见
        }

        public override void AI() {
            Vector2 targetPos = new Vector2(Projectile.ai[1], Projectile.ai[2]);

            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = 1;
                Projectile.alpha = 0;
                //初始向上抛出
                Projectile.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -4f);
            }

            //飞向目标
            Vector2 toTarget = targetPos - Projectile.Center;
            float dist = toTarget.Length();

            if (dist < 16f) {
                Projectile.Kill();
                return;
            }

            //加速飞向目标
            float speed = Math.Min(dist / 5f, 20f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget.SafeNormalize(Vector2.Zero) * speed, 0.1f);

            //旋转效果
            Projectile.rotation += 0.2f;
        }

        public override bool PreDraw(ref Color lightColor) {
            int itemType = (int)Projectile.ai[0];
            if (itemType <= 0) return false;

            Main.instance.LoadItem(itemType);
            Texture2D texture = TextureAssets.Item[itemType].Value;

            if (texture != null) {
                Rectangle rect = Main.itemAnimations[itemType] != null
                    ? Main.itemAnimations[itemType].GetFrame(texture)
                    : texture.Frame();

                Vector2 origin = rect.Size() / 2f;
                float scale = 0.7f;

                Main.EntitySpriteDraw(
                    texture,
                    Projectile.Center - Main.screenPosition,
                    rect,
                    lightColor,
                    Projectile.rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }
    }
}
