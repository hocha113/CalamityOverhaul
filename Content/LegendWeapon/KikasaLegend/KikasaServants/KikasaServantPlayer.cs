using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants
{
    /// <summary>
    /// 鬼伞·沉影盘玩家态。沉溺过的 boss 化作沉影永久入册（收集册），
    /// 湖底有三席影位，驻影即役使：影位上的鬼奴在湖就绪时自动出水随行，
    /// 湖退自散、湖涨自回；快捷转盘可逐席召/收（收起的席位溶解遣返、不再自动补位）。
    /// 编成在湖心景的编成区里改（数据随存档保存，储钱罐语义只活在所有者本机）。
    /// 影位键编码：0=空，正数=鬼奴规范 NPC 类型，负数=-械奴物品类型。
    /// 记录在沉溺权威完成帧入账（单机直写、联机走 KikasaDrownNet 的完成通报），与演出层无耦合
    /// </summary>
    public class KikasaServantPlayer : ModPlayer, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        /// <summary>沉影盘影位数</summary>
        public const int SlotCount = 3;

        /// <summary>湖最近沉溺的生物类型，0=还没沉过；未驯服生物的展示与提示仍认它</summary>
        public int LastDrownedType { get; private set; }

        /// <summary>湖最近沉入的已注册武器类型，0=没沉过；与生物记忆互斥覆盖</summary>
        public int LastDrownedItemType { get; private set; }

        //收集册：沉溺过即永久入册。鬼奴以规范 NPC 类型为键，械奴以物品类型为键
        private readonly HashSet<int> collectedServants = [];
        private readonly HashSet<int> collectedArms = [];

        //三席影位（memory key 编码见类注释）与各席的出水延迟（错拍防三只同帧破水）
        private readonly int[] lakeSlots = new int[SlotCount];
        private readonly int[] respawnDelay = new int[SlotCount];
        //逐席收起标记：true=玩家主动收起（转盘翻转），自动在场只维持未收起的席
        private readonly bool[] slotHeld = new bool[SlotCount];

        /// <summary>鬼奴溶解离场后到下一次自动出水的间隔帧</summary>
        private const int RespawnGapFrames = 26;

        public static LocalizedText ServantUnknown { get; private set; }

        public override void SetStaticDefaults() {
            ServantUnknown = this.GetLocalization(nameof(ServantUnknown), () => "还不能驱使它");
        }

        //==================== 影位读数 ====================

        /// <summary>第 i 席影位的记忆键；0=空</summary>
        internal int SlotKeyAt(int index)
            => index >= 0 && index < SlotCount ? lakeSlots[index] : 0;

        /// <summary>第 i 席是否被玩家收起（收起=不自动出水）；空席恒 false</summary>
        internal bool SlotHeldAt(int index)
            => index >= 0 && index < SlotCount && lakeSlots[index] != 0 && slotHeld[index];

        /// <summary>已驻影的席数</summary>
        internal int FilledSlotCount {
            get {
                int count = 0;
                for (int i = 0; i < SlotCount; i++) {
                    if (lakeSlots[i] != 0) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>驻着影且未被收起的席数，出力分摊按实际出战席算</summary>
        internal int ActiveSlotCount {
            get {
                int count = 0;
                for (int i = 0; i < SlotCount; i++) {
                    if (lakeSlots[i] != 0 && !slotHeld[i]) {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>第 i 席的灵异亲和；空席/械奴为 None</summary>
        internal KikasaAffinity SlotAffinity(int index) => AffinityOfKey(SlotKeyAt(index));

        /// <summary>记忆键的灵异亲和</summary>
        internal static KikasaAffinity AffinityOfKey(int key)
            => key > 0 ? KikasaServantIndex.AffinityOf(key) : KikasaAffinity.None;

        /// <summary>该记忆驻在哪一席；不在盘上返回 -1</summary>
        internal int SlotIndexOf(int key) {
            if (key == 0) {
                return -1;
            }
            for (int i = 0; i < SlotCount; i++) {
                if (lakeSlots[i] == key) {
                    return i;
                }
            }
            return -1;
        }

        //==================== 收集册 ====================

        /// <summary>该记忆是否已入册</summary>
        internal bool IsCollected(int key)
            => key > 0 ? collectedServants.Contains(key)
            : key < 0 && collectedArms.Contains(-key);

        /// <summary>已入册的鬼奴记忆数（不含械奴）</summary>
        internal int CollectedServantCount => collectedServants.Count;

        /// <summary>鬼奴记忆总数（注册表规范条目数）</summary>
        internal static int ServantCodexTotal => KikasaServantIndex.AllEntries.Count;

        /// <summary>收集册键序：鬼奴按注册表进度序，械奴排在末尾</summary>
        internal List<int> BuildCodexKeys() {
            List<int> keys = [];
            foreach (KikasaServantIndex.ServantEntry entry in KikasaServantIndex.AllEntries) {
                if (collectedServants.Contains(entry.CanonicalType)) {
                    keys.Add(entry.CanonicalType);
                }
            }
            foreach (int itemType in collectedArms.OrderBy(static t => t)) {
                keys.Add(-itemType);
            }
            return keys;
        }

        /// <summary>记忆键的展示名：鬼奴取 NPC 名，械奴取物品名</summary>
        internal static string KeyDisplayName(int key)
            => key > 0 ? Lang.GetNPCNameValue(key)
            : key < 0 ? Lang.GetItemNameValue(-key) : string.Empty;

        //==================== 记录 ====================

        /// <summary>
        /// 沉溺权威完成帧的入账口：最近记忆照旧覆盖式记录（未驯服生物的提示靠它），
        /// 已注册的 boss 同时化作沉影永久入册，有空席就自动落座。
        /// 所有者本机之外调用无害但无意义（数据不外播）
        /// </summary>
        internal void RecordDrowned(int npcType) {
            if (npcType <= NPCID.None || npcType >= NPCLoader.NPCCount) {
                return;
            }
            LastDrownedType = npcType;
            LastDrownedItemType = 0;
            bool autoSlotted = false;
            int canonical = KikasaServantIndex.CanonicalOf(npcType);
            if (canonical > 0) {
                collectedServants.Add(canonical);
                autoSlotted = TryAutoSlot(canonical);
            }
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //轻声确认拍：湖把它收进了记性里；落座另有一记低应
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, Player.Center);
            if (autoSlotted) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.7f, MaxInstances = 2 }, Player.Center);
            }
        }

        /// <summary>
        /// 已注册武器沉湖时的入账口：入械奴册，有空席自动落座。
        /// 由 KikasaVaultPlayer.TrySink 在入账帧调用（湖藏数据只活在所有者本机）
        /// </summary>
        internal void RecordDrownedItem(int itemType) {
            if (itemType <= ItemID.None || itemType >= ItemLoader.ItemCount) {
                return;
            }
            if (!KikasaArmsIndex.TryGet(itemType, out _)) {
                return;
            }
            LastDrownedItemType = itemType;
            LastDrownedType = 0;
            collectedArms.Add(itemType);
            TryAutoSlot(-itemType);
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //同一记确认拍：湖学会了驱使这批武器
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, Player.Center);
        }

        /// <summary>
        /// 入世追认湖藏：注册表扩了专门条目后（如传奇武器专属械奴），
        /// 更新前就泡在湖里的原件静默补入械奴册，不落座、不出声，编成自己来
        /// </summary>
        public override void OnEnterWorld() {
            foreach (Item item in Player.GetModPlayer<KikasaVaultPlayer>().Stored) {
                if (item?.IsAir == false && !collectedArms.Contains(item.type)
                    && KikasaArmsIndex.TryGet(item.type, out _)) {
                    collectedArms.Add(item.type);
                }
            }
        }

        /// <summary>新记忆有空席就自动落座，沉下去当场看见回报（落座默认出战）</summary>
        private bool TryAutoSlot(int key) {
            if (SlotIndexOf(key) >= 0) {
                return false;
            }
            for (int i = 0; i < SlotCount; i++) {
                if (lakeSlots[i] == 0) {
                    lakeSlots[i] = key;
                    slotHeld[i] = false;
                    respawnDelay[i] = 20;
                    return true;
                }
            }
            return false;
        }

        //==================== 影位编成（湖心景编成区与转盘的写入口，仅本机） ====================

        /// <summary>
        /// 落影：把已入册的记忆放进影位。同鬼不占两席（旧席自动腾出），
        /// 顶掉的旧驻影遣返回湖。原样放回同席返回 false（调用方当收手处理）
        /// </summary>
        internal bool TrySetSlot(int slotIndex, int key) {
            if (slotIndex < 0 || slotIndex >= SlotCount || key == 0 || !IsCollected(key)) {
                return false;
            }
            if (lakeSlots[slotIndex] == key) {
                return false;
            }
            //同鬼挪席：旧席腾出但不遣返，鬼还是那只鬼
            for (int i = 0; i < SlotCount; i++) {
                if (i != slotIndex && lakeSlots[i] == key) {
                    lakeSlots[i] = 0;
                }
            }
            int displaced = lakeSlots[slotIndex];
            if (displaced != 0) {
                DismissServantOf(displaced);
            }
            lakeSlots[slotIndex] = key;
            slotHeld[slotIndex] = false;
            respawnDelay[slotIndex] = 10;
            return true;
        }

        /// <summary>腾出影位，驻影遣返回湖</summary>
        internal bool ClearSlot(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= SlotCount || lakeSlots[slotIndex] == 0) {
                return false;
            }
            DismissServantOf(lakeSlots[slotIndex]);
            lakeSlots[slotIndex] = 0;
            slotHeld[slotIndex] = false;
            return true;
        }

        /// <summary>
        /// 召/收翻转（快捷转盘的写入口，仅本机）：收起=驻影溶解遣返且不再自动补位，
        /// 召回=恢复自动出水（湖就绪时略候即破水）。空席不受理返回 false
        /// </summary>
        internal bool ToggleSlotHeld(int slotIndex, out bool nowHeld) {
            nowHeld = false;
            if (slotIndex < 0 || slotIndex >= SlotCount || lakeSlots[slotIndex] == 0) {
                return false;
            }
            slotHeld[slotIndex] = !slotHeld[slotIndex];
            nowHeld = slotHeld[slotIndex];
            if (nowHeld) {
                DismissServantOf(lakeSlots[slotIndex]);
            }
            else {
                respawnDelay[slotIndex] = 6;
            }
            return true;
        }

        //==================== 驻影自动出水（驻湖即役使） ====================

        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.dead) {
                return;
            }
            if (HackTime.Active) {
                return;
            }
            UpdateAutoPresence();
        }

        /// <summary>
        /// 驻影在场维持：湖就绪时缺席的驻影自动出水（各席错拍），
        /// 湖退时鬼奴自会溶解（生命线在各鬼奴 AI 里），这里只管补位；
        /// 玩家收起的席位只遣返不补位
        /// </summary>
        private void UpdateAutoPresence() {
            bool lakeReady = Player.GetModPlayer<KikasaVaultPlayer>().LakeReady;
            for (int i = 0; i < SlotCount; i++) {
                int key = lakeSlots[i];
                if (key == 0) {
                    continue;
                }
                if (slotHeld[i]) {
                    //收起席：在场的溶解遣返（重复下令无害），不再补位
                    DismissServantOf(key);
                    continue;
                }
                if (FindServantOf(key) != null) {
                    //在场：溶解后要等这一拍再回来
                    respawnDelay[i] = RespawnGapFrames;
                    continue;
                }
                if (!lakeReady) {
                    //湖没就绪先候着；就绪后各席错拍破水，不站成仪仗队
                    respawnDelay[i] = 14 + i * 22;
                    continue;
                }
                if (--respawnDelay[i] > 0) {
                    continue;
                }
                respawnDelay[i] = RespawnGapFrames;
                SpawnSlotServant(i, key);
            }
        }

        /// <summary>驻影出水：出水点在主人近旁按席位错开，纵位就是湖面</summary>
        private void SpawnSlotServant(int slotIndex, int key) {
            KikasaDomainPlayer domain = Player.GetModPlayer<KikasaDomainPlayer>();
            Vector2 emergeAt = new(Player.Center.X + (slotIndex - 1) * 150f, domain.LakeWorldY);
            if (key > 0) {
                if (KikasaServantIndex.TryGetEntry(key, out KikasaServantIndex.ServantEntry entry)) {
                    entry.Spawner(Player, emergeAt);
                }
                return;
            }
            //械奴：复制体数量按湖藏存量折算，原件不消耗；原件被捞光就候着，等再沉
            int itemType = -key;
            int count = CountStoredArms(Player.GetModPlayer<KikasaVaultPlayer>(), itemType);
            if (count <= 0) {
                respawnDelay[slotIndex] = 150;
                return;
            }
            if (KikasaArmsIndex.TryGet(itemType, out KikasaArmsIndex.ArmsSpawner spawner)) {
                spawner(Player, emergeAt, count);
            }
        }

        //==================== 在场查询 ====================

        /// <summary>场上属于此玩家的任意鬼奴（穷举实现共用 IKikasaServant 报到）；无则 null</summary>
        internal IKikasaServant FindActiveServant() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active == true && proj.owner == Player.whoAmI
                    && proj.ModProjectile is IKikasaServant servant) {
                    return servant;
                }
            }
            return null;
        }

        /// <summary>某条记忆的驻影是否在场（含溶解中）；械奴按复制的武器区分</summary>
        internal IKikasaServant FindServantOf(int key) {
            if (key == 0) {
                return null;
            }
            if (key > 0) {
                if (!KikasaServantIndex.TryGetEntry(key, out KikasaServantIndex.ServantEntry entry)) {
                    return null;
                }
                int projType = entry.ProjType();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == projType && proj.owner == Player.whoAmI) {
                        return proj.ModProjectile as IKikasaServant;
                    }
                }
                return null;
            }
            //械奴不锁具体弹幕类型（枪奴/刀奴同一张报到面），按复制的武器认身份
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == Player.whoAmI
                    && proj.ModProjectile is IKikasaArmsServant arms
                    && arms.ArmsItemType == -key) {
                    return arms;
                }
            }
            return null;
        }

        private void DismissServantOf(int key) {
            IKikasaServant servant = FindServantOf(key);
            if (servant != null && !servant.IsDismissing) {
                servant.BeginDismiss();
            }
        }

        /// <summary>湖藏里该武器的存量（计堆叠），械奴复制体数量的依据</summary>
        internal static int CountStoredArms(KikasaVaultPlayer vault, int itemType) {
            int count = 0;
            foreach (Item item in vault.Stored) {
                if (item?.IsAir == false && item.type == itemType) {
                    count += Math.Max(item.stack, 1);
                }
            }
            return count;
        }

        //==================== 存档 ====================

        public override void SaveData(TagCompound tag) {
            if (LastDrownedType > NPCID.None) {
                if (LastDrownedType < NPCID.Count) {
                    tag["KikasaServantMemory"] = LastDrownedType;
                }
                else if (NPCLoader.GetNPC(LastDrownedType) is ModNPC modNPC) {
                    //模组 NPC 的类型号跨会话不稳定，存全名
                    tag["KikasaServantMemoryName"] = modNPC.FullName;
                }
            }
            if (LastDrownedItemType > ItemID.None) {
                if (LastDrownedItemType < ItemID.Count) {
                    tag["KikasaArmsMemory"] = LastDrownedItemType;
                }
                else if (ItemLoader.GetItem(LastDrownedItemType) is ModItem modItem) {
                    //模组物品同理存全名
                    tag["KikasaArmsMemoryName"] = modItem.FullName;
                }
            }
            //沉影盘：鬼奴注册表只收原版 boss，键即原版类型号，跨会话稳定
            if (collectedServants.Count > 0) {
                tag["KikasaServantCodex"] = collectedServants.ToList();
            }
            //械奴册对任意武器开放：原版按类型号、模组物品按全名存（类型号跨会话不稳定）
            if (collectedArms.Count > 0) {
                List<int> vanillaArms = [];
                List<string> moddedArms = [];
                foreach (int type in collectedArms) {
                    if (type < ItemID.Count) {
                        vanillaArms.Add(type);
                    }
                    else if (ItemLoader.GetItem(type) is ModItem modArm) {
                        moddedArms.Add(modArm.FullName);
                    }
                }
                if (vanillaArms.Count > 0) {
                    tag["KikasaArmsCodex"] = vanillaArms;
                }
                if (moddedArms.Count > 0) {
                    tag["KikasaArmsCodexNames"] = moddedArms;
                }
            }
            tag["KikasaLakeSlots"] = lakeSlots.ToList();
            //驻模组械奴的席位并行存全名，读档按名恢复（int 表里的失效键会被校验丢弃）
            List<string> slotNames = [.. Enumerable.Repeat(string.Empty, SlotCount)];
            bool anySlotName = false;
            for (int i = 0; i < SlotCount; i++) {
                int key = lakeSlots[i];
                if (key < 0 && -key >= ItemID.Count && ItemLoader.GetItem(-key) is ModItem slotArm) {
                    slotNames[i] = slotArm.FullName;
                    anySlotName = true;
                }
            }
            if (anySlotName) {
                tag["KikasaLakeSlotNames"] = slotNames;
            }
            tag["KikasaSlotHeld"] = slotHeld.ToList();
        }

        public override void LoadData(TagCompound tag) {
            LastDrownedType = 0;
            LastDrownedItemType = 0;
            collectedServants.Clear();
            collectedArms.Clear();
            Array.Clear(lakeSlots, 0, SlotCount);
            Array.Clear(slotHeld, 0, SlotCount);

            //最近记忆：武器与生物互斥，读到即定（写侧保证最多一个存在）
            if (tag.TryGet("KikasaArmsMemoryName", out string armsName)
                && ModContent.TryFind(armsName, out ModItem modItem)) {
                LastDrownedItemType = modItem.Type;
            }
            else if (tag.TryGet("KikasaArmsMemory", out int vanillaItem)
                && vanillaItem > ItemID.None && vanillaItem < ItemID.Count) {
                LastDrownedItemType = vanillaItem;
            }
            if (tag.TryGet("KikasaServantMemoryName", out string fullName)
                && ModContent.TryFind(fullName, out ModNPC modNPC)) {
                LastDrownedType = modNPC.Type;
            }
            else if (tag.TryGet("KikasaServantMemory", out int vanillaType)
                && vanillaType > NPCID.None && vanillaType < NPCID.Count) {
                LastDrownedType = vanillaType;
            }

            //收集册与影位：逐条校验解析链，读损/退役条目静默丢弃
            bool hasCodexTag = tag.ContainsKey("KikasaServantCodex")
                || tag.ContainsKey("KikasaArmsCodex") || tag.ContainsKey("KikasaArmsCodexNames")
                || tag.ContainsKey("KikasaLakeSlots");
            if (tag.TryGet("KikasaServantCodex", out List<int> codex)) {
                foreach (int type in codex) {
                    int canonical = KikasaServantIndex.CanonicalOf(type);
                    if (canonical > 0) {
                        collectedServants.Add(canonical);
                    }
                }
            }
            if (tag.TryGet("KikasaArmsCodex", out List<int> armsCodex)) {
                foreach (int type in armsCodex) {
                    if (KikasaArmsIndex.TryGet(type, out _)) {
                        collectedArms.Add(type);
                    }
                }
            }
            //模组械奴按全名找回当前会话的类型号，找不到（卸了模组）静默丢弃
            if (tag.TryGet("KikasaArmsCodexNames", out List<string> armsNames)) {
                foreach (string armName in armsNames) {
                    if (ModContent.TryFind(armName, out ModItem modArm)
                        && KikasaArmsIndex.TryGet(modArm.Type, out _)) {
                        collectedArms.Add(modArm.Type);
                    }
                }
            }
            if (tag.TryGet("KikasaLakeSlots", out List<int> slots)) {
                for (int i = 0; i < SlotCount && i < slots.Count; i++) {
                    int key = slots[i];
                    if (key != 0 && IsCollected(key) && SlotIndexOf(key) < 0) {
                        lakeSlots[i] = key;
                    }
                }
            }
            //模组械奴席位按全名覆写恢复（int 表里存的是上一会话的失效类型号）
            if (tag.TryGet("KikasaLakeSlotNames", out List<string> slotArmNames)) {
                for (int i = 0; i < SlotCount && i < slotArmNames.Count; i++) {
                    string armName = slotArmNames[i];
                    if (string.IsNullOrEmpty(armName)
                        || !ModContent.TryFind(armName, out ModItem modArm)) {
                        continue;
                    }
                    int key = -modArm.Type;
                    if (IsCollected(key) && SlotIndexOf(key) < 0) {
                        lakeSlots[i] = key;
                    }
                }
            }
            if (tag.TryGet("KikasaSlotHeld", out List<bool> held)) {
                for (int i = 0; i < SlotCount && i < held.Count; i++) {
                    //收起标记只对实际驻着影的席有意义
                    slotHeld[i] = held[i] && lakeSlots[i] != 0;
                }
            }
            if (!hasCodexTag) {
                MigrateLegacyMemory();
            }
        }

        /// <summary>老档折算：唯一的「最后记忆」入册并落首席，旧玩家开档就有编成，不用重沉</summary>
        private void MigrateLegacyMemory() {
            int canonical = KikasaServantIndex.CanonicalOf(LastDrownedType);
            if (canonical > 0) {
                collectedServants.Add(canonical);
                lakeSlots[0] = canonical;
                return;
            }
            if (LastDrownedItemType > 0 && KikasaArmsIndex.TryGet(LastDrownedItemType, out _)) {
                collectedArms.Add(LastDrownedItemType);
                lakeSlots[0] = -LastDrownedItemType;
            }
        }
    }

    /// <summary>场上鬼奴的公共报到面：遣返命令由所有者本机下达</summary>
    internal interface IKikasaServant
    {
        /// <summary>已在溶解遣返途中</summary>
        bool IsDismissing { get; }

        /// <summary>进入溶解遣返；从任意状态可达，重复调用无害</summary>
        void BeginDismiss();
    }

    /// <summary>械奴（武器复制体）的公共报到面：以复制的武器物品类型认在场身份</summary>
    internal interface IKikasaArmsServant : IKikasaServant
    {
        /// <summary>复制的原型武器物品类型</summary>
        int ArmsItemType { get; }
    }
}
