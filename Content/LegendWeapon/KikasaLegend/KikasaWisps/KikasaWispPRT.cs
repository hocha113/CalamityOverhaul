using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps
{
    /// <summary>
    /// 鬼火舌：贴根蹿起的小簇金焰——速度拉伸泪滴、明灭闪变、幽缓上浮（鬼火不窜天、不冒烟）。
    /// 层次=外琥珀辉/金体/白金芯，全 A=0 加色进 AlphaBlend 批；Extra_98 真 alpha 底只作形状承载，
    /// 单层速度拉伸，不做灰度叠层复合（设计约束）
    /// </summary>
    internal class PRT_KikasaWispFlame : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 300;

        private Color baseGold;
        private float seed;

        public PRT_KikasaWispFlame Configure(int lifetime) {
            Lifetime = lifetime;
            baseGold = Color;
            seed = Main.rand.NextFloat(20f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            baseGold = default;
            seed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 30;
            }
            if (baseGold == default) {
                baseGold = Color;
            }
        }

        public override void AI() {
            //幽缓上浮：初速被阻尼吃掉、轻托接管，升势先急后缓
            Velocity.X *= 0.965f;
            Velocity.Y = Velocity.Y * 0.955f - 0.028f;

            float t = LifetimeCompletion;
            float flick = 0.78f + 0.22f * MathF.Sin(seed * 9f + t * 34f);
            Opacity = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi) * flick;
            Scale *= 0.988f;
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.16f, 0.2f, 1.4f);
            Vector2 body = new Vector2(0.26f * (1f - stretch * 0.25f), 0.44f * (1f + stretch)) * Scale;

            //A=0 全加色：外琥珀辉 → 金体 → 白金芯（芯偏向根部，尖端只剩辉）
            Color amber = new(KikasaWisp.AmberTip.R, KikasaWisp.AmberTip.G, KikasaWisp.AmberTip.B, (byte)0);
            Color gold = new(baseGold.R, baseGold.G, baseGold.B, (byte)0);
            Color core = new(KikasaWisp.GoldCore.R, KikasaWisp.GoldCore.G, KikasaWisp.GoldCore.B, (byte)0);
            Vector2 toBase = (Rotation - MathHelper.PiOver2).ToRotationVector2() * (-3.5f * Scale);

            spriteBatch.Draw(tex, pos, null, amber * (0.40f * Opacity), Rotation, origin,
                body * new Vector2(1.65f, 1.25f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, gold * (0.85f * Opacity), Rotation, origin,
                body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + toBase, null, core * (0.55f * Opacity), Rotation, origin,
                body * new Vector2(0.50f, 0.55f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 离体鬼火珠：脱火上浮的游魂灯——正弦游移、速度向淡拖尾、呼吸明灭、末段一缩即灭。
    /// dying 形态（鬼雨压制中出逃）升得急、喘得凶、灭得快
    /// </summary>
    internal class PRT_KikasaWispOrb : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 260;

        private Color baseGold;
        private float swayAmp;
        private float seed;
        private bool dying;

        public PRT_KikasaWispOrb Configure(int lifetime, float sway, bool dyingMode = false) {
            Lifetime = lifetime;
            baseGold = Color;
            swayAmp = sway;
            dying = dyingMode;
            seed = Main.rand.NextFloat(20f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            baseGold = default;
            swayAmp = 0f;
            seed = 0f;
            dying = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 90;
            }
            if (baseGold == default) {
                baseGold = Color;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //游移：横向正弦找不着方向，纵向缓浮；濒死珠被热浪抬得急
            Velocity.X = Velocity.X * 0.94f + MathF.Sin(seed * 11f + t * (dying ? 30f : 12f)) * swayAmp * 0.09f;
            float lift = dying ? 0.050f : 0.016f;
            float cap = dying ? -3.6f : -1.6f;
            Velocity.Y = MathF.Max(Velocity.Y * 0.985f - lift, cap);

            float breathe = dying
                ? 0.55f + 0.45f * MathF.Sin(seed * 7f + t * 66f)
                : 0.80f + 0.20f * MathF.Sin(seed * 7f + t * 15f);
            Opacity = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi) * breathe;
            //末段一缩即灭
            if (t > 0.82f) {
                Scale *= 0.93f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float speed = Velocity.Length();

            Color amber = new(KikasaWisp.AmberTip.R, KikasaWisp.AmberTip.G, KikasaWisp.AmberTip.B, (byte)0);
            Color gold = new(baseGold.R, baseGold.G, baseGold.B, (byte)0);
            Color core = new(KikasaWisp.GoldCore.R, KikasaWisp.GoldCore.G, KikasaWisp.GoldCore.B, (byte)0);

            //速度向淡拖尾：珠不是静止光点，是被气流拖着走的灯
            float tailLen = 0.30f + speed * 0.22f;
            spriteBatch.Draw(tex, pos - Velocity * 2.2f, null, amber * (0.22f * Opacity), Rotation, origin,
                new Vector2(0.16f, tailLen) * Scale, SpriteEffects.None, 0f);
            //珠体双色：金晕 + 白金芯
            spriteBatch.Draw(tex, pos, null, gold * (0.70f * Opacity), 0f, origin,
                new Vector2(0.30f, 0.32f) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, core * (0.60f * Opacity), 0f, origin,
                new Vector2(0.15f, 0.16f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
