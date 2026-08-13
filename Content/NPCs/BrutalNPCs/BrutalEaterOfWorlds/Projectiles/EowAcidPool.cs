using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles
{
    /// <summary>地面残留酸池；ai[0]尺寸档 0小 1大；中心即地表点，冒泡渐干</summary>
    internal class EowAcidPool : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeTime = 300;
        private const int DryTime = 70;

        private bool IsBig => Projectile.ai[0] == 1f;
        private float WidthPx => IsBig ? 168f : 104f;
        /// <summary>0新鲜→1干涸(最后DryTime帧渐干)</summary>
        private float DryProgress => MathHelper.Clamp((DryTime - Projectile.timeLeft) / (float)DryTime, 0f, 1f);
        private float LifeT => 1f - Projectile.timeLeft / (float)LifeTime;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 104;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧定尺寸：判定框略窄于视觉，顶边贴地
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Vector2 basePos = Projectile.Center;
                Projectile.Resize((int)(WidthPx * 0.82f), 22);
                Projectile.Center = basePos - new Vector2(0f, 8f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 4 }, basePos);
                }
            }

            //干涸期无伤害
            if (DryProgress > 0.35f) {
                Projectile.hostile = false;
            }

            //冒泡与酸雾(客户端)
            if (!VaultUtils.isServer && EowMotionFX.OnScreen(Projectile.Center)) {
                float freshness = 1f - DryProgress;
                if (Main.rand.NextBool(14) && freshness > 0.2f) {
                    Vector2 bubblePos = Projectile.Center
                        + new Vector2(Main.rand.NextFloat(-0.42f, 0.42f) * WidthPx, 2f);
                    PRTLoader.NewParticle<PRT_ToxicBubble>(bubblePos,
                        -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f), Color.White,
                        Main.rand.NextFloat(0.12f, 0.22f)).Configure(Main.rand.Next(26, 44));
                }
                if (Main.rand.NextBool(26)) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(Projectile.Center - new Vector2(0, 6f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.7f), Color.White,
                        Main.rand.NextFloat(0.35f, 0.6f) * (0.4f + freshness)).Configure(Main.rand.Next(30, 55), 0.45f);
                }
                Lighting.AddLight(Projectile.Center, EowMotionFX.AcidGreen.ToVector3() * 0.35f * freshness);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.EowAcid?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (effect != null) {
                DrawShaderPool(effect, drawPos);
            }
            else {
                DrawSpriteFallback(drawPos);
            }
            return false;
        }

        private void DrawShaderPool(Effect effect, Vector2 drawPos) {
            float heightPx = 46f;

            effect.CurrentTechnique = effect.Techniques["TechPool"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 89 * 0.211f);
            effect.Parameters["uLife"]?.SetValue(DryProgress);
            effect.Parameters["uAspect"]?.SetValue(WidthPx / heightPx);
            effect.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(LifeT * 7f, 0f, 1f));
            effect.Parameters["uColorDeep"]?.SetValue(EowMotionFX.AcidDeep.ToVector3());
            effect.Parameters["uColorBright"]?.SetValue(EowMotionFX.AcidBright.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(WidthPx / pixel.Width, heightPx / pixel.Height);
            //quad中心略沉入地面，上缘为液面
            sb.Draw(pixel, drawPos + new Vector2(0f, 12f), null, Color.White,
                0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>回退：压扁液团双层</summary>
        private void DrawSpriteFallback(Vector2 drawPos) {
            Texture2D tex = CWRAsset.SoftGlow.Value;
            Vector2 origin = tex.Size() / 2f;
            float freshness = 1f - DryProgress;
            float alpha = MathHelper.Clamp(LifeT * 7f, 0f, 1f) * (0.25f + freshness * 0.5f);
            Vector2 scale = new Vector2(WidthPx / tex.Width * 1.1f, 0.16f);
            Main.EntitySpriteDraw(tex, drawPos + new Vector2(0, 6f), null,
                EowMotionFX.AcidDeep with { A = 120 } * alpha, 0f, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos + new Vector2(0, 3f), null,
                EowMotionFX.AcidGreen with { A = 30 } * (alpha * 0.8f), 0f, origin, scale * 0.72f, SpriteEffects.None, 0);
        }
    }
}
