using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>闪光皇舞专属着色器</summary>
    internal class FishSparklingAssets
    {
        /// <summary>相干激光束，暗外晕/饱和中层/热芯三层 + 蓄束导引线与击发过冲</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishSparklingBeam { get; private set; }
    }

    /// <summary>闪光皇舞 VFX，单色激光玫红→品红→紫→蓝阶梯，白仅击发过冲</summary>
    internal static class SparklingVFX
    {
        /// <summary>鱼序号→饱和中层单色，序列色阶四级循环</summary>
        public static Color BeamHue(int index) {
            return (((index % 4) + 4) % 4) switch {
                0 => new Color(255, 84, 168),  //玫红
                1 => new Color(232, 66, 245),  //品红
                2 => new Color(158, 88, 255),  //紫
                _ => new Color(86, 140, 255),  //蓝
            };
        }

        /// <summary>暗色外晕，向深靛压暗，束体压底层</summary>
        public static Color DarkOf(Color hue) => Color.Lerp(hue, new Color(24, 8, 48), 0.68f);

        /// <summary>热芯常驻色，淡色调而非纯白</summary>
        public static Color CoreOf(Color hue) => Color.Lerp(hue, Color.White, 0.45f);

        /// <summary>
        /// 发射端棱镜耀斑，Additive 批次内调用
        /// energy 0~1 总强度；overshoot 0~1 击发过冲，驱动白闪与色散扇
        /// </summary>
        public static void DrawMuzzleFlare(SpriteBatch sb, Vector2 screenPos, float rot, Color hue, float energy, float overshoot, float time) {
            if (energy <= 0.01f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D streak = CWRAsset.Extra_98?.Value;
            Texture2D flare = CWRAsset.StarFlare02?.Value;
            if (glow == null || streak == null || flare == null) {
                return;
            }

            Color hueA0 = hue with { A = 0 };
            Color coreA0 = CoreOf(hue) with { A = 0 };
            float breath = 1f + 0.07f * MathF.Sin(time * 22f + rot * 3f);

            //压底柔光，仅作底层，占比克制
            sb.Draw(glow, screenPos, null, hueA0 * (0.4f * energy), 0f
                , glow.Size() * 0.5f, 0.9f * energy * breath, SpriteEffects.None, 0f);

            //沿束方向拉丝，各向异性的发射闪光主体
            float stretch = (1.9f + overshoot * 1.6f) * energy;
            sb.Draw(streak, screenPos, null, coreA0 * (0.8f * energy)
                , rot + MathHelper.PiOver2, streak.Size() * 0.5f
                , new Vector2(0.24f, stretch), SpriteEffects.None, 0f);

            //星芒核，击发帧放大过冲
            float flareScale = (0.34f + overshoot * 0.3f) * energy * breath;
            sb.Draw(flare, screenPos, null, coreA0 * (0.85f * energy)
                , time * 0.6f, flare.Size() * 0.5f, flareScale, SpriteEffects.None, 0f);

            //击发过冲专属，白闪≤2帧 + 三色棱镜微扇(仅发射端，束体保持单色)
            if (overshoot > 0.3f) {
                sb.Draw(flare, screenPos, null, (Color.White with { A = 0 }) * (overshoot * 0.8f)
                    , -time * 0.8f, flare.Size() * 0.5f, flareScale * 0.6f, SpriteEffects.None, 0f);
                Vector2 perp = (rot + MathHelper.PiOver2).ToRotationVector2();
                Color[] fan = [new Color(255, 120, 180), new Color(210, 160, 255), new Color(120, 170, 255)];
                for (int i = 0; i < 3; i++) {
                    Vector2 off = perp * (i - 1) * 5f;
                    sb.Draw(glow, screenPos + off, null, (fan[i] with { A = 0 }) * (0.5f * overshoot)
                        , 0f, glow.Size() * 0.5f, 0.24f, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>向心收束火花，从 radius 圆周向中心汇聚的针状光条</summary>
        public static void SpawnConvergeSparks(Vector2 center, Color hue, int count, float radius) {
            for (int i = 0; i < count; i++) {
                Vector2 edge = center + Main.rand.NextVector2CircularEdge(radius, radius);
                Vector2 vel = (center - edge).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(3.4f, 5.2f);
                PRTLoader.NewParticle<PRT_Spark>(edge, vel
                    , Color.Lerp(hue, CoreOf(hue), Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(false, Main.rand.Next(9, 14));
            }
        }

        /// <summary>电离微尘迸发，击发/命中时的余韵种子，活得比束体久</summary>
        public static void SpawnIonBurst(Vector2 pos, Vector2 dir, Color hue, int count) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(1.2f, 3.4f);
                PRTLoader.NewParticle<PRT_FishSparklingIon>(pos + Main.rand.NextVector2Circular(6f, 6f), vel
                    , hue, Main.rand.NextFloat(0.14f, 0.26f))
                    ?.Configure(Main.rand.Next(30, 52));
            }
        }
    }

    /// <summary>
    /// 电离微尘，激光路径上被电离的空气残迹，急减速后近似悬浮缓升
    /// 高频硬闪烁(通电感)，四芒星小光点 + SoftGlow 微底光，熄束后仍缓浮存续
    /// </summary>
    internal class PRT_FishSparklingIon : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;

        private float flickerSeed;
        private float buoyancy;
        private Color baseColor;

        public PRT_FishSparklingIon Configure(int lifetime, float buoyancyStrength = 0.014f) {
            Lifetime = lifetime;
            buoyancy = buoyancyStrength;
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            flickerSeed = 0f;
            buoyancy = 0f;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            flickerSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(30, 52);
            }
            if (buoyancy == 0f) {
                buoyancy = 0.014f;
            }
            if (baseColor == default) {
                baseColor = Color;
            }
        }

        public override void AI() {
            //急减速→悬浮缓升，带微布朗抖动
            Velocity *= 0.86f;
            Velocity.Y -= buoyancy;
            Velocity += Main.rand.NextVector2Circular(0.05f, 0.05f);

            float lc = LifetimeCompletion;
            //硬闪烁，通电电离的断续感，非平滑脉动
            float wave = MathF.Sin(Time * 1.05f + flickerSeed);
            float flicker = wave > 0f ? 1f : 0.42f;
            Opacity = MathF.Min(lc * 6f, 1f) * (1f - lc * lc) * flicker;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D star = TexValue;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = baseColor with { A = 0 };

            //微底光压底 + 四芒星核，异质双层非同图叠亮
            if (glow != null) {
                spriteBatch.Draw(glow, pos, null, col * (0.3f * Opacity), 0f
                    , glow.Size() * 0.5f, Scale * 0.55f, SpriteEffects.None, 0f);
            }
            spriteBatch.Draw(star, pos, null, col * Opacity, flickerSeed
                , star.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
