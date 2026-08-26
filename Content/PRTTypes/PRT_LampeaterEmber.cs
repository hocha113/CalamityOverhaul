using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 噬灯魂的余烬屑（C2 专用）：吞灯者身上簌簌掉落的热渣。
    /// 生命三段：热浮（微上升+明闪）→ 失温停滞 → 尘坠（重力+暗红渐熄）。
    /// 与 PRT_Spark 的区别：Spark 是迸射亮条，这个是会「凉掉」的粒——
    /// 颜色沿 暖白→烬橙→深红→透明 走完余烬的一生，加色批里变透明即读作熄灭。
    /// </summary>
    internal class PRT_LampeaterEmber : BasePRT
    {
        private static readonly Color HotWhite = new(255, 230, 180);
        private static readonly Color DeepRed = new(120, 32, 8);

        public Color InitialColor;
        /// <summary>热浮强度（0=直接尘坠，1=足秒上飘）</summary>
        public float Buoyancy;

        public override int InGame_World_MaxCount => 600;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Extra_98";

        public PRT_LampeaterEmber Configure(int lifetime, float buoyancy = 0.6f) {
            InitialColor = Color;
            Lifetime = lifetime;
            Buoyancy = buoyancy;
            //每粒独立明闪相位（ID 是类型全局量不能当相位；纯表现，客户端掷点无碍）
            ai[0] = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
            Buoyancy = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float t = LifetimeCompletion;
            //热浮 → 停滞 → 尘坠
            float hot = 1f - MathHelper.Clamp(t * 2.4f, 0f, 1f);
            Velocity *= 0.94f;
            Velocity.Y += -0.05f * Buoyancy * hot + 0.085f * (1f - hot);

            //明闪（每粒相位独立，凉了就不闪）
            float flick = 0.75f + 0.25f * MathF.Sin(Time * 0.9f + ai[0]);
            //冷却色程：暖白/初始色 → 烬橙 → 深红 → 透明
            Color cool = t < 0.35f
                ? Color.Lerp(HotWhite, InitialColor, t / 0.35f)
                : Color.Lerp(InitialColor, DeepRed, (t - 0.35f) / 0.65f);
            Color = cool * (flick * (1f - t * t));

            Scale *= 0.985f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //小圆粒本体 + 极短热尾
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color,
                Velocity.ToRotation() + MathHelper.PiOver2, tex.Size() * 0.5f,
                new Vector2(0.35f, 0.55f) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.8f, 0f,
                tex.Size() * 0.5f, new Vector2(0.3f, 0.3f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
