using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 世界侧厉鬼进度宿主：这个世界发现过哪些鬼。数据只在服务器/单人有效并随世界落档，
    /// 客户端侧为空壳（展示所需的同步等 UI 接线时再补）。
    /// 与载体绑定的驾驭数据不在这里，将来由 LegendData/ModPlayer 各自嵌一份 <see cref="WraithProgressStore"/>
    /// </summary>
    public sealed class WraithWorldProgress : ModSystem
    {
        public static WraithProgressStore Store { get; private set; } = new();

        /// <summary>登记一次遭遇，仅权威端生效</summary>
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
