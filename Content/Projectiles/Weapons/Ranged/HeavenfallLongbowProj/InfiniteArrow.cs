using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.CWRDamageTypes;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class InfiniteArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private Color chromaColor => CWRUtils.MultiStepColorLerp(Projectile.ai[0] % 45 / 45f, HeavenfallLongbow.rainbowColors);
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 30;
        }

        public override void SetDefaults() {
            Projectile.height = 54;
            Projectile.width = 54;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 100;
            Projectile.MaxUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, chromaColor.ToVector3() * 1.5f);
            Projectile.velocity += (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * 0.1f;
            if (Projectile.ai[0] == 0) {
                Projectile.ai[1] = Main.rand.Next(30);
            }
            if (Projectile.ai[0] > 0 && !CWRUtils.isServer) {
                Color outerSparkColor = chromaColor;
                float scaleBoost = MathHelper.Clamp(Projectile.ai[1] * 0.005f, 0f, 2f);
                float outerSparkScale = 3.2f + scaleBoost;
                PRT_HeavenfallStar spark = new PRT_HeavenfallStar(Projectile.Center, Projectile.velocity, false, 7, outerSparkScale, outerSparkColor);
                PRTLoader.AddParticle(spark);

                Color innerSparkColor = CWRUtils.MultiStepColorLerp(Projectile.ai[1] % 30 / 30f, HeavenfallLongbow.rainbowColors);
                float innerSparkScale = 0.6f + scaleBoost;
                PRT_HeavenfallStar spark2 = new PRT_HeavenfallStar(Projectile.Center, Projectile.velocity, false, 7, innerSparkScale, innerSparkColor);
                PRTLoader.AddParticle(spark2);

                //生成彩色的星光粒子
                if (Main.rand.NextBool(2))
                    SpanStarPrt(chromaColor);
            }

            if (Projectile.ai[0] < 3) {
                Projectile.position += Main.player[Projectile.owner].velocity;
            }
            Projectile.ai[0]++;
            Projectile.ai[1]++;
        }

        public void SpanStarPrt(Color color) {
            Vector2 vector = Projectile.velocity * 0.75f;
            float slp = Main.rand.NextFloat(0.5f, 0.9f);
            GeneralParticleHandler.SpawnParticle(new FlareShine(Projectile.Center + Main.rand.NextVector2Unit() * 13, vector, Color.White, color, 0f, new Vector2(0.6f, 1f) * slp, new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
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
            if (Projectile.numHits == 0) {
                int lightningDamage = (int)(Projectile.damage * 1.3f);
                Vector2 ownerPos = Main.player[Projectile.owner].Center;
                Vector2 spanPos = ownerPos + ownerPos.To(target.Center).UnitVector().RotatedBy((120 + Main.rand.Next(120)) * CWRUtils.atoR) * Main.rand.Next(909, 1045);
                Vector2 vr = (target.Center - spanPos + target.velocity * 7.5f).SafeNormalize(Vector2.UnitY) * 17f;
                int lightning = Projectile.NewProjectile(Projectile.GetSource_FromThis(), spanPos, vr, ModContent.ProjectileType<HeavenLightning>(), lightningDamage, 0f, Projectile.owner);
                if (Main.projectile.IndexInRange(lightning)) {
                    Main.projectile[lightning].ai[0] = vr.ToRotation();
                    Main.projectile[lightning].ai[1] = Main.rand.Next(100);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 16; i++) {
                    Vector2 particleSpeed = Projectile.velocity * Main.rand.NextFloat(0.5f, 0.7f);
                    Vector2 pos = Projectile.position + new Vector2(Main.rand.Next(Projectile.width), Main.rand.Next(Projectile.height));
                    BasePRT energyLeak = new PRT_Light(pos, particleSpeed
                        , Main.rand.NextFloat(0.3f, 0.7f), CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(1), HeavenfallLongbow.rainbowColors), 30, 1, 1.5f, hueShift: 0.0f);
                    PRTLoader.AddParticle(energyLeak);
                }

                for (int i = 0; i < 6; i++)
                    GeneralParticleHandler.SpawnParticle(new PulseRing(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.Next(13, 130), Vector2.Zero, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(1), HeavenfallLongbow.rainbowColors), 0.05f, 0.8f, 8));
            }
            Projectile.Explode(spanSound: false);
        }

        public float PrimitiveWidthFunction(float completionRatio) => Projectile.scale * 30f;

        public Color PrimitiveColorFunction(float _) => Color.AliceBlue * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, HeavenfallLongbow.rainbowColors);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, HeavenfallLongbow.rainbowColors);

            mainColor = Color.Lerp(Color.White, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.White, secondaryColor, 0.85f);

            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/EternityStreak"));
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseColor(mainColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseSecondaryColor(secondaryColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].Apply();

            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction
                , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]), 53);
            return false;
        }
    }
}
