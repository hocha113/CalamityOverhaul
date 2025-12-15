using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.TileProcessors;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using CalamityOverhaul.OtherMods.MagicStorage;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Tiles
{
    internal class TransmutationOfMatter : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "TransmutationOfMatter";
        public const int Width = 5;
        public const int Height = 3;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;
        [VaultLoaden(CWRConstant.Asset + "Tiles/" + "TransmutationOfMatter")]
        private static Asset<Texture2D> tileAsset = null;
        [VaultLoaden(CWRConstant.Asset + "Tiles/" + "TransmutationOfMatterGlow")]
        private static Asset<Texture2D> tileGlowAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<TransmutationOfMatterItem>());
            AnimationFrameHeight = 68;

            AdjTiles = [
                TileID.WorkBenches,
                TileID.Chairs,
                TileID.Tables,
                TileID.Anvils,
                TileID.MythrilAnvil,
                TileID.Furnaces,
                TileID.Hellforge,
                TileID.AdamantiteForge,
                TileID.TinkerersWorkbench,
                TileID.LunarCraftingStation,
                TileID.DemonAltar,
                CWRID.Tile_CosmicAnvil,
                CWRID.Tile_SCalAltarLarge,
                CWRID.Tile_AncientAltar,
                CWRID.Tile_AshenAltar,
                CWRID.Tile_BotanicPlanter,
                CWRID.Tile_EutrophicShelf,
                CWRID.Tile_MonolithAmalgam,
                CWRID.Tile_VoidCondenser,
                CWRID.Tile_WulfrumLabstation,
                CWRID.Tile_StaticRefiner,
                CWRID.Tile_ProfanedCrucible,
                CWRID.Tile_PlagueInfuser,
                CWRID.Tile_DraedonsForge,
                ModContent.TileType<DarkMatterCompressor>(),
            ];

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(OriginOffsetX, OriginOffsetY);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }
        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<TransmutationOfMatterItem>());

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            for (int z = 0; z < 33; z++) {
                Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            }
        }

        public override bool RightClick(int i, int j) {
            if (SupertableUI.Instance.Active && SupertableUI.TramTP is null && MSRef.Has) {//如果是这种状态，说明正在进行魔法存储的联动启动
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.3f });
                return false;
            }

            //获取图块的左上角位置
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return true;
            }

            //播放交互音效
            SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.3f });

            //获取对应的 TileProcessor
            if (!TileProcessorLoader.ByPositionGetTP(point, out TramModuleTP tram)) {
                return true;
            }

            //打开UI
            tram.OpenUI(Main.LocalPlayer);

            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out TramModuleTP tram)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += tram.frame * (Height * SheetSquare);
            Texture2D tex = tileAsset.Value;
            Texture2D glow = tileGlowAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                if (tram.drawGlow) {
                    spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, t.TileFrameY, 16, 16)
                    , tram.gloaColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                }
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }

            return false;
        }
    }
}
