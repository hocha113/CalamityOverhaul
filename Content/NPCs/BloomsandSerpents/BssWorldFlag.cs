using System.IO;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>荒花沙蟒击杀旗标：世界存档 + 联机全量同步（镜像脓蕾沙蟒旗标）</summary>
    internal class BssWorldFlag : BssModSystem
    {
        /// <summary>本世界是否已击败荒花沙蟒</summary>
        public static bool DownedBloomSerpent;

        public override void ClearWorld() => DownedBloomSerpent = false;

        public override void SaveWorldData(TagCompound tag) {
            if (DownedBloomSerpent) {
                tag["downedBloomSerpent"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
            => DownedBloomSerpent = tag.ContainsKey("downedBloomSerpent");

        public override void NetSend(BinaryWriter writer) => writer.Write(DownedBloomSerpent);

        public override void NetReceive(BinaryReader reader) => DownedBloomSerpent = reader.ReadBoolean();
    }
}
