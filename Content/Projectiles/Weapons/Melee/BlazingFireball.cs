using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class BlazingFireball : ModProjectile, ICWRLoader, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "AegisBeam";
        public static Asset<Texture2D> effectAsset;
        private Trail Trail;
        void ICWRLoader.LoadAsset() => effectAsset = CWRUtils.GetT2DAsset("CalamityMod/ExtraTextures/GreyscaleGradients/EternityStreak");
        void ICWRLoader.UnLoadData() => effectAsset = null;
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.MaxUpdates = 2;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 20;
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
            dust.velocity = velocity.RotatedByRandom(0.4f) * -0.5f;
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

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos.Length != 20) {
                return;
            }
            Vector2[] newPoss = new Vector2[Projectile.oldPos.Length];
            Vector2 norlVer = Projectile.velocity.UnitVector();
            for (int i = 0; i < newPoss.Length; i++) {
                newPoss[i] = Projectile.oldPos[i] + Projectile.Size / 2 + norlVer * 26;
                if (newPoss[i] == Vector2.Zero || newPoss[i] == default || newPoss[i].DistanceSQ(Projectile.Center) > 111180) {
                    newPoss[i] = Projectile.Center + norlVer * 26;
                }
            }
            Trail ??= new Trail(newPoss, (float completionRatio) => Projectile.scale * 30f, (Vector2 _) => Color.Gold * Projectile.Opacity);
            Trail.TrailPositions = newPoss;

            Effect effect = Filters.Scene["CWRMod:gradientTrail"].GetShader().Shader;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * -0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRAsset.LightShotAlt.Value);
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "AegisBlade_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
