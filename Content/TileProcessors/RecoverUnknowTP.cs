using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using InnoVault.TileProcessors;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileProcessors
{
    internal class RecoverUnknownTP : GlobalTileProcessor
    {
        public static int UnknownTPID;
        public override void SetStaticDefaults() => UnknownTPID = TPUtils.GetID<UnknowTP>();
        public override bool PreUpdate(TileProcessor tileProcessor) {
            if (VaultUtils.isClient) {
                return true;
            }
            if (tileProcessor.ID != UnknownTPID) {
                return true;
            }
            if (tileProcessor is not UnknowTP unknow) {
                return true;
            }
            RecoverUEPipelineInputTP(unknow);
            return true;
        }

        private static void RecoverUEPipelineInputTP(UnknowTP unknow) {
            if (unknow.HoverString != "CalamityOverhaul/UEPipelineInputTP") {
                return;
            }
            if (!Main.player.Any(p => p.Alives() && p.DistanceSQ(unknow.CenterInWorld) < 90000)) {
                return;
            }

            unknow.UnModName = "UEPipeline";

            WorldGen.KillTile(unknow.Position.X, unknow.Position.Y, false, false, true);
            unknow.Kill();

            int tileID = ModContent.TileType<UEPipelineTile>();
            WorldGen.PlaceTile(unknow.Position.X, unknow.Position.Y, tileID, true, true);
            var tp = TileProcessorLoader.AddInWorld(tileID, unknow.Position, null);
            if (tp == null) {
                return;
            }

            if (tp is UEPipelineTP pipelineTP && unknow.Data?.Count > 0) {
                pipelineTP.LoadData(unknow.Data);
            }

            if (VaultUtils.isSinglePlayer) {
                return;
            }

            NetMessage.SendTileSquare(Main.myPlayer, unknow.Position.X, unknow.Position.Y);
            TileProcessorNetWork.SendTPDeathByServer(unknow);
            TileProcessorNetWork.PlaceInWorldNetSend(VaultMod.Instance, tileID, unknow.Position);
            tp.SendData();
        }
    }
}
