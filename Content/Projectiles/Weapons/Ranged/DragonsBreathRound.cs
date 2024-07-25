using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class DragonsBreathRound : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "DragonsBreathRound";
        public new string LocalizationCategory => "Projectiles.Ranged";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.extraUpdates = 10;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            Projectile.spriteDirection = Projectile.direction = (Projectile.velocity.X > 0).ToDirectionInt();
            Projectile.rotation = Projectile.velocity.ToRotation() + (Projectile.spriteDirection == 1 ? 0f : MathHelper.Pi) + (MathHelper.ToRadians(90) * Projectile.direction);

            Projectile.localAI[0] += 1f;
            if (Projectile.localAI[0] > 4f) {
                float num93 = Projectile.velocity.X / 3f;
                float num94 = Projectile.velocity.Y / 3f;
                int num95 = 4;
                int idx = Dust.NewDust(new Vector2(Projectile.position.X + num95, Projectile.position.Y + num95), Projectile.width - (num95 * 2), Projectile.height - (num95 * 2), DustID.Flare, 0f, 0f, 100, default, 2f);
                Dust dust = Main.dust[idx];
                dust.noGravity = true;
                dust.velocity *= 0.1f;
                dust.velocity += Projectile.velocity * 0.1f;
                dust.position.X -= num93;
                dust.position.Y -= num94;

                if (Main.rand.NextBool(20)) {
                    int num97 = 4;
                    int num98 = Dust.NewDust(new Vector2(Projectile.position.X + num97, Projectile.position.Y + num97), Projectile.width - (num97 * 2), Projectile.height - (num97 * 2), DustID.Flare, 0f, 0f, 100, default, 0.6f);
                    Main.dust[num98].velocity *= 0.25f;
                    Main.dust[num98].velocity += Projectile.velocity * 0.5f;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.owner == Main.myPlayer) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<FuckYou>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0.85f + (Main.rand.NextFloat() * 1.15f));
                if (proj.WithinBounds(Main.maxProjectiles)) {
                    Main.projectile[proj].DamageType = DamageClass.Ranged;
                }
            }
            Projectile.Explode(50);
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 16; i++) {
                    Vector2 particleSpeed = (Main.rand.Next(70, 110) * CWRUtils.atoR).ToRotationVector2() * -Main.rand.Next(11, 17);
                    Vector2 pos = Projectile.position + new Vector2(Main.rand.Next(Projectile.width), Main.rand.Next(Projectile.height));
                    BaseParticle energyLeak = new PRK_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.1f, 0.3f), Color.Red, 30, 1, 1.5f, hueShift: 0.0f);
                    DRKLoader.AddParticle(energyLeak);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Dragonfire>(), 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor);
            return false;
        }
    }
}
