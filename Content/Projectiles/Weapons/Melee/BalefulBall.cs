using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class BalefulBall : ModProjectile
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
            for (int i = 0; i < 33; i++) {
                int dust = Dust.NewDust(Projectile.Center + new Vector2(Main.rand.Next(-33, 33), Main.rand.Next(-33, 33)), 1, 1, Main.rand.NextBool() ? 5 : 6);
                Main.dust[dust].scale = Main.rand.NextFloat(1.1f, 1.6f);
                Main.dust[dust].velocity = new Vector2(Main.rand.NextFloat(-0.1f, 0.1f), Main.rand.NextFloat(-5.1f, -1.6f));
            }
            Projectile.Explode();
            Projectile.Kill();
            Projectile.ai[0]++;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            CWRNpc.MultipleSegmentsLimitDamage(target, ref modifiers);
        }
    }
}
