using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles
{
    /// <summary>蚀土间歇泉；ai[0]=喷发前延迟 ai[1]=高度档 0常规 1高柱；中心锚在地表点</summary>
    internal class EowGeyserProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int EruptRise = 9;
        private const int EruptHold = 22;
        private const int EruptFade = 12;

        private int Delay => (int)Projectile.ai[0];
        private float ColumnHeight => Projectile.ai[1] == 1f ? 360f : 270f;
        private const float ColumnWidth = 66f;

        private int Age => (int)Projectile.localAI[0];
        /// <summary>0未喷→1满柱→回落</summary>
        private float RiseT => MathHelper.Clamp((Age - Delay) / (float)EruptRise, 0f, 1f);
        private float FadeT => MathHelper.Clamp((Age - Delay - EruptRise - EruptHold) / (float)EruptFade, 0f, 1f);
        private bool Erupting => Age >= Delay;

        private Vector2 basePoint;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧锚定地表
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                basePoint = Projectile.Center;
                Projectile.timeLeft = Delay + EruptRise + EruptHold + EruptFade + 4;
            }
            basePoint = basePoint == Vector2.Zero ? Projectile.Center : basePoint;
            Projectile.localAI[0]++;

            if (!Erupting) {
                UpdateOmen();
                return;
            }

            //喷发帧：判定框变竖柱，底边钉在地表
            if (Age == Delay) {
                if (!VaultUtils.isServer) {
                    EowMotionFX.SpawnDirtBurst(basePoint, 1.1f, withSound: false);
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1f, Pitch = 0.15f, MaxInstances = 6 }, basePoint);
                    SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 6 }, basePoint);
                    EowMotionFX.CameraPunch(basePoint, 3f, 9, "EowGeyser", -Vector2.UnitY);
                }
            }

            float coverage = RiseT * (1f - FadeT);
            int hitHeight = Math.Max((int)(ColumnHeight * coverage), 24);
            Projectile.hostile = coverage > 0.25f;
            Vector2 keepBase = basePoint;
            Projectile.Resize((int)(ColumnWidth * 0.8f), hitHeight);
            Projectile.Center = keepBase - new Vector2(0f, hitHeight * 0.5f);

            //柱内粒子(客户端)
            if (!VaultUtils.isServer && FadeT < 1f && EowMotionFX.OnScreen(basePoint)) {
                int per = RiseT < 1f ? 4 : 2;
                for (int i = 0; i < per; i++) {
                    Vector2 dustPos = basePoint + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * ColumnWidth * 0.7f, -2f);
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Dirt, 0, 0, 80, default, Main.rand.NextFloat(1.2f, 2f));
                    dust.velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f),
                        -Main.rand.NextFloat(8f, 15f) * coverage);
                    dust.noGravity = Main.rand.NextBool(3);
                }
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_AcidSplash>(
                        basePoint + new Vector2(Main.rand.NextFloat(-14f, 14f), 0f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(6f, 11f) * coverage),
                        Color.White, Main.rand.NextFloat(0.4f, 0.75f)).Configure(Main.rand.Next(20, 32));
                }
                Lighting.AddLight(basePoint - new Vector2(0, ColumnHeight * 0.4f * coverage),
                    EowMotionFX.AcidGreen.ToVector3() * 0.4f * coverage);
            }
        }

        /// <summary>喷发前地表预兆：汇聚尘+微光</summary>
        private void UpdateOmen() {
            Projectile.hostile = false;
            if (VaultUtils.isServer || !EowMotionFX.OnScreen(basePoint)) {
                return;
            }
            float t = Age / (float)Math.Max(Delay, 1);
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = basePoint + new Vector2(Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(-4f, 4f));
                Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.CorruptGibs, 0, 0, 120, default, Main.rand.NextFloat(0.9f, 1.5f));
                dust.velocity = (basePoint - dustPos).SafeNormalize(Vector2.Zero) * (1.5f + t * 3f) - Vector2.UnitY * 1.2f;
                dust.noGravity = true;
            }
            Lighting.AddLight(basePoint, EowMotionFX.AcidGreen.ToVector3() * (0.2f + 0.4f * t));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (FadeT >= 1f) {
                return false;
            }

            Effect effect = EffectLoader.EowGeyser?.Value;
            Vector2 baseDraw = basePoint - Main.screenPosition;

            if (effect == null) {
                return false; //回退时粒子密度已足够
            }

            //喷发前：地面小预兆盘(独立可读的单泉预警)
            if (!Erupting) {
                DrawOmenDisc(effect, baseDraw);
                return false;
            }

            effect.CurrentTechnique = effect.Techniques["TechColumn"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 83 * 0.131f);
            effect.Parameters["uProgress"]?.SetValue(RiseT);
            effect.Parameters["uFade"]?.SetValue(FadeT);
            effect.Parameters["uAspect"]?.SetValue(ColumnHeight / ColumnWidth);
            effect.Parameters["uDirtColor"]?.SetValue(EowMotionFX.DirtBrown.ToVector3());
            effect.Parameters["uAcidColor"]?.SetValue(EowMotionFX.AcidGreen.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(ColumnWidth * 1.35f / pixel.Width, ColumnHeight / pixel.Height);
            //底边锚地表：origin取贴图底中
            sb.Draw(pixel, baseDraw, null, Color.White, 0f,
                new Vector2(pixel.Width / 2f, pixel.Height), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>喷发前地表预兆盘(TechOmen 小尺寸)</summary>
        private void DrawOmenDisc(Effect effect, Vector2 baseDraw) {
            float chargeT = MathHelper.Clamp(Age / (float)Math.Max(Delay, 1), 0f, 1f);

            effect.CurrentTechnique = effect.Techniques["TechOmen"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 83 * 0.131f);
            effect.Parameters["uProgress"]?.SetValue(chargeT);
            effect.Parameters["uFade"]?.SetValue(0f);
            effect.Parameters["uAspect"]?.SetValue(1f);
            effect.Parameters["uDirtColor"]?.SetValue(EowMotionFX.DirtBrown.ToVector3());
            effect.Parameters["uAcidColor"]?.SetValue(EowMotionFX.AcidGreen.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(150f / pixel.Width, 46f / pixel.Height);
            sb.Draw(pixel, baseDraw, null, Color.White, 0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
