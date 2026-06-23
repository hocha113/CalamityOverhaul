using System;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Common
{
    internal static class CWRSaveData
    {
        public static Item LoadItemTag(TagCompound itemTag, string context) {
            if (itemTag == null) {
                return new Item();
            }
            try {
                return ItemIO.Load(itemTag) ?? new Item();
            } catch (Exception ex) {
                CWRMod.Instance?.Logger?.Error($"[{context}] Failed to load saved item: {ex.Message}");
                return new Item();
            }
        }

        public static Item LoadItemFromTag(TagCompound tag, string key, string context) {
            if (tag != null && tag.TryGet<TagCompound>(key, out TagCompound itemTag)) {
                return LoadItemTag(itemTag, $"{context}:{key}");
            }
            return new Item();
        }
    }
}
