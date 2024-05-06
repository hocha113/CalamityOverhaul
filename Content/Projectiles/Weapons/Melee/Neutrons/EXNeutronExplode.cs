using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using Terraria.GameContent;
using Terraria;
using Terraria.ModLoader;
using Terraria.Audio;
using CalamityOverhaul.Content.CWRDamageTypes;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons
{
    internal class EXNeutronExplode : ModProjectile, IDrawWarp
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 2000;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = EndlessDamageClass.Instance;
        }

        public bool canDraw() => false;

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                SoundEngine.PlaySound(CWRSound.Pecharge);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 133; j++) {
                        CWRParticle spark = new HeavenfallStarParticle(Projectile.Center
                            , vr * (0.1f + j * 0.34f), false, 7, Main.rand.NextFloat(2.2f, 2.3f), Color.BlueViolet);
                        CWRParticleHandler.AddParticle(spark);
                    }
                }
            }
            if (Projectile.ai[2] % 3 == 0) {
                Vector2 rand = CWRUtils.randVr(255);
                for (int i = 0; i < 4; i++) {
                    float rot1 = MathHelper.PiOver2 * i;
                    Vector2 vr = rot1.ToRotationVector2();
                    for (int j = 0; j < 73; j++) {
                        CWRParticle spark = new HeavenfallStarParticle(Projectile.Center + rand
                            , vr * 0.24f, false, 13, Main.rand.NextFloat(0.9f, 1.3f), Color.CadetBlue);
                        CWRParticleHandler.AddParticle(spark);
                    }
                }
            }
            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;
            for (int i = 0; i < 133; i++) {
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, Projectile.ai[0] + i * 115f
                    , drawOrig, Projectile.localAI[0] + i * 0.015f, SpriteEffects.None, 0f);
            }
        }

        public void costomDraw(SpriteBatch spriteBatch) { }
    }
}
