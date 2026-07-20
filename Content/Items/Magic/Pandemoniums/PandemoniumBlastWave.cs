using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>
    /// 爆炸波
    /// </summary>
    internal class PandemoniumBlastWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private ref float ExpandTimer => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            ExpandTimer++;
            float progress = ExpandTimer / 60f;

            Projectile.scale = progress * 15f;
            Projectile.width = Projectile.height = (int)(50 + progress * 600f);

            if (Main.rand.NextBool(1)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = progress * 400f;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * distance;

                Dust d = Dust.NewDustPerfect(pos, DustID.Torch, angle.ToRotationVector2() * 5f,
                    100, Color.OrangeRed, 2.5f);
                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 3.0f * progress, 1.0f * progress, 0.5f * progress);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 360);
            target.AddBuff(BuffID.Ichor, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float progress = ExpandTimer / 60f;

            DrawBlastWaveShader(sb, center, progress);
            return false;
        }

        private void DrawBlastWaveShader(SpriteBatch sb, Vector2 center, float progress) {
            Effect shader = EffectLoader.BrimstoneBlastWave?.Value;
            if (shader == null) return;

            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;
            if (canvas == null || noise == null) return;

            //爆炸波的绘制范围随进度扩大
            float drawRadius = 50f + progress * 600f;
            float drawDiameter = drawRadius;

            float fadeAlpha = 1f - progress;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["ringProgress"]?.SetValue(progress);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["pulseIntensity"]?.SetValue(0.6f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f) * 0.4f);

            shader.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.78f, 0.39f));
            shader.Parameters["midColor"]?.SetValue(new Vector3(1f, 0.39f, 0.2f));
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.78f, 0.2f, 0.12f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(canvas, center, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
