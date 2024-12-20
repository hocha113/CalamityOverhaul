using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityMod.Projectiles.Melee;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AegisBeams : ModProjectile, ICWRLoader
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "AegisBeam";
        public static Asset<Texture2D> effectAsset;
        void ICWRLoader.LoadAsset() => effectAsset = CWRUtils.GetT2DAsset("CalamityMod/ExtraTextures/GreyscaleGradients/EternityStreak");
        void ICWRLoader.UnLoadData() => effectAsset = null;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.MaxUpdates = 2;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.25f, 0.25f, 0f);
            Projectile.rotation += 1f;
            Projectile.alpha = Math.Max(Projectile.alpha - 25, 0);

            if (Projectile.localAI[0] == 0f) {
                SoundEngine.PlaySound(SoundID.Item73, Projectile.position);
                Projectile.localAI[0] = 1f;
            }

            CreateDust(Projectile.position, Projectile.width, Projectile.height, Projectile.velocity);
            if (Projectile.timeLeft < 90) {
                NPC npc = Projectile.Center.FindClosestNPC(250);
                if (npc != null) {
                    float power = Projectile.ai[0] == 1 ? 0.085f : 0.045f;
                    Projectile.SmoothHomingBehavior(npc.Center, 1.001f, power);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(32, SoundID.Item20);
            CreateScatterDust(Projectile.Center, 30);
            if (Projectile.IsOwnedByLocalPlayer()) {
                int flameCount = Main.rand.Next(2, 4);
                for (int i = 0; i < flameCount; i++) {
                    Vector2 velocity = CalamityUtils.RandomVelocity(100f, 70f, 100f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                        , ModContent.ProjectileType<AegisFlame>(), (int)(Projectile.damage * 0.75), 0f, Projectile.owner);
                }
            }
        }

        private void CreateDust(Vector2 position, int width, int height, Vector2 velocity) {
            int dustIndex = Dust.NewDust(position, width, height, DustID.GoldCoin, 0f, 0f, 100, new Color(255, Main.DiscoG, 53), 0.8f);
            Dust dust = Main.dust[dustIndex];
            dust.noGravity = true;
            dust.velocity *= 0.5f;
            dust.velocity += velocity * 0.1f;
        }

        private void CreateScatterDust(Vector2 center, int amount) {
            for (int i = 0; i <= amount; i++) {
                float randX = Main.rand.Next(-10, 11);
                float randY = Main.rand.Next(-10, 11);
                float speed = Main.rand.Next(3, 9);
                float normFactor = speed / (float)Math.Sqrt(randX * randX + randY * randY);
                randX *= normFactor;
                randY *= normFactor;

                int dustIndex = Dust.NewDust(center, 0, 0, DustID.GoldCoin, 0f, 0f, 100, new Color(255, Main.DiscoG, 53), 1.2f);
                Dust dust = Main.dust[dustIndex];
                dust.noGravity = true;
                dust.position = center + new Vector2(Main.rand.Next(-10, 11), Main.rand.Next(-10, 11));
                dust.velocity = new Vector2(randX, randY);
            }
        }

        public float PrimitiveWidthFunction(float completionRatio) => Projectile.scale * 30f;

        public Color PrimitiveColorFunction(float _) => Color.Gold * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, Color.Gold, Color.White, Color.Goldenrod, Color.DarkGoldenrod, Color.Gold);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, Color.Gold, Color.White, Color.Goldenrod, Color.DarkGoldenrod, Color.Gold);

            mainColor = Color.Lerp(Color.White, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.White, secondaryColor, 0.85f);

            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].SetMiscShaderAsset_1(effectAsset);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseColor(mainColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseSecondaryColor(secondaryColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].Apply();
            //非常好的改动，PrimitiveTrail的绘制非常烦杂，使用这种形式会是一个绝佳的选择
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction
                , (float _) => Projectile.Size / 2f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]), 53);
            return true;
        }
    }
}
