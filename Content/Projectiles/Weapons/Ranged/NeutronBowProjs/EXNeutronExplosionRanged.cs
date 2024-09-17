using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs
{
    internal class EXNeutronExplosionRanged : ModProjectile, IDrawWarp
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.timeLeft = 30;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 1;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.ArmorPenetration = 80;
        }

        public bool canDraw() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        BasePRT spark = new PRT_HeavenfallStar(Projectile.Center
                            , vr * (0.1f + j * 0.24f), false, 30, Main.rand.NextFloat(1.2f, 2.3f), Color.BlueViolet);
                        PRTLoader.AddParticle(spark);
                    }
                }
                Projectile.ai[2]++;
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 20) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            for (int i = 0; i < 5; i++) {
                Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition
                    , null, warpColor, 0, warpTex.Size() / 2, Projectile.localAI[0], SpriteEffects.None, 0f);
            }
        }

        public void costomDraw(SpriteBatch spriteBatch) { }
    }
}
