using InnoVault.PRT;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;

namespace CalamityOverhaul.Content
{
    internal class CWRDust
    {
        public static void SpawnGunDust(Projectile projectile, Vector2 pos, Vector2 velocity, int splNum = 1) {
            if (Main.myPlayer != projectile.owner) return;

            pos += velocity.SafeNormalize(Vector2.Zero) * projectile.width * projectile.scale * 0.71f;
            for (int i = 0; i < 30 * splNum; i++) {
                int dustID;
                switch (Main.rand.Next(6)) {
                    case 0:
                        dustID = 262;
                        break;
                    case 1:
                    case 2:
                        dustID = 54;
                        break;
                    default:
                        dustID = 53;
                        break;
                }
                float num = Main.rand.NextFloat(3f, 13f) * splNum;
                float angleRandom = 0.06f;
                Vector2 dustVel = new Vector2(num, 0f).RotatedBy((double)velocity.ToRotation(), default);
                dustVel = dustVel.RotatedBy(0f - angleRandom);
                dustVel = dustVel.RotatedByRandom(2f * angleRandom);
                if (Main.rand.NextBool(4)) {
                    dustVel = Vector2.Lerp(dustVel, -Vector2.UnitY * dustVel.Length(), Main.rand.NextFloat(0.6f, 0.85f)) * 0.9f;
                }
                float scale = Main.rand.NextFloat(0.5f, 1.5f);
                int idx = Dust.NewDust(pos, 1, 1, dustID, dustVel.X, dustVel.Y, 0, default, scale);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].position = pos;
            }
        }


        public static void BlastingSputteringDust(Projectile Projectile, int dustID1, int dustID2, int dustID3, int dustID4, int dustID5) {
            for (int i = 0; i < 40; i++) {
                int idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID1, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= 3f;
                if (Main.rand.NextBool()) {
                    Main.dust[idx].scale = 0.5f;
                    Main.dust[idx].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                }
            }
            for (int i = 0; i < 70; i++) {
                int idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID2, 0f, 0f, 100, default, 3f);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].velocity *= 5f;
                idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID3, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= 2f;
            }
            Vector2 ver = Projectile.velocity * -1;
            for (int i = 0; i < 70; i++) {
                int idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID4, 0f, 0f, 100, default, 3f);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].velocity = ver.RotatedByRandom(0.2f) * Main.rand.NextFloat(0.2f, 3.6f);
                idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustID5, 0f, 0f, 100, default, 2f);
                Main.dust[idx].velocity *= ver.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.2f, 1.6f);
            }
        }

        public static void DrawParticleElectricity(BasePRT particle, Vector2 point1, Vector2 point2
            , float scale = 1, int armLength = 30, float density = 0.05f, float ai0 = 0) {
            int nodeCount = (int)Vector2.Distance(point1, point2) / armLength;
            Vector2[] nodes = new Vector2[nodeCount + 1];

            nodes[nodeCount] = point2;

            for (int k = 1; k < nodes.Length; k++) {
                nodes[k] = Vector2.Lerp(point1, point2, k / (float)nodeCount) +
                    (k == nodes.Length - 1 ? Vector2.Zero : Vector2.Normalize(point1 - point2)
                    .RotatedBy(1.58f) * Main.rand.NextFloat(-armLength / 2, armLength / 2));
                Vector2 prevPos = k == 1 ? point1 : nodes[k - 1];
                for (float i = 0; i < 1; i += density) {
                    float size = MathHelper.Lerp(scale, 0f, (float)k / nodes.Length);
                    particle.Position = Vector2.Lerp(prevPos, nodes[k], i);
                    particle.Velocity = Vector2.Zero;
                    particle.Color = Color.White;
                    particle.Scale = size;
                    particle.ai[0] = ai0;
                    PRTLoader.AddParticle(particle);
                }
            }
        }

        public static void StramDustAI(Dust dust) {
            if (dust.customData is null) {
                dust.position -= Vector2.One * 32 * dust.scale;
                dust.customData = true;
            }

            Vector2 currentCenter = dust.position + Vector2.One.RotatedBy(dust.rotation) * 32 * dust.scale;
            dust.scale *= 0.95f;
            Vector2 nextCenter = dust.position + Vector2.One.RotatedBy(dust.rotation + 0.06f) * 32 * dust.scale;

            dust.rotation += 0.06f;
            dust.position += currentCenter - nextCenter;
            dust.shader.UseColor(dust.color);
            dust.position += dust.velocity;

            if (!dust.noGravity) {
                dust.velocity.Y += 0.1f;
            }

            dust.velocity *= 0.99f;
            dust.color *= 0.95f;

            if (!dust.noLight) {
                Lighting.AddLight(dust.position, dust.color.ToVector3());
            }

            if (dust.scale < 0.05f) {
                dust.active = false;
            }
        }

        public static void StramDustAI2(Dust dust) {
            if ((float)dust.customData != 0f) {
                dust.position -= new Vector2(4, 64) * dust.scale;
                dust.scale = (float)dust.customData;
                dust.customData = 0f;
            }

            dust.rotation = dust.velocity.ToRotation() + 1.57f;
            dust.position += dust.velocity;

            dust.velocity *= 0.98f;
            dust.color *= 0.97f;

            if (dust.fadeIn <= 2)
                dust.shader.UseColor(Color.Transparent);
            else
                dust.shader.UseColor(dust.color);

            dust.fadeIn++;

            Lighting.AddLight(dust.position, dust.color.ToVector3() * 0.6f);

            if (dust.fadeIn > 60)
                dust.active = false;
        }

        public static void SplashDust(Projectile Projectile, int mode, int dustID1, int dustID2, float speed, Color dustColor, ArmorShaderData shader = null) {
            for (int i = 4; i < mode; i++) {
                Vector2 vector = Projectile.velocity.UnitVector() * speed;
                float oldXPos = vector.X * (30f / i);
                float oldYPos = vector.Y * (30f / i);
                int killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 2, 2
                    , dustID1, vector.X, vector.Y, 100, default, 1.8f);
                Main.dust[killDust].noGravity = true;
                Dust dust2 = Main.dust[killDust];
                dust2.velocity *= 0.5f;
                dust2.color = dustColor;
                if (shader != null) {
                    dust2.shader = shader;
                    dust2.shader.UseColor(dust2.color);
                }
                killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 2, 2
                    , dustID2, vector.X, vector.Y, 100, default, 1.4f);
                dust2 = Main.dust[killDust];
                dust2.velocity *= 0.05f;
                dust2.noGravity = true;
            }
        }

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
