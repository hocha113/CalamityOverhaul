using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using InnoVault.GameSystem;
using System.IO;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon
{
    public abstract class LegendData
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
        /// 上一次提升等级的世界内部名字
        /// </summary>
        public string UpgradeWorldFullName = "";
        /// <summary>
        /// 标签名是否为空
        /// </summary>
        public bool UpgradeTagNameIsEmpty => UpgradeWorldName == "" || UpgradeWorldFullName == "";
        /// <summary>
        /// 当前是否是上次升级的世界
        /// </summary>
        public bool IsUpgradeWorld => UpgradeWorldFullName == SaveWorld.WorldFullName;
        /// <summary>
        /// 这个传奇应该升级到的等级
        /// </summary>
        public virtual int TargetLevle => 0;

        public void NetSend(Item item, BinaryWriter writer) {
            writer.Write(Level);
            writer.Write(UpgradeWorldName);
            writer.Write(UpgradeWorldFullName);
            SendLegend(item, writer);
        }

        public void NetReceive(Item item, BinaryReader reader) {
            Level = reader.ReadInt32();
            UpgradeWorldName = reader.ReadString();
            UpgradeWorldFullName = reader.ReadString();
            ReceiveLegend(item, reader);
        }

        public virtual void SendLegend(Item item, BinaryWriter writer) {

        }

        public virtual void ReceiveLegend(Item item, BinaryReader reader) {

        }

        public static string GetWorldUpLines(CWRItem cwrItem) {
            string text = "";
            if (!cwrItem.LegendData.UpgradeTagNameIsEmpty && !cwrItem.LegendData.IsUpgradeWorld) {
                string worldName = cwrItem.LegendData.UpgradeWorldName;
                string key = MuraText.GetTextKey("World_Text0");
                text = VaultUtils.FormatColorTextMultiLine($"{Language.GetTextValue(key, worldName, cwrItem.LegendData.Level)}", Color.Gold);
            }
            return text;
        }

        public static string GetLevelTrialPreText(CWRItem cwrItem, string key, string level) {
            string worldLine = GetWorldUpLines(cwrItem);
            string trialPreText = $"[c/00736d:{CWRLocText.GetTextValue(key) + " "}{level}]";
            if (worldLine == "") {
                return trialPreText;
            }
            return worldLine + "\n" + trialPreText;
        }

        public virtual void SaveData(Item item, TagCompound tag) {
            if (Level > 0) {
                tag["LegendData:Level"] = Level;
            }
            if (UpgradeWorldName != "") {
                tag["LegendData:UpgradeWorldName"] = UpgradeWorldName;
            }
            if (UpgradeWorldFullName != "") {
                tag["LegendData:UpgradeWorldFullName"] = UpgradeWorldFullName;
            }
        }

        public virtual void LoadData(Item item, TagCompound tag) {
            try {
                if (!tag.TryGet("LegendData:Level", out Level)) {
                    Level = 0;
                }
                if (!tag.TryGet("LegendData:UpgradeWorldName", out UpgradeWorldName)) {
                    UpgradeWorldName = "";
                }
                if (!tag.TryGet("LegendData:UpgradeWorldFullName", out UpgradeWorldFullName)) {
                    UpgradeWorldFullName = UpgradeWorldName;//这样赋值，如果是第一次加载，可以适配旧存档
                }
            } catch {
                Level = 0;
                UpgradeWorldName = "";
                UpgradeWorldFullName = "";
            }
        }

        public virtual void Update() {
            if (TargetLevle > Level || UpgradeTagNameIsEmpty) {
                UpgradeWorldName = Main.worldName;
                UpgradeWorldFullName = SaveWorld.WorldFullName;
                Level = TargetLevle;
            }
        }

        public void DoUpdate() {
            if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                return;
            }
            Update();
        }
    }
}
