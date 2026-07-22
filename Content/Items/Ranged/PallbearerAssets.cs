using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>抬棺人域内shader，不经EffectLoader</summary>
    internal class PallbearerAssets
    {
        /// <summary>落棺棺体SDF</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect PallbearerSeal { get; private set; }

        /// <summary>血红拖尾，钉/掷棺共用</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect PallbearerTrail { get; private set; }
    }

    /// <summary>
    /// 抬棺人VFX。色板仅黑+深红，暖色只许小面积瞬时过曝
    /// </summary>
    internal static class PallbearerVFX
    {
        //色板
        /// <summary>焦黑底</summary>
        public static readonly Color CharDark = new(13, 10, 9);
        /// <summary>焦黑板面</summary>
        public static readonly Color Charcoal = new(32, 22, 18);
        /// <summary>深红暗部</summary>
        public static readonly Color BloodDeep = new(94, 12, 16);
        /// <summary>血色主色</summary>
        public static readonly Color Blood = new(190, 32, 30);
        /// <summary>暖橙余烬，小面积瞬时过曝</summary>
        public static readonly Color Ember = new(236, 98, 44);

        //打击链

        /// <summary>定向震屏统一入口</summary>
        public static void Punch(Vector2 pos, Vector2 dir, float strength, float vibrationsPerSec, int frames, float falloff = 800f) {
            if (Main.dedServ || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                pos, dir.SafeNormalize(Vector2.UnitY), strength, vibrationsPerSec, frames, falloff, "Pallbearer"));
        }

        //音效

        /// <summary>落棺钟鸣，depth 0..1 沉度</summary>
        public static void BellToll(Vector2 pos, float depth, float volume = 0.85f) {
            SoundEngine.PlaySound(SoundID.Item35 with {
                Pitch = -0.45f - 0.45f * depth,
                Volume = volume,
                MaxInstances = 3
            }, pos);
            SoundEngine.PlaySound(SoundID.Dig with {
                Pitch = -0.75f,
                Volume = volume * 0.5f,
                MaxInstances = 3
            }, pos);
        }

        /// <summary>命中闷击，marked附轻钟</summary>
        public static void NailThunk(Vector2 pos, bool marked) {
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.65f, Volume = 0.92f, MaxInstances = 5 }, pos);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.45f, Volume = 0.5f, MaxInstances = 5 }, pos);
            if (marked) {
                SoundEngine.PlaySound(SoundID.Item35 with { Pitch = -0.25f, Volume = 0.3f, MaxInstances = 3 }, pos);
            }
        }

        //粒子

        /// <summary>
        /// 血爆迸溅。ke 0..1 动能
        /// </summary>
        public static void BloodBurst(Vector2 pos, Vector2 dir, float ke, bool marked = false) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            int sparkCount = (int)((7 + 7 * ke) * (marked ? 1.5f : 1f));
            for (int i = 0; i < sparkCount; i++) {
                //迸溅锥
                Vector2 vel = Main.rand.NextBool(4)
                    ? -dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 5f)
                    : dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(3f, 8f + 6f * ke);
                Color col = Main.rand.NextBool(3) ? BloodDeep : Blood;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, col, Main.rand.NextFloat(0.55f, 1f))
                    ?.Configure(true, Main.rand.Next(14, 24));
            }
            int emberCount = (int)(3 + 4 * ke);
            for (int i = 0; i < emberCount; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(pos, dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4.5f)
                    , Blood, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(16, 28));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(Vector2.One, dir.ToRotation(), 0.34f + 0.2f * ke, 10);
        }

        /// <summary>余烬迸发，大节点用</summary>
        public static void EmberBurst(Vector2 pos, int count, float speed, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.35f, 1f) * speed;
                PRTLoader.NewParticle<PRT_PallbearerEmber>(pos + Main.rand.NextVector2Circular(8f, 8f), vel
                    , Main.rand.NextBool(3) ? BloodDeep : Blood, Main.rand.NextFloat(0.6f, 1.05f) * scale)
                    ?.Configure(Main.rand.Next(18, 32));
            }
        }

        /// <summary>焦黑木屑+黑烟</summary>
        public static void Splinters(Vector2 pos, Vector2 dir, int count, float speed) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedByRandom(0.65f) * Main.rand.NextFloat(0.4f, 1f) * speed;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Charcoal, Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(true, Main.rand.Next(16, 26));
            }
            for (int i = 0; i < count / 2; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Smoke, dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(1f, 3f)
                    , 180, CharDark, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        //shader装配

        /// <summary>Trail参数，phase用whoAmI派生错相</summary>
        public static void ApplyTrail(Effect fx, float phase) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 0.9f + phase);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColCore"]?.SetValue(Blood.ToVector3());
            fx.Parameters["uColEdge"]?.SetValue(BloodDeep.ToVector3());
            fx.Parameters["uColDark"]?.SetValue(CharDark.ToVector3());
        }

        //锁链

        /// <summary>
        /// 分段链环，taut 0..1绷直度，亮环沿time奔向to
        /// </summary>
        public static void DrawChain(SpriteBatch sb, Vector2 from, Vector2 to, float taut, float alpha, float time) {
            Texture2D tex = CWRAsset.Line?.Value;
            if (tex == null || alpha <= 0.01f) {
                return;
            }
            float dist = Vector2.Distance(from, to);
            if (dist < 4f) {
                return;
            }
            int segs = Math.Clamp((int)(dist / 11f), 4, 42);
            //垂弧中点
            float sagAmount = dist * 0.18f * (1f - taut);
            Vector2 mid = (from + to) * 0.5f + new Vector2(0f, sagAmount);
            float glintT = time % 1f; //亮环相位
            Vector2 prev = from;
            for (int i = 1; i <= segs; i++) {
                float t = i / (float)segs;
                //二次贝塞尔
                Vector2 a = Vector2.Lerp(from, mid, t);
                Vector2 b = Vector2.Lerp(mid, to, t);
                Vector2 p = Vector2.Lerp(a, b, t);
                Vector2 seg = p - prev;
                float rot = seg.ToRotation() + MathHelper.PiOver2;
                //明暗交替
                Color linkCol = (i & 1) == 0 ? BloodDeep : CharDark;
                float glint = MathF.Exp(-MathF.Pow((t - glintT) * 7f, 2f));
                Color col = Color.Lerp(linkCol, Blood, glint * 0.9f) * alpha;
                float linkScale = 9f / tex.Height;
                Vector2 drawPos = (prev + p) * 0.5f - Main.screenPosition;
                sb.Draw(tex, drawPos, null, col, rot, tex.Size() * 0.5f
                    , new Vector2(linkScale * ((i & 1) == 0 ? 2.4f : 1.5f), seg.Length() / tex.Height * 1.15f), SpriteEffects.None, 0);
                prev = p;
            }
        }

        //数学

        public static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - MathHelper.Clamp(x, 0f, 1f), 3f);

        /// <summary>过冲缓出</summary>
        public static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
