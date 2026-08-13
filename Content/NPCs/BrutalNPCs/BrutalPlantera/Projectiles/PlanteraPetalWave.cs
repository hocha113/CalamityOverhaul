using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>
    /// 凋零绽放花瓣波：整圈扩张的花瓣环，留两道旋转安全缺口。
    /// ai[0]=扩速px/f ai[1]=缺口初始角 ai[2]=缺口转速
    /// </summary>
    internal class PlanteraPetalWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float MaxRadius = 1450f;
        private const float BandHalf = 26f;
        /// <summary>缺口半宽(弧度)</summary>
        private const float GapHalf = 0.42f;

        private float ExpandSpeed => Projectile.ai[0] > 0f ? Projectile.ai[0] : 9f;
        private float Radius => 50f + Age * ExpandSpeed;
        private float Age => 600f - Projectile.timeLeft;
        private float GapNow => Projectile.ai[1] + Age * Projectile.ai[2];

        internal static int GetDamage(NPC boss) => Math.Max((int)(boss.defDamage * 0.4f), 18);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Age <= 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
            }

            if (Radius >= MaxRadius) {
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.GlowMagenta.ToVector3() * 0.5f);

            //环沿掉瓣(客户端，稀疏)
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                //缺口里不掉瓣，读得出门在哪
                if (!InGap(angle)) {
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Radius;
                    if (PlanteraRenderHelper.OnScreen(pos, 60f)) {
                        InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraPetal>(pos,
                            angle.ToRotationVector2() * ExpandSpeed * 0.4f,
                            PlanteraRenderHelper.FleshCrimson, Main.rand.NextFloat(0.8f, 1.3f))
                            ?.Configure(40, 1f, PlanteraRenderHelper.GlowMagenta);
                    }
                }
            }
        }

        /// <summary>角度是否落在任一安全缺口内</summary>
        private bool InGap(float worldAngle) {
            float gap1 = GapNow;
            float gap2 = GapNow + MathHelper.Pi;
            return Math.Cos(worldAngle - gap1) > Math.Cos(GapHalf)
                || Math.Cos(worldAngle - gap2) > Math.Cos(GapHalf);
        }

        /// <summary>出生10帧无伤(公平阀)</summary>
        public override bool? CanDamage() => Age < 10f ? false : null;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 target = targetHitbox.Center.ToVector2();
            float dist = Vector2.Distance(Projectile.Center, target);
            //环带判定
            if (Math.Abs(dist - Radius) > BandHalf + Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f) {
                return false;
            }
            //缺口豁免
            float angle = (target - Projectile.Center).ToRotation();
            return !InGap(angle);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.PlanteraBloom?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            //quad 半径已按 0.82 放大补偿，这里进度=命中半径/最大半径，视觉环与判定环重合
            float progress = Radius / MaxRadius;

            if (shader == null || noise == null) {
                DrawFallback();
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            shader.Parameters["uIntensity"]?.SetValue(1f);
            shader.Parameters["uPhase2"]?.SetValue(1f);
            shader.Parameters["uGapOn"]?.SetValue(1f);
            shader.Parameters["uGap1"]?.SetValue(GapNow);
            shader.Parameters["uGap2"]?.SetValue(GapNow + MathHelper.Pi);
            shader.Parameters["uGapCos"]?.SetValue((float)Math.Cos(GapHalf));
            shader.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.211f % 1f);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = VaultAsset.placeholder2.Value;
            //quad 尺寸=最大半径的规格，环由 uProgress 控制
            float size = MaxRadius / 0.82f * 2f;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, quad.Size() / 2f, size / quad.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>回退：圈点花瓣光</summary>
        private void DrawFallback() {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            const int points = 44;
            for (int i = 0; i < points; i++) {
                float angle = MathHelper.TwoPi * i / points;
                if (InGap(angle)) {
                    continue;
                }
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Radius - Main.screenPosition;
                Main.EntitySpriteDraw(glow, pos, null,
                    PlanteraRenderHelper.GlowMagenta with { A = 0 } * 0.7f,
                    0f, glow.Size() / 2f, 0.4f, SpriteEffects.None, 0);
            }
        }
    }
}
