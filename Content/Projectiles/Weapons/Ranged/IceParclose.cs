using CalamityMod.NPCs.Cryogen;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class IceParclose : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "IceParclose";
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 38;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            NPC npc = Main.npc[(int)Projectile.ai[0]];
            if (!npc.Alives()) {
                Projectile.Kill();
                return;
            }
            if (npc.type != (int)Projectile.ai[1]) {
                Projectile.Kill();
                return;
            }

            if (!Main.dedServ) {
                Projectile.scale = npc.scale * (npc.height / (float)TextureAssets.Projectile[Type].Value.Height) * 2;
            }

            npc.Center = Projectile.Center;
            npc.rotation = Projectile.ai[2];
            npc.CWR().IceParclose = true;
            npc.CWR().FrozenActivity = true;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(Cryogen.HitSound, Projectile.Center);
            for (int i = 0; i < 10 * Projectile.scale; i++) {
                int index2 = Dust.NewDust(Projectile.Center + CWRUtils.randVr(Projectile.width * Projectile.scale), 1, 1, DustID.BlueCrystalShard, Projectile.velocity.X, Projectile.velocity.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
