using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
namespace CalamityOverhaul.Content.TileEntitys.Core
{
    internal class TESystem : ModSystem
    {
        public static List<BaseCWRTE> BaseCWRTEs { get; private set; }

        public override void PostSetupContent() {
            BaseCWRTEs = new();
            BaseCWRTEs = CWRUtils.HanderSubclass<BaseCWRTE>();
        }

        public override void PostUpdateTime() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            Dictionary<int, TileEntity>.ValueCollection.Enumerator enumerator = TileEntity.ByID.Values.GetEnumerator();
            int bloodAltarType = ModContent.GetInstance<TEBloodAltar>().Type;
            int tramType = ModContent.GetInstance<TETram>().Type;
            do {
                TileEntity te = enumerator.Current;
                if (te == null) {
                    continue;
                }

                if (te.type == bloodAltarType || te.type == tramType) {
                    BaseCWRTE baseCWRTE = te as BaseCWRTE;
                    baseCWRTE.AI();
                }
            } while (enumerator.MoveNext());
        }
    }
}
