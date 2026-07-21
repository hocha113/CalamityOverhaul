using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>热带梭鱼域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishBarracudaAssets
    {
        /// <summary>白沫水射流条带，横穿呼啸段的速度尾迹</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishBarracudaJet { get; private set; }
    }

    /// <summary>热带梭鱼 VFX，异于 FishSwarm 银鳞群、FishNeonTetra 霓虹</summary>
    internal static class FishBarracudaVFX
    {
        /// <summary>深礁青（压底/外缘/暗雾）</summary>
        public static readonly Color SeaDeep = new(10, 48, 62);
        /// <summary>绿松石（主色中层）</summary>
        public static readonly Color Turquoise = new(38, 196, 182);
        /// <summary>珊瑚橙（条纹/破水暖闪，小面积瞬时）</summary>
        public static readonly Color Coral = new(255, 126, 70);
        /// <summary>柠檬黄（条纹点缀）</summary>
        public static readonly Color Lemon = new(255, 222, 96);
        /// <summary>海沫（亮芯与水珠，非纯白）</summary>
        public static readonly Color Foam = new(210, 244, 238);

        /// <summary>三色条纹轮换，青绿 → 珊瑚橙 → 柠檬黄</summary>
        public static Color Stripe(int i) => (i % 3) switch {
            0 => Turquoise,
            1 => Coral,
            _ => Lemon
        };

        /// <summary>FishBarracudaJet 标准参数；phase 传鱼 whoAmI 派生量防多鱼同相</summary>
        public static void ApplyJet(Effect fx, float phase) {
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.2f + phase);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDark"]?.SetValue(SeaDeep.ToVector3());
            fx.Parameters["uColMid"]?.SetValue(Turquoise.ToVector3());
            fx.Parameters["uColFoam"]?.SetValue(Foam.ToVector3());
        }

        /// <summary>入场预告，屏缘涌动线一条 + 沿线内漂气泡数枚，时长与鱼群出闸对齐</summary>
        public static void EdgeTelegraph(Vector2 lineCenter, Vector2 tangent, Vector2 inward, float length, int ticks) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishBarracudaSurge>(lineCenter, Vector2.Zero, Turquoise, 1f)
                ?.Configure(tangent, inward, length, ticks);
            for (int i = 0; i < 6; i++) {
                Vector2 pos = lineCenter + tangent * Main.rand.NextFloat(-0.5f, 0.5f) * length;
                PRTLoader.NewParticle<PRT_FishBarracudaBubble>(pos, inward * Main.rand.NextFloat(0.4f, 1.2f)
                    , Foam, Main.rand.NextFloat(0.08f, 0.14f))?.Configure(Main.rand.Next(24, 40));
            }
        }

        /// <summary>破水/化水水花，压扁暗环 + 前向水珠扇 + 海沫细珠 + 悬浮气泡，scale 0.4~1.2</summary>
        public static void BurstSplash(Vector2 pos, Vector2 dir, float scale) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, SeaDeep * 0.85f, 0.07f * scale)
                ?.Configure(new Vector2(1f, 0.55f), dir.ToRotation(), 0.42f * scale, 11);
            if (scale > 0.8f) {
                PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Foam * 0.5f, 0.05f * scale)
                    ?.Configure(new Vector2(1f, 0.5f), dir.ToRotation(), 0.3f * scale, 9);
            }
            int drops = (int)(4 * scale) + 2;
            for (int i = 0; i < drops; i++) {
                Vector2 vel = dir.RotatedByRandom(0.7f) * Main.rand.NextFloat(2.5f, 6.5f) * scale
                    - Vector2.UnitY * Main.rand.NextFloat(1.8f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel
                    , Color.Lerp(Turquoise, SeaDeep, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.6f, 1f) * scale)
                    ?.Configure(Main.rand.Next(18, 28));
            }
            for (int i = 0; i < 2; i++) {
                Vector2 vel = dir.RotatedByRandom(1f) * Main.rand.NextFloat(1.8f, 4f) * scale;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Foam, Main.rand.NextFloat(0.35f, 0.5f) * scale)
                    ?.Configure(Main.rand.Next(10, 16), 0.09f, 0.93f);
            }
            int bubbles = (int)(2 * scale) + 1;
            for (int i = 0; i < bubbles; i++) {
                PRTLoader.NewParticle<PRT_FishBarracudaBubble>(pos + Main.rand.NextVector2Circular(10f, 10f)
                    , dir * Main.rand.NextFloat(0.5f, 1.5f), Foam, Main.rand.NextFloat(0.08f, 0.13f))
                    ?.Configure(Main.rand.Next(26, 44));
            }
            if (scale >= 0.8f) {
                PRTLoader.NewParticle<PRT_Smoke>(pos, dir * 0.8f + Main.rand.NextVector2Circular(0.5f, 0.5f)
                    , Color.Lerp(SeaDeep, Turquoise, 0.35f), 0.15f)?.Configure(30, 0.24f, 0.012f);
            }
        }

        /// <summary>穿体喷溅，沿冲刺方向的水珠锥 + 条纹色锐线 + 顺行进压扁波环 + 一撮悬雾，ke 0..1 动能系数</summary>
        public static void ImpactSpray(Vector2 pos, Vector2 vel, Color stripe, float ke) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = vel.SafeNormalize(Vector2.UnitX);
            int drops = (int)(3 + 3 * ke);
            for (int i = 0; i < drops; i++) {
                Vector2 v = dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(3f, 7f + 5f * ke)
                    - Vector2.UnitY * Main.rand.NextFloat(1.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v
                    , Color.Lerp(Turquoise, SeaDeep, Main.rand.NextFloat(0.55f)), Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(20, 32));
            }
            for (int i = 0; i < 2; i++) {
                Vector2 v = dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 4.5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, v, Foam, Main.rand.NextFloat(0.35f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 18), 0.09f, 0.93f);
            }
            int lines = 2 + (int)(2 * ke);
            for (int i = 0; i < lines; i++) {
                Vector2 v = dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(5f, 10f + 4f * ke);
                PRTLoader.NewParticle<PRT_Spark>(pos, v, stripe, Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(true, Main.rand.Next(10, 15));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, Turquoise * 0.6f, 0.06f)
                ?.Configure(new Vector2(1f, 0.5f), dir.ToRotation(), 0.34f, 10);
            PRTLoader.NewParticle<PRT_Smoke>(pos, dir * 0.6f + Main.rand.NextVector2Circular(0.5f, 0.5f)
                , Color.Lerp(SeaDeep, Turquoise, 0.3f), 0.13f)?.Configure(24, 0.22f, 0.015f);
            PRTLoader.NewParticle<PRT_FishBarracudaBubble>(pos, -dir * Main.rand.NextFloat(0.5f, 1f)
                , Foam, Main.rand.NextFloat(0.08f, 0.12f))?.Configure(Main.rand.Next(24, 38));
        }
    }

    /// <summary>
    /// 屏缘涌动预告线，鱼群入场侧的水压前锋，宽暗底光 + 主涌动带 + 两道向屏内推进的
    /// 波纹副线 + 沿线海沫闪点，亮度随寿命渐强（预期感），到点即被破水帧接管，纯程序化
    /// </summary>
    internal class PRT_FishBarracudaSurge : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Vector2 tangent;
        private Vector2 inward;
        private float length;
        private float seed;

        public PRT_FishBarracudaSurge Configure(Vector2 tangentDir, Vector2 inwardDir, float lineLength, int lifetimeTicks) {
            tangent = tangentDir.SafeNormalize(Vector2.UnitY);
            inward = inwardDir.SafeNormalize(Vector2.UnitX);
            length = lineLength;
            Lifetime = lifetimeTicks;
            return this;
        }

        public override void Reset() {
            base.Reset();
            tangent = inward = Vector2.Zero;
            length = 0f;
            seed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Velocity = Vector2.Zero;
            seed = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            //水压前锋缓慢压向屏内
            Position += inward * 0.4f;
            //渐强包络，预告越接近出闸越亮
            Opacity = MathF.Pow(LifetimeCompletion, 1.4f);
            //沿线零星内漂气泡
            if (Time % 5 == 0) {
                Vector2 pos = Position + tangent * Main.rand.NextFloat(-0.5f, 0.5f) * length;
                PRTLoader.NewParticle<PRT_FishBarracudaBubble>(pos, inward * Main.rand.NextFloat(0.5f, 1.4f)
                    , FishBarracudaVFX.Foam, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(Main.rand.Next(18, 30));
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D streak = CWRAsset.Extra_98?.Value;
            Texture2D soft = CWRAsset.SoftGlow?.Value;
            Texture2D glint = CWRAsset.StarGlow01?.Value;
            if (streak == null || soft == null || length <= 1f) {
                return false;
            }
            Vector2 mid = Position - Main.screenPosition;
            float lineRot = tangent.ToRotation();
            float streakRot = lineRot + MathHelper.PiOver2;
            Vector2 streakOrigin = streak.Size() * 0.5f;
            float lenScale = length / streak.Height;

            //底光，宽而暗的深礁青雾带，只作压底
            spriteBatch.Draw(soft, mid, null, FishBarracudaVFX.SeaDeep with { A = 0 } * (0.55f * Opacity)
                , lineRot, soft.Size() / 2f, new Vector2(length / soft.Width, 96f / soft.Height), SpriteEffects.None, 0f);

            //主涌动带，绿松石
            spriteBatch.Draw(streak, mid, null, FishBarracudaVFX.Turquoise with { A = 0 } * (0.5f * Opacity)
                , streakRot, streakOrigin, new Vector2(24f / streak.Width, lenScale), SpriteEffects.None, 0f);

            //两道推进副线
            for (int k = 0; k < 2; k++) {
                float push = (6f + 5f * k) + MathF.Sin(Time * 0.45f + seed + k * 2.1f) * 4.5f;
                Color c = (k == 0 ? FishBarracudaVFX.Foam : FishBarracudaVFX.Turquoise) with { A = 0 };
                spriteBatch.Draw(streak, mid + inward * push, null, c * ((0.42f - 0.14f * k) * Opacity)
                    , streakRot, streakOrigin, new Vector2((10f - 3f * k) / streak.Width, lenScale * (0.92f - 0.1f * k))
                    , SpriteEffects.None, 0f);
            }

            //海沫闪点
            if (glint != null) {
                for (int i = 0; i < 4; i++) {
                    float t = 0.12f + 0.253f * i;
                    float jitter = MathF.Sin(Time * 0.6f + seed + t * 17f) * 5f;
                    Vector2 pos = mid + tangent * (t - 0.5f) * length + inward * (3f + jitter);
                    float twinkle = 0.4f + 0.35f * MathF.Sin(Time * 0.8f + i * 1.9f + seed);
                    Color c = (i % 2 == 0 ? FishBarracudaVFX.Foam : FishBarracudaVFX.Lemon) with { A = 0 };
                    spriteBatch.Draw(glint, pos, null, c * (twinkle * Opacity), 0f
                        , glint.Size() / 2f, 15f / glint.Width, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 海水气泡，缓升 + 横向摆动，末段轻微鼓胀后破裂消散
    /// DiffusionCircle6 三层（外圈/内芯/偏置高光点），呼啸余波与预告内漂共用，纯程序化
    /// </summary>
    internal class PRT_FishBarracudaBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle6";
        public override bool CanPool => true;

        private float wobblePhase;
        private float rise;

        public PRT_FishBarracudaBubble Configure(int lifetime, float risePerFrame = 0.045f) {
            Lifetime = lifetime;
            rise = risePerFrame;
            return this;
        }

        public override void Reset() {
            base.Reset();
            wobblePhase = 0f;
            rise = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(24, 40);
            }
            if (rise == 0f) {
                rise = 0.045f;
            }
        }

        public override void AI() {
            wobblePhase += 0.2f;
            Velocity.X = Velocity.X * 0.95f + MathF.Sin(wobblePhase) * 0.05f;
            Velocity.Y = MathF.Max(Velocity.Y - rise, -1.3f);

            float lc = LifetimeCompletion;
            //浮升渐显，末段鼓胀破裂
            Opacity = MathF.Min(lc * 6f, 1f);
            if (lc > 0.86f) {
                Opacity *= (1f - lc) / 0.14f;
                Scale *= 1.05f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            Color rim = Color with { A = 0 };

            spriteBatch.Draw(tex, pos, null, rim * (0.5f * Opacity), 0f, origin, Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, FishBarracudaVFX.Turquoise with { A = 0 } * (0.3f * Opacity)
                , 0f, origin, Scale * 0.55f, SpriteEffects.None, 0f);
            //偏置高光点
            Vector2 highlight = new Vector2(-tex.Width, -tex.Width) * Scale * 0.16f;
            spriteBatch.Draw(tex, pos + highlight, null, rim * (0.85f * Opacity), 0f, origin, Scale * 0.2f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 残留白沫水痕，破水口与鱼身化水处多活 10~18 帧的沫线
    /// 尾端先蚀（尾点向头点回缩）+ 头尾不同速沉降；Extra_98 三层同轴
    /// 深礁青宽晕 / 绿松石中层 / 海沫细芯（芯先熄），纯程序化
    /// </summary>
    internal class PRT_FishBarracudaWake : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Vector2 head;
        private Vector2 tail;
        private float width;

        public PRT_FishBarracudaWake Configure(Vector2 headPos, Vector2 tailPos, float lineWidth, int lifetime) {
            head = headPos;
            tail = tailPos;
            width = lineWidth;
            Lifetime = lifetime;
            Position = (headPos + tailPos) * 0.5f;
            return this;
        }

        public override void Reset() {
            base.Reset();
            head = tail = Vector2.Zero;
            width = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            float lc = LifetimeCompletion;
            Opacity = MathF.Pow(1f - lc, 1.5f);
            //尾端先蚀，沫线从最旧端消散
            tail = Vector2.Lerp(tail, head, 0.05f);
            //沫水沉降
            head.Y += 0.1f;
            tail.Y += 0.18f;
            Position = (head + tail) * 0.5f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D streak = CWRAsset.Extra_98?.Value;
            if (streak == null || head == tail) {
                return false;
            }
            float lc = LifetimeCompletion;
            Vector2 delta = head - tail;
            Vector2 mid = Position - Main.screenPosition;
            float rot = delta.ToRotation() + MathHelper.PiOver2;
            Vector2 texOrigin = streak.Size() * 0.5f;
            float lenScale = delta.Length() / streak.Height;
            float xScale = width * (1f - lc * 0.4f) / streak.Width;

            Color dark = FishBarracudaVFX.SeaDeep with { A = 0 };
            Color midCol = (Color == default ? FishBarracudaVFX.Turquoise : Color) with { A = 0 };
            Color foam = FishBarracudaVFX.Foam with { A = 0 };
            //海沫芯先熄，亮芯只存在于最初几帧
            float coreOpacity = MathF.Pow(1f - lc, 3.2f);

            spriteBatch.Draw(streak, mid, null, dark * (0.45f * Opacity), rot, texOrigin
                , new Vector2(xScale * 2.2f, lenScale), SpriteEffects.None, 0f);
            spriteBatch.Draw(streak, mid, null, midCol * (0.75f * Opacity), rot, texOrigin
                , new Vector2(xScale, lenScale), SpriteEffects.None, 0f);
            spriteBatch.Draw(streak, mid, null, foam * (0.85f * coreOpacity), rot, texOrigin
                , new Vector2(xScale * 0.3f, lenScale), SpriteEffects.None, 0f);
            return false;
        }
    }
}
