using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    //来自珊瑚石，谢谢你瓶中微光
    internal class PRT_TileHightlight : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "TileHightlight";
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        public override void AI() {
            Opacity++;
            if (Opacity > 1) {
                Opacity = 0;

                Frame.X++;
                if (Frame.X > 2) {
                    Frame.X = 0;
                    Frame.Y++;
                    if (Frame.Y > 2) {
                        active = false;
                    }   
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Rectangle frame = TexValue.Frame(3, 3, Frame.X, Frame.Y);
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, frame
                , Color, Rotation, frame.Size() / 2, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
