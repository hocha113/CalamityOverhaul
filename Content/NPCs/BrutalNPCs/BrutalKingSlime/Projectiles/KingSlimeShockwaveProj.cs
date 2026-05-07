using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 史莱姆王落地皇室冲击波。
    /// <br/>使用 <see cref="EffectLoader.KingSlimeShockwave"/> 着色器渲染，
    /// 沿径向扩散一个皇室凝胶环，最大半径与命中范围由 ai[0] 标量缩放。
    /// </summary>
    internal class KingSlimeShockwaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int MaxLife = 50;
        private ref float ScaleMul => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.idStaticNPCHitCooldown = 12;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (ScaleMul <= 0f) ScaleMul = 1f;

            float t01 = 1f - (float)Projectile.timeLeft / MaxLife;
            //命中半径随扩散增大
            float radius = 80f + 320f * t01 * ScaleMul;
            Projectile.width = Projectile.height = (int)(radius * 2f);
            //保持 Center 不变（用 Hitbox 中心）
            Projectile.position = Projectile.Center - new Vector2(Projectile.width, Projectile.height) * 0.5f;
            Projectile.position.X -= 5;

            Lighting.AddLight(Projectile.Center,
                1.0f * ScaleMul * (1f - t01),
                0.25f * ScaleMul * (1f - t01),
                0.05f * ScaleMul * (1f - t01));

            //扩散粒子点缀
            if (!VaultUtils.isServer && Projectile.timeLeft % 4 == 0) {
                int dustCount = (int)(2 * ScaleMul);
                for (int i = 0; i < dustCount; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dir = angle.ToRotationVector2();
                    Vector2 spawn = Projectile.Center + dir * radius * 0.85f;
                    Dust dust = Dust.NewDustPerfect(spawn, DustID.RedTorch,
                        dir * 3f, 100, default, 1.4f * ScaleMul);
                    dust.noGravity = true;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //椭圆形（扁平）冲击波——纵向半径较小
            float t01 = 1f - (float)Projectile.timeLeft / MaxLife;
            float rx = (80f + 320f * t01) * ScaleMul;
            float ry = rx * 0.45f;
            Vector2 c = Projectile.Center;
            Vector2 tCenter = new Vector2(targetHitbox.Center.X, targetHitbox.Center.Y);
            float dx = (tCenter.X - c.X) / rx;
            float dy = (tCenter.Y - c.Y) / ry;
            float distSq = dx * dx + dy * dy;
            //仅在环形带上判定，避免内部 / 外部都中招
            if (distSq < 0.55f) return false;
            if (distSq > 1.20f) return false;
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.KingSlimeShockwave?.Value;
            if (shader == null) return false;
            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (canvas == null || noise == null) return false;

            float t01 = 1f - (float)Projectile.timeLeft / MaxLife;
            //淡入淡出
            float fade;
            if (t01 < 0.15f) fade = MathHelper.SmoothStep(0f, 1f, t01 / 0.15f);
            else fade = MathHelper.SmoothStep(1f, 0f, (t01 - 0.15f) / 0.85f);

            float drawDiameter = (80f + 320f * t01) * 2.4f * ScaleMul;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.018f);
            shader.Parameters["ringProgress"]?.SetValue(t01);
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["pulseIntensity"]?.SetValue(0.55f);
            shader.Parameters["coreColor"]?.SetValue(KingSlimeRenderHelper.RoyalCore);
            shader.Parameters["midColor"]?.SetValue(new Vector3(0.85f, 0.25f, 0.08f));
            shader.Parameters["edgeColor"]?.SetValue(KingSlimeRenderHelper.RoyalEdge);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White,
                0f, canvas.Size() * 0.5f,
                new Vector2(drawDiameter, drawDiameter * 0.55f), //扁平椭圆
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override bool ShouldUpdatePosition() => false;
    }
}
