using Terraria.ID;
using Terraria;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content
{
    internal class CWRDust
    {
        public static void SpanCycleDust(Projectile Projectile, int dustID1, int dustID2) {
            for (int i = 0; i < 1; i++) {
                if (Main.rand.NextBool()) {
                    Vector2 vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = Main.dust[Dust.NewDust(Projectile.Center - vector3 * 30f, 0, 0, dustID1)];
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    dust.color = Color.Crimson;
                }
                else {
                    Vector2 vector4 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = Main.dust[Dust.NewDust(Projectile.Center - vector4 * 30f, 0, 0, dustID2)];
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector4 * Main.rand.Next(20, 31);
                    dust.velocity = vector4.RotatedBy(-1.5707963705062866) * 5f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                }
            }
        }

        public static void SpanCycleDust(Projectile Projectile, Dust dust1, Dust dust2) {
            for (int i = 0; i < 1; i++) {
                if (Main.rand.NextBool()) {
                    Vector2 vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = dust1;
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                    dust.color = Color.Crimson;
                }
                else {
                    Vector2 vector4 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = dust2;
                    dust.noGravity = true;
                    dust.position = Projectile.Center - vector4 * Main.rand.Next(20, 31);
                    dust.velocity = vector4.RotatedBy(-1.5707963705062866) * 5f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = Projectile;
                }
            }
        }

        public static void SpanCycleDust(NPC npc, int dustID1, int dustID2) {
            for (int i = 0; i < 1; i++) {
                if (Main.rand.NextBool()) {
                    Vector2 vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = Main.dust[Dust.NewDust(npc.Center - vector3 * 30f, 0, 0, dustID1)];
                    dust.noGravity = true;
                    dust.position = npc.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = npc;
                    vector3 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    dust.noGravity = true;
                    dust.position = npc.Center - vector3 * Main.rand.Next(10, 21);
                    dust.velocity = vector3.RotatedBy(1.5707963705062866) * 6f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = npc;
                    dust.color = Color.Crimson;
                }
                else {
                    Vector2 vector4 = Vector2.UnitY.RotatedByRandom(6.2831854820251465);
                    Dust dust = Main.dust[Dust.NewDust(npc.Center - vector4 * 30f, 0, 0, dustID2)];
                    dust.noGravity = true;
                    dust.position = npc.Center - vector4 * Main.rand.Next(20, 31);
                    dust.velocity = vector4.RotatedBy(-1.5707963705062866) * 5f;
                    dust.scale = 0.9f + Main.rand.NextFloat();
                    dust.fadeIn = 0.5f;
                    dust.customData = npc;
                }
            }
        }
    }
}
