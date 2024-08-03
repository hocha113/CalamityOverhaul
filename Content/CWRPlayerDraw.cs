using CalamityOverhaul.Content.Items.Armor.DemonshadeExter;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal class CWRPlayerDraw : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            Player player = drawInfo.drawPlayer;

            Item headItem = player.armor[0];

            int dyeShader = player.dye?[0].dye ?? 0;

            Vector2 headDrawPosition = drawInfo.Position - Main.screenPosition;

            headDrawPosition += new Vector2((player.width - player.bodyFrame.Width) / 2f
                , player.height - player.bodyFrame.Height + 4f);

            headDrawPosition = new Vector2((int)headDrawPosition.X, (int)headDrawPosition.Y);
            headDrawPosition += player.headPosition + drawInfo.headVect;

            Rectangle frame;
            Texture2D extraPieceTexture;

            if (headItem.type <= ItemID.None) {
                return;
            }

            if (headItem.type == DemonshadeHelmMagic.PType) {
                headDrawPosition += new Vector2(-4, -16);
                extraPieceTexture = DemonshadeHelmMagic.Hand.Value;

                frame = extraPieceTexture.Frame(1, 20, 0, player.bodyFrame.Y / player.bodyFrame.Height);

                DrawData pieceDrawData = new DrawData(extraPieceTexture, headDrawPosition, frame
                    , drawInfo.colorArmorHead, player.headRotation, drawInfo.headVect, 1f, drawInfo.playerEffect, 0) {
                    shader = dyeShader
                };

                drawInfo.DrawDataCache.Add(pieceDrawData);
            }

            else if (headItem.type == DemonshadeHelmRanged.PType) {
                headDrawPosition += new Vector2(-6, -18);
                extraPieceTexture = DemonshadeHelmRanged.Hand.Value;

                frame = extraPieceTexture.Frame(1, 20, 0, player.bodyFrame.Y / player.bodyFrame.Height);

                DrawData pieceDrawData = new DrawData(extraPieceTexture, headDrawPosition, frame
                    , drawInfo.colorArmorHead, player.headRotation, drawInfo.headVect, 1f, drawInfo.playerEffect, 0) {
                    shader = dyeShader
                };

                drawInfo.DrawDataCache.Add(pieceDrawData);
            }

            else if (headItem.type == DemonshadeHelmSummon.PType) {
                headDrawPosition += new Vector2(-6, -4);
                extraPieceTexture = DemonshadeHelmSummon.Hand.Value;

                frame = extraPieceTexture.Frame(1, 20, 0, player.bodyFrame.Y / player.bodyFrame.Height);

                DrawData pieceDrawData = new DrawData(extraPieceTexture, headDrawPosition, frame
                    , drawInfo.colorArmorHead, player.headRotation, drawInfo.headVect, 1f, drawInfo.playerEffect, 0) {
                    shader = dyeShader
                };

                drawInfo.DrawDataCache.Add(pieceDrawData);
            }

            else if (headItem.type == DemonshadeHelmRogue.PType) {
                headDrawPosition += new Vector2(-4, -14);
                extraPieceTexture = DemonshadeHelmRogue.Hand.Value;

                frame = extraPieceTexture.Frame(1, 20, 0, player.bodyFrame.Y / player.bodyFrame.Height);

                DrawData pieceDrawData = new DrawData(extraPieceTexture, headDrawPosition, frame
                    , drawInfo.colorArmorHead, player.headRotation, drawInfo.headVect, 1f, drawInfo.playerEffect, 0) {
                    shader = dyeShader
                };

                drawInfo.DrawDataCache.Add(pieceDrawData);
            }
        }
    }
}
