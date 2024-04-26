using CalamityMod.CalPlayer;
using CalamityMod.Items.DraedonMisc;
using CalamityMod.Items.Placeables.DraedonStructures;
using CalamityMod.TileEntities;
using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Microsoft.Xna.Framework;
using Terraria.Enums;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityMod.Items.Materials;
using FTile = CalamityMod.Items.Placeables.Furniture.CraftingStations;
using System.Collections.Generic;
using CalamityOverhaul.Content.TileEntitys;
using CalamityOverhaul.Content.UIs.SupertableUIs;

namespace CalamityOverhaul.Content.Tiles
{
    internal class TransmutationOfMatter : ModTile
    {
        public override string Texture => CWRConstant.Asset + "Tiles/" + "TransmutationOfMatter";
        public const int Width = 3;
        public const int Height = 3;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
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
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);

            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16 };
            TileObjectData.newTile.LavaDeath = false;
            ModTileEntity te = ModContent.GetInstance<TETram>();
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(te.Hook_AfterPlacement, -1, 0, true);
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.addTile(Type);
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<TransmutationOfMatterItem>());
            AnimationFrameHeight = 68;

            AdjTiles = new int[] {
                TileID.WorkBenches,
                TileID.Chairs,
                TileID.Tables,
                TileID.Anvils,
                TileID.MythrilAnvil,
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
                TileID.Furnaces,
                TileID.Hellforge,
                TileID.AdamantiteForge,
                TileID.TinkerersWorkbench,
                TileID.LunarCraftingStation,
                TileID.DemonAltar
            };
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void KillMultiTile(int i, int j, int frameX, int frameY) {
            Tile t = Main.tile[i, j];
            int left = i - t.TileFrameX % (Width * SheetSquare) / SheetSquare;
            int top = j - t.TileFrameY % (Height * SheetSquare) / SheetSquare;
            TETram te = CalamityUtils.FindTileEntity<TETram>(i, j, Width, Height, SheetSquare);
            te?.Kill(left, top);
        }

        public override bool RightClick(int i, int j) {
            TileEntity.InitializeAll();
            SupertableUI.Instance.Active = !SupertableUI.Instance.Active;
            if (SupertableUI.Instance.Active && !Main.playerInventory) {//如果是开启合成UI但此时玩家并没有打开背包，那么就打开背包UI
                Main.playerInventory = true;
            }
            Tile t = Main.tile[i, j];
            int left = i - t.TileFrameX % (Width * SheetSquare) / SheetSquare;
            int top = j - t.TileFrameY % (Height * SheetSquare) / SheetSquare;
            TETram te = CalamityUtils.FindTileEntity<TETram>(i, j, Width, Height, SheetSquare);
            if (te != null) {
                Main.LocalPlayer.CWR().TETramContrType = te.ID;
            }
            SoundEngine.PlaySound(SoundID.Chat with { Pitch = 0.3f });
            Recipe.FindRecipes();
            return true;
        }
        int frameIndex = 1;
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameIndex = (int)(Main.GameUpdateCount / 10 % 4);
            //TransmutationOfMatterEntity transmutationOfMatterEntity = CalamityUtils.FindTileEntity<TransmutationOfMatterEntity>(i, j, Width, Height, SheetSquare);
            frameYPos += frameIndex % 4 * (Height * SheetSquare);
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);

            if (!t.IsHalfBlock && t.Slope == 0)
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            else if (t.IsHalfBlock)
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            return false;
        }
    }
}
