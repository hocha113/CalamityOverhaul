using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>猩红裁决域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishBloodyManowarAssets
    {
        /// <summary>血腥水母伞膜</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishBloodyManowarBell { get; private set; }
    }

    /// <summary>猩红裁决</summary>
    internal static class FishBloodyManowarVFX
    {
        /// <summary>伞膜暗缘（近黑的瘀血色，压底）</summary>
        public static readonly Color MembraneDark = new(44, 7, 13);
        /// <summary>伞膜主体深红</summary>
        public static readonly Color Membrane = new(118, 17, 26);
        /// <summary>伞缘血色（收缩时增亮的上限）</summary>
        public static readonly Color Rim = new(168, 30, 34);
        /// <summary>内脏团微光</summary>
        public static readonly Color Organ = new(150, 36, 44);
        /// <summary>深血（滴落、雾底）</summary>
        public static readonly Color BloodDeep = new(96, 12, 18);
        /// <summary>血主色（压着的红）</summary>
        public static readonly Color Blood = new(156, 22, 28);
        /// <summary>暖色过曝，仅破裂爆点一瞬</summary>
        public static readonly Color HotFlash = new(222, 92, 62);

        /// <summary>定向震屏，尊重服务器配置；本技能所有震动统一走此入口</summary>
        public static void Punch(Vector2 pos, Vector2 dir, float strength, float vibrationsPerSec, int frames, float falloff = 800f) {
            if (Main.dedServ || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, dir.SafeNormalize(Vector2.UnitY), strength, vibrationsPerSec, frames, falloff, "FishBloodyManowar"));
        }


        /// <summary>暗血雾团</summary>
        public static void MistPuff(Vector2 pos, int count, float speed, float scale, int lifeMin, int lifeMax) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.25f, 1f) * speed;
                Color col = Color.Lerp(MembraneDark, BloodDeep, Main.rand.NextFloat(0.7f));
                PRTLoader.NewParticle<PRT_FishBloodyManowarMist>(pos + Main.rand.NextVector2Circular(10f, 8f)
                    , vel, col, Main.rand.NextFloat(0.8f, 1.25f) * scale)
                    ?.Configure(Main.rand.Next(lifeMin, lifeMax), Main.rand.NextFloat(0.5f, 0.7f));
            }
        }

        /// <summary>血珠喷洒</summary>
        public static void DropletSpray(Vector2 pos, Vector2 dir, int count, float speedMin, float speedMax, float cone, float gravity = 0.3f) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(cone) * Main.rand.NextFloat(speedMin, speedMax);
                Color col = Main.rand.NextBool(3) ? BloodDeep : Blood;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, col, Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(Main.rand.Next(22, 38), gravity);
            }
        }

        /// <summary>伞膜碎片迸散</summary>
        public static void ShredBurst(Vector2 pos, Vector2 dir, int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(-Vector2.UnitY);
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(0.5f, 1f) * speed;
                Color col = Color.Lerp(Membrane, Rim, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_FishBloodyManowarShred>(pos, vel, col, Main.rand.NextFloat(0.13f, 0.22f))
                    ?.Configure(Main.rand.Next(28, 46));
            }
        }


        /// <summary>粘稠血丝</summary>
        public static void DrawBloodThread(SpriteBatch sb, Vector2 from, Vector2 to, float sag, float alpha, float seed) {
            Texture2D tex = CWRAsset.Line?.Value;
            if (tex == null || alpha <= 0.02f) {
                return;
            }
            float dist = Vector2.Distance(from, to);
            if (dist < 8f) {
                return;
            }
            int segs = Math.Clamp((int)(dist / 15f), 4, 13);
            Vector2 mid = (from + to) * 0.5f + new Vector2(0f, dist * 0.16f * sag);
            Vector2 prev = from;
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs;
                Vector2 a = Vector2.Lerp(from, mid, t);
                Vector2 b = Vector2.Lerp(mid, to, t);
                Vector2 p = Vector2.Lerp(a, b, t);
                Vector2 seg = p - prev;
                //端部收细 + 沿程珠节
                float taperEnd = 1f - MathF.Abs(t * 2f - 1f) * 0.45f;
                float beadWobble = 0.75f + 0.4f * MathF.Sin(t * 9.3f + seed * 6.7f);
                float widthPx = 2.4f * taperEnd * beadWobble;
                Color col = Color.Lerp(BloodDeep, MembraneDark, MathF.Abs(t * 2f - 1f)) * alpha;
                sb.Draw(tex, (prev + p) * 0.5f - Main.screenPosition, null, col
                    , seg.ToRotation() + MathHelper.PiOver2, tex.Size() * 0.5f
                    , new Vector2(widthPx / tex.Width, seg.Length() / tex.Height * 1.1f), SpriteEffects.None, 0f);
                prev = p;
            }
            //沿丝滑行的血珠
            Texture2D drop = CWRAsset.Extra_98?.Value;
            if (drop != null) {
                float bt = (Main.GlobalTimeWrappedHourly * 0.8f + seed) % 1f;
                Vector2 ba = Vector2.Lerp(from, mid, bt);
                Vector2 bb = Vector2.Lerp(mid, to, bt);
                Vector2 bp = Vector2.Lerp(ba, bb, bt);
                sb.Draw(drop, bp - Main.screenPosition, null, Blood * (alpha * 0.9f)
                    , (bb - ba).ToRotation() + MathHelper.PiOver2, drop.Size() * 0.5f
                    , new Vector2(0.14f, 0.24f), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>暗血雾团</summary>
    internal class PRT_FishBloodyManowarMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float baseOpacity;
        private Color initialColor;
        private float spin;

        public PRT_FishBloodyManowarMist Configure(int lifetime, float opacity) {
            Lifetime = lifetime;
            baseOpacity = opacity;
            initialColor = Color;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            spin = Main.rand.NextFloat(-0.012f, 0.012f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            baseOpacity = 0f;
            initialColor = default;
            spin = 0f;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //前15%快速胀开，随后缓慢继续扩
            Scale += t < 0.15f ? 0.028f : 0.004f;
            Velocity *= 0.955f;
            Velocity.Y -= 0.004f;
            Rotation += spin;
            //包络
            float envelope = MathHelper.Clamp(t / 0.12f, 0f, 1f) * (1f - MathF.Pow(t, 1.6f));
            Color = Color.Lerp(initialColor, FishBloodyManowarVFX.MembraneDark, t * 0.8f);
            Opacity = baseOpacity * envelope;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity
                , Rotation, tex.Size() * 0.5f, Scale * 0.02f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>伞膜碎片</summary>
    internal class PRT_FishBloodyManowarShred : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "HitJagged01";
        public override bool CanPool => true;

        private Color initialColor;
        private float flutterPhase;
        private float flutterRate;
        private float tumble;

        public PRT_FishBloodyManowarShred Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            flutterPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            flutterRate = Main.rand.NextFloat(0.24f, 0.42f);
            tumble = Main.rand.NextFloat(-0.11f, 0.11f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            flutterPhase = 0f;
            flutterRate = 0f;
            tumble = 0f;
        }

        public override void AI() {
            //初速衰减后进入飘落
            Velocity.X *= 0.94f;
            Velocity.Y = MathF.Min(Velocity.Y * 0.94f + 0.055f, 2.6f);
            flutterPhase += flutterRate;
            Velocity.X += MathF.Sin(flutterPhase * 0.5f) * 0.05f;
            Rotation += tumble;
            tumble *= 0.985f;

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, FishBloodyManowarVFX.MembraneDark, t * 0.7f)
                * (1f - MathF.Pow(t, 2.2f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //横轴振荡=薄片翻面；接近0时几乎侧对镜头
            float face = 0.3f + 0.7f * MathF.Abs(MathF.Sin(flutterPhase));
            Vector2 scale = new Vector2(Scale * face, Scale);
            Vector2 pos = Position - Main.screenPosition;
            //暗底衬出膜厚，再叠主膜色
            spriteBatch.Draw(tex, pos + new Vector2(1f, 2f), null, FishBloodyManowarVFX.MembraneDark * (Color.A / 255f * 0.6f)
                , Rotation, tex.Size() * 0.5f, scale * 1.12f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.82f
                , Rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
