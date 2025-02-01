using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon
{
    public class LegendData
    {
        /// <summary>
        /// 成长等级
        /// </summary>
        public int Level = 0;
        /// <summary>
        /// 上一次提升等级的世界名字
        /// </summary>
        public string UpgradeWorldName = "";

        public static void Create(Item item) {
            if (item.type == ModContent.ItemType<Murasama>()) {
                item.CWR().LegendData = new MuraData();
            }
            else if (item.type == ModContent.ItemType<SHPC>()) {
                item.CWR().LegendData = new LegendData();
            }
            else if (item.type == ModContent.ItemType<HalibutCannon>()) {
                item.CWR().LegendData = new LegendData();
            }
        }

        public static string GetWorldUpLines(CWRItems cwrItem) {
            string worldName = cwrItem.LegendData.UpgradeWorldName;
            string text = "";
            if (Main.worldName != worldName && worldName != "" && worldName != null) {
                text = CWRUtils.FormatColorTextMultiLine($"--{MuraText.GetTextValue("World_Text0")}:<{worldName}>", Color.Gold);
            }
            return text;
        }

        public virtual void SaveData(Item item, TagCompound tag) {
            if (Level > 0) {
                tag["LegendData:Level"] = Level;
            }
            if (UpgradeWorldName != "") {
                tag["LegendData:UpgradeWorldName"] = UpgradeWorldName;
            }
        }
        public virtual void LoadData(Item item, TagCompound tag) {
            if (!tag.TryGet("LegendData:Level", out Level)) {
                Level = 0;
            }
            if (!tag.TryGet("LegendData:UpgradeWorldName", out UpgradeWorldName)) {
                UpgradeWorldName = "";
            }
        }
        public virtual void Update(int currentLevel) {
            if (currentLevel > Level) {
                UpgradeWorldName = Main.worldName;
                Level = currentLevel;
            }
        }
    }
}
