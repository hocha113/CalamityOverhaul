using CalamityMod;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class OrderbringerBeams2 : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Melee";
        public override string Texture => CWRConstant.Cay_Proj_Melee + "OrderbringerBeam";
        public Color ProjColor;
        public static Color[] richColors = new Color[]{
            new Color(255, 0, 0),
            new Color(0, 255, 0),
            new Color(0, 0, 255),
            new Color(255, 255, 0),
            new Color(255, 0, 255),
            new Color(0, 255, 255),
            new Color(255, 165, 0),
            new Color(128, 0, 128),
            new Color(255, 192, 203),
            new Color(0, 128, 0),
            new Color(128, 128, 0),
            new Color(0, 128, 128),
            new Color(128, 0, 0),
            new Color(139, 69, 19),
            new Color(0, 255, 127),
            new Color(255, 215, 0)
        };
        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.penetrate = 2;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 180;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.scale = 0.7f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            if (Projectile.ai[1] == 0f) {
                ProjColor = CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), richColors);
                Projectile.ai[1] = 1f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3());
            if (!CWRUtils.isServer) {
                BaseParticle energyLeak = new PRK_Light(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(3), Projectile.velocity * 0.25f
                                , Main.rand.NextFloat(0.3f, 0.5f), ProjColor, 50, 1, 1.5f, hueShift: 0.0f);
                DRKLoader.AddParticle(energyLeak);
            }
            CalamityUtils.HomeInOnNPC(Projectile, true, 200f, 18f, 20f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.spriteBatch.SetAdditiveState();
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, ProjColor, Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            Main.spriteBatch.ResetBlendState();
            return false;
        }
    }
}
