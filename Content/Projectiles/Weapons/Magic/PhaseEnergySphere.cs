using CalamityMod.Projectiles.Magic;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class PhaseEnergySphere : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Magic + "SHPB";
        public override void SetStaticDefaults() => Main.projFrames[Projectile.type] = 4;
        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.ai[2] = 120;
        }

        public override void AI() {
            float lightIntensity = Main.rand.Next(90, 111) * 0.01f * Main.essScale;
            Lighting.AddLight(Projectile.Center, lightIntensity, 0.2f * lightIntensity, 0.75f * lightIntensity);
            Projectile.alpha = Math.Max(Projectile.alpha - 2, 0);
            if (++Projectile.frameCounter > 4) {
                Projectile.frame = (Projectile.frame + 1) % 4;
                Projectile.frameCounter = 0;
            }

            Projectile.ai[0] = Main.rand.NextFloat(-0.25f, 0.25f);
            Projectile.ai[1] = Main.rand.NextFloat(-0.25f, 0.25f);
            Projectile.scale += Projectile.localAI[0] == 0f ? 0.05f : -0.05f;
            if (Projectile.scale > 1.2f || Projectile.scale < 0.8f) {
                Projectile.localAI[0] = 1f - Projectile.localAI[0];
            }

            Projectile.velocity *= 0.985f;

            // 检测附近的NPC
            bool canExplode = false;

            foreach (var n in Main.ActiveNPCs) {
                if (n.CanBeChasedBy(Projectile) && Collision.CanHit(Projectile.Center, 1, 1, n.Center, 1, 1) 
                    && Vector2.Distance(Projectile.Center, n.Center) < 250f) {
                    canExplode = true; 
                    break;
                }
            }

            // 倒计时爆炸
            if (canExplode && --Projectile.ai[2] <= 0) {
                Projectile.Kill();
            }
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, Main.DiscoG, 155, Projectile.alpha);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            int frameY = frameHeight * Projectile.frame;
            Rectangle frameRect = new Rectangle(0, frameY, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
            Vector2 position = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
            Main.spriteBatch.Draw(texture, position, frameRect, Projectile.GetAlpha(lightColor)
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item105, Projectile.Center);
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center.X, Projectile.Center.Y, 0f, 0f
                    , ModContent.ProjectileType<SHPExplosion>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0f);
            }
        }
    }
}
