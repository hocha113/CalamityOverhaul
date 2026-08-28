using System.IO;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>脓蕾沙蟒击杀旗标：世界存档 + 联机全量同步（镜像渊晶海虾旗标）</summary>
    internal class FssWorldFlag : FssModSystem
    {
        /// <summary>本世界是否已击败脓蕾沙蟒</summary>
        public static bool DownedFesterSerpent;

        public override void ClearWorld() => DownedFesterSerpent = false;

        public override void SaveWorldData(TagCompound tag) {
            if (DownedFesterSerpent) {
                tag["downedFesterSerpent"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
            => DownedFesterSerpent = tag.ContainsKey("downedFesterSerpent");

        public override void NetSend(BinaryWriter writer) => writer.Write(DownedFesterSerpent);

        public override void NetReceive(BinaryReader reader) => DownedFesterSerpent = reader.ReadBoolean();
    }
}
