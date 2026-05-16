using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.NeutronBows
{
    internal class EXNeutronExplosionRanged : ModProjectile, IWarpDrawable
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

        public bool CanDrawCustom() => false;

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
            float scale = Math.Max(Projectile.localAI[0], 0.01f);
            NeutronWarpHelper.DrawWarp(
                Projectile.Center,
                screenWidth: 500f * scale,
                screenHeight: 500f * scale,
                intensity: Projectile.ai[1] * 0.8f,
                progress: Projectile.ai[1],
                rotation: Projectile.ai[0],
                technique: "ShockwaveRing",
                radius: 0.48f
            );
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }
}
