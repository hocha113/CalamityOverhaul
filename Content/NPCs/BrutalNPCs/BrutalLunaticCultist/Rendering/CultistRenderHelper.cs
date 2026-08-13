using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>法阵/元素球绘制与元素特效配方，全客户端</summary>
    internal static class CultistRenderHelper
    {
        //CWRAsset 未暴露的遮罩，自装载
        [VaultLoaden(CWRConstant.Masking + "LightBeam")]
        internal static ReLogic.Content.Asset<Texture2D> LightBeam = null;
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
