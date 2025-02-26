using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Terraria;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Generator
{
    internal abstract class BaseGeneratorTile : ModTile
    {
        public virtual int TargetItem => ItemID.None;
        public virtual int GeneratorUI => 0;
        public virtual int GeneratorTP => 0;
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
            player.cursorItemIconID = TargetItem;//当玩家鼠标悬停在物块之上时，显示该物品的材质
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }

            BaseGeneratorTP baseGeneratorTP = TileProcessorLoader.FindModulePreciseSearch(GeneratorTP, point.X, point.Y) as BaseGeneratorTP;
            BaseGeneratorUI baseGeneratorUI = UIHandleLoader.GetUIHandleInstance(GeneratorUI) as BaseGeneratorUI;
            bool newTP = baseGeneratorUI.GeneratorTP != baseGeneratorTP;
            baseGeneratorUI.GeneratorTP = baseGeneratorTP;
            baseGeneratorTP.GeneratorUI = baseGeneratorUI;
            baseGeneratorUI?.RightClickByTile(newTP);
            baseGeneratorTP?.RightClickByTile(newTP);
            return true;
        }
    }
}
