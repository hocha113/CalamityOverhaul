using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.TileModules;
using CalamityOverhaul.Content.TileModules.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
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
    internal class TransmutationOfMatter : ModTile, ILoader
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "TransmutationOfMatter";
        public const int Width = 5;
        public const int Height = 3;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 16;
        private static Asset<Texture2D> assetValue;
        void ILoader.LoadAsset() => assetValue = ModContent.Request<Texture2D>(Texture);
        void ILoader.UnLoad() => assetValue = null;
        public override bool IsLoadingEnabled(Mod mod) {
            return !CWRServerConfig.Instance.AddExtrasContent ? false : base.IsLoadingEnabled(mod);
        }
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = Width;
            TileObjectData.newTile.Height = Height;
            TileObjectData.newTile.Origin = new Point16(OriginOffsetX, OriginOffsetY);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile 
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);

            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16 };
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
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
        }
        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            for (int z = 0; z < 33; z++) {
                Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            }
        }

        public override bool RightClick(int i, int j) {
            SupertableUI.Instance.Active = !SupertableUI.Instance.Active;
            if (SupertableUI.Instance.Active && !Main.playerInventory) {
                //如果是开启合成UI但此时玩家并没有打开背包，那么就打开背包UI
                Main.playerInventory = true;
            }

            if (CWRUtils.SafeGetTopLeft(i, j, out var point)) {
                int type = TileModuleLoader.GetModuleID(typeof(TramModule));
                BaseTileModule module = TileModuleLoader.FindModulePreciseSearch(type, point.X, point.Y);
                if (module != null) {
                    Main.LocalPlayer.CWR().TETramContrType = module.WhoAmI;
                }
                SoundEngine.PlaySound(SoundID.Chat with { Pitch = 0.3f });
                Recipe.FindRecipes();
            }

            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            frameXPos = frameXPos / 18 * 16;
            int frameYPos = t.TileFrameY;
            frameYPos = frameYPos / 18 * 16;
            frameYPos += (int)(Main.GameUpdateCount / 10 % 11) % 11 * (Height * SheetSquare);
            Texture2D tex = assetValue.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            offset -= new Vector2(-2, 0);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

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
    }
}
