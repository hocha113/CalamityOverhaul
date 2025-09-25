using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileProcessors
{
    internal class RecoverUnknownTP : GlobalTileProcessor
    {
        public static int UnknownTPID;
        public override void SetStaticDefaults() {
            UnknownTPID = TPUtils.GetID<UnknowTP>();
        }

        public override bool PreUpdate(TileProcessor tileProcessor) {
            if (VaultUtils.isServer) {
                return true;
            }
            if (tileProcessor.ID != UnknownTPID) {
                return true;
            }
            if (tileProcessor is not UnknowTP unknow) {
                return true;
            }
            if (tileProcessor.CenterInWorld.To(Main.LocalPlayer.Center).LengthSquared() >= 90000) {
                return true;
            }
            RecoverUEPipelineInputTP(unknow);
            return true;
        }

        private static void RecoverUEPipelineInputTP(UnknowTP unknow) {
            if (unknow.HoverString != "CalamityOverhaul/UEPipelineInputTP") {
                return;
            }
            unknow.UnModName = "UEPipeline";
            WorldGen.KillTile(unknow.Position.X, unknow.Position.Y, false, false, true);
            unknow.Active = false;
            int tileID = ModContent.TileType<UEPipelineTile>();
            WorldGen.PlaceTile(unknow.Position.X, unknow.Position.Y, tileID, true, true);
            var tp = TileProcessorLoader.AddInWorld(tileID, unknow.Position, null);
            if (tp != null && !VaultUtils.isSinglePlayer) {
                TileProcessorNetWork.PlaceInWorldNetSend(VaultMod.Instance, tileID, unknow.Position);
                NetMessage.SendTileSquare(Main.myPlayer, unknow.Position.X, unknow.Position.Y);
            }
        }
    }
}
