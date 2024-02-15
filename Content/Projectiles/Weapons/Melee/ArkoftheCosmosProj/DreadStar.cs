using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.ArkoftheCosmosProj
{
    internal class DreadStar : ModProjectile
    {
        internal PrimitiveTrail TrailDrawer;

        public NPC target;

        private Particle Head;

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => CWRConstant.Placeholder;

        public Player Owner => Main.player[Projectile.owner];

        public ref float Hue => ref Projectile.ai[0];

        public ref float HomingStrenght => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.scale = 0.7f;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
        }

        public int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void OnSpawn(IEntitySource source) {

        }

        public override void AI() {
            ThisTimeValue++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Head == null) {
                Head = new GenericSparkle(Projectile.Center, Vector2.Zero, Color.White, Main.hslToRgb(Hue, 100f, 50f), 1.2f, 2, 0.06f, 3f, needed: true);
                GeneralParticleHandler.SpawnParticle(Head);
            }
            else {
                Head.Position = Projectile.Center + Projectile.velocity * 0.5f;
                Head.Time = 0;
                Head.Scale += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.02f * Projectile.scale;
            }

            if (Status == 0) {
                for (int i = 0; i < 3; i++) {
                    Projectile projectile = Projectile.NewProjectileDirect(
                        Projectile.parent(),
                        Projectile.position,
                        Vector2.Zero,
                        Type,
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner,
                        2,
                        Projectile.whoAmI
                        );
                    projectile.localAI[0] = MathHelper.TwoPi / 3f * i;
                }
                Status = 1;
            }

            if (Status == 1) {
                if (Projectile.timeLeft < 40) {
                    NPC target = Projectile.ProjFindingNPCTarget(600);
                    if (target != null) {
                        Vector2 toTarget = Projectile.Center.To(target.Center);
                        Projectile.EntityToRot(toTarget.ToRotation(), 0.1f);
                        Projectile.velocity = Projectile.rotation.ToRotationVector2() * Projectile.velocity.Length();
                        Projectile.velocity *= 1.001f;
                    }
                }
            }

            if (Status == 2) {
                Projectile ownerProj = CWRUtils.GetProjectileInstance(Behavior);
                if (ownerProj != null) {
                    Vector2 toPos = ownerProj.Center + (MathHelper.ToRadians(Main.GameUpdateCount * 5) + Projectile.localAI[0]).ToRotationVector2() * 16;
                    Projectile.Center = toPos;
                }
                else Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center, 0.75f, 1f, 0.24f);
            if (Main.rand.NextBool(2)) {
                GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f)), 20, Main.rand.NextFloat(0.6f, 1.2f) * Projectile.scale, 0.28f, 0f, glowing: false, 0f, required: true));
                if (Main.rand.NextBool(3)) {
                    GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Main.hslToRgb(Hue, 1f, 0.7f), 15, Main.rand.NextFloat(0.4f, 0.7f) * Projectile.scale, 0.8f, 0f, glowing: true, 0.05f, required: true));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer())
                Projectile.NewProjectileDirect(
                    Projectile.parent(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<SlaughterExplosion>(),
                    Projectile.damage / 2,
                    0,
                    Projectile.owner
                    );
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;
            Color value = Color.Lerp(Main.hslToRgb(Hue, 1f, 0.8f), Color.PaleTurquoise, (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            return Color.Lerp(Color.White, value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 12f * Projectile.scale * Projectile.Opacity, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Status == 1) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);
                Texture2D texture = CWRUtils.GetT2DValue(CWRConstant.Masking + "StarTexture_White");

                for (int i = 0; i < 6; i++) {
                    Main.EntitySpriteDraw(
                        texture,
                        Projectile.Center - Main.screenPosition,
                        null,
                        Color.Red,
                        Projectile.rotation + MathHelper.ToRadians(ThisTimeValue),
                        CWRUtils.GetOrig(texture),
                        Projectile.scale,
                        SpriteEffects.None,
                        0
                        );
                }

                Main.spriteBatch.ResetBlendState();
            }
            if (Status == 2) {
                if (TrailDrawer == null) {
                    TrailDrawer = new PrimitiveTrail(WidthFunction, ColorFunction, null, GameShaders.Misc["CalamityMod:TrailStreak"]);
                }

                GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
                TrailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 30);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);
                Texture2D texture = CWRUtils.GetT2DValue(CWRConstant.Masking + "StarTexture_White");

                for (int i = 0; i < 6; i++) {
                    Main.EntitySpriteDraw(
                        texture,
                        Projectile.Center - Main.screenPosition,
                        null,
                        Color.DarkRed,
                        Projectile.rotation,
                        CWRUtils.GetOrig(texture),
                        Projectile.scale,
                        SpriteEffects.None,
                        0
                        );
                }

                Main.spriteBatch.ResetBlendState();
            }
            return false;
        }
    }
}
