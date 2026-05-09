using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend;
using CalamityOverhaul.OtherMods.SubWorld;
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

    /// <summary>
    /// 传奇武器的可序列化升级数据基类
    /// <para>
    /// 数据职责清晰拆分：
    /// <list type="bullet">
    /// <item>数据本身：<see cref="Level"/>、<see cref="UpgradeWorldFullName"/> 等</item>
    /// <item>判断逻辑：<see cref="NeedUpgrade"/>、<see cref="NeedCrossWorldConfirm"/></item>
    /// <item>升级动作：<see cref="PerformUpgrade"/>、<see cref="MarkSkippedInCurrentWorld"/></item>
    /// <item>世界感知：所有"是否在升级世界"的判断都集中在 <see cref="IsUpgradeWorld"/></item>
    /// </list>
    /// </para>
    /// <para>
    /// UI 弹窗逻辑被完全外置到 <see cref="LegendUpgradeManager"/>，本类不再直接接触 UI
    /// </para>
    /// </summary>
    public abstract class LegendData
    {
        /// <summary>
        /// 成长等级
        /// </summary>
        public int Level = 0;
        /// <summary>
        /// 上一次提升等级的世界名字(显示用)
        /// </summary>
        public string UpgradeWorldName = "";
        /// <summary>
        /// 上一次提升等级的世界完整名字(用于唯一性判断)
        /// </summary>
        public string UpgradeWorldFullName = "";
        /// <summary>
        /// 当前会话中被玩家显式跳过的世界完整名字
        /// <para>该字段是会话级别的，不写入磁盘，玩家重新进入世界时由<see cref="ResetInventory"/>清空</para>
        /// </summary>
        public string SkipUpgradeWorldFullName = string.Empty;

        /// <summary>
        /// 升级世界标签是否完全为空(说明这是首次升级)
        /// </summary>
        public bool UpgradeTagNameIsEmpty => string.IsNullOrEmpty(UpgradeWorldName) || string.IsNullOrEmpty(UpgradeWorldFullName);
        /// <summary>
        /// 当前所在世界是否就是上次升级的世界
        /// </summary>
        public bool IsUpgradeWorld => UpgradeWorldFullName == SaveWorld.WorldFullName;
        /// <summary>
        /// 这个传奇应该升级到的等级，由派生类根据世界 Boss 进度等条件计算
        /// </summary>
        public virtual int TargetLevel => 0;

        /// <summary>
        /// 兼容旧字段名：保留对外暴露的<see cref="DontUpgradeName"/>属性
        /// </summary>
        public string DontUpgradeName {
            get => SkipUpgradeWorldFullName;
            set => SkipUpgradeWorldFullName = value ?? string.Empty;
        }

        #region 序列化

        public void NetSend(Item item, BinaryWriter writer) {
            writer.Write(Level);
            writer.Write(UpgradeWorldName ?? string.Empty);
            writer.Write(UpgradeWorldFullName ?? string.Empty);
            writer.Write(SkipUpgradeWorldFullName ?? string.Empty);
            SendLegend(item, writer);
        }

        public void NetReceive(Item item, BinaryReader reader) {
            Level = reader.ReadInt32();
            UpgradeWorldName = reader.ReadString();
            UpgradeWorldFullName = reader.ReadString();
            SkipUpgradeWorldFullName = reader.ReadString();
            ReceiveLegend(item, reader);
        }

        public virtual void SendLegend(Item item, BinaryWriter writer) { }

        public virtual void ReceiveLegend(Item item, BinaryReader reader) { }

        public virtual void SaveData(Item item, TagCompound tag) {
            if (Level > 0) {
                tag["LegendData:Level"] = Level;
            }
            if (!string.IsNullOrEmpty(UpgradeWorldName)) {
                tag["LegendData:UpgradeWorldName"] = UpgradeWorldName;
            }
            if (!string.IsNullOrEmpty(UpgradeWorldFullName)) {
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
                //旧存档兼容：如果只存了 UpgradeWorldName，直接拿来当 UpgradeWorldFullName
                if (!tag.TryGet("LegendData:UpgradeWorldFullName", out UpgradeWorldFullName)) {
                    UpgradeWorldFullName = UpgradeWorldName;
                }
                //会话级跳过标记不持久化
                SkipUpgradeWorldFullName = string.Empty;
            } catch {
                Level = 0;
                UpgradeWorldName = "";
                UpgradeWorldFullName = "";
                SkipUpgradeWorldFullName = string.Empty;
            }
        }

        #endregion

        #region 工具方法

        public static string GetWorldUpLines(CWRItem cwrItem) {
            string text = "";
            if (cwrItem?.LegendData == null) {
                return text;
            }
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

        /// <summary>
        /// 进入世界时调用：清理玩家背包内所有传奇武器的会话级跳过标记
        /// </summary>
        public static void ResetInventory(Player player) {
            if (player == null) {
                return;
            }
            foreach (var i in player.inventory) {
                if (!i.Alives()) {
                    continue;
                }
                try {
                    var data = i.CWR()?.LegendData;
                    if (data == null) {
                        continue;
                    }
                    data.SkipUpgradeWorldFullName = string.Empty;
                } catch {
                    continue;
                }
            }
        }

        #endregion

        #region 升级判定与动作

        /// <summary>
        /// 是否仍然需要升级(综合等级、世界标签、跳过标记的判断)
        /// </summary>
        public bool NeedUpgrade() {
            //当前世界已显式跳过 -> 不需要
            if (!string.IsNullOrEmpty(SkipUpgradeWorldFullName) && SkipUpgradeWorldFullName == SaveWorld.WorldFullName) {
                return false;
            }
            //已经达到目标等级且记录了来源世界 -> 不需要
            if (TargetLevel <= Level && !UpgradeTagNameIsEmpty) {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 是否需要跨世界确认(从其它世界带过来的传奇武器)
        /// </summary>
        public bool NeedCrossWorldConfirm() {
            //子世界切换不视为跨世界，避免在副本/特殊场景反复弹窗
            if (SubWorldRef.AnyActiveSubWorld()) {
                return false;
            }
            //首次升级不算跨世界
            if (UpgradeTagNameIsEmpty) {
                return false;
            }
            //来源世界与当前世界一致 -> 不需要确认
            return UpgradeWorldFullName != SaveWorld.WorldFullName;
        }

        /// <summary>
        /// 实际执行一次升级：将等级抬升到<see cref="TargetLevel"/>并记录当前世界
        /// </summary>
        public void PerformUpgrade() {
            UpgradeWorldName = Main.worldName;
            UpgradeWorldFullName = SaveWorld.WorldFullName;
            SkipUpgradeWorldFullName = string.Empty;
            Level = TargetLevel;
        }

        /// <summary>
        /// 在当前世界标记为"跳过升级"，下一次进入世界(<see cref="ResetInventory"/>)时会清除
        /// </summary>
        public void MarkSkippedInCurrentWorld() {
            SkipUpgradeWorldFullName = SaveWorld.WorldFullName;
        }

        #endregion

        #region 调用入口

        /// <summary>
        /// 主更新入口
        /// <para>不同<paramref name="context"/>有不同行为：</para>
        /// <list type="bullet">
        /// <item><see cref="LegendUpdateContext.PlayerHolding"/> / <see cref="LegendUpdateContext.PlayerInventory"/>:
        ///   同世界则静默升级；跨世界且物品归属本地玩家时通过<see cref="LegendUpgradeManager"/>请求 UI 确认</item>
        /// <item><see cref="LegendUpdateContext.StorageOperation"/> / <see cref="LegendUpdateContext.WorldItem"/>:
        ///   仅同世界静默升级，跨世界一律不动</item>
        /// </list>
        /// </summary>
        /// <param name="item">承载本数据的物品</param>
        /// <param name="owner">物品所属玩家(仅 PlayerHolding/PlayerInventory 上下文需要)</param>
        /// <param name="context">调用上下文</param>
        public virtual void Update(Item item, Player owner, LegendUpdateContext context) {
            if (!NeedUpgrade()) {
                return;
            }

            if (item == null || item.type <= ItemID.None) {
                return;
            }

            //数据归属校验：必须挂在该物品上才能动它，避免越权写入
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null || cwrItem.LegendData != this) {
                return;
            }

            switch (context) {
                case LegendUpdateContext.PlayerHolding:
                case LegendUpdateContext.PlayerInventory:
                    if (NeedCrossWorldConfirm()) {
                        //仅当物品归属本地玩家时弹窗，避免多人模式下 A 的物品在 B 屏幕上弹窗
                        LegendUpgradeManager.Request(this, item, TargetLevel, owner);
                        return;
                    }
                    PerformUpgrade();
                    break;
                case LegendUpdateContext.StorageOperation:
                case LegendUpdateContext.WorldItem:
                    //存储/世界物品上下文：跨世界一律静默不动，等玩家拿到背包再说
                    if (NeedCrossWorldConfirm()) {
                        return;
                    }
                    PerformUpgrade();
                    break;
            }
        }

        /// <summary>
        /// 玩家相关上下文(手持/背包)的便捷调用
        /// </summary>
        public void DoUpdate(Item item, Player owner, LegendUpdateContext context) {
            Update(item, owner, context);
        }

        /// <summary>
        /// 非玩家归属上下文(存档/世界物品)的便捷调用
        /// </summary>
        public void DoUpdate(Item item, LegendUpdateContext context) {
            Update(item, null, context);
        }

        /// <summary>
        /// 默认上下文为<see cref="LegendUpdateContext.WorldItem"/>(静默升级)，保留兼容旧调用
        /// </summary>
        public void DoUpdate(Item item) {
            Update(item, null, LegendUpdateContext.WorldItem);
        }

        #endregion
    }
}
