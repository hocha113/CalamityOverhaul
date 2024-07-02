using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj
{
    internal class EarthenBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                Projectile.position.Y += -480;
                for (int i = 0; i < 60; i++) {
                    Vector2 pos = new Vector2(0, i * 16) + Projectile.position;
                    if (CWRUtils.GetTile(CWRUtils.WEPosToTilePos(pos)).HasSolidTile()) {
                        Projectile.position = pos;
                        break;
                    }
                }
            }
            for (int i = 0; i < 13; i++) {
                int dust = Dust.NewDust(Projectile.Center + new Vector2(Main.rand.Next(-33, 33), Main.rand.Next(-33, 33)), 1, 1, DustID.Sand);
                Main.dust[dust].scale = Main.rand.NextFloat(1.1f, 2.6f);
                Main.dust[dust].velocity = new Vector2(Main.rand.NextFloat(-0.1f, 0.1f), Main.rand.NextFloat(-5.1f, -1.6f));
            }
            Projectile.Explode();
            Projectile.Kill();
            Projectile.ai[0]++;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            CWRNpc.MultipleSegmentsLimitDamage(target, ref modifiers);
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.parent(), Projectile.Center, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.Next(-5, -3))
                         , ModContent.ProjectileType<MeleeFossilShard>(), Projectile.damage, 2, Projectile.owner);
            }
        }
    }
}
