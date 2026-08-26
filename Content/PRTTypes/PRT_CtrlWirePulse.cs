using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 控制层信号流光:发信器件沿机关线方向滑出 1-2 格的短命光点,示意脉冲去向。
    /// 加色绘制+速度拉伸;只由绘制帧边沿生成,天然屏内出现
    /// </summary>
    internal class PRT_CtrlWirePulse : BasePRT
    {
        public override int InGame_World_MaxCount => 240;
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private Color initialColor;

        public PRT_CtrlWirePulse Configure(int lifetime) {
            initialColor = Color;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.93f;
            //头两帧淡入,其后平方衰减熄灭
            float head = MathHelper.Clamp(Time / 3f, 0f, 1f);
            float fade = 1f - LifetimeCompletion * LifetimeCompletion;
            Color = initialColor * (head * fade);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //速度拉伸:窄轴恒定,长轴随速度伸长
            Vector2 stretch = new Vector2(0.28f, 0.40f + Velocity.Length() * 0.10f) * Scale;
            Vector2 pos = Position - Main.screenPosition;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, tex.Size() * 0.5f, stretch, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.75f, Rotation, tex.Size() * 0.5f, stretch * new Vector2(0.5f, 0.85f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
