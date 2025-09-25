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
            if (tileProcessor.CenterInWorld.To(Main.LocalPlayer.Center).LengthSquared() >= 360000) {
                return true;
            }
            RecoverUEPipelineInputTP(unknow);
            return true;
        }

        private static void RecoverUEPipelineInputTP(UnknowTP unknow) {
            if (unknow.HoverString != "CalamityOverhaul/UEPipelineInputTP") {
                return;
            }
            WorldGen.KillTile(unknow.Position.X, unknow.Position.Y, false, false, true);
            WorldGen.PlaceTile(unknow.Position.X, unknow.Position.Y, ModContent.TileType<UEPipelineTile>(), true, true);
            NetMessage.SendTileSquare(Main.myPlayer, unknow.Position.X, unknow.Position.Y);
        }
    }
}
