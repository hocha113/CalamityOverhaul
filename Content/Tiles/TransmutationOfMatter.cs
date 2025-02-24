using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.TileModules;
using CalamityOverhaul.Content.UIs.SupertableUIs;
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
    internal class TransmutationOfMatter : ModTile, ICWRLoader
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "TransmutationOfMatter";
        public const int Width = 5;
        public const int Height = 3;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;
        private static Asset<Texture2D> tileAsset;
        private static Asset<Texture2D> tileGlowAsset;
        void ICWRLoader.LoadAsset() {
            tileAsset = ModContent.Request<Texture2D>(Texture);
            tileGlowAsset = ModContent.Request<Texture2D>(Texture + "Glow");
        }
        void ICWRLoader.UnLoadData() {
            tileAsset = null;
            tileGlowAsset = null;
        }
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<TransmutationOfMatterItem>());
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
                ModContent.TileType<CosmicAnvil>(),
                ModContent.TileType<SCalAltarLarge>(),
                ModContent.TileType<AncientAltar>(),
                ModContent.TileType<AshenAltar>(),
                ModContent.TileType<BotanicPlanter>(),
                ModContent.TileType<EutrophicShelf>(),
                ModContent.TileType<MonolithAmalgam>(),
                ModContent.TileType<VoidCondenser>(),
                ModContent.TileType<WulfrumLabstation>(),
                ModContent.TileType<StaticRefiner>(),
                ModContent.TileType<DraedonsForge>(),
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

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<TransmutationOfMatterItem>();//当玩家鼠标悬停在物块之上时，显示该物品的材质
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            for (int z = 0; z < 33; z++) {
                Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            }
        }

        public override bool RightClick(int i, int j) {
            if (VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                if (TileProcessorLoader.ByPositionGetTP(point, out TramModuleTP tram)) {
                    ref int playerTramType = ref Main.LocalPlayer.CWR().TETramContrType;
                    if (playerTramType == tram.WhoAmI && playerTramType >= 0) {
                        SupertableUI.tramModuleEntity ??= tram;
                        SupertableUI.Instance.Active = !SupertableUI.Instance.Active;
                    }
                    else {
                        playerTramType = tram.WhoAmI;
                        SupertableUI.tramModuleEntity = tram;
                        SupertableUI.Instance.Active = true;
                        SupertableUI.Instance.downSengsTime = 5;
                        if (SupertableUI.Instance.Active && !Main.playerInventory) {
                            //如果是开启合成UI但此时玩家并没有打开背包，那么就打开背包UI
                            Main.playerInventory = true;
                        }
                    }
                }
                SoundEngine.PlaySound(SoundID.Chat with { Pitch = 0.3f });
            }

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
