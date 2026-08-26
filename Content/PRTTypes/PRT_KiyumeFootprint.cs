using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 鬼梦泥地脚印斑：看不见的赶路人踩出的贴地暗痕，压扁椭圆随坡转角，
    /// 零速零重力原地驻留，寿命尾段渐隐。暗层必须真 alpha：Extra_98 + AlphaBlend
    /// （加色与 A=0 物理上压不暗，黑底图禁令遵 VFX.md）。
    /// </summary>
    internal class PRT_KiyumeFootprint : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 40;

        //压扁比即脚印身份：横长竖扁的一枚泥斑（乘外部 Scale 做步间微差）
        private const float SquashX = 0.30f;
        private const float SquashY = 0.10f;

        private Color initialColor;
        private int fadeTail;

        public PRT_KiyumeFootprint Configure(int lifetime, int fadeTailFrames, float rotation) {
            Lifetime = lifetime;
            fadeTail = fadeTailFrames;
            Rotation = rotation;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            fadeTail = 0;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Velocity = Vector2.Zero;
            if (Lifetime <= 0) {
                Lifetime = 300;
            }
            if (fadeTail <= 0) {
                fadeTail = 60;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        //踩下即定格：贴地零速零重力，不随帧移动
        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //踩出即全显（印记是一瞬拍下的），只在尾段渐隐
            int remaining = Lifetime - Time;
            Color = initialColor * MathHelper.Clamp(remaining / (float)fadeTail, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            var squash = new Vector2(SquashX, SquashY) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation,
                tex.Size() * 0.5f, squash, SpriteEffects.None, 0f);
            return false;
        }
    }
}
