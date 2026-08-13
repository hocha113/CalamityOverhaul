using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 虚空裂殇，虚空机匣撕裂点暗痕；Extra_98 真 alpha AlphaBlend 吸光暗斑，
    /// 爆闪熄灭后显形，末段沿随机轴捏拢缝合并迸出一线紫光
    /// </summary>
    internal class PRT_SHPCNullspaceScar : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 400;

        private Color accentColor;
        private float initialScale;
        private float wobblePhase;

        public PRT_SHPCNullspaceScar Configure(Color accent, int lifetime) {
            accentColor = accent;
            Lifetime = lifetime;
            initialScale = Scale;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            accentColor = default;
            initialScale = 0f;
            wobblePhase = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() {
            Velocity *= 0.9f;
            float t = LifetimeCompletion;
            //揭示→驻留呼吸→捏拢
            float reveal = MathHelper.Clamp(t / 0.18f, 0f, 1f);
            float wobble = 1f + 0.06f * MathF.Sin(Time * 0.35f + wobblePhase);
            Scale = initialScale * wobble;
            Opacity = reveal * (t > 0.62f ? 1f - (t - 0.62f) / 0.38f : 1f);
        }

        /// <summary>末段捏合度 0..1</summary>
        private float PinchT() {
            float t = LifetimeCompletion;
            return t > 0.62f ? (t - 0.62f) / 0.38f : 0f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity < 0.02f || Scale < 0.03f) return false;
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //缝合沿 Rotation 轴捏扁
            float pinch = PinchT();
            float axial = 1f - pinch * 0.92f;
            Vector2 outerScale = new Vector2(axial, 1f - pinch * 0.45f) * Scale;
            Vector2 coreScale = outerScale * new Vector2(0.62f, 0.7f);

            //暗晕+近黑吸光核，AlphaBlend 真暗层
            Color halo = Color * (Opacity * 0.45f);
            Color core = new Color(8, 2, 14) * (Opacity * 0.85f);
            spriteBatch.Draw(tex, pos, null, halo, Rotation, origin, outerScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, core, Rotation, origin, coreScale, SpriteEffects.None, 0f);

            //捏合瞬间一线紫光，A=0 只在预乘 AlphaBlend 批合法
            if (pinch > 0.55f) {
                float glint = (pinch - 0.55f) / 0.45f;
                Color seam = (accentColor with { A = 0 }) * (glint * (1f - glint) * 4f * Opacity);
                Vector2 seamScale = new Vector2(0.05f, (1f - pinch * 0.3f) * 1.15f) * Scale;
                spriteBatch.Draw(tex, pos, null, seam, Rotation, origin, seamScale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
