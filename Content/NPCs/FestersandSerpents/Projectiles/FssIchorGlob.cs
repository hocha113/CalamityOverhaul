using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 灵液痰弹；ai[0]模式 0抛射团 1雨滴 2重团；落地留脓池（雨滴不留）。
    /// 借世吞 EowAcid 共享着色器（TechGlob），色板换灼金灵液——共享 shader 每次全参数重设。
    /// </summary>
    internal class FssIchorGlob : FssModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private int Mode => (int)Projectile.ai[0];
        private bool IsRainDrop => Mode == 1;
        private bool IsHeavy => Mode == 2;

        private float Gravity => IsRainDrop ? 0.42f : IsHeavy ? 0.3f : FssDirector.IchorGlobGravity;
        private float MaxFall => IsRainDrop ? 19f : 15f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.alpha = 60;
        }

        public override void AI() {
            //首帧尺寸按模式定型（各端本地执行，ai随生成包到达）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (IsHeavy) {
                    Projectile.Resize(24, 24);
                    Projectile.scale = 1.35f;
                }
                else if (IsRainDrop) {
                    Projectile.Resize(10, 10);
                    Projectile.scale = 0.7f;
                    Projectile.timeLeft = 240;
                }
            }

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFall) {
                Projectile.velocity.Y = MaxFall;
            }
            //空气阻力让抛物线尾段下坠更陡（有机手感）
            Projectile.velocity.X *= 0.998f;

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行途中甩落金珠
            if (!VaultUtils.isServer && !IsRainDrop && Main.rand.NextBool(6)) {
                Dust drip = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.Ichor, -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                    40, default, Main.rand.NextFloat(0.6f, 0.95f));
                drip.noGravity = false;
            }

            Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * (IsHeavy ? 0.55f : 0.32f));
        }

        public override void OnKill(int timeLeft) {
            //落点脓池：权威端生成（雨滴不留池）
            if (!VaultUtils.isClient && !IsRainDrop) {
                Vector2 ground = new(Projectile.Center.X,
                    FssVfx.FindGroundY(Projectile.Center - new Vector2(0f, 8f), 400f));
                if (Vector2.Distance(ground, Projectile.Center) < 120f) {
                    FssIchorPool.TrySpawn(Projectile.GetSource_FromAI(), ground,
                        (int)(Projectile.damage * 0.6f), IsHeavy);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            float power = IsHeavy ? 1.2f : IsRainDrop ? 0.4f : 0.8f;
            FssVfx.IchorBurst(Projectile.Center, power, -Projectile.velocity.SafeNormalize(Vector2.Zero));
            if (!IsRainDrop) {
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 5 }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - Projectile.alpha / 255f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() / 9f, 0.7f, 2.1f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //着色器灵液团（雨滴走廉价精灵路径）
            Effect effect = EffectLoader.EowAcid?.Value;
            if (effect != null && !IsRainDrop) {
                DrawShaderGlob(effect, drawPos, stretch, fade);
            }
            else {
                DrawSpriteGlob(drawPos, stretch, fade);
            }
            return false;
        }

        /// <summary>着色器路径：翻转批到 Immediate 画灵液团 quad（金色参数槽）</summary>
        private void DrawShaderGlob(Effect effect, Vector2 drawPos, float stretch, float fade) {
            float sizePx = (IsHeavy ? 66f : 44f) * Projectile.scale;

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
            //quad长轴沿速度方向拉伸
            Vector2 scale = new Vector2(sizePx * stretch / pixel.Width, sizePx / pixel.Height);
            sb.Draw(pixel, drawPos, null, Color.White,
                Projectile.rotation - MathHelper.PiOver2, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>精灵回退：深琥珀底+金膜+高光点三层（实体感的暗缘打底）</summary>
        private void DrawSpriteGlob(Vector2 drawPos, float stretch, float fade) {
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float baseScale = (IsHeavy ? 0.42f : IsRainDrop ? 0.16f : 0.3f) * Projectile.scale;
            Vector2 scaleVec = new Vector2(baseScale, baseScale * stretch);

            //拖尾残影（同材质缩淡重画）
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, FssVfx.IchorDeep with { A = 40 } * (0.3f * t * fade),
                    Projectile.rotation, origin, scaleVec * (0.7f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, drawPos, null, FssVfx.IchorDeep with { A = 160 } * fade,
                Projectile.rotation, origin, scaleVec, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, FssVfx.IchorGold with { A = 90 } * (0.9f * fade),
                Projectile.rotation, origin, scaleVec * 0.72f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos + new Vector2(-2f, -3f), null,
                FssVfx.IchorBright with { A = 0 } * (0.55f * fade),
                Projectile.rotation, origin, scaleVec * 0.3f, SpriteEffects.None, 0);
        }
    }
}
