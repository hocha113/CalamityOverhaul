using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>孢子雾滞留区：ai[0]=尺寸系数 ai[1]=1时二阶段配色；前12帧无伤(公平阀)</summary>
    internal class PlanteraSporeCloud : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int LifeTime = 150;
        private const int GrowTime = 22;
        private const int DecayTime = 30;

        private float SizeMult => Projectile.ai[0] > 0f ? Projectile.ai[0] : 1f;
        private float Radius => 120f * SizeMult;
        private bool Phase2 => Projectile.ai[1] > 0.5f;
        private float Age => LifeTime - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>前12帧以及消散期无伤</summary>
        public override bool? CanDamage() {
            if (Age < 12 || Projectile.timeLeft < DecayTime / 2) {
                return false;
            }
            return null;
        }

        public override void AI() {
            //缓慢漂移衰减
            Projectile.velocity *= 0.97f;

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.SporeGreen.ToVector3() * 0.4f * SizeMult);

            //雾内零星孢子微光
            if (!VaultUtils.isServer && Main.rand.NextBool(8)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(
                    Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.7f, Radius * 0.7f),
                    new Vector2(0f, -0.3f), PlanteraRenderHelper.SporeGreen * 0.8f,
                    Main.rand.NextFloat(0.5f, 1f))?.SetLife(40);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //圆形判定，略小于视觉
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, closest) < Radius * Radius * 0.72f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.PlanteraSporeFog?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            float birth = MathHelper.Clamp(Age / (float)GrowTime, 0f, 1f);
            float decay = MathHelper.Clamp(1f - Projectile.timeLeft / (float)DecayTime, 0f, 1f);

            if (shader == null || noise == null) {
                DrawFallback(birth, decay);
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uBirth"]?.SetValue(birth);
            shader.Parameters["uDecay"]?.SetValue(decay);
            shader.Parameters["uPhase2"]?.SetValue(Phase2 ? 1f : 0f);
            shader.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.173f % 1f);
            //噪声显式绑到 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，
            //参数式贴图绑定实机失效（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = InnoVault.VaultAsset.placeholder2.Value;
            float size = Radius * 2.6f;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, quad.Size() / 2f, size / quad.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>回退：雾贴图三层错相</summary>
        private void DrawFallback(float birth, float decay) {
            Texture2D fog = CWRAsset.Fog.Value;
            Color tint = (Phase2 ? PlanteraRenderHelper.FleshCrimson : PlanteraRenderHelper.SporeGreen)
                * (0.4f * birth * (1f - decay));
            for (int i = 0; i < 3; i++) {
                float rot = Main.GlobalTimeWrappedHourly * (0.1f + i * 0.06f) + i * 2.1f;
                SpriteEffects flip = i == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Main.EntitySpriteDraw(fog, Projectile.Center - Main.screenPosition, null, tint,
                    rot, fog.Size() / 2f, Radius / 110f * (0.8f + i * 0.18f), flip, 0);
            }
        }
    }
}
