using CalamityOverhaul.Content.TileModify.Core;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets
{
    internal abstract class BaseTurretTile : TileOverride
    {
        public virtual int TargetTPID => 0;
        public override bool? CanDrop(int i, int j, int type) => false;
        public override bool? PreDraw(int i, int j, int type, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (TileProcessorLoader.ByPositionGetTP(TargetTPID, point.X, point.Y, out TileProcessor tp) && tp is BaseTurretTP turret) {
                Tile t = Main.tile[i, j];
                int frameXPos = t.TileFrameX;
                int frameYPos = t.TileFrameY;
                Texture2D tex = ModifyTurretLoader.TurretBase.Value;
                Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
                Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;

                Color drawColor = Lighting.GetColor(i, j);
                if (turret.BatteryLow) {//在没电时设置颜色偏暗，让玩家知道这个炮塔没电了
                    drawColor.R /= 2;
                    drawColor.G /= 2;
                    drawColor.B /= 2;
                    drawColor.A = 255;
                }

                if (!t.IsHalfBlock && t.Slope == 0) {
                    spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                        , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                }
                else if (t.IsHalfBlock) {
                    spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                        , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                }
                return false;
            }
            return null;
        }
    }
}
