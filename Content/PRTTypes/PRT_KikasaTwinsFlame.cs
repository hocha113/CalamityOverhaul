using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaTwins;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 双子鬼奴的血焰舌：燃烧的液血，不是气体火
    /// 前段热浮上窜、中段膨大翻卷、后段烧尽转暗坠落（血比火重）。
    /// 暗缘压边给体积、鲜活期白热芯湿反光，Extra_98 真 alpha 主体 + A=0 加色芯
    /// </summary>
    internal class PRT_KikasaTwinsFlame : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 600;

        private Color initialColor;
        private float buoyancy;
        private float baseScale;
        private float flickerSeed;

        public PRT_KikasaTwinsFlame Configure(int lifetime, float buoyancyPerFrame) {
            Lifetime = lifetime;
            buoyancy = buoyancyPerFrame;
            initialColor = Color;
            baseScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            buoyancy = 0f;
            baseScale = 0f;
            flickerSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            flickerSeed = Main.rand.NextFloat(10f);
            if (Lifetime <= 0) {
                Lifetime = 22;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
            if (baseScale <= 0f) {
                baseScale = Scale;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;

            //前段热浮、后段烧尽的血坠回
            Velocity *= 0.915f;
            Velocity.Y += t > 0.55f ? 0.09f : -buoyancy;

            //先胀后缩的翻卷体量
            Scale = baseScale * (0.7f + 0.95f * MathF.Sin(MathF.Min(t * 1.15f, 1f) * MathHelper.Pi));
            Rotation += MathF.Sin(flickerSeed + t * 9f) * 0.06f;

            //鲜血亮 → 焦血暗，透明度尾段陡降
            Color = Color.Lerp(initialColor, KikasaTwinsServant.BloodDark, MathF.Pow(t, 1.5f) * 0.85f);
            Opacity = 1f - MathF.Pow(t, 2.4f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.06f, 0f, 0.8f);
            Vector2 scale = new Vector2(0.4f * (1f - stretch * 0.3f), 0.5f * (1f + stretch * 1.3f)) * Scale;
            float rot = Velocity.Length() > 1.2f ? Velocity.ToRotation() + MathHelper.PiOver2 : Rotation;

            Color body = Color * Opacity;
            Color rim = Color.Lerp(Color, KikasaTwinsServant.BloodDark, 0.6f) * Opacity;
            //暗缘压边略宽一圈，火舌有体积不是光斑
            spriteBatch.Draw(tex, pos, null, rim, rot, origin, scale * new Vector2(1.35f, 1.1f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, rot, origin, scale, SpriteEffects.None, 0f);

            //鲜活期白热芯：小面积加色湿反光
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 1.8f, 0f, 1f);
            if (fresh > 0.05f) {
                Color core = Color.Lerp(new Color(255, 224, 200), initialColor, 0.4f) with { A = 0 };
                spriteBatch.Draw(tex, pos, null, core * (0.55f * fresh * Opacity), rot, origin,
                    scale * new Vector2(0.42f, 0.6f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
