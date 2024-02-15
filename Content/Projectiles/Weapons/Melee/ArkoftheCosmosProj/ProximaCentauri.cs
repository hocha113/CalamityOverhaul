using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.ArkoftheCosmosProj
{
    /// <summary>
    /// 用作伴飞效果的弹幕
    /// </summary>
    internal class ProximaCentauri : ModProjectile
    {
        internal PrimitiveTrail TrailDrawer;

        public NPC target;

        private Particle Head;

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => CWRConstant.Projectile_Melee + "ProximaCentauri";

        public Player Owner => Main.player[Projectile.owner];

        public ref float Hue => ref Projectile.ai[0];

        public ref float HomingStrenght => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void AI() {
            ThisTimeValue++;
            Projectile OwnerProj = CWRUtils.GetProjectileInstance(Behavior);
            if (OwnerProj == null) {
                Projectile.Kill();
                return;
            }

            if (Head == null) {
                Head = new GenericSparkle(Projectile.Center, Vector2.Zero, Color.White, Main.hslToRgb(Hue, 100f, 50f), 1.2f, 2, 0.06f, 3f, needed: true);
                GeneralParticleHandler.SpawnParticle(Head);
            }
            else {
                Head.Position = Projectile.Center + Projectile.velocity * 0.5f;
                Head.Time = 0;
                Head.Scale += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.02f * Projectile.scale;
            }

            float mode = MathF.Sin(MathHelper.ToRadians(Main.GameUpdateCount * 30)) * 52;
            Vector2 toPos = OwnerProj.Center + OwnerProj.velocity.GetNormalVector() * mode * (Status == 0 ? 1 : -1);
            Projectile.Center = toPos;
            Projectile.rotation = Projectile.oldPos[Projectile.oldPos.Length - 1].To(Projectile.position).ToRotation();

            Lighting.AddLight(Projectile.Center, 0.75f, 1f, 0.24f);
            if (Main.rand.NextBool(2)) {
                GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f)), 20, Main.rand.NextFloat(0.6f, 1.2f) * Projectile.scale, 0.28f, 0f, glowing: false, 0f, required: true));
                if (Main.rand.NextBool(3)) {
                    GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Main.hslToRgb(Hue, 1f, 0.7f), 15, Main.rand.NextFloat(0.4f, 0.7f) * Projectile.scale, 0.8f, 0f, glowing: true, 0.05f, required: true));
                }
            }
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;
            Color value = Color.Lerp(Main.hslToRgb(Hue, 1f, 0), Color.Gold, (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            return Color.Lerp(Color.White, value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 22f * Projectile.scale * Projectile.Opacity, amount);
        }

        public void DrawStar() {
            //Texture2D mainValue = DrawUtils.GetT2DValue(CWRConstant.Masking + "StarTexture_White");
            //Vector2 pos = Projectile.Center - Main.screenPosition;
            //int Time = ThisTimeValue;
            //int slp = Time * 5;
            //if (slp > 255) { slp = 255; }

            //Main.spriteBatch.End();
            //Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            //for (int i = 0; i < 5; i++)
            //{
            //    Main.spriteBatch.Draw(
            //        mainValue,
            //        pos,
            //        null,
            //        Color.Red,
            //        MathHelper.ToRadians(Time * 5 + i * 17),
            //        DrawUtils.GetOrig(mainValue),
            //        (slp / 1755f),
            //        SpriteEffects.None,
            //        0
            //        );
            //}
            //for (int i = 0; i < 5; i++)
            //{
            //    Main.spriteBatch.Draw(
            //        mainValue,
            //        pos,
            //        null,
            //        Color.White,
            //        MathHelper.ToRadians(Time * 6 + i * 17),
            //        DrawUtils.GetOrig(mainValue),
            //        (slp / 2055f),
            //        SpriteEffects.None,
            //        0
            //        );
            //}
            //for (int i = 0; i < 5; i++)
            //{
            //    Main.spriteBatch.Draw(
            //        mainValue,
            //        pos,
            //        null,
            //        Color.Gold,
            //        MathHelper.ToRadians(Time * 9 + i * 17),
            //        DrawUtils.GetOrig(mainValue),
            //        (slp / 2355f),
            //        SpriteEffects.None,
            //        0
            //        );
            //}
            //Main.spriteBatch.ResetBlendState();
        }

        public override bool PreDraw(ref Color lightColor) {
            if (TrailDrawer == null) {
                TrailDrawer = new PrimitiveTrail(WidthFunction, ColorFunction, null, GameShaders.Misc["CalamityMod:TrailStreak"]);
            }

            GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            TrailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 30);
            Texture2D value = ModContent.Request<Texture2D>(Texture).Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.5f), Projectile.rotation + MathHelper.PiOver2, value.Size() / 2f, Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
