using CalamityMod;
using CalamityMod.Buffs.StatDebuffs;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class GuardianTerraBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 160;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GlacialState>(), 60);
        }

        public override void AI() {
            if (Projectile.timeLeft < 122) {
                NPC target = Projectile.Center.FindClosestNPC(900);
                if (target != null) {
                    Vector2 idealVelocity = Projectile.SafeDirectionTo(target.Center) * (Projectile.velocity.Length() + 6.5f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, idealVelocity, 0.08f);
                }
            }

            if (++Projectile.ai[0] >= 2 && Projectile.Distance(Main.LocalPlayer.Center) < 1200) {
                for (int i = 0; i < 3; i++) {
                    Color color = Color.Blue;
                    int id = DustID.FireworkFountain_Blue;
                    if (i == 1) {
                        color = Color.Green;
                        id = DustID.FireworkFountain_Green;
                    }
                    else if (i == 2) {
                        color = Color.Yellow;
                        id = DustID.FireworkFountain_Yellow;
                    }
                    if (Main.rand.NextBool(6)) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + CWRUtils.randVr(6), id
                        , Projectile.velocity, 56, Main.DiscoColor, Main.rand.NextFloat(0.6f, 1.6f));
                        dust.noGravity = true;
                        dust.color = color;
                    }
                    if (Projectile.ai[1] > 6) {
                        CWRParticle spark = new GuardianTerraStar(Projectile.Center
                            , Projectile.velocity / 10, false, 12, Main.rand.NextFloat(1.2f, 2.3f), color);
                        CWRParticleHandler.AddParticle(spark);
                    }
                }
                Projectile.ai[0] = 0;
            }
            Projectile.ai[1]++;
        }
    }
}
