using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>虹彩序曲域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishJewelAssets
    {
        /// <summary>宝石窄条带拖尾</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishJewelTrail { get; private set; }

        /// <summary>四向星芒</summary>
        [VaultLoaden(CWRConstant.Masking + "RayCross01")]
        internal static Asset<Texture2D> RayCross = null;
    }

    /// <summary>虹彩序曲</summary>
    internal static class FishJewelVFX
    {
        /// <summary>单色宝石三件套</summary>
        internal readonly struct JewelPalette
        {
            public readonly Color Deep;
            public readonly Color Bright;
            public readonly Color Glint;
            public JewelPalette(Color deep, Color bright, Color glint) {
                Deep = deep;
                Bright = bright;
                Glint = glint;
            }
        }

        private static readonly JewelPalette[] palettes = [
            new(new Color(126, 12, 30), new Color(236, 52, 70), new Color(255, 216, 216)),
            new(new Color(16, 34, 132), new Color(64, 104, 244), new Color(216, 228, 255)),
            new(new Color(10, 100, 46), new Color(52, 206, 110), new Color(216, 255, 230)),
            new(new Color(152, 90, 12), new Color(246, 184, 56), new Color(255, 242, 210)),
            new(new Color(90, 24, 132), new Color(180, 88, 240), new Color(242, 220, 255)),
            new(new Color(58, 86, 104), new Color(172, 220, 236), new Color(242, 252, 255)),
        ];

        public static JewelPalette Palette(int gemType) => palettes[Math.Clamp(gemType, 0, palettes.Length - 1)];

        /// <summary>FishJewelTrail 标准参数；phase 传弹幕 whoAmI 派生量避免多条拖尾同相</summary>
        public static void ApplyTrail(Effect fx, int gemType, float phase) {
            JewelPalette pal = Palette(gemType);
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.8f + phase);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDark"]?.SetValue(pal.Deep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(pal.Bright.ToVector3());
            fx.Parameters["uColGlint"]?.SetValue(pal.Glint.ToVector3());
        }

        /// <summary>定向震屏，尊重服务器配置；虹彩序曲所有震动统一走此入口</summary>
        public static void Punch(Vector2 pos, Vector2 dir, float strength, float vibrationsPerSec, int frames) {
            if (Main.dedServ || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, dir.SafeNormalize(Vector2.UnitY), strength, vibrationsPerSec, frames, 620f, "FishJewel"));
        }

        /// <summary>同色碎晶锥</summary>
        public static void ShardBurst(Vector2 pos, Vector2 dir, int gemType, int count, float speed, float cone) {
            if (Main.dedServ) {
                return;
            }
            Vector2 baseDir = dir.SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                //上抛分量让抛物线读得出来
                Vector2 vel = baseDir.RotatedByRandom(cone) * Main.rand.NextFloat(0.45f, 1f) * speed
                    - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.6f);
                PRTLoader.NewParticle<PRT_FishJewelShard>(pos + Main.rand.NextVector2Circular(4f, 4f), vel
                    , default, Main.rand.NextFloat(0.7f, 1.15f))?.Configure(gemType, Main.rand.Next(20, 34));
            }
        }

        /// <summary>单枚玻白星闪</summary>
        public static void GlintStar(Vector2 pos, int gemType, float scale) {
            if (Main.dedServ) {
                return;
            }
            JewelPalette pal = Palette(gemType);
            PRTLoader.NewParticle<PRT_Sparkle>(pos, Vector2.Zero, pal.Glint, scale)
                ?.Configure(pal.Bright * 0.55f, 14, 0.02f, 0.7f);
        }

        /// <summary>出膛</summary>
        public static void LaunchBurst(Vector2 pos, Vector2 dir, int gemType, bool accent) {
            if (Main.dedServ) {
                return;
            }
            JewelPalette pal = Palette(gemType);
            Vector2 d = dir.SafeNormalize(Vector2.UnitX);
            float k = accent ? 1.4f : 1f;
            PRTLoader.NewParticle<PRT_DWave>(pos + d * 10f, Vector2.Zero, pal.Bright * 0.75f, 0.08f)
                ?.Configure(new Vector2(1.15f, 0.6f), d.ToRotation(), 0.24f * k, 9);
            ShardBurst(pos + d * 6f, d * 4.5f, gemType, accent ? 5 : 3, 4.2f, 0.5f);
            GlintStar(pos + d * 8f, gemType, accent ? 0.85f : 0.6f);
            int dustCount = accent ? 7 : 5;
            for (int i = 0; i < dustCount; i++) {
                Dust dust = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(8f, 8f)
                    , DustID.GemTopaz + gemType, d.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.5f, 4f)
                    , 120, pal.Bright, Main.rand.NextFloat(0.9f, 1.4f) * k);
                dust.noGravity = true;
            }
        }

        /// <summary>六色循环完成拍</summary>
        public static void SequenceFan(Vector2 pos, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            Vector2 d = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < palettes.Length; i++) {
                float ang = MathHelper.Lerp(-0.85f, 0.85f, i / (float)(palettes.Length - 1));
                Vector2 vel = d.RotatedBy(ang) * Main.rand.NextFloat(3.2f, 4.2f);
                JewelPalette pal = palettes[i];
                PRTLoader.NewParticle<PRT_Sparkle>(pos, vel, pal.Glint, Main.rand.NextFloat(0.5f, 0.62f))
                    ?.Configure(pal.Bright * 0.55f, 26, 0.05f, 0.75f);
            }
        }

        /// <summary>命中</summary>
        public static void ImpactBurst(Vector2 pos, Vector2 incident, int gemType, bool accent) {
            if (Main.dedServ) {
                return;
            }
            JewelPalette pal = Palette(gemType);
            ShardBurst(pos, incident.SafeNormalize(Vector2.UnitX) * 4f, gemType, accent ? 8 : 6, 5f, 0.65f);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, pal.Bright * 0.7f, 0.07f)
                ?.Configure(Vector2.One, 0f, accent ? 0.3f : 0.22f, 8);
            GlintStar(pos, gemType, accent ? 0.85f : 0.6f);
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(pos, DustID.GemTopaz + gemType
                    , Main.rand.NextVector2Circular(3f, 3f), 140, pal.Bright, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }

        /// <summary>弹体死亡的破碎爆发</summary>
        public static void ShatterBurst(Vector2 pos, Vector2 lastVelocity, int gemType, int shardCount) {
            if (Main.dedServ) {
                return;
            }
            JewelPalette pal = Palette(gemType);
            //破碎主锥顺残余速度，附带全向少量
            ShardBurst(pos, lastVelocity.SafeNormalize(Vector2.UnitX) * 3.5f, gemType, shardCount, 4.6f, 0.9f);
            ShardBurst(pos, -Vector2.UnitY * 2f, gemType, 2, 3f, MathHelper.Pi);
            GlintStar(pos, gemType, 0.75f);
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(pos, DustID.GemTopaz + gemType
                    , Main.rand.NextVector2Circular(4f, 4f), 140, pal.Bright, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
        }

        /// <summary>沿 oldPos 铺驻留光痕</summary>
        public static void RibbonResidue(Projectile proj, int gemType) {
            if (Main.dedServ || proj.oldPos == null || proj.oldPos.Length < 5) {
                return;
            }
            const int step = 4;
            int idx = 0;
            for (int i = step; i < proj.oldPos.Length; i += step) {
                if (proj.oldPos[i] == Vector2.Zero) {
                    break;
                }
                Vector2 a = proj.oldPos[i - step] + proj.Size * 0.5f;
                Vector2 b = proj.oldPos[i] + proj.Size * 0.5f;
                float segLen = Vector2.Distance(a, b);
                if (segLen < 3f) {
                    continue;
                }
                int life = 16 - idx * 3;
                if (life <= 4) {
                    break;
                }
                PRTLoader.NewParticle<PRT_FishJewelGlint>((a + b) * 0.5f, Vector2.Zero, default, 1f)
                    ?.Configure(gemType, life, (b - a).ToRotation(), segLen);
                idx++;
            }
        }
    }

    /// <summary>切割碎晶</summary>
    internal class PRT_FishJewelShard : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 2000;

        private Color deep;
        private Color bright;
        private Color glintCol;
        private float spin;
        private float glintPhase;
        private float glintSpeed;
        private float baseScale;

        public PRT_FishJewelShard Configure(int gemType, int lifetime) {
            FishJewelVFX.JewelPalette pal = FishJewelVFX.Palette(gemType);
            deep = pal.Deep;
            bright = pal.Bright;
            glintCol = pal.Glint;
            Lifetime = lifetime;
            baseScale = Scale;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.16f, 0.3f) * (Main.rand.NextBool() ? 1f : -1f);
            glintPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            glintSpeed = Main.rand.NextFloat(0.35f, 0.6f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            deep = default;
            bright = default;
            glintCol = default;
            spin = 0f;
            glintPhase = 0f;
            glintSpeed = 0.5f;
            baseScale = 0f;
        }

        public override void AI() {
            //晶片抛物
            Velocity = new Vector2(Velocity.X * 0.965f, Math.Min(Velocity.Y + 0.24f, 13f));
            Rotation += spin;
            float t = LifetimeCompletion;
            Scale = baseScale * (1f - t * 0.35f);
            Opacity = 1f - MathF.Pow(t, 3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 scale = new Vector2(0.18f, 0.46f) * Scale;

            //翻滚镜面反光
            float glint = MathF.Pow(MathF.Abs(MathF.Sin(Time * glintSpeed + glintPhase)), 14f);

            //旋转拖影
            Color smear = bright with { A = 0 };
            for (int i = 2; i >= 1; i--) {
                spriteBatch.Draw(tex, pos - Velocity * (i * 0.7f), null, smear * (0.3f / i * Opacity)
                    , Rotation - spin * i * 2.6f, origin, scale * (1f - i * 0.1f), SpriteEffects.None, 0f);
            }
            //暗体色薄片本体 + 稍窄亮边
            spriteBatch.Draw(tex, pos, null, deep * (0.95f * Opacity), Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, bright * (0.85f * Opacity), Rotation, origin
                , scale * new Vector2(0.5f, 0.82f), SpriteEffects.None, 0f);
            //瞬时反光核
            if (glint > 0.2f) {
                spriteBatch.Draw(tex, pos, null, (glintCol with { A = 0 }) * (glint * Opacity), Rotation, origin
                    , scale * new Vector2(0.3f, 0.66f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>棱面反光残迹</summary>
    internal class PRT_FishJewelGlint : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 2000;

        private Color deep;
        private Color bright;
        private float segLen;

        /// <summary>pathRotation 为条痕走向，segLen 为像素长度</summary>
        public PRT_FishJewelGlint Configure(int gemType, int lifetime, float pathRotation, float segLen) {
            FishJewelVFX.JewelPalette pal = FishJewelVFX.Palette(gemType);
            deep = pal.Deep;
            bright = pal.Bright;
            Lifetime = lifetime;
            Rotation = pathRotation + MathHelper.PiOver2;
            this.segLen = segLen;
            return this;
        }

        public override void Reset() {
            base.Reset();
            deep = default;
            bright = default;
            segLen = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.86f;
            float t = LifetimeCompletion;
            Opacity = MathF.Min(t * 6f, 1f) * (1f - t * t);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float t = LifetimeCompletion;
            //长度随生命收缩，残光向中心熄灭
            float len = segLen * (1f - t * 0.55f) / tex.Height;
            Vector2 wide = new Vector2(0.09f, len) * Scale;

            spriteBatch.Draw(tex, pos, null, deep * (0.55f * Opacity), Rotation, origin, wide, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, bright * (0.8f * Opacity), Rotation, origin
                , wide * new Vector2(0.45f, 0.92f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
