using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class BloodVerdict : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private ref float Time => ref Projectile.ai[0];
        private ref float Fuerrs => ref Projectile.ai[1];
        public Vector2 offsetVr;
        public Vector2[] effusionDirection;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 30;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.timeLeft = 120;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                NPC npc = CWRUtils.GetNPCInstance((int)Fuerrs);
                if (npc == null || effusionDirection == null) {
                    Projectile.Kill();
                    return;
                }

                Projectile.Center = npc.position + offsetVr;
                if (Time % 5 == 0 && Projectile.IsOwnedByLocalPlayer()) {
                    int num = Main.player[Projectile.owner].ownedProjectileCounts[Type];
                    int projTime = 35 - num / 10;
                    if (projTime < 5)
                        projTime = 5;

                    for (int i = 0; i < 3; i++) {
                        Vector2 vr = effusionDirection[i];
                        int proj = Projectile.NewProjectile(
                            Projectile.parent(),
                            Projectile.Center,
                            vr,
                            Type,
                            Projectile.damage / 4,
                            Projectile.knockBack,
                            Projectile.owner,
                            ai2: 1
                            );
                        Main.projectile[proj].timeLeft = projTime;
                        Main.projectile[proj].penetrate = 1;
                        Main.projectile[proj].netUpdate = true;
                        Main.projectile[proj].netUpdate2 = true;
                    }
                }
            }
            if (Projectile.ai[2] == 1) {
                Projectile.velocity.Y += 0.5f;
                //for(int i = 0; i < Projectile.oldPos.Length; i++)
                //{
                //    Dust.NewDust(Projectile.oldPos[i], Projectile.width, Projectile.height
                //    , DustID.Blood, Projectile.velocity.X * 0.75f, Projectile.velocity.Y * 0.75f);
                //}
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height
                    , DustID.Blood, Projectile.velocity.X * 0.75f, Projectile.velocity.Y * 0.75f);
            }
            Time++;
        }

        public override bool? CanHitNPC(NPC target) {
            return Main.rand.NextBool(26);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Projectile.ai[2] == 0 ? false : null;
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }
}
