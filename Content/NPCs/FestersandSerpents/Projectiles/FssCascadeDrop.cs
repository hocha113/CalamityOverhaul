using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 瀑洗痰滴：灵液水管的流单元。前段直喷（无重力、高拉伸 = 连成水柱读感），
    /// 后段重力接管散成瀑尾。ai[0]=1 落地留池（每 HosePoolEvery 滴一颗，稀疏播种声明）。
    /// 借 EowAcid TechGlob（金色参数槽 + 高 uStretch）。
    /// </summary>
    internal class FssCascadeDrop : FssModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private bool LeavesPool => Projectile.ai[0] == 1f;
        /// <summary>直喷段帧数（此后重力接管）</summary>
        private const int JetFrames = 16;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.alpha = 40;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            if (Projectile.localAI[0] > JetFrames) {
                Projectile.velocity.Y += 0.36f;
                if (Projectile.velocity.Y > 18f) {
                    Projectile.velocity.Y = 18f;
                }
                Projectile.velocity.X *= 0.997f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!VaultUtils.isServer && Main.rand.NextBool(8)) {
                Dust gold = Dust.NewDustPerfect(Projectile.Center, DustID.Ichor,
                    -Projectile.velocity * 0.05f, 50, default, Main.rand.NextFloat(0.5f, 0.8f));
                gold.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * 0.25f);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isClient && LeavesPool) {
                Vector2 ground = new(Projectile.Center.X,
                    FssVfx.FindGroundY(Projectile.Center - new Vector2(0f, 8f), 300f));
                if (Vector2.Distance(ground, Projectile.Center) < 100f) {
                    FssIchorPool.TrySpawn(Projectile.GetSource_FromAI(), ground,
                        (int)(Projectile.damage * 0.6f), false);
                }
            }
            if (!VaultUtils.isServer) {
                FssVfx.IchorBurst(Projectile.Center, 0.5f, -Projectile.velocity.SafeNormalize(Vector2.UnitY));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - Projectile.alpha / 255f;
            //高拉伸：流单元首尾相接读成连续水柱
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() / 5.5f, 1.2f, 3.2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Effect effect = EffectLoader.EowAcid?.Value;
            if (effect != null) {
                effect.CurrentTechnique = effect.Techniques["TechGlob"];
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 97 * 0.173f);
                effect.Parameters["uStretch"]?.SetValue(stretch);
                effect.Parameters["uIntensity"]?.SetValue(fade);
                effect.Parameters["uColorDeep"]?.SetValue(FssVfx.IchorDeep.ToVector3());
                effect.Parameters["uColorBright"]?.SetValue(FssVfx.IchorBright.ToVector3());

                SpriteBatch sb = Main.spriteBatch;
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
                effect.CurrentTechnique.Passes[0].Apply();

                Texture2D pixel = VaultAsset.placeholder2.Value;
                float sizePx = 34f;
                Vector2 scale = new(sizePx * stretch / pixel.Width, sizePx / pixel.Height);
                sb.Draw(pixel, drawPos, null, Color.White,
                    Projectile.rotation - MathHelper.PiOver2, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                //精灵回退：深琥珀拉伸滴
                Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
                Vector2 origin = tex.Size() / 2f;
                Vector2 scaleVec = new(0.2f, 0.2f * stretch);
                Main.EntitySpriteDraw(tex, drawPos, null, FssVfx.IchorDeep with { A = 150 } * fade,
                    Projectile.rotation, origin, scaleVec, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, null, FssVfx.IchorBright with { A = 0 } * (0.5f * fade),
                    Projectile.rotation, origin, scaleVec * 0.5f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
