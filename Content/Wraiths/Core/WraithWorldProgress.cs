using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>世界侧发现进度，仅权威落档；驾驭数据不在此</summary>
    public sealed class WraithWorldProgress : ModSystem
    {
        public static WraithProgressStore Store { get; private set; } = new();

        /// <summary>登记遭遇，仅权威</summary>
        public static void MarkEncounter(string key) {
            if (VaultUtils.isClient || string.IsNullOrEmpty(key)) {
                return;
            }
            Store.MarkEncounter(key);
        }

        public override void ClearWorld() => Store = new WraithProgressStore();

        public override void SaveWorldData(TagCompound tag) => Store.SaveData(tag);

        public override void LoadWorldData(TagCompound tag) => Store.LoadData(tag);
    }
}
