using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Particles;
using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.CWRDamageTypes;
using CalamityOverhaul.Common.Effects;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class ParadiseArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public PrimitiveTrail PierceDrawer = null;

        Color chromaColor => CWRUtils.MultiStepColorLerp(Projectile.ai[0] % 35 / 35f, HeavenfallLongbow.rainbowColors);

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
            //PierceDrawer ??= new(PrimitiveWidthFunction, PrimitiveColorFunction, null, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]);
            MiscShaderData flowColorShader = EffectsRegistry.FlowColorShader;
            PierceDrawer ??= new(PrimitiveWidthFunction, PrimitiveColorFunction, null, flowColorShader);

            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, HeavenfallLongbow.rainbowColors);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, HeavenfallLongbow.rainbowColors);

            mainColor = Color.Lerp(Color.White, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.White, secondaryColor, 0.85f);

            Vector2 trailOffset = Projectile.Size * 0.5f - Main.screenPosition;
            flowColorShader.SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/EternityStreak"));
            flowColorShader.UseImage2("Images/Extra_189");
            flowColorShader.UseColor(mainColor);
            flowColorShader.UseSecondaryColor(secondaryColor);
            flowColorShader.Apply();
            PierceDrawer.Draw(Projectile.oldPos, trailOffset, 53);

            Vector2 scale = new Vector2(0.5f, 1.6f) * Projectile.scale;
            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Projectiles/StarProj").Value;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, mainColor, Projectile.rotation, texture.Size() * 0.5f, scale, 0, 0f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, secondaryColor, Projectile.rotation, texture.Size() * 0.5f, scale * new Vector2(0.45f, 1f), 0, 0f);

            return false;
        }
    }
}
