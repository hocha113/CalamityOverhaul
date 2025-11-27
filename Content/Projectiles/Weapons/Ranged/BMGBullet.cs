using CalamityMod;
using CalamityMod.Buffs.StatDebuffs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class BMGBullet : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Ranged/AMRShot";

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.light = 0.5f;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 10;
            Projectile.scale = 1.18f;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = ProjAIStyleID.Arrow;
            AIType = ProjectileID.BulletHighVelocity;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.SetProjPointBlankShotDuration(18);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (modifiers.SuperArmor || target.defense > 999 || target.Calamity().DR >= 0.95f || target.Calamity().unbreakableDR)
                return;
            modifiers.DefenseEffectiveness *= 0f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            OnHitEffects(target.Center, hit.Crit);
            target.AddBuff(ModContent.BuffType<MarkedforDeath>(), 300);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            OnHitEffects(target.Center, true);
            target.AddBuff(ModContent.BuffType<MarkedforDeath>(), 300);
        }

        private void OnHitEffects(Vector2 targetPos, bool crit) {
            if (Projectile.numHits == 0) {
                if (Projectile.ai[1] == 0) {
                    for (int i = 0; i < 34; i++) {
                        Vector2 vr = Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(0.6f, 1.7f);
                        Projectile.NewProjectile(Projectile.FromObjectGetParent(), targetPos + Projectile.velocity * -3, vr
                            , ModContent.ProjectileType<BMGFIRE>(), (int)(Projectile.damage * (crit ? 0.35f : 0.2f)), 0, Projectile.owner, Main.rand.Next(23));
                    }
                }
                else {
                    for (int i = 0; i < 24; i++) {
                        Vector2 vr = Projectile.velocity.RotatedBy(Main.rand.NextFloat(-0.2f, 0.2f)) * Main.rand.NextFloat(0.6f, 1.7f);
                        Projectile.NewProjectile(Projectile.FromObjectGetParent(), targetPos + Projectile.velocity * -3, vr
                            , ModContent.ProjectileType<BMGFIRE2>(), (int)(Projectile.damage * (crit ? 0.3f : 0.2f)), 0, Projectile.owner, Main.rand.Next(13));
                    }
                }
            }
        }
    }
}
