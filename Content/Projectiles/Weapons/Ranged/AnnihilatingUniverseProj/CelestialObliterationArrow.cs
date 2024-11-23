using CalamityMod;
using CalamityMod.Graphics.Metaballs;
using CalamityMod.Graphics.Primitives;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.AnnihilatingUniverseProj
{
    internal class CelestialObliterationArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Item + "Ammo/VanquisherArrow";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
        }

        public override void SetDefaults() {
            Projectile.height = 54;
            Projectile.width = 54;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = 13;
            Projectile.timeLeft = 100;
            Projectile.MaxUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            Color color = Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat(0.3f, 0.64f));
            Lighting.AddLight(Projectile.Center, color.ToVector3());
            Projectile.velocity += (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * 0.1f;
        }

        public static void StarRT(Projectile projectile, Entity target) {
            if (Main.netMode != NetmodeID.Server) {
                Color color = Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat(0.3f, 0.64f));
                GeneralParticleHandler.SpawnParticle(new ImpactParticle(Vector2.Lerp(projectile.Center, target.Center, 0.65f), 0.1f, 20, Main.rand.NextFloat(0.4f, 0.5f), color));
                for (int i = 0; i < 20; i++) {
                    Vector2 spawnPosition = target.Center + Main.rand.NextVector2Circular(30f, 30f);
                    StreamGougeMetaball.SpawnParticle(spawnPosition, Main.rand.NextVector2Circular(3f, 3f), 60f);

                    float scale = MathHelper.Lerp(24f, 64f, CalamityUtils.Convert01To010(i / 19f));
                    spawnPosition = target.Center + projectile.velocity.SafeNormalize(Vector2.UnitY) * MathHelper.Lerp(-40f, 90f, i / 19f);
                    Vector2 particleVelocity = projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.23f) * Main.rand.NextFloat(2.5f, 9f);
                    StreamGougeMetaball.SpawnParticle(spawnPosition, particleVelocity, scale);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                StarRT(Projectile, target);
                if (Main.rand.NextBool(3)) {
                    int proj = Projectile.NewProjectile(Projectile.parent(), Projectile.Center + Projectile.Center.To(target.Center) / 2, Vector2.Zero
                    , ModContent.ProjectileType<CelestialDevourer>(), Projectile.damage / 2, 0, Projectile.owner);
                    Main.projectile[proj].scale = 0.3f;
                }
            }
            if (target.type == ModContent.NPCType<SepulcherHead>() || target.type == ModContent.NPCType<SepulcherBody>() || target.type == ModContent.NPCType<SepulcherTail>()) {
                foreach (NPC targetHead in Main.npc) {
                    if (targetHead.type == ModContent.NPCType<SepulcherHead>()) {
                        ModNPC modNPC = targetHead.ModNPC;
                        modNPC.NPC.life = 0;
                        modNPC.NPC.checkDead();
                        modNPC.OnKill();
                        modNPC.HitEffect(hit);
                        modNPC.NPC.active = false;
                    }
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 16; i++) {
                    Vector2 particleSpeed = Projectile.velocity * Main.rand.NextFloat(0.5f, 0.7f);
                    Vector2 pos = Projectile.position + new Vector2(Main.rand.Next(Projectile.width), Main.rand.Next(Projectile.height));
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.3f, 0.7f), Color.Purple, 30, 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }
            }
        }

        public float PrimitiveWidthFunction(float completionRatio) => Projectile.scale * 30f;

        public Color PrimitiveColorFunction(float _) => Color.AliceBlue * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, Color.Blue, Color.White, Color.BlueViolet, Color.CadetBlue, Color.DarkBlue);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, Color.Blue, Color.White, Color.BlueViolet, Color.CadetBlue, Color.DarkBlue);

            mainColor = Color.Lerp(Color.White, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.White, secondaryColor, 0.85f);

            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/EternityStreak"));
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseColor(mainColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseSecondaryColor(secondaryColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].Apply();
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction, (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]), 53);

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPosition, null, Projectile.GetAlpha(Color.White), Projectile.rotation, origin, Projectile.scale, 0, 0f);
            return false;
        }
    }
}
