using CalamityMod;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class StellarStrikerBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private bool onhitNPCBool = true;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.MaxUpdates = 5;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
        }

        public override void AI() {
            if (!VaultUtils.isServer) {
                Vector2 dustVel = Projectile.velocity;
                Dust dust = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, 272, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
                dust.noGravity = true;
                Dust dust2 = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, 226, dustVel, 0, default, 1f);
                dust2.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
                dust2.noGravity = true;
                CWRDust.SpanCycleDust(Projectile, dust, dust2);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0 && onhitNPCBool) {
                for (int i = 0; i < 6; i++) {
                    int proj = Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center + CWRUtils.randVr(255), Vector2.Zero, ProjectileID.LunarFlare
                , (int)(Projectile.damage * 0.5), 0, Main.myPlayer, 0f, Main.rand.Next(3));
                    if (proj.WithinBounds(Main.maxProjectiles)) {
                        Main.projectile[proj].DamageType = DamageClass.Melee;
                        Main.projectile[proj].timeLeft = 30;
                    }
                }
                onhitNPCBool = false;
            }
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }
    }
}
