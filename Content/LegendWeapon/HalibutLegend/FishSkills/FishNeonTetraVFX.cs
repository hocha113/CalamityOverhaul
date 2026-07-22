using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>霓虹足迹域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishNeonTetraAssets
    {
        /// <summary>青-品红荧光缎带（NeonTetraLightProjectile 尾迹）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishNeonTrail { get; private set; }
    }

    /// <summary>霓虹足迹 VFX，青 <see cref="NeonCyan"/> ↔ 品红 <see cref="NeonMagenta"/>，深渊压底，呼吸明暗，禁常驻纯白</summary>
    internal static class FishNeonTetraVFX
    {
        /// <summary>深海青（饱和低明度）</summary>
        public static readonly Color NeonCyan = new(0, 158, 184);
        /// <summary>品红（饱和低明度）</summary>
        public static readonly Color NeonMagenta = new(196, 22, 138);
        /// <summary>深渊暗蓝（外圈/垫底）</summary>
        public static readonly Color Abyss = new(10, 16, 38);
        /// <summary>鱼体暗冷底色（自发光体的非照明部）</summary>
        public static readonly Color AbyssBody = new(26, 34, 52);

        /// <summary>青↔品红取色，t: 0=青 1=品红</summary>
        public static Color HueColor(float t) => Color.Lerp(NeonCyan, NeonMagenta, MathHelper.Clamp(t, 0f, 1f));

        /// <summary>带过冲缓出（化现落定曲线）</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        /// <summary>FishNeonTrail 参数装配；phase 每鱼相位防同相，breath 呼吸 0..1，fade 生命包络 0..1</summary>
        public static void ApplyTrail(Effect fx, float phase, float breath, float fade) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.55f + phase);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uBreath"]?.SetValue(MathHelper.Clamp(breath, 0f, 1f));
            fx.Parameters["uFade"]?.SetValue(MathHelper.Clamp(fade, 0f, 1f));
            fx.Parameters["uColCyan"]?.SetValue(NeonCyan.ToVector3());
            fx.Parameters["uColMagenta"]?.SetValue(NeonMagenta.ToVector3());
            fx.Parameters["uColAbyss"]?.SetValue(Abyss.ToVector3());
        }

        /// <summary>化现</summary>
        public static void MaterializeBurst(Vector2 pos, float hueT) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.6f, 1.8f);
                PRTLoader.NewParticle<PRT_FishNeonMote>(pos + Main.rand.NextVector2Circular(6f, 6f), vel
                    , HueColor(hueT + Main.rand.NextFloat(-0.3f, 0.3f)), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(36, 60));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Color.Lerp(HueColor(hueT), Abyss, 0.45f), 0.06f)
                ?.Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.16f, 12);
            SoundEngine.PlaySound(SoundID.Drip with {
                Volume = 0.18f,
                Pitch = 0.35f + Main.rand.NextFloat(0.2f),
                MaxInstances = 3
            }, pos);
        }

        /// <summary>触碰迸发，荧光沿命中方向 squirt + 目标处小暗环，无白闪</summary>
        public static void TouchBurst(Vector2 pos, Vector2 targetCenter, float hueT) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = (targetCenter - pos).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(1.6f, 4.2f);
                PRTLoader.NewParticle<PRT_FishNeonMote>(pos + dir * 8f, vel
                    , HueColor(Main.rand.NextBool() ? hueT : 1f - hueT), Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(26, 44));
            }
            PRTLoader.NewParticle<PRT_DWave>(targetCenter, Vector2.Zero, Color.Lerp(HueColor(hueT), Abyss, 0.45f), 0.08f)
                ?.Configure(new Vector2(1f, 0.8f), dir.ToRotation(), 0.22f, 9);
        }

        /// <summary>消散，光斑上浮散逸 + 暗环，粒子寿命长于弹体承载 aftermath</summary>
        public static void DissolveBurst(Vector2 pos, float hueT) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.8f, 1.4f) - new Vector2(0f, Main.rand.NextFloat(0.4f, 1.1f));
                PRTLoader.NewParticle<PRT_FishNeonMote>(pos + Main.rand.NextVector2Circular(8f, 8f), vel
                    , HueColor(hueT + Main.rand.NextFloat(-0.35f, 0.35f)), Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Color.Lerp(HueColor(hueT), Abyss, 0.4f), 0.1f)
                ?.Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.2f, 12);
        }

        /// <summary>巡游期环境光斑，单个缓浮 mote，随鱼残速漂出</summary>
        public static void AmbientMote(Vector2 center, Vector2 fishVel, float hueT) {
            if (Main.dedServ) {
                return;
            }
            Vector2 pos = center + Main.rand.NextVector2Circular(14f, 10f);
            Vector2 vel = fishVel * 0.2f + Main.rand.NextVector2Circular(0.4f, 0.3f);
            PRTLoader.NewParticle<PRT_FishNeonMote>(pos, vel
                , HueColor(hueT + Main.rand.NextFloat(-0.25f, 0.25f)), Main.rand.NextFloat(0.4f, 0.75f))
                ?.Configure(Main.rand.Next(45, 80));
        }
    }

    /// <summary>
    /// 深海荧光浮游光斑，浮力缓升 + 正弦横摆 + 慢呼吸明暗（生物节律，非随机火花闪烁）
    /// 速度快时顺速度拉伸成丝；SoftGlow 仅作垫底晕 + Photosphere 小芯（同色提亮，无纯白）
    /// </summary>
    internal class PRT_FishNeonMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        [VaultLoaden(CWRConstant.Masking + "Photosphere")]
        internal static Asset<Texture2D> CoreTex = null;

        private float breathSeed;
        private float swaySeed;

        public PRT_FishNeonMote Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            breathSeed = 0f;
            swaySeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            breathSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            swaySeed = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(40, 70);
            }
        }

        public override void AI() {
            //拖曳减速后浮力缓升 + 横向正弦摆游
            Velocity *= 0.94f;
            Velocity.Y -= 0.012f;
            Velocity.X += MathF.Sin(Time * 0.09f + swaySeed) * 0.014f;

            float lc = LifetimeCompletion;
            //荧光呼吸，慢正弦节律叠首尾渐入渐出
            float breath = 0.62f + 0.38f * MathF.Sin(Time * 0.12f + breathSeed);
            Opacity = MathF.Min(lc * 6f, 1f) * (1f - lc * lc) * breath;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D halo = TexValue;
            Texture2D core = CoreTex?.Value;
            Vector2 pos = Position - Main.screenPosition;
            Color col = Color with { A = 0 };

            //速度拉伸，迸发时呈丝、缓浮时圆点
            float speed = Velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.30f, 0f, 1.1f);
            float rot = Velocity.ToRotation() + MathHelper.PiOver2;
            Vector2 shape = new Vector2(1f - stretch * 0.25f, 1f + stretch) * (0.24f * Scale);

            spriteBatch.Draw(halo, pos, null, col * (0.40f * Opacity), rot, halo.Size() * 0.5f
                , shape * 2.1f, SpriteEffects.None, 0f);
            if (core != null) {
                spriteBatch.Draw(core, pos, null, col * (0.9f * Opacity), rot, core.Size() * 0.5f
                    , shape * 0.22f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
