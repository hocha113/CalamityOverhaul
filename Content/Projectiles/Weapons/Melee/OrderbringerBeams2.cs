using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class OrderbringerBeams2 : ModProjectile
    {
        public new string LocalizationCategory => "Projectiles.Melee";
        public override string Texture => CWRConstant.Cay_Proj_Melee + "OrderbringerBeam";
        public Color ProjColor;

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
                ProjColor = CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), IridescentExcalibur.richColors);
                Projectile.ai[1] = 1f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Lighting.AddLight(Projectile.Center, Main.DiscoColor.ToVector3());
            if (!CWRUtils.isServer) {
                CWRParticle energyLeak = new LightParticle(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(3), Projectile.velocity * 0.25f
                                , Main.rand.NextFloat(0.3f, 0.5f), ProjColor, 50, 1, 1.5f, hueShift: 0.0f);
                CWRParticleHandler.AddParticle(energyLeak);
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
