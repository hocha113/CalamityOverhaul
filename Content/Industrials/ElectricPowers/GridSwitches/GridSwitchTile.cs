using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches
{
    /// <summary>电网总闸瓦片,2x2 落地配电箱;本体由 TP 程序化绘制</summary>
    internal class GridSwitchTile : ModTile
    {
        //占位贴图:PreDraw 关闭后仅放置预览取样用,借 2x2 机器图
        public override string Texture => CWRConstant.Asset + "ElectricPowers/LifeWeaverTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(146, 118, 62), VaultUtils.GetLocalizedItemName<GridSwitch>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<GridSwitch>());

        public override void HitWire(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<GridSwitchTP>(i, j, out var tp)) {
                return;
            }
            //一次脉冲可能扫过本体多格:先跳过整个占位,保证只翻转一次(原版多格机关件口径)
            for (int x = 0; x < 2; x++) {
                for (int y = 0; y < 2; y++) {
                    Wiring.SkipWire(tp.Position.X + x, tp.Position.Y + y);
                }
            }
            tp.Toggle();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!TileProcessorLoader.AutoPositionGetTP<GridSwitchTP>(i, j, out var tp)) {
                return;
            }
            if (!tp.Disabled) {
                r = 0.12f;
                g = 0.22f;
                b = 0.10f;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }
}
