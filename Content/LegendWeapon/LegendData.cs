using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon
{
    /// <summary>传奇升级 Update 调用场景</summary>
    public enum LegendUpdateContext
    {
        /// <summary>手持</summary>
        PlayerHolding,
        /// <summary>背包</summary>
        PlayerInventory,
        /// <summary>存读档</summary>
        StorageOperation,
        /// <summary>世界物品(掉落/箱子)</summary>
        WorldItem
    }

    /// <summary>传奇武器可序列化升级数据；<see cref="PerformUpgrade"/> 用 Math.Max，等级只升不降</summary>
    public abstract class LegendData
    {
        /// <summary>信任世界上限</summary>
        private const int MaxTrustedWorlds = 32;

        /// <summary>已完成试炼键联机上限</summary>
        private const int MaxCompletedTrialKeys = 64;

        private const int MaxWorldNameBytes = 1024;
        private const int MaxWorldFullNameBytes = 4096;
        private const int MaxTrialKeyBytes = 256;
        private const int MaxTrialRouteSignatureBytes = MaxCompletedTrialKeys * MaxTrialKeyBytes;

        /// <summary>成长等级</summary>
        public int Level = 0;
        /// <summary>上次升级世界名(显示)</summary>
        public string UpgradeWorldName = "";
        /// <summary>上次升级世界 FullName</summary>
        public string UpgradeWorldFullName = "";
        /// <summary>本会话跳过世界 FullName，不入档，<see cref="ResetInventory"/> 清空</summary>
        public string SkipUpgradeWorldFullName = string.Empty;
        /// <summary>信任世界 FullName 列表，持久化</summary>
        public List<string> TrustedWorldFullNames = new();
        /// <summary>试炼 schema 版本，旧档从 Level 迁移稳定键</summary>
        public int TrialSchemaVersion;
        /// <summary>试炼路线签名，外部模组开关重链</summary>
        public string TrialRouteSignature = string.Empty;
        /// <summary>已完成试炼稳定键，非数组下标</summary>
        public List<string> CompletedTrialKeys = new();

        /// <summary>升级世界 tag 为空(首次升级)</summary>
        public bool UpgradeTagNameIsEmpty => string.IsNullOrEmpty(UpgradeWorldName) || string.IsNullOrEmpty(UpgradeWorldFullName);
        /// <summary>当前世界即上次升级世界</summary>
        public bool IsUpgradeWorld => UpgradeWorldFullName == SaveWorld.WorldFullName;
        /// <summary>派生类按 Boss 进度算的目标等级</summary>
        public virtual int TargetLevel => 0;
        /// <summary>版本化试炼路线，无试炼则 null</summary>
        internal virtual IReadOnlyList<LegendTrialDefinition> TrialDefinitions => null;
        /// <summary>试炼路线 schema 版本</summary>
        public virtual int CurrentTrialSchemaVersion => 1;

        /// <summary>旧字段名 <see cref="DontUpgradeName"/></summary>
        public string DontUpgradeName {
            get => SkipUpgradeWorldFullName;
            set => SkipUpgradeWorldFullName = value ?? string.Empty;
        }

        /// <summary>
        /// 深拷贝钩子，<see cref="CWRItem.CloneCWRItem"/> 调用
        /// 基类拷贝值与列表；派生若持引用型进度须覆写，否则两把刀共享进度
        /// </summary>
        public virtual LegendData Clone(Item item) {
            LegendData clone = (LegendData)MemberwiseClone();
            clone.TrustedWorldFullNames = TrustedWorldFullNames != null ? new List<string>(TrustedWorldFullNames) : new List<string>();
            clone.CompletedTrialKeys = CompletedTrialKeys != null ? new List<string>(CompletedTrialKeys) : new List<string>();
            return clone;
        }

        #region 序列化

        public void NetSend(Item item, BinaryWriter writer) {
            writer.Write(Level);
            CWRNetGuard.WriteString(writer, UpgradeWorldName, MaxWorldNameBytes);
            CWRNetGuard.WriteString(writer, UpgradeWorldFullName, MaxWorldFullNameBytes);
            CWRNetGuard.WriteString(writer, SkipUpgradeWorldFullName, MaxWorldFullNameBytes);
            //可信世界，长度前缀+字符串
            List<string> trustedWorlds = TrustedWorldFullNames ?? [];
            int trustedCount = Math.Min(trustedWorlds.Count, MaxTrustedWorlds);
            int trustedStart = trustedWorlds.Count - trustedCount;
            writer.Write(trustedCount);
            for (int i = 0; i < trustedCount; i++) {
                CWRNetGuard.WriteString(writer, trustedWorlds[trustedStart + i], MaxWorldFullNameBytes);
            }
            writer.Write(TrialSchemaVersion);
            CWRNetGuard.WriteString(writer, TrialRouteSignature, MaxTrialRouteSignatureBytes);
            List<string> completedTrials = CompletedTrialKeys ?? [];
            int completedCount = Math.Min(completedTrials.Count, MaxCompletedTrialKeys);
            writer.Write(completedCount);
            for (int i = 0; i < completedCount; i++) {
                CWRNetGuard.WriteString(writer, completedTrials[i], MaxTrialKeyBytes);
            }
            SendLegend(item, writer);
        }

        public void NetReceive(Item item, BinaryReader reader) {
            int level = Math.Max(reader.ReadInt32(), 0);
            string upgradeWorldName = CWRNetGuard.ReadString(reader, MaxWorldNameBytes, "LegendData.UpgradeWorldName");
            string upgradeWorldFullName = CWRNetGuard.ReadString(reader, MaxWorldFullNameBytes, "LegendData.UpgradeWorldFullName");
            string skipUpgradeWorldFullName = CWRNetGuard.ReadString(reader, MaxWorldFullNameBytes, "LegendData.SkipUpgradeWorldFullName");
            int trustedCount = CWRNetGuard.ReadCount(reader, MaxTrustedWorlds, "LegendData.TrustedWorlds");
            List<string> trustedWorlds = new(trustedCount);
            for (int i = 0; i < trustedCount; i++) {
                string world = CWRNetGuard.ReadString(reader, MaxWorldFullNameBytes, "LegendData.TrustedWorld");
                if (!string.IsNullOrEmpty(world) && !trustedWorlds.Contains(world)) {
                    trustedWorlds.Add(world);
                }
            }
            int trialSchemaVersion = Math.Max(reader.ReadInt32(), 0);
            string trialRouteSignature = CWRNetGuard.ReadString(reader, MaxTrialRouteSignatureBytes, "LegendData.TrialRouteSignature");
            int completedCount = CWRNetGuard.ReadCount(reader, MaxCompletedTrialKeys, "LegendData.CompletedTrialKeys");
            List<string> completedTrials = new(completedCount);
            for (int i = 0; i < completedCount; i++) {
                string key = CWRNetGuard.ReadString(reader, MaxTrialKeyBytes, "LegendData.CompletedTrialKey");
                if (!string.IsNullOrEmpty(key) && !completedTrials.Contains(key)) {
                    completedTrials.Add(key);
                }
            }
            ReceiveLegend(item, reader);

            Level = level;
            UpgradeWorldName = upgradeWorldName;
            UpgradeWorldFullName = upgradeWorldFullName;
            SkipUpgradeWorldFullName = skipUpgradeWorldFullName;
            TrustedWorldFullNames = trustedWorlds;
            TrialSchemaVersion = trialSchemaVersion;
            TrialRouteSignature = trialRouteSignature;
            CompletedTrialKeys = completedTrials;
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
            //仅持久化信任世界(SkipUpgradeWorldFullName 会话级，不入档)
            if (TrustedWorldFullNames != null && TrustedWorldFullNames.Count > 0) {
                tag["LegendData:TrustedWorlds"] = TrustedWorldFullNames;
            }
            if (TrialDefinitions != null) {
                //只落已确认进度，存档时不吞并当前世界击杀
                //(否则高进度世界存一次档就永久升级)
                TrialSchemaVersion = CurrentTrialSchemaVersion;
                TrialRouteSignature = LegendTrialRouteResolver.GetRouteSignature(TrialDefinitions);
                tag["LegendData:TrialSchemaVersion"] = TrialSchemaVersion;
                if (!string.IsNullOrEmpty(TrialRouteSignature)) {
                    tag["LegendData:TrialRouteSignature"] = TrialRouteSignature;
                }
                if (CompletedTrialKeys != null && CompletedTrialKeys.Count > 0) {
                    tag["LegendData:CompletedTrialKeys"] = CompletedTrialKeys;
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
                //旧档兼容，UpgradeWorldName 顶替 FullName
                if (!tag.TryGet("LegendData:UpgradeWorldFullName", out UpgradeWorldFullName)) {
                    UpgradeWorldFullName = UpgradeWorldName;
                }
                //会话级跳过标记不持久化
                SkipUpgradeWorldFullName = string.Empty;
                TrustedWorldFullNames = new List<string>();
                if (tag.TryGet("LegendData:TrustedWorlds", out List<string> trusted) && trusted != null) {
                    foreach (var w in trusted) {
                        if (!string.IsNullOrEmpty(w) && !TrustedWorldFullNames.Contains(w)) {
                            TrustedWorldFullNames.Add(w);
                        }
                    }
                }
                if (!tag.TryGet("LegendData:TrialSchemaVersion", out TrialSchemaVersion)) {
                    TrialSchemaVersion = 0;
                }
                if (!tag.TryGet("LegendData:TrialRouteSignature", out TrialRouteSignature)) {
                    TrialRouteSignature = string.Empty;
                }
                CompletedTrialKeys = new List<string>();
                if (tag.TryGet("LegendData:CompletedTrialKeys", out List<string> completed) && completed != null) {
                    foreach (string key in completed) {
                        if (!string.IsNullOrEmpty(key) && !CompletedTrialKeys.Contains(key)) {
                            CompletedTrialKeys.Add(key);
                        }
                    }
                }
                MigrateTrialProgressFromLegacyLevel();
            } catch {
                Level = 0;
                UpgradeWorldName = "";
                UpgradeWorldFullName = "";
                SkipUpgradeWorldFullName = string.Empty;
                TrustedWorldFullNames = new List<string>();
                TrialSchemaVersion = 0;
                TrialRouteSignature = string.Empty;
                CompletedTrialKeys = new List<string>();
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
                text = VaultUtils.FormatColorTextMultiLine(
                    LegendUpgradeManagerSystem.World_Text0.Format(worldName, cwrItem.LegendData.Level), Color.Gold);
            }
            return text;
        }

        public static string GetLevelTrialPreText(CWRItem cwrItem, LocalizedText trialLabel, string level) {
            string worldLine = GetWorldUpLines(cwrItem);
            string trialPreText = $"[c/00736d:{trialLabel.Value + " "}{level}]";
            if (worldLine == "") {
                return trialPreText;
            }
            return worldLine + "\n" + trialPreText;
        }

        /// <summary>进世界时清空背包传奇的会话跳过标记</summary>
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

        /// <summary>当前世界在信任列表中</summary>
        public bool IsTrustedWorld() {
            if (TrustedWorldFullNames == null || TrustedWorldFullNames.Count == 0) {
                return false;
            }
            string current = SaveWorld.WorldFullName;
            return !string.IsNullOrEmpty(current) && TrustedWorldFullNames.Contains(current);
        }

        /// <summary>当前世界加入信任列表</summary>
        public void TrustCurrentWorld() {
            string current = SaveWorld.WorldFullName;
            if (string.IsNullOrEmpty(current)) {
                return;
            }
            TrustedWorldFullNames ??= new List<string>();
            if (TrustedWorldFullNames.Contains(current)) {
                return;
            }
            //信任世界上限，丢最早一条
            if (TrustedWorldFullNames.Count >= MaxTrustedWorlds) {
                TrustedWorldFullNames.RemoveAt(0);
            }
            TrustedWorldFullNames.Add(current);
        }

        /// <summary>仍待升级</summary>
        public bool NeedUpgrade() {
            //只读判定，勿 SyncTrialProgressFromWorld(会永久并入 CompletedTrialKeys)
            //本会话已跳过
            if (!string.IsNullOrEmpty(SkipUpgradeWorldFullName) && SkipUpgradeWorldFullName == SaveWorld.WorldFullName) {
                return false;
            }
            //已达目标且 tag 完整
            if (TargetLevel <= Level && !UpgradeTagNameIsEmpty) {
                return false;
            }
            return true;
        }

        /// <summary>跨世界/无信任/遗留无 tag 时需弹窗</summary>
        public bool NeedCrossWorldConfirm() {
            //子世界不算跨世界
            if (SubWorldRef.AnyActiveSubWorld()) {
                return false;
            }
            //信任世界直接同步
            if (IsTrustedWorld()) {
                return false;
            }
            //无 tag，Level==0 静默，Level>0 遗留须确认
            if (UpgradeTagNameIsEmpty) {
                return Level > 0;
            }
            //tag 世界与当前不同
            return UpgradeWorldFullName != SaveWorld.WorldFullName;
        }

        /// <summary>UI 展示等级 max(Level, TargetLevel)</summary>
        public int GetEffectiveTargetLevel() => Math.Max(Level, TargetLevel);

        /// <summary>升级并记当前世界，Math.Max 只升不降；<paramref name="owner"/> 供试炼键按其击杀登记过滤</summary>
        public void PerformUpgrade(Player owner) {
            SyncTrialProgressFromWorld(owner);
            UpgradeWorldName = Main.worldName;
            UpgradeWorldFullName = SaveWorld.WorldFullName;
            SkipUpgradeWorldFullName = string.Empty;
            //TargetLevel 更低也保留 Level
            Level = Math.Max(Level, TargetLevel);
        }

        /// <summary>本会话跳过，<see cref="ResetInventory"/> 清除</summary>
        public void MarkSkippedInCurrentWorld() {
            SkipUpgradeWorldFullName = SaveWorld.WorldFullName;
        }

        #endregion

        #region 调用入口

        //上次判定"无需升级"后的打盹：目标等级链（试炼路线 × 反射旗）只需低频重估。
        //用无符号帧差而非绝对帧点，换世界 GameUpdateCount 归零时差值回绕成大数、自然立即重判
        private bool upgradeCheckSnoozed;
        private uint lastUpgradeCheckFrame;

        /// <summary>升级主入口；手持/背包需确认时走 <see cref="LegendUpgradeManager"/>，存读/世界物品跨世界不动</summary>
        public virtual void Update(Item item, Player owner, LegendUpdateContext context) {
            uint now = Main.GameUpdateCount;
            if (upgradeCheckSnoozed && now - lastUpgradeCheckFrame < 30) {
                return;
            }
            lastUpgradeCheckFrame = now;
            upgradeCheckSnoozed = false;

            if (!NeedUpgrade()) {
                upgradeCheckSnoozed = true;
                return;
            }

            if (item == null || item.type <= ItemID.None) {
                return;
            }

            //LegendData 须挂在本 item
            CWRItem cwrItem = item.CWR();
            if (cwrItem == null || cwrItem.LegendData != this) {
                return;
            }

            switch (context) {
                case LegendUpdateContext.PlayerHolding:
                case LegendUpdateContext.PlayerInventory:
                    if (NeedCrossWorldConfirm()) {
                        //仅 myPlayer 弹窗
                        LegendUpgradeManager.Request(this, item, GetEffectiveTargetLevel(), owner);
                        return;
                    }
                    //信任/同世界/新物品静默升级
                    PerformUpgrade(owner);
                    break;
                case LegendUpdateContext.StorageOperation:
                case LegendUpdateContext.WorldItem:
                    //跨世界时存读/掉落不动
                    if (NeedCrossWorldConfirm()) {
                        return;
                    }
                    //无持有人语境，试炼键无登记可并，仅结算等级与世界 tag
                    PerformUpgrade(owner);
                    break;
            }
        }

        public void DoUpdate(Item item, Player owner, LegendUpdateContext context) {
            Update(item, owner, context);
        }

        public void DoUpdate(Item item, LegendUpdateContext context) {
            Update(item, null, context);
        }

        /// <summary>默认 <see cref="LegendUpdateContext.WorldItem"/></summary>
        public void DoUpdate(Item item) {
            Update(item, null, LegendUpdateContext.WorldItem);
        }

        #endregion

        #region 版本化试炼进度

        protected int GetVersionedTrialTargetLevel() {
            IReadOnlyList<LegendTrialDefinition> definitions = TrialDefinitions;
            if (definitions == null) {
                return 0;
            }
            return LegendTrialRouteResolver.GetSequentialOriginalLevel(definitions, IsTrialCompletedInVersionedState);
        }

        /// <summary>试炼已完成，键已记录或世界已击杀(委托/tooltip 同源)</summary>
        internal bool IsTrialCompleted(LegendTrialDefinition trial) => IsTrialCompletedInVersionedState(trial);

        /// <summary>
        /// 静默同步（十三·#102）：只并入「世界旗已倒 且 <paramref name="owner"/> 击杀登记里有」的试炼，
        /// 世界打过但本玩家没亲手打过的不并入，留给玩家自己触发，保住对话/礼物叙事的触发边沿。<br/>
        /// 拍板取舍（宁少并不多并，不做迁移猜测）：击杀登记是新设施，老玩家档没有历史登记，
        /// 信任老世界会从"全并"变"几乎不并"，需重打或在原升级世界续档；owner 为空
        /// （世界物品/存读档语境）同样无登记可查，一律不并
        /// </summary>
        public void SyncTrialProgressFromWorld(Player owner) {
            IReadOnlyList<LegendTrialDefinition> definitions = TrialDefinitions;
            if (definitions == null) {
                return;
            }

            CompletedTrialKeys ??= new List<string>();
            TrialSchemaVersion = CurrentTrialSchemaVersion;
            TrialRouteSignature = LegendTrialRouteResolver.GetRouteSignature(definitions);

            LegendTrialKillLedgerPlayer ledger = LegendTrialKillLedgerPlayer.TryGet(owner);
            foreach (LegendTrialDefinition trial in LegendTrialRouteResolver.GetAvailableTrials(definitions)) {
                if (!trial.IsCompleted) {
                    continue;
                }
                if (ledger == null || trial.Target?.IsPersonallyCleared(ledger.HasKilled) != true) {
                    continue;
                }
                AddCompletedTrialKey(trial.Key);
            }
            NormalizeCompletedTrialKeys(definitions);
        }

        private bool IsTrialCompletedInVersionedState(LegendTrialDefinition trial) {
            if (trial == null) {
                return false;
            }
            return (CompletedTrialKeys?.Contains(trial.Key) == true) || trial.IsCompleted;
        }

        private void MigrateTrialProgressFromLegacyLevel() {
            IReadOnlyList<LegendTrialDefinition> definitions = TrialDefinitions;
            if (definitions == null) {
                return;
            }

            CompletedTrialKeys ??= new List<string>();
            if (TrialSchemaVersion <= 0 && Level > 0) {
                foreach (string key in LegendTrialRouteResolver.GetLegacyCompletedKeys(definitions, Level)) {
                    AddCompletedTrialKey(key);
                }
            }

            TrialSchemaVersion = CurrentTrialSchemaVersion;
            TrialRouteSignature = LegendTrialRouteResolver.GetRouteSignature(definitions);
            NormalizeCompletedTrialKeys(definitions);
        }

        private void AddCompletedTrialKey(string key) {
            if (!string.IsNullOrEmpty(key) && !CompletedTrialKeys.Contains(key)) {
                CompletedTrialKeys.Add(key);
            }
        }

        /// <summary>
        /// 已发布试炼键的改名迁移表:归一化时旧键先重写再过滤,老玩家已过的席位不重打。
        /// 目标键写当前目录里的现行键,禁止链式映射(映射值不得再作映射键)
        /// </summary>
        private static readonly Dictionary<string, string> TrialKeyAliases = new() {
            //0.9202:鬼伞线利维坦席换渊晶海虾并顺移到石巨人后,石巨人席序号随之前移
            ["kikasa.012.leviathan"] = "kikasa.013.sea_shrimp",
            ["kikasa.013.golem"] = "kikasa.012.golem",
            //0.9202:四线渊海灾虫席位原位换脓蕾沙蟒
            ["shpc.005.aquatic_scourge"] = "shpc.005.fester_serpent",
            ["onikiri.005.aquatic_scourge"] = "onikiri.005.fester_serpent",
            ["kikasa.009.aquatic_scourge"] = "kikasa.009.fester_serpent",
            ["halibut.004.mech_or_aquatic_scourge"] = "halibut.004.mech_or_fester_serpent",
        };

        private void NormalizeCompletedTrialKeys(IReadOnlyList<LegendTrialDefinition> definitions) {
            if (CompletedTrialKeys == null || CompletedTrialKeys.Count == 0 || definitions == null) {
                return;
            }

            HashSet<string> knownKeys = [.. definitions
                .Where(static d => d != null && !string.IsNullOrEmpty(d.Key))
                .Select(static d => d.Key)];

            CompletedTrialKeys = [.. CompletedTrialKeys
                .Select(static key => key != null && TrialKeyAliases.TryGetValue(key, out string alias) ? alias : key)
                .Where(key => !string.IsNullOrEmpty(key) && knownKeys.Contains(key))
                .Distinct()];
        }

        #endregion
    }
}
