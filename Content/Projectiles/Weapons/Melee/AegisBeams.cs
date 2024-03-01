using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class AegisBeams : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "AegisBeam";

        public new string LocalizationCategory => "Projectiles.Melee";

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
            Projectile.alpha -= 25;
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }
            if (Projectile.localAI[0] == 0f) {
                _ = SoundEngine.PlaySound(SoundID.Item73, Projectile.position);
                Projectile.localAI[0] += 1f;
            }
            int num458 = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 100, new Color(255, Main.DiscoG, 53), 0.8f);
            Main.dust[num458].noGravity = true;
            Main.dust[num458].velocity *= 0.5f;
            Main.dust[num458].velocity += Projectile.velocity * 0.1f;
            if (Projectile.timeLeft < 90) {
                NPC npc = Projectile.Center.FindClosestNPC(250);
                if (npc != null)
                    Projectile.ChasingBehavior2(npc.Center, 1.001f, 0.045f);
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(32, SoundID.Item20);
            for (int d = 0; d <= 30; d++) {
                float num463 = Main.rand.Next(-10, 11);
                float num464 = Main.rand.Next(-10, 11);
                float speed = Main.rand.Next(3, 9);
                float num466 = (float)Math.Sqrt((double)((num463 * num463) + (num464 * num464)));
                num466 = speed / num466;
                num463 *= num466;
                num464 *= num466;
                int num467 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 100, new Color(255, Main.DiscoG, 53), 1.2f);
                Dust dust = Main.dust[num467];
                dust.noGravity = true;
                dust.position.X = Projectile.Center.X;
                dust.position.Y = Projectile.Center.Y;
                dust.position.X += Main.rand.Next(-10, 11);
                dust.position.Y += Main.rand.Next(-10, 11);
                dust.velocity.X = num463;
                dust.velocity.Y = num464;
            }
            int flameAmt = Main.rand.Next(2, 4);
            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < flameAmt; i++) {
                    Vector2 velocity = CalamityUtils.RandomVelocity(100f, 70f, 100f);
                    _ = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity, ModContent.ProjectileType<AegisFlame>(), (int)(Projectile.damage * 0.75), 0f, Projectile.owner, 0f, 0f);
                }
            }
        }

        public PrimitiveTrail PierceAfterimageDrawer = null;

        public float PrimitiveWidthFunction(float completionRatio) => Projectile.scale * 30f;

        public Color PrimitiveColorFunction(float _) => Color.Gold * Projectile.Opacity;

        public override bool PreDraw(ref Color lightColor) {
            PierceAfterimageDrawer ??= new(PrimitiveWidthFunction, PrimitiveColorFunction, null, GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"]);

            float localIdentityOffset = Projectile.identity * 0.1372f;
            Color mainColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset) % 1f, Color.Gold, Color.White, Color.Goldenrod, Color.DarkGoldenrod, Color.Gold);
            Color secondaryColor = CalamityUtils.MulticolorLerp((Main.GlobalTimeWrappedHourly * 2f + localIdentityOffset + 0.2f) % 1f, Color.Gold, Color.White, Color.Goldenrod, Color.DarkGoldenrod, Color.Gold);

            mainColor = Color.Lerp(Color.White, mainColor, 0.85f);
            secondaryColor = Color.Lerp(Color.White, secondaryColor, 0.85f);

            Vector2 trailOffset = Projectile.Size * 0.5f - Main.screenPosition;
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/EternityStreak"));
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseImage2("Images/Extra_189");
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseColor(mainColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].UseSecondaryColor(secondaryColor);
            GameShaders.Misc["CalamityMod:HeavenlyGaleTrail"].Apply();
            PierceAfterimageDrawer.Draw(Projectile.oldPos, trailOffset, 53);

            return true;
        }
    }
}
