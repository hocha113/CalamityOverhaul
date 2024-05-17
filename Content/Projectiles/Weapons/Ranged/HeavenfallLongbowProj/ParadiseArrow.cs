using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Content.CWRDamageTypes;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class ParadiseArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        Color chromaColor => CWRUtils.MultiStepColorLerp(Projectile.ai[0] % 35 / 35f, HeavenfallLongbow.rainbowColors);
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
        }

        public override void SetDefaults() {
            Projectile.height = 24;
            Projectile.width = 24;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7 * Projectile.MaxUpdates;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, chromaColor.ToVector3());
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            NPC target = Projectile.Center.FindClosestNPC(1300);
            if (target != null && Projectile.ai[0] > 30) {
                Projectile.ChasingBehavior2(target.Center, 1, 0.3f);
            }

            if (!CWRUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 vector = Projectile.velocity * 1.05f;
                float slp = Main.rand.NextFloat(0.5f, 0.9f);
                CWRParticleHandler.AddParticle(new HeavenStarParticle(Projectile.Center, vector, Color.White
                    , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors), 0f, new Vector2(0.6f, 1f) * slp
                    , new Vector2(1.5f, 2.7f) * slp, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2, Main.rand.NextFloat(-0.3f, 0.3f)));
            }

            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                float slp = Main.rand.NextFloat(0.5f, 1.2f);
                CWRParticleHandler.AddParticle(new StarPulseRing(target.Center + Main.rand.NextVector2Unit() * Main.rand.Next(13, 330), Vector2.Zero, CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(1), HeavenfallLongbow.rainbowColors), 0.05f * slp, 0.8f * slp, 8));
            }
            Projectile.timeLeft -= 15;
            if (Projectile.timeLeft <= 0)
                Projectile.timeLeft = 0;
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(300);
        }

        public float PrimitiveWidthFunction(float completionRatio) => Projectile.scale * 15f;

        public Color PrimitiveColorFunction(float _) => CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + Projectile.identity * 0.1372f) % 1f, HeavenfallLongbow.rainbowColors) * Projectile.Opacity * (Projectile.timeLeft / 30f);

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
            
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction, (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]), 53);

            Vector2 scale = new Vector2(0.5f, 1.6f) * Projectile.scale;
            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Projectiles/StarProj").Value;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, texture.Size() * 0.5f, scale, 0, 0f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, secondaryColor, Projectile.rotation, texture.Size() * 0.5f, scale * new Vector2(0.45f, 1f), 0, 0f);

            return false;
        }
    }
}
