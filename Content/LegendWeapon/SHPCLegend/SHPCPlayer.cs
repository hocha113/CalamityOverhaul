using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    internal class SHPCPlayer : ModPlayer
    {
        public const int PresetCount = 3;

        public Item[] Modules;

        //当前激活的预设索引（0/1/2）
        public int ActivePreset = 0;

        //三套预设，每套包含 SlotCount 个槽位
        public Item[][] Presets;

        //超杀层数（0-10），每次击杀叠加，随时间衰减
        public int OverkillStacks;
        //层数衰减计时器（每120帧 -1 层）
        public int OverkillTimer;

        //模具加工台：六类碎片数量（按 SHPCSlotCategory 索引）
        public int[] MoldShards;
        //模具加工台：已发现模块的 ItemType 集合（图鉴主数据）
        public HashSet<int> DiscoveredModules;
        //模具加工台：六类钉选的"固定重铸目标" ItemType，-1 表示该类别仍为随机模式
        public int[] PinnedReforgeTarget;

        public static SHPCPlayer Get(Player player) => player.GetModPlayer<SHPCPlayer>();

        public override void Initialize() {
            Modules = CreateEmptyModules();
            Presets = CreateEmptyPresets();
            ActivePreset = 0;

            MoldShards = new int[SHPCData.SlotCount];
            DiscoveredModules = new HashSet<int>();
            PinnedReforgeTarget = new int[SHPCData.SlotCount];
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                PinnedReforgeTarget[i] = -1;
            }
        }

        /// <summary>
        /// 注册一次"发现"事件，返回是否为首次（true 表示之前未发现）
        /// </summary>
        public bool RegisterDiscovered(int moduleType) {
            if (moduleType <= 0) {
                return false;
            }
            DiscoveredModules ??= new HashSet<int>();
            return DiscoveredModules.Add(moduleType);
        }

        /// <summary>
        /// 钉选/取消钉选指定类别的固定重铸目标
        /// moduleType = -1 时取消钉选；否则要求该 type 是合法的 SHPCModuleItem 且槽位匹配且在图鉴中
        /// </summary>
        public bool TryPinReforge(SHPCSlotCategory cat, int moduleType) {
            PinnedReforgeTarget ??= new int[SHPCData.SlotCount];
            int idx = (int)cat;
            if (idx < 0 || idx >= SHPCData.SlotCount) {
                return false;
            }
            if (moduleType == -1) {
                PinnedReforgeTarget[idx] = -1;
                return true;
            }
            if (DiscoveredModules == null || !DiscoveredModules.Contains(moduleType)) {
                return false;
            }
            if (!ContentSamples.ItemsByType.TryGetValue(moduleType, out Item sample)) {
                return false;
            }
            if (sample.ModItem is SHPCModuleItem mod && mod.SlotCategory == cat) {
                PinnedReforgeTarget[idx] = moduleType;
                return true;
            }
            return false;
        }

        private static Item[] CreateEmptyModules() {
            Item[] arr = new Item[SHPCData.SlotCount];
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                arr[i] = new Item();
            }
            return arr;
        }

        private static Item[][] CreateEmptyPresets() {
            Item[][] p = new Item[PresetCount][];
            for (int i = 0; i < PresetCount; i++) {
                p[i] = CreateEmptyModules();
            }
            return p;
        }

        private static Item[] CloneModules(Item[] src) {
            Item[] dst = new Item[SHPCData.SlotCount];
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                dst[i] = src[i] != null && !src[i].IsAir ? src[i].Clone() : new Item();
            }
            return dst;
        }

        //切换到指定预设，先保存当前槽位到活跃预设，再载入目标预设
        public void SwitchPreset(int newIdx) {
            if (newIdx < 0 || newIdx >= PresetCount || newIdx == ActivePreset) {
                return;
            }
            Presets ??= CreateEmptyPresets();
            Presets[ActivePreset] = CloneModules(SafeModules());
            ActivePreset = newIdx;
            Modules = CloneModules(Presets[ActivePreset]);
        }

        private Item[] SafeModules() {
            Modules ??= CreateEmptyModules();
            return Modules;
        }

        public Item GetModule(int slotIdx) {
            if (slotIdx < 0 || slotIdx >= SHPCData.SlotCount) {
                return null;
            }
            Item it = SafeModules()[slotIdx];
            return it == null || it.IsAir ? null : it;
        }

        public Item TakeModule(int slotIdx) {
            if (slotIdx < 0 || slotIdx >= SHPCData.SlotCount) {
                return null;
            }
            Item[] modules = SafeModules();
            Item old = modules[slotIdx];
            modules[slotIdx] = new Item();
            return old == null || old.IsAir ? null : old;
        }

        public Item PutModule(int slotIdx, Item module) {
            if (slotIdx < 0 || slotIdx >= SHPCData.SlotCount || module == null || module.IsAir) {
                return null;
            }
            Item[] modules = SafeModules();
            Item old = modules[slotIdx];
            Item cloned = module.Clone();
            cloned.stack = 1;
            modules[slotIdx] = cloned;
            return old == null || old.IsAir ? null : old;
        }

        public int EquippedCount() {
            int n = 0;
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                if (GetModule(i) != null) {
                    n++;
                }
            }
            return n;
        }

        public override void PostUpdate() {
            SHPCModificationSystem.ForEachModule(Player, mod => mod.OnPlayerUpdate(Player));
        }

        public override void SaveData(TagCompound tag) {
            try {
                //保存前先将当前槽位同步到活跃预设
                Presets ??= CreateEmptyPresets();
                Presets[ActivePreset] = CloneModules(SafeModules());

                tag["SHPC_ActivePreset"] = ActivePreset;

                for (int p = 0; p < PresetCount; p++) {
                    for (int s = 0; s < SHPCData.SlotCount; s++) {
                        Item m = Presets[p][s];
                        if (m != null && !m.IsAir) {
                            tag[$"SHPC_Preset_{p}_{s}"] = ItemIO.Save(m);
                        }
                    }
                }

                //模具加工台持久化：碎片数 / 图鉴 / 钉选
                MoldShards ??= new int[SHPCData.SlotCount];
                PinnedReforgeTarget ??= new int[SHPCData.SlotCount];
                DiscoveredModules ??= new HashSet<int>();

                //写入前重新校验长度 = SlotCount（升版若 SlotCount 改了能自动适配）
                int[] shardsSafe = new int[SHPCData.SlotCount];
                int[] pinnedSafe = new int[SHPCData.SlotCount];
                for (int i = 0; i < SHPCData.SlotCount; i++) {
                    shardsSafe[i] = i < MoldShards.Length ? System.Math.Max(0, MoldShards[i]) : 0;
                    pinnedSafe[i] = i < PinnedReforgeTarget.Length ? PinnedReforgeTarget[i] : -1;
                }

                //DiscoveredModules 排序后写出，避免每次保存产生无意义的 diff
                List<int> discoveredSorted = DiscoveredModules.Where(t => t > 0).Distinct().OrderBy(t => t).ToList();

                tag["SHPC_MoldShards"] = shardsSafe.ToList();
                tag["SHPC_DiscoveredModules"] = discoveredSorted;
                tag["SHPC_PinnedReforgeTarget"] = pinnedSafe.ToList();
            } catch (System.Exception ex) {
                CWRMod.Instance.Logger.Error($"SHPCPlayer.SaveData Error: {ex}");
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                Presets ??= CreateEmptyPresets();

                //读取活跃预设索引（旧存档无此字段则默认0）
                ActivePreset = tag.TryGet("SHPC_ActivePreset", out int savedPreset)
                    ? System.Math.Clamp(savedPreset, 0, PresetCount - 1)
                    : 0;

                //以 SHPC_ActivePreset 是否存在作为新格式标记，空存档（全槽位为空）下该 key 同样存在
                bool isNewFormat = tag.ContainsKey("SHPC_ActivePreset");
                for (int p = 0; p < PresetCount; p++) {
                    for (int s = 0; s < SHPCData.SlotCount; s++) {
                        if (tag.TryGet($"SHPC_Preset_{p}_{s}", out TagCompound modTag)) {
                            try {
                                Presets[p][s] = ItemIO.Load(modTag);
                            } catch {
                                Presets[p][s] = new Item();
                            }
                        }
                        else {
                            Presets[p][s] = new Item();
                        }
                    }
                }

                //兼容旧存档：旧格式只保存 SHPC_Mod_{i}，迁移到预设0
                if (!isNewFormat) {
                    for (int i = 0; i < SHPCData.SlotCount; i++) {
                        if (tag.TryGet($"SHPC_Mod_{i}", out TagCompound modTag)) {
                            try {
                                Presets[0][i] = ItemIO.Load(modTag);
                            } catch {
                                Presets[0][i] = new Item();
                            }
                        }
                    }
                }

                //将活跃预设的内容加载到 Modules 作为当前使用状态
                Modules = CloneModules(Presets[ActivePreset]);

                //模具加工台持久化读取，旧存档兜底
                MoldShards = new int[SHPCData.SlotCount];
                PinnedReforgeTarget = new int[SHPCData.SlotCount];
                for (int i = 0; i < SHPCData.SlotCount; i++) {
                    PinnedReforgeTarget[i] = -1;
                }
                DiscoveredModules = new HashSet<int>();

                //碎片：仅复制可用范围；负值与异常大值都做约束（防存档损坏 / 篡改）
                const int ShardHardCap = 9_999_999;
                if (tag.TryGet("SHPC_MoldShards", out List<int> shardList) && shardList != null) {
                    int copy = System.Math.Min(SHPCData.SlotCount, shardList.Count);
                    for (int i = 0; i < copy; i++) {
                        MoldShards[i] = System.Math.Clamp(shardList[i], 0, ShardHardCap);
                    }
                }
                //图鉴：去重 + 过滤已不存在的 type（mod 卸载 / type 重排时不残留 ghost ID）
                if (tag.TryGet("SHPC_DiscoveredModules", out List<int> discList) && discList != null) {
                    foreach (int t in discList) {
                        if (t > 0 && IsValidShpcModuleType(t)) {
                            DiscoveredModules.Add(t);
                        }
                    }
                }
                //钉选：校验目标类型有效且与索引类别匹配；否则降级为 -1
                if (tag.TryGet("SHPC_PinnedReforgeTarget", out List<int> pinList) && pinList != null) {
                    int copy = System.Math.Min(SHPCData.SlotCount, pinList.Count);
                    for (int i = 0; i < copy; i++) {
                        int target = pinList[i];
                        if (target <= 0) {
                            PinnedReforgeTarget[i] = -1;
                            continue;
                        }
                        if (!ContentSamples.ItemsByType.TryGetValue(target, out Item sample)
                            || sample.ModItem is not SHPCModuleItem mod
                            || (int)mod.SlotCategory != i) {
                            //目标无效或类别不匹配，回退到随机模式而不是抛弃整段存档
                            PinnedReforgeTarget[i] = -1;
                            continue;
                        }
                        PinnedReforgeTarget[i] = target;
                    }
                }

                //老存档兜底：扫描背包 + 所有预设里已有的 SHPC 改件，确保图鉴不丢
                BackfillDiscoveredFromInventoryAndPresets();
            } catch (System.Exception ex) {
                CWRMod.Instance.Logger.Error($"SHPCPlayer.LoadData Error: {ex}");
            }
        }

        /// <summary>校验某 ItemType 是否仍为合法的 SHPC 改件（mod 卸载、type 重排时返回 false）</summary>
        private static bool IsValidShpcModuleType(int type) {
            return ContentSamples.ItemsByType.TryGetValue(type, out Item sample)
                && sample.ModItem is SHPCModuleItem;
        }

        private void BackfillDiscoveredFromInventoryAndPresets() {
            DiscoveredModules ??= new HashSet<int>();
            if (Player?.inventory != null) {
                for (int i = 0; i < Player.inventory.Length; i++) {
                    Item it = Player.inventory[i];
                    if (it != null && !it.IsAir && it.ModItem is SHPCModuleItem) {
                        DiscoveredModules.Add(it.type);
                    }
                }
            }
            if (Presets != null) {
                for (int p = 0; p < PresetCount; p++) {
                    if (Presets[p] == null) {
                        continue;
                    }
                    for (int s = 0; s < SHPCData.SlotCount; s++) {
                        Item m = Presets[p][s];
                        if (m != null && !m.IsAir && m.ModItem is SHPCModuleItem) {
                            DiscoveredModules.Add(m.type);
                        }
                    }
                }
            }
        }
    }
}
