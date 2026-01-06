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
    /// <summary>
    /// 传奇武器升级更新的调用上下文，用于区分不同场景下的升级行为
    /// </summary>
    public enum LegendUpdateContext
    {
        /// <summary>
        /// 玩家正在手持该物品
        /// </summary>
        PlayerHolding,
        /// <summary>
        /// 物品在玩家背包中
        /// </summary>
        PlayerInventory,
        /// <summary>
        /// 物品正在被存储或加载(存档操作)
        /// </summary>
        StorageOperation,
        /// <summary>
        /// 物品在世界中(掉落物、箱子等)
        /// </summary>
        WorldItem
    }

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
        public string DontUpgradeName = string.Empty;

        public void NetSend(Item item, BinaryWriter writer) {
            writer.Write(Level);
            writer.Write(UpgradeWorldName);
            writer.Write(UpgradeWorldFullName);
            DontUpgradeName ??= string.Empty;
            writer.Write(DontUpgradeName);
            SendLegend(item, writer);
        }

        public void NetReceive(Item item, BinaryReader reader) {
            Level = reader.ReadInt32();
            UpgradeWorldName = reader.ReadString();
            UpgradeWorldFullName = reader.ReadString();
            DontUpgradeName = reader.ReadString();
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

        /// <summary>
        /// 检查物品是否需要升级
        /// </summary>
        /// <returns>如果需要升级返回true</returns>
        public bool NeedUpgrade() {
            if (DontUpgradeName == SaveWorld.WorldFullName) {
                return false;
            }
            if (TargetLevel <= Level && !UpgradeTagNameIsEmpty) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检查是否需要跨世界升级确认(即从别的世界带过来的传奇武器)
        /// </summary>
        /// <returns>如果需要跨世界确认返回true</returns>
        public bool NeedCrossWorldConfirm() {
            return UpgradeWorldFullName != string.Empty && UpgradeWorldFullName != SaveWorld.WorldFullName;
        }

        /// <summary>
        /// 执行实际的升级操作
        /// </summary>
        private void PerformUpgrade() {
            UpgradeWorldName = Main.worldName;
            UpgradeWorldFullName = SaveWorld.WorldFullName;
            Level = TargetLevel;
        }

        public virtual void Update(Item item, LegendUpdateContext context) {
            //基础检查，如果不需要升级就直接返回
            if (!NeedUpgrade()) {
                return;
            }

            //验证物品有效性
            if (item == null || item.type <= ItemID.None) {
                return;
            }

            //验证物品的LegendData是否就是当前实例
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null || cwrItem.LegendData != this) {
                return;
            }

            //根据上下文决定升级行为
            switch (context) {
                case LegendUpdateContext.PlayerHolding:
                case LegendUpdateContext.PlayerInventory:
                    //玩家背包或手持中的物品，如果是跨世界升级需要确认
                    if (NeedCrossWorldConfirm()) {
                        //弹出确认UI，等待用户确认
                        LegendUpgradeConfirmUI.RequestUpgrade(item, this, TargetLevel);
                        return;
                    }
                    //同世界或首次升级，直接执行
                    if (!LegendUpgradeConfirmUI.Instance.Active) {
                        PerformUpgrade();
                    }
                    break;

                case LegendUpdateContext.StorageOperation:
                case LegendUpdateContext.WorldItem:
                    //存储操作或世界物品，静默升级不弹窗
                    //这样可以保证箱子里的传奇武器也能正常升级而不会干扰玩家
                    PerformUpgrade();
                    break;
            }
        }

        /// <summary>
        /// 带上下文的更新调用，推荐使用此方法
        /// </summary>
        public void DoUpdate(Item item, LegendUpdateContext context) {
            Update(item, context);
        }

        /// <summary>
        /// 无上下文的更新调用，默认为世界物品上下文(静默升级)
        /// 保留此重载以兼容旧代码
        /// </summary>
        public void DoUpdate(Item item) {
            Update(item, LegendUpdateContext.WorldItem);
        }
    }
}
