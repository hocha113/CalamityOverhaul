using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using static Humanizer.In;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class GhostSkull : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private float updateVelcterSengs = 1;
        private int Time;

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.scale = 1.5f;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10 * Projectile.MaxUpdates;
        }

        public override void AI() {
            if (Time == 0) {
                SoundEngine.PlaySound(in SoundID.Item117, Projectile.position);
            }

            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2 + MathHelper.Pi;

            if (Projectile.velocity.LengthSquared() < 900) {
                Projectile.velocity *= updateVelcterSengs;
                updateVelcterSengs += 0.0001f;
            }

            if (Time > 60) {
                NPC target = Projectile.Center.FindClosestNPC(1300);
                if (target != null) {
                    Projectile.ChasingBehavior2(target.Center, 1, 0.25f);
                }
            }

            if (!CWRUtils.isServer) {
                Vector2 dustVel = Projectile.velocity;
                Dust dust = Dust.NewDustPerfect(Projectile.Center + (dustVel * 2), 5, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
                Dust dust2 = Dust.NewDustPerfect(Projectile.Center + (dustVel * 2), 6, dustVel, 0, default, 1f);
                dust2.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
                CWRDust.SpanCycleDust(Projectile, dust, dust2);
            }

            Time++;
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
