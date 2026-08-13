using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>法阵/元素球绘制与元素特效配方，全客户端</summary>
    internal static class CultistRenderHelper
    {
        //CWRAsset 未暴露的遮罩，自装载
        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        internal static ReLogic.Content.Asset<Texture2D> TearFlame01 = null;

        #region 批次辅助
        internal static void BeginAdditive(SpriteBatch spriteBatch) {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.AnisotropicClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        internal static void EndAdditive(SpriteBatch spriteBatch) {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        #endregion

        #region 法阵绘制

        /// <summary>
        /// 法阵，shader 优先，缺省 CPU 圆环回退；调用方须处于实体绘制批（AlphaBlend Deferred）
        /// </summary>
        public static void DrawSigil(SpriteBatch sb, Vector2 worldPos, float radiusPx, CultistElement element,
            float progress, float spin, float flash, float breakGrade, float alpha, bool cloneTint = false) {
            if (alpha <= 0.01f || progress <= 0.01f) {
                return;
            }

            Color main = cloneTint ? CultistPalette.CloneMain(element) : CultistPalette.Main(element);
            Color deep = CultistPalette.Deep(element);
            Color bright = CultistPalette.Bright(element);

            Effect effect = EffectLoader.CultistSigil?.Value;
            if (effect != null) {
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
                effect.Parameters["uBreak"]?.SetValue(MathHelper.Clamp(breakGrade, 0f, 1f));
                effect.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
                effect.Parameters["uElement"]?.SetValue((float)(int)element);
                effect.Parameters["uSpin"]?.SetValue(spin);
                effect.Parameters["uAlpha"]?.SetValue(MathHelper.Clamp(alpha, 0f, 1f));
                effect.Parameters["uColDeep"]?.SetValue(deep.ToVector3());
                effect.Parameters["uColMain"]?.SetValue(main.ToVector3());
                effect.Parameters["uColBright"]?.SetValue(bright.ToVector3());

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
                effect.CurrentTechnique.Passes[0].Apply();

                Texture2D pixel = VaultAsset.placeholder2.Value;
                float quadSize = radiusPx * 2.3f;
                sb.Draw(pixel, worldPos - Main.screenPosition, null, Color.White, 0f,
                    pixel.Size() / 2f, quadSize / pixel.Width, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            //CPU回退：双环+星芒
            BeginAdditive(sb);
            Vector2 drawPos = worldPos - Main.screenPosition;
            Texture2D circle = CWRAsset.DiffusionCircle.Value;
            Texture2D flare = CWRAsset.StarFlare01.Value;
            float s = radiusPx / (circle.Width * 0.5f);
            sb.Draw(circle, drawPos, null, main * (0.8f * alpha * progress), spin,
                circle.Size() / 2f, s, SpriteEffects.None, 0f);
            sb.Draw(circle, drawPos, null, deep * (0.55f * alpha * progress), -spin * 0.7f,
                circle.Size() / 2f, s * 0.72f, SpriteEffects.None, 0f);
            sb.Draw(flare, drawPos, null, bright * (0.5f * alpha * progress + flash * 0.5f), spin * 0.4f,
                flare.Size() / 2f, s * 0.5f, SpriteEffects.None, 0f);
            EndAdditive(sb);
        }

        #endregion

        #region 元素球绘制

        /// <summary>元素球，shader 优先；调用方须处于实体绘制批</summary>
        public static void DrawOrb(SpriteBatch sb, Vector2 worldPos, float radiusPx, CultistElement element,
            float charge, float flash, float seed, bool cloneTint = false) {
            if (charge <= 0.01f) {
                return;
            }

            Color main = cloneTint ? CultistPalette.CloneMain(element) : CultistPalette.Main(element);
            Color deep = CultistPalette.Deep(element);
            Color bright = CultistPalette.Bright(element);

            Effect effect = EffectLoader.CultistElementOrb?.Value;
            if (effect != null) {
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uElement"]?.SetValue((float)(int)element);
                effect.Parameters["uCharge"]?.SetValue(MathHelper.Clamp(charge, 0f, 1f));
                effect.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
                effect.Parameters["uSeed"]?.SetValue(seed);
                effect.Parameters["uColDeep"]?.SetValue(deep.ToVector3());
                effect.Parameters["uColMain"]?.SetValue(main.ToVector3());
                effect.Parameters["uColBright"]?.SetValue(bright.ToVector3());

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
                effect.CurrentTechnique.Passes[0].Apply();

                Texture2D pixel = VaultAsset.placeholder2.Value;
                float quadSize = radiusPx * 3.4f;
                sb.Draw(pixel, worldPos - Main.screenPosition, null, Color.White, 0f,
                    pixel.Size() / 2f, quadSize / pixel.Width, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            //CPU回退：结构=芒星剪影+外晕垫底
            BeginAdditive(sb);
            Vector2 drawPos = worldPos - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarFlare02.Value;
            float gs = radiusPx / (glow.Width * 0.5f);
            sb.Draw(glow, drawPos, null, deep * (0.5f * charge), 0f, glow.Size() / 2f, gs * 1.5f, SpriteEffects.None, 0f);
            sb.Draw(star, drawPos, null, main * (0.85f * charge + flash * 0.4f),
                Main.GlobalTimeWrappedHourly * 2f + seed, star.Size() / 2f,
                radiusPx / (star.Width * 0.42f), SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, bright * (0.35f * charge + flash * 0.5f), 0f,
                glow.Size() / 2f, gs * 0.5f, SpriteEffects.None, 0f);
            EndAdditive(sb);
        }

        #endregion

        #region 晶枪绘制

        /// <summary>
        /// 霜牢晶枪（CultistCrystal shader），rotation 沿枪轴（+x=尖端）；调用方须处于实体绘制批
        /// </summary>
        public static void DrawCrystal(SpriteBatch sb, Vector2 worldPos, float lengthPx, float rotation,
            float grow, float flash, float seed, float alpha = 1f) {
            if (grow <= 0.01f || alpha <= 0.01f) {
                return;
            }

            Effect effect = EffectLoader.CultistCrystal?.Value;
            if (effect != null) {
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uGrow"]?.SetValue(MathHelper.Clamp(grow, 0f, 1f));
                effect.Parameters["uFlash"]?.SetValue(MathHelper.Clamp(flash, 0f, 1f));
                effect.Parameters["uSeed"]?.SetValue(seed);
                effect.Parameters["uColDeep"]?.SetValue(CultistPalette.IceDeep.ToVector3());
                effect.Parameters["uColMain"]?.SetValue(CultistPalette.IceMain.ToVector3());
                effect.Parameters["uColBright"]?.SetValue(CultistPalette.IceBright.ToVector3());

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
                effect.CurrentTechnique.Passes[0].Apply();

                Texture2D pixel = VaultAsset.placeholder2.Value;
                Vector2 scale = new(lengthPx / pixel.Width, lengthPx * 0.40f / pixel.Height);
                sb.Draw(pixel, worldPos - Main.screenPosition, null, Color.White * alpha, rotation,
                    pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            //CPU回退：双层硬边星芒（亮芯+冷缘）
            BeginAdditive(sb);
            Vector2 drawPos = worldPos - Main.screenPosition;
            Texture2D star = CWRAsset.StarGlow01.Value;
            float len = lengthPx / star.Width;
            sb.Draw(star, drawPos, null, CultistPalette.IceDeep * (0.8f * grow * alpha),
                rotation, star.Size() / 2f, new Vector2(len * 1.9f, 0.34f), SpriteEffects.None, 0f);
            sb.Draw(star, drawPos, null, CultistPalette.IceBright * ((0.9f * grow + flash * 0.6f) * alpha),
                rotation, star.Size() / 2f, new Vector2(len * 1.5f, 0.16f), SpriteEffects.None, 0f);
            EndAdditive(sb);
        }

        #endregion

        #region 原版材质基底（真实纹理为本体，程序化只做叠加层）

        /// <summary>确定性抖动哈希（时间片驱动的伪随机，各端一致）</summary>
        public static float JitterHash(int seed, int i) {
            float h = (float)Math.Sin(seed * 12.9898f + i * 78.233f) * 43758.5453f;
            return h - (float)Math.Floor(h);
        }

        /// <summary>
        /// 元素核心的原版纹理本体：火=原版信徒火球467、雷=原版信徒雷球465、冰=原版霜晶349三瓣簇；
        /// 全亮绘制，调用方须处于实体绘制批（AlphaBlend）
        /// </summary>
        public static void DrawElementCore(SpriteBatch sb, Vector2 worldPos, CultistElement element,
            float scale, float alpha, float animTick, float rotation = 0f) {
            if (alpha <= 0.01f || scale <= 0.01f) {
                return;
            }
            Vector2 drawPos = worldPos - Main.screenPosition;
            switch (element) {
                case CultistElement.Fire: {
                    Main.instance.LoadProjectile(ProjectileID.CultistBossFireBall);
                    Texture2D tex = TextureAssets.Projectile[ProjectileID.CultistBossFireBall].Value;
                    int fh = tex.Height / 4;
                    Rectangle src = new(0, (int)(animTick / 4f) % 4 * fh, tex.Width, fh);
                    sb.Draw(tex, drawPos, src, Color.White * alpha, rotation,
                        new Vector2(tex.Width / 2f, fh / 2f), scale, SpriteEffects.None, 0f);
                    break;
                }
                case CultistElement.Thunder: {
                    Main.instance.LoadProjectile(ProjectileID.CultistBossLightningOrb);
                    Texture2D tex = TextureAssets.Projectile[ProjectileID.CultistBossLightningOrb].Value;
                    int fh = tex.Height / 4;
                    Rectangle src = new(0, (int)(animTick / 5f) % 4 * fh, tex.Width, fh);
                    sb.Draw(tex, drawPos, src, Color.White * alpha, rotation,
                        new Vector2(tex.Width / 2f, fh / 2f), scale, SpriteEffects.None, 0f);
                    break;
                }
                default: {
                    //冰无原版球体，用霜晶349三瓣簇拼核（尖端向外慢旋；贴图尖端朝下=角度PiOver2）
                    Main.instance.LoadProjectile(ProjectileID.FrostShard);
                    Texture2D tex = TextureAssets.Projectile[ProjectileID.FrostShard].Value;
                    int fh = tex.Height / 5;
                    for (int k = 0; k < 3; k++) {
                        Rectangle src = new(0, k * 2 % 5 * fh, tex.Width, fh);
                        float ang = rotation + k * MathHelper.TwoPi / 3f + animTick * 0.02f;
                        //根锚圆心、尖端向外：以晶根（贴图顶边中点）为原点，绘制旋转=向外角-PiOver2
                        sb.Draw(tex, drawPos, src, Color.White * alpha, ang - MathHelper.PiOver2,
                            new Vector2(tex.Width / 2f, 4f), scale, SpriteEffects.None, 0f);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 两锚点之间的顶点闪电弧：确定性中点抖动折线（2帧一重掷），双股+亮芯；
        /// 端点各压一枚小晕遮接缝；调用方须处于加色批
        /// </summary>
        public static void DrawLightningBetween(SpriteBatch sb, Vector2 a, Vector2 b,
            Color main, Color bright, int seed, float intensity, float widthScale = 1f) {
            if (intensity <= 0.01f) {
                return;
            }
            Texture2D bolt = CWRAsset.ThunderTrail.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 span = b - a;
            float dist = span.Length();
            if (dist < 8f) {
                return;
            }
            int segs = (int)MathHelper.Clamp(dist / 30f, 4f, 16f);
            Vector2 normal = span.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            int slice = (int)(Main.GameUpdateCount / 2u) * 31 + seed;

            //频闪：亮度骤变+偶发压暗帧
            float flicker = 0.62f + 0.38f * JitterHash(slice, 3);
            if (JitterHash(slice, 11) < 0.08f) {
                flicker *= 0.3f;
            }
            float amp = MathHelper.Clamp(dist * 0.06f, 8f, 26f);

            for (int strand = 0; strand < 2; strand++) {
                Vector2 prev = a;
                float strandAmp = strand == 0 ? amp : amp * 0.55f;
                Color col = strand == 0 ? main * (0.8f * intensity * flicker) : bright * (0.55f * intensity * flicker);
                float thick = (strand == 0 ? 0.20f : 0.11f) * widthScale;
                for (int i = 1; i <= segs; i++) {
                    float t = i / (float)segs;
                    //端点归零的正弦包络，保证两端严丝合缝锚在球上
                    float envelope = (float)Math.Sin(t * MathHelper.Pi);
                    float off = (JitterHash(slice + strand * 97, i) - 0.5f) * 2f * strandAmp * envelope;
                    Vector2 node = i == segs ? b : a + span * t + normal * off;
                    Vector2 segSpan = node - prev;
                    sb.Draw(bolt, prev - Main.screenPosition, null, col,
                        segSpan.ToRotation(), new Vector2(0f, bolt.Height / 2f),
                        new Vector2(segSpan.Length() / bolt.Width, thick), SpriteEffects.None, 0f);
                    prev = node;
                }
            }

            //端点晕：遮住弧根接缝
            Color anchorCol = main * (0.5f * intensity * flicker);
            sb.Draw(glow, a - Main.screenPosition, null, anchorCol, 0f, glow.Size() / 2f, 0.24f * widthScale, SpriteEffects.None, 0f);
            sb.Draw(glow, b - Main.screenPosition, null, anchorCol, 0f, glow.Size() / 2f, 0.24f * widthScale, SpriteEffects.None, 0f);
        }

        #endregion

        #region 元素特效配方（粒子+音效，客户端）

        /// <summary>瞬移离场：内爆符文+收拢闪光</summary>
        public static void BlinkOut(Vector2 pos, CultistElement element) {
            if (VaultUtils.isServer) {
                return;
            }
            Color main = CultistPalette.Main(element);
            for (int i = 0; i < 10; i++) {
                Vector2 start = pos + Main.rand.NextVector2CircularEdge(70f, 70f);
                PRTLoader.NewParticle<PRT_CultistRune>(start, Vector2.Zero, main, Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(pos, 0.24f, 18);
            }
            PRTLoader.NewParticle<PRT_CultistShard>(pos, -Vector2.UnitY * 2f, main, 1f)?.Configure(14);
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.2f, Volume = 0.65f, MaxInstances = 4 }, pos);
            Lighting.AddLight(pos, main.ToVector3() * 0.8f);
        }

        /// <summary>瞬移到场：符文外散+光爆</summary>
        public static void BlinkIn(Vector2 pos, CultistElement element) {
            if (VaultUtils.isServer) {
                return;
            }
            Color main = CultistPalette.Main(element);
            Color bright = CultistPalette.Bright(element);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f);
                SpawnElementMote(pos, vel, element, Main.rand.NextFloat(0.7f, 1.2f), Main.rand.Next(14, 24));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, bright, 0.08f)?.Configure(0.08f, 0.9f, 16);
            SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.25f, Volume = 0.6f, MaxInstances = 4 }, pos);
            Lighting.AddLight(pos, main.ToVector3() * 1.2f);
        }

        /// <summary>按元素生成一枚基础粒子</summary>
        public static void SpawnElementMote(Vector2 pos, Vector2 vel, CultistElement element, float scale, int life) {
            if (VaultUtils.isServer) {
                return;
            }
            switch (element) {
                case CultistElement.Fire:
                    PRTLoader.NewParticle<PRT_CultistEmber>(pos, vel, CultistPalette.FireBright, scale)?.Configure(life);
                    break;
                case CultistElement.Ice:
                    PRTLoader.NewParticle<PRT_CultistFrost>(pos, vel * 0.6f, CultistPalette.IceBright, scale)?.Configure(life + 8);
                    break;
                default:
                    PRTLoader.NewParticle<PRT_CultistVolt>(pos, vel * 1.3f, CultistPalette.ThunderBright, scale)?.Configure(Math.Max(life - 4, 8));
                    break;
            }
        }

        /// <summary>施法起手爆点：手位光爆+短促元素粒</summary>
        public static void CastBurst(Vector2 handPos, Vector2 aimDir, CultistElement element, float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            Color main = CultistPalette.Main(element);
            int count = (int)(6 * strength);
            for (int i = 0; i < count; i++) {
                Vector2 vel = aimDir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 7f) * strength;
                SpawnElementMote(handPos, vel, element, Main.rand.NextFloat(0.6f, 1.1f) * strength, Main.rand.Next(12, 22));
            }
            Lighting.AddLight(handPos, main.ToVector3() * strength);
        }

        /// <summary>汇聚吟唱：从环外拉符文向中心（密度按进度，72%后静默由调用方裁决）</summary>
        public static void ConvergeRunes(Vector2 center, float radius, CultistElement element, float density) {
            if (VaultUtils.isServer || density <= 0f) {
                return;
            }
            if (Main.rand.NextFloat() > density) {
                return;
            }
            Color main = CultistPalette.Main(element);
            Vector2 start = center + Main.rand.NextVector2CircularEdge(radius, radius * 0.8f);
            //切向初速让汇聚带旋
            Vector2 tangent = (center - start).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(2f, 5f);
            PRTLoader.NewParticle<PRT_CultistRune>(start, tangent, main, Main.rand.NextFloat(0.7f, 1.4f))
                ?.Configure(center, 0.085f, 46);
        }

        /// <summary>元素命中/爆裂</summary>
        public static void ElementImpact(Vector2 pos, CultistElement element, float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            Color main = CultistPalette.Main(element);
            Color bright = CultistPalette.Bright(element);
            int count = (int)(10 * strength);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 8.5f) * strength;
                SpawnElementMote(pos, vel, element, Main.rand.NextFloat(0.7f, 1.3f) * strength, Main.rand.Next(14, 26));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, bright, 0.06f * strength)
                ?.Configure(0.06f * strength, 0.7f * strength, 14);
            Lighting.AddLight(pos, main.ToVector3() * 1.1f * strength);
        }

        /// <summary>分身破灭：无害亮片+碎晶</summary>
        public static void CloneBurst(Vector2 pos, CultistElement element) {
            if (VaultUtils.isServer) {
                return;
            }
            Color main = CultistPalette.CloneMain(element);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_CultistShard>(pos + Main.rand.NextVector2Circular(16f, 24f), vel, main,
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(20, 34));
            }
            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = 0.3f, Volume = 0.5f, MaxInstances = 5 }, pos);
        }

        /// <summary>随机低语吟唱声</summary>
        public static void ChantVoice(Vector2 pos, float volume = 0.8f, float pitch = 0f) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundStyle s = Main.rand.Next(4) switch {
                0 => SoundID.Zombie88,
                1 => SoundID.Zombie89,
                2 => SoundID.Zombie90,
                _ => SoundID.Zombie91,
            };
            SoundEngine.PlaySound(s with { Volume = volume, Pitch = pitch, MaxInstances = 3 }, pos);
        }

        #endregion
    }
}
