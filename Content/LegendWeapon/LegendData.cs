using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using System.IO;
using Terraria;
using Terraria.Localization;
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
        /// <summary>
        /// 这个传奇应该升级到的等级
        /// </summary>
        public virtual int TargetLevle => 0;

        public void NetSend(Item item, BinaryWriter writer) {
            writer.Write(Level);
            writer.Write(UpgradeWorldName);
            SendLegend(item, writer);
        }

        public void NetReceive(Item item, BinaryReader reader) {
            Level = reader.ReadInt32();
            UpgradeWorldName = reader.ReadString();
            ReceiveLegend(item, reader);
        }

        public virtual void SendLegend(Item item, BinaryWriter writer) {

        }

        public virtual void ReceiveLegend(Item item, BinaryReader reader) {

        }

        public static string GetWorldUpLines(CWRItems cwrItem) {
            string worldName = cwrItem.LegendData.UpgradeWorldName;
            string text = "";
            if (worldName != null && Main.worldName != worldName && worldName != "") {
                string key = MuraText.GetTextKey("World_Text0");
                text = CWRUtils.FormatColorTextMultiLine($"{Language.GetTextValue(key, worldName)}", Color.Gold);
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
        public virtual void Update() {
            if (TargetLevle > Level) {
                UpgradeWorldName = Main.worldName;
                Level = TargetLevle;
            }
        }
    }
}
