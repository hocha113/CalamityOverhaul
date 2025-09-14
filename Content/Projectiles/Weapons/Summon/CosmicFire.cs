using CalamityMod;
using CalamityMod.Graphics.Metaballs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Summon
{
    internal class CosmicFire : ModProjectile
    {
        public bool ableToHit = true;
        public NPC target;
        public override string Texture => "CalamityMod/Projectiles/InvisibleProj";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.MinionShot[Projectile.type] = true;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 3;
            Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override bool? CanDamage() {
            return Projectile.timeLeft > 150 ? false : null;
        }

        public override void AI() {
            target = Projectile.Center.FindClosestNPC(1600);
            if (target != null && Projectile.timeLeft <= 150) {
                if (Projectile.timeLeft == 150)
                    Projectile.velocity = Projectile.Center.To(target.Center).UnitVector() * 3;
                Projectile.SmoothHomingBehavior(target.Center, 1.01f, 0.01f);
            }
            for (int i = 0; i < 6; i++) {
                StreamGougeMetaball.SpawnParticle(Projectile.Center + VaultUtils.RandVr(13), Projectile.velocity, Main.rand.NextFloat(11.3f, 21.5f));
            }
        }

        public void FindTarget(Player player) {
            float num = 3000f;
            bool flag = false;
            if (player.HasMinionAttackTargetNPC) {
                NPC nPC = Main.npc[player.MinionAttackTargetNPC];
                if (nPC.CanBeChasedBy(Projectile)) {
                    float num2 = Vector2.Distance(nPC.Center, Projectile.Center);
                    if (num2 < num) {
                        num = num2;
                        flag = true;
                        target = nPC;
                    }
                }
            }

            if (!flag) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC nPC2 = Main.npc[i];
                    if (nPC2.CanBeChasedBy(Projectile)) {
                        float num3 = Vector2.Distance(nPC2.Center, Projectile.Center);
                        if (num3 < num) {
                            num = num3;
                            flag = true;
                            target = nPC2;
                        }
                    }
                }
            }

            if (!flag) {
                Projectile.velocity *= 0.98f;
            }
            else {
                KillTheThing(target);
            }
        }

        public void KillTheThing(NPC npc) {
            Projectile.velocity = Projectile.SuperhomeTowardsTarget(npc, 50f / (Projectile.extraUpdates + 1), 60f / (Projectile.extraUpdates + 1), 1f / (Projectile.extraUpdates + 1));
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
