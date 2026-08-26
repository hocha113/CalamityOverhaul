using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Woodsong
{
    /// <summary>
    /// 叶浪飞叶：原版森林树叶 Gore 贴图承载真实剪影，风推横飞+翻滚+触地贴停。
    /// AlphaBlend 直绘，颜色乘所在处环境光落地。
    /// </summary>
    internal class PRT_WoodsongLeaf : BasePRT
    {
        public override string Texture => $"Terraria/Images/Gore_{GoreID.TreeLeaf_Normal}";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private float spin;
        private float phase;
        private float windPush;
        private bool grounded;

        /// <param name="windDrive">横向巡航速度（px/tick，带符号，随风向）</param>
        public PRT_WoodsongLeaf Configure(float windDrive, int lifetime) {
            windPush = windDrive;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            phase = 0f;
            windPush = 0f;
            grounded = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            phase = Main.rand.NextFloat(100f);
            spin = Main.rand.NextFloat(0.05f, 0.13f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(130, 210);
            }
        }

        public override void AI() {
            if (!grounded) {
                //横向被风裹挟+扑动摆，纵向低终端速度飘坠
                Velocity.X = MathHelper.Lerp(Velocity.X,
                    windPush + MathF.Sin((Time + phase) * 0.09f) * 0.5f, 0.03f);
                Velocity.Y = MathHelper.Lerp(Velocity.Y,
                    0.55f + MathF.Sin((Time + phase * 1.7f) * 0.11f) * 0.40f, 0.05f);
                Rotation += spin * (0.6f + Math.Abs(Velocity.X) * 0.25f);

                //触地：贴停并提前谢幕
                if (Time % 4 == 0 && WorldGen.SolidTile((int)(Position.X / 16f), (int)((Position.Y + 4f) / 16f))) {
                    grounded = true;
                    Velocity *= 0.05f;
                    Lifetime = Math.Min(Lifetime, Time + 26);
                }
            }
            else {
                Velocity *= 0.8f;
            }

            float lc = LifetimeCompletion;
            Opacity = MathHelper.Clamp(lc / 0.08f, 0f, 1f) * MathHelper.Clamp((1f - lc) / 0.22f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Opacity <= 0.01f) {
                return false;
            }
            Texture2D tex = TexValue;
            //乘环境光：叶片属于世界而非发光体
            Color lit = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            SpriteEffects flip = spin > 0f ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, lit * Opacity,
                Rotation, tex.Size() * 0.5f, Scale, flip, 0f);
            return false;
        }
    }
}
