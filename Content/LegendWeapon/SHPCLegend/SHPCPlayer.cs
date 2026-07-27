using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.TimeFreezes;
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

        //当前预设 0/1/2
        public int ActivePreset = 0;

        //三套预设
        public Item[][] Presets;

        //超杀层 0-15，击杀叠，随时间衰减
        public int OverkillStacks;
        //超杀衰减计时
        public int OverkillTimer;
        private float overkillTimerCarry;

        //六类模具碎片
        public int[] MoldShards;
        //图鉴已发现模块
        public HashSet<int> DiscoveredModules;
        //钉选固定重铸目标，-1=随机
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

        /// <summary>注册发现，true 为首次</summary>
        public bool RegisterDiscovered(int moduleType) {
            if (moduleType <= 0) {
                return false;
            }
            DiscoveredModules ??= new HashSet<int>();
            return DiscoveredModules.Add(moduleType);
        }

        /// <summary>钉选/取消固定重铸目标</summary>
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

        //切预设，先存当前再载入
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
            UpdateOverkillStacks();
        }

        //超杀衰减放ModPlayer，卸机匣后钩子停否则层数冻结
        private void UpdateOverkillStacks() {
            if (OverkillStacks <= 0) {
                return;
            }
            if (!SHPCModificationSystem.HasModule<Modules.Frame.OverkillFrameModule>(Player)) {
                OverkillStacks = 0;
                OverkillTimer = 0;
                overkillTimerCarry = 0f;
                return;
            }
            if (OverkillTimer > 0) {
                TimeGear.ConsumeFrames(ref OverkillTimer, ref overkillTimerCarry);
                return;
            }
            OverkillStacks--;
            OverkillTimer = 90;
            overkillTimerCarry = 0f;
        }

        public override void SaveData(TagCompound tag) {
            try {
                //保存前同步当前槽到活跃预设
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

                //模具持久化，碎片/图鉴/钉选
                MoldShards ??= new int[SHPCData.SlotCount];
                PinnedReforgeTarget ??= new int[SHPCData.SlotCount];
                DiscoveredModules ??= new HashSet<int>();

                //长度对齐SlotCount
                int[] shardsSafe = new int[SHPCData.SlotCount];
                int[] pinnedSafe = new int[SHPCData.SlotCount];
                for (int i = 0; i < SHPCData.SlotCount; i++) {
                    shardsSafe[i] = i < MoldShards.Length ? System.Math.Max(0, MoldShards[i]) : 0;
                    pinnedSafe[i] = i < PinnedReforgeTarget.Length ? PinnedReforgeTarget[i] : -1;
                }

                //图鉴排序写出，减无意义diff
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

                //活跃预设，旧档默认0
                ActivePreset = tag.TryGet("SHPC_ActivePreset", out int savedPreset)
                    ? System.Math.Clamp(savedPreset, 0, PresetCount - 1)
                    : 0;

                //有SHPC_ActivePreset即新格式
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

                //旧档SHPC_Mod_i迁到预设0
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

                //活跃预设载入Modules
                Modules = CloneModules(Presets[ActivePreset]);

                //模具读取，旧档兜底
                MoldShards = new int[SHPCData.SlotCount];
                PinnedReforgeTarget = new int[SHPCData.SlotCount];
                for (int i = 0; i < SHPCData.SlotCount; i++) {
                    PinnedReforgeTarget[i] = -1;
                }
                DiscoveredModules = new HashSet<int>();

                //碎片钳位防坏档
                const int ShardHardCap = 9_999_999;
                if (tag.TryGet("SHPC_MoldShards", out List<int> shardList) && shardList != null) {
                    int copy = System.Math.Min(SHPCData.SlotCount, shardList.Count);
                    for (int i = 0; i < copy; i++) {
                        MoldShards[i] = System.Math.Clamp(shardList[i], 0, ShardHardCap);
                    }
                }
                //图鉴过滤失效type
                if (tag.TryGet("SHPC_DiscoveredModules", out List<int> discList) && discList != null) {
                    foreach (int t in discList) {
                        if (t > 0 && IsValidShpcModuleType(t)) {
                            DiscoveredModules.Add(t);
                        }
                    }
                }
                //钉选校验类别，失败降-1
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
                            //目标无效回退随机
                            PinnedReforgeTarget[i] = -1;
                            continue;
                        }
                        PinnedReforgeTarget[i] = target;
                    }
                }

                //老档补图鉴
                BackfillDiscoveredFromInventoryAndPresets();
            } catch (System.Exception ex) {
                CWRMod.Instance.Logger.Error($"SHPCPlayer.LoadData Error: {ex}");
            }
        }

        /// <summary>ItemType是否仍为合法改件</summary>
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
