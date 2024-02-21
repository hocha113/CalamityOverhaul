using CalamityMod.Projectiles.Ranged;
using CalamityMod;
using Microsoft.Xna.Framework;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.CodeAnalysis;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class PetrifiedDiseaseAorrw : ModProjectile
    {
        public override string Texture => "CalamityMod/Items/Weapons/Rogue/TarragonThrowingDart";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
            Projectile.penetrate = 3;
            Projectile.MaxUpdates = 3;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Lighting.AddLight(Projectile.Center, Color.Green.ToVector3() * 2);
            Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
            , DustID.Stone, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            CWRDust.SpanCycleDust(Projectile, DustID.Sand, DustID.Stone);
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Venom, 600);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Venom, 60);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], Color.Green, 1);
            return false;
        }
    }
}
