using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    internal class ADVSave
    {
        public bool FirstMet;

        public virtual TagCompound SaveData() {
            TagCompound tag = new TagCompound {
                ["FirstMet"] = FirstMet,
            };
            return tag;
        }

        public virtual void LoadData(TagCompound tag) {
            if (tag.TryGet("FirstMet", out bool firstMet)) {
                FirstMet = firstMet;
            }
        }
    }
}
