using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 机器开/关图腾:待机位翻转边沿在机器头顶弹出的小状态牌,绿▶=启用、红‖=待机
    /// (双竖条与待机角标同一符号语言)。弹入过冲→缓升→淡出,全程序化绘制
    /// </summary>
    internal class PRT_CtrlToggleTotem : BasePRT
    {
        public override int InGame_World_MaxCount => 80;
        public override bool CanPool => true;
        //声明贴图仅为满足加载契约,绘制全走 placeholder 像素
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private bool turnOn;

        public PRT_CtrlToggleTotem Configure(bool on) {
            turnOn = on;
            Lifetime = 46;
            return this;
        }

        public override void Reset() {
            base.Reset();
            turnOn = false;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AlphaBlend;

        public override void AI() => Velocity *= 0.90f;//上浮减速

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 center = Position - Main.screenPosition;

            //弹入过冲 + 尾段淡出
            float pop = Time < 7 ? MathHelper.Lerp(0.4f, 1.18f, Time / 7f)
                : Time < 11 ? MathHelper.Lerp(1.18f, 1f, (Time - 7) / 4f) : 1f;
            float fade = Time > Lifetime - 14 ? (Lifetime - Time) / 14f : 1f;

            void DrawRect(Vector2 offset, float w, float h, Color color) {
                spriteBatch.Draw(px, center + offset * pop, src, color * fade, 0f,
                    new Vector2(0.5f, 0.5f), new Vector2(w, h) * pop, SpriteEffects.None, 0f);
            }

            Color glyph = turnOn ? new Color(112, 232, 140) : new Color(242, 96, 82);
            //背板与内板
            DrawRect(Vector2.Zero, 18f, 14f, new Color(14, 15, 19) * 0.55f);
            DrawRect(Vector2.Zero, 16f, 12f, new Color(30, 32, 39) * 0.92f);
            //底缘一线状态色,给牌子"设备铭牌"感
            DrawRect(new Vector2(0f, 5.5f), 16f, 1f, glyph * 0.55f);

            if (turnOn) {
                //▶:四列渐窄的右指三角
                for (int i = 0; i < 4; i++) {
                    DrawRect(new Vector2(-2.2f + i * 1.6f, 0f), 1.6f, 7.5f - i * 2.1f, glyph);
                }
            }
            else {
                //‖:双竖条
                DrawRect(new Vector2(-1.8f, 0f), 1.8f, 7f, glyph);
                DrawRect(new Vector2(1.8f, 0f), 1.8f, 7f, glyph);
            }
            return false;
        }
    }
}
