using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Subworlds.FogDungeons
{
    internal class DungeonSystem : ModSystem
    {
        public override void PreUpdateWorld() {
            if (DungeonWorld.Active) {
                Wiring.UpdateMech();
                TileEntity.UpdateStart();
                foreach (TileEntity te in TileEntity.ByID.Values) {
                    te.Update();
                }
                TileEntity.UpdateEnd();
                if (++Liquid.skipCount > 1) {
                    Liquid.UpdateLiquid();
                    Liquid.skipCount = 0;
                }
            }
        }
    }
}
