using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles.BloodAltars
{
    internal class BloodAltar : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "BloodAltar";
        public const int Width = 4;
        public const int Height = 3;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;
        /// <summary>贴图竖排 4 帧，每帧高 Height * SheetSquare</summary>
        public const int FrameCount = 4;
        public const int FrameHeight = Height * SheetSquare;

        [VaultLoaden(CWRConstant.Asset + "Tiles/" + "BloodAltar")]
        private static Asset<Texture2D> tileAsset = null;
        //描边贴图只有 1 帧（72×54），故取的是未加帧偏移的原始 frameY
        [VaultLoaden(CWRConstant.Asset + "Tiles/" + "BloodAltarGlow")]
        private static Asset<Texture2D> tileGlowAsset = null;

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileFrameImportant[Type] = true;
            AddMapEntry(Color.Red, VaultUtils.GetLocalizedItemName<Items.Placeable.BloodAltar>());
            AnimationFrameHeight = FrameHeight;
            AdjTiles = [TileID.DemonAltar];
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(OriginOffsetX, OriginOffsetY);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        //光标显示的是祭坛吃什么，而不是祭坛自己
        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(BloodAltarTP.OfferingType);

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Blood);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            for (int z = 0; z < 33; z++) {
                Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Blood);
            }
        }

        //交互逻辑在 BloodAltarTP.RightClick：InnoVault 会把那个钩子派发到服务端与其他客户端，
        //而这里只跑在点击者本地，拿不到权威端
        public override bool RightClick(int i, int j) => true;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;

            BloodAltarTP module = null;
            if (VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                TileProcessorLoader.ByPositionGetTP(point, out module);
            }
            if (module != null) {
                frameYPos += module.FrameIndex % FrameCount * FrameHeight;
            }

            Texture2D tex = tileAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                return false;
            }
            if (t.Slope != 0) {
                return false;
            }

            spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);

            if (module != null && module.HoverGlow) {
                spriteBatch.Draw(tileGlowAsset.Value, drawOffset, new Rectangle(frameXPos, t.TileFrameY, 16, 16)
                    , module.HoverGlowColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }
}
