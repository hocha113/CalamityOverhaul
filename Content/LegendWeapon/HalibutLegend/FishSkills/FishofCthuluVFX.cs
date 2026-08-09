using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>深渊凝视域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishCthuluAssets
    {
        /// <summary>凝视之瞳冲刺暗绸带（AlphaBlend 压暗型条带，非发光）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishCthuluRibbon { get; private set; }
    }

    /// <summary>深渊凝视 VFX，虚空紫黑+暗血肉+虹膜暗红小亮点，压暗无常驻纯白；异于 FishHunger 群</summary>
    internal static class FishCthuluVFX
    {
        //==== 色彩脚本 ====
        /// <summary>虚空紫黑（雾外圈/绸带边缘）</summary>
        public static readonly Color VoidDark = new(16, 9, 22);
        /// <summary>虚空雾中层（暗紫）</summary>
        public static readonly Color VoidMist = new(38, 22, 48);
        /// <summary>暗血肉（体色压暗/碎屑冷端）</summary>
        public static readonly Color FleshDark = new(58, 18, 26);
        /// <summary>血肉中层（碎屑/血珠主色）</summary>
        public static readonly Color FleshMid = new(118, 34, 42);
        /// <summary>虹膜暗红，唯一允许的亮点，只在瞳孔尺度与瞬时闪帧出现</summary>
        public static readonly Color IrisRed = new(196, 38, 38);
        /// <summary>瞳墨（近黑）</summary>
        public static readonly Color PupilInk = new(12, 6, 10);

        /// <summary>FishCthuluRibbon 标准参数；seed 传弹幕 whoAmI 派生量防多眼同相</summary>
        public static void ApplyRibbon(Effect fx, float seed, float fade) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(seed);
            fx.Parameters["uFade"]?.SetValue(fade);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
        }

        //==== 粒子族 ====

        /// <summary>虚空雾涌，暗色半透明雾团，缓慢布朗漂移后自散</summary>
        public static void MistPuff(Vector2 pos, int count, float scale, Vector2 baseVel = default) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = baseVel + Main.rand.NextVector2Circular(0.7f, 0.7f);
                PRTLoader.NewParticle<PRT_FishCthuluMist>(pos + Main.rand.NextVector2Circular(10f, 10f)
                    , vel, VoidMist, Main.rand.NextFloat(0.8f, 1.25f) * scale)
                    ?.Configure(Main.rand.Next(38, 62));
            }
        }

        /// <summary>暗血飞沫，重力血珠锥，颜色压在暗血肉带（liquid 不是能量）</summary>
        public static void BloodSpray(Vector2 pos, Vector2 dir, int drops, float speed) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(0.5f, 1f) * speed
                    - Vector2.UnitY * Main.rand.NextFloat(1.4f);
                Color col = Color.Lerp(FleshMid, FleshDark, Main.rand.NextFloat(0.6f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, col, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(20, 32));
            }
        }

        /// <summary>眼膜碎屑，撕膜瞬间的翻滚肉片，旋转拖影编码自旋</summary>
        public static void FleshBurst(Vector2 pos, Vector2 dir, int chips) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < chips; i++) {
                Vector2 vel = dir.RotatedByRandom(1.15f) * Main.rand.NextFloat(2.2f, 5.5f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.8f, 2f);
                PRTLoader.NewParticle<PRT_FishCthuluFlesh>(pos + Main.rand.NextVector2Circular(6f, 6f)
                    , vel, FleshMid, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(20, 32));
            }
        }

        /// <summary>暗环脉冲，压扁的暗紫扩散环，召唤/命中定向事件用（克制，非亮圈）</summary>
        public static void DarkRing(Vector2 pos, Vector2 dir, float finalScale) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, VoidMist * 0.8f, 0.07f)
                ?.Configure(new Vector2(1f, 0.6f), dir.ToRotation(), finalScale, 13);
        }

        //==== 数学 ====

        /// <summary>带过冲缓出（眼睑开启的「弹开」曲线）</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }

    /// <summary>
    /// 虚空雾丝，暗紫黑半透明雾团，AlphaBlend 压暗画面（非发光）
    /// 缓慢布朗漂移 + 微旋 + 先胀后敛，凝视之瞳的待机脱落物与事件雾涌共用
    /// </summary>
    internal class PRT_FishCthuluMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float spin;
        private float wanderSeed;
        private Color baseColor;

        public PRT_FishCthuluMist Configure(int lifetime) {
            Lifetime = lifetime;
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            wanderSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            baseColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            wanderSeed = 0f;
            baseColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Opacity = 0f;
            //InnoVault 约定 Lifetime < 0 为不限时；遗漏 Configure 时防止永久堆积
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(38, 62);
            }
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            //淡入淡出，雾不 pop
            float fadeIn = Math.Min(Time / 10f, 1f);
            Opacity = fadeIn * (1f - MathF.Pow(lc, 2.2f)) * 0.44f;

            //布朗漂移，低频正弦游动替代直线平移
            float t = Main.GlobalTimeWrappedHourly * 1.4f + wanderSeed;
            Velocity += new Vector2(MathF.Sin(t * 1.7f), MathF.Cos(t * 1.3f)) * 0.012f;
            Velocity *= 0.965f;

            Rotation += spin;
            Scale *= lc < 0.35f ? 1.006f : 0.997f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //外圈更暗更大 + 中层主体
            spriteBatch.Draw(tex, pos, null, FishCthuluVFX.VoidDark * (Opacity * 0.7f)
                , Rotation * 0.8f, origin, Scale * 0.18f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, (baseColor == default ? FishCthuluVFX.VoidMist : baseColor) * Opacity
                , Rotation, origin, Scale * 0.138f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 眼膜碎屑，撕膜瞬间迸出的小块血肉，受重力翻滚坠落
    /// 自旋由旋转拖影编码（两帧残影反向叠画），色程血肉中层 → 暗血肉
    /// </summary>
    internal class PRT_FishCthuluFlesh : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float spin;
        private float baseScale;

        public PRT_FishCthuluFlesh Configure(int lifetime) {
            Lifetime = lifetime;
            spin = Main.rand.NextFloat(0.18f, 0.38f) * (Main.rand.NextBool() ? 1f : -1f);
            baseScale = Scale;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            baseScale = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 32);
            }
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            Velocity = new Vector2(Velocity.X * 0.975f, Math.Min(Velocity.Y + 0.34f, 12f));
            spin *= 0.965f;
            Rotation += spin;
            Scale = baseScale * (1f - lc * 0.5f);
            Color = Color.Lerp(FishCthuluVFX.FleshMid, FishCthuluVFX.FleshDark, lc);
            Opacity = 1f - MathF.Pow(lc, 2.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //肉片微扁 + 沿速度轻拉伸
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.5f);
            Vector2 scale = new Vector2(0.5f, 0.36f * (1f + stretch)) * Scale;

            //旋转拖影
            spriteBatch.Draw(tex, pos, null, Color * (Opacity * 0.16f), Rotation - spin * 4.4f
                , origin, scale * 1.02f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * (Opacity * 0.34f), Rotation - spin * 2.2f
                , origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
