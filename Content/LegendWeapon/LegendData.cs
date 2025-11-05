using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using InnoVault.GameSystem;
using System.IO;
using Terraria;
using Terraria.ID;
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
        public virtual int TargetLevel => 0;
        /// <summary>
        /// 是否跳过升级（用于UI确认后调用）
        /// </summary>
        public string DontUpgradeName;

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

        public static void ResetInventory(Player player) {
            foreach (var i in player.inventory) {
                if (!i.Alives()) {
                    continue;
                }
                try {
                    var data = i.CWR().LegendData;
                    if (data == null) {
                        continue;
                    }
                    data.DontUpgradeName = string.Empty;//重置跳过升级标记
                } catch {
                    continue;
                }
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

        public virtual void Update(Item item) {
            if (DontUpgradeName == SaveWorld.WorldFullName) {
                return;//跳过升级
            }
            //检测是否需要升级
            if (TargetLevel <= Level && !UpgradeTagNameIsEmpty) {
                return;
            }
            //确保不是在同一个世界内多次升级
            if (UpgradeWorldFullName != string.Empty && UpgradeWorldFullName != SaveWorld.WorldFullName) {
                if (item != null && item.type > ItemID.None) {
                    //检查该物品是否就是当前LegendData所属的物品
                    if (item.CWR().LegendData == this) {
                        //弹出确认UI
                        LegendUpgradeConfirmUI.RequestUpgrade(item, this, TargetLevel);
                        return;//等待用户确认，不自动升级
                    }
                }
            }

            //如果不是手持状态，或者确认UI已经处理完毕，则自动升级（保持原有行为）
            //这样可以兼容旧存档和非手持情况
            if (!LegendUpgradeConfirmUI.Instance.Active) {
                UpgradeWorldName = Main.worldName;
                UpgradeWorldFullName = SaveWorld.WorldFullName;
                Level = TargetLevel;
            }
        }

        public void DoUpdate(Item item) {
            Update(item);
        }
    }
}
