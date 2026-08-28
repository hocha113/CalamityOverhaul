using System.IO;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp
{
    /// <summary>渊晶海虾击杀旗标：世界存档 + 联机全量同步</summary>
    internal class SeaShrimpWorldFlag : SeaShrimpModSystem
    {
        /// <summary>本世界是否已击败渊晶海虾</summary>
        public static bool DownedSeaShrimp;

        public override void ClearWorld() => DownedSeaShrimp = false;

        public override void SaveWorldData(TagCompound tag) {
            if (DownedSeaShrimp) {
                tag["downedSeaShrimp"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
            => DownedSeaShrimp = tag.ContainsKey("downedSeaShrimp");

        public override void NetSend(BinaryWriter writer) => writer.Write(DownedSeaShrimp);

        public override void NetReceive(BinaryReader reader) => DownedSeaShrimp = reader.ReadBoolean();
    }
}
