using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.WireInterfaces
{
    /// <summary>机关接口器瓦片,1x1 机关件规格,任意位置可放;本体由 TP 程序化绘制</summary>
    internal class WireInterfaceTile : ModTile
    {
        //占位贴图:PreDraw 关闭后仅放置预览取样用,借 1x1 管道图
        public override string Texture => CWRConstant.Asset + "MaterialFlow/Pipeline";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(120, 62, 56), VaultUtils.GetLocalizedItemName<WireInterface>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            //与管道同规:无支撑要求,贴着目标机器的任意一侧都能放
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.RedTorch);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<WireInterface>());

        public override void HitWire(int i, int j) {
            if (!TileProcessorLoader.AutoPositionGetTP<WireInterfaceTP>(i, j, out var tp)) {
                return;
            }
            tp.OnHitWire();
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }
}
