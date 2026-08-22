using CalamityOverhaul.Content.HackTimes.Protocols;
using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// 容器扫描目标。身份 = 箱子的锚点格（左上角 tile 座标），
    /// 悬停命中任意一格都归一化到锚点，<c>Main.chest</c> 靠
    /// <see cref="Chest.FindChest(int, int)"/> 反查。<br/>
    /// 假箱（陷阱箱 <c>BasicChestFake</c>）没有 chest 实体，反查失败即不可扫
    /// 这本身就是一条可读的信息
    /// </summary>
    internal class ContainerScannable : IHackTarget
    {
        private readonly int anchorX;
        private readonly int anchorY;

        /// <summary>锚点格 X（箱体左上角）</summary>
        public int AnchorX => anchorX;
        /// <summary>锚点格 Y（箱体左上角）</summary>
        public int AnchorY => anchorY;

        public ContainerScannable(int anchorX, int anchorY) {
            this.anchorX = anchorX;
            this.anchorY = anchorY;
        }

        public Vector2 WorldCenter {
            get {
                Rectangle bounds = GetContainerWorldBounds(anchorX, anchorY);
                return bounds.Center.ToVector2();
            }
        }

        public bool IsValid => IsContainerAnchorAt(anchorX, anchorY);

        public bool IsHackable => IsValid;

        #region 扫描面板

        //基础 6 行：类型/锁/槽位/估值/最高稀有度/座标；
        //被索引预读缓存后换成内容清单，行数吃满面板上限
        private const int BaseRowCount = 6;
        private const int MaxPanelRows = 10;
        //索引态：3 行基础 + 1 行最值钱 + 内容行
        private const int IndexedHeadRows = 4;

        public int ScanRowCount {
            get {
                if (!IsValid) return 0;
                if (!IndexPreread.IsIndexed(anchorX, anchorY)) return BaseRowCount;
                int entries = CountOccupiedSlots();
                //内容清单行数吃满面板上限（超出部分折进"其余 N 项"），空箱只留头部
                int listRows = Math.Min(entries, MaxPanelRows - IndexedHeadRows);
                return Math.Min(IndexedHeadRows + listRows, MaxPanelRows);
            }
        }

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            if (!IsValid) return;
            bool indexed = IndexPreread.IsIndexed(anchorX, anchorY);
            bool locked = Chest.IsLocked(anchorX, anchorY);
            int used = CountOccupiedSlots();

            labels[0] = ContainerTargetType.ScanType.Value;
            values[0] = GetContainerName();
            colors[0] = HackTheme.TextBright;

            labels[1] = ContainerTargetType.ScanLock.Value;
            values[1] = locked
                ? ContainerTargetType.LockStateLocked.Value
                : ContainerTargetType.LockStateOpen.Value;
            colors[1] = locked ? HackTheme.Danger : HackTheme.Accent;

            labels[2] = ContainerTargetType.ScanSlots.Value;
            values[2] = $"{used} / {Chest.maxItems}";
            colors[2] = used > 0 ? HackTheme.AccentAlt : HackTheme.TextDim;

            if (!indexed) {
                BuildBaseRows(labels, values, colors);
                return;
            }
            BuildIndexedRows(labels, values, colors, used);
        }

        //未索引：估值与最高稀有度是"扫描免费送的一半情报"，内容清单要靠索引预读
        private void BuildBaseRows(string[] labels, string[] values, Color[] colors) {
            labels[3] = ContainerTargetType.ScanValue.Value;
            long totalValue = SumContainedValue();
            values[3] = totalValue > 0
                ? Main.ValueToCoins(totalValue)
                : ContainerTargetType.ScanEmpty.Value;
            colors[3] = totalValue > 0 ? HackTheme.Uploading : HackTheme.TextDim;

            labels[4] = ContainerTargetType.ScanTopRare.Value;
            Item topRare = FindTopRarityItem();
            values[4] = topRare != null ? topRare.Name : ContainerTargetType.ScanEmpty.Value;
            colors[4] = topRare != null ? ItemRarity.GetColor(topRare.rare) : HackTheme.TextDim;

            labels[5] = ContainerTargetType.ScanCoord.Value;
            values[5] = $"{anchorX}, {anchorY}";
            colors[5] = HackTheme.TextDim;
        }

        //已索引：全部条目按价值降序铺进面板，装不下的收进"其余 N 项"
        private void BuildIndexedRows(string[] labels, string[] values, Color[] colors, int used) {
            labels[3] = ContainerTargetType.ScanTopValue.Value;
            Item topValue = FindTopValueItem();
            if (topValue != null) {
                values[3] = topValue.stack > 1
                    ? $"{topValue.Name} x{topValue.stack}"
                    : topValue.Name;
                colors[3] = ItemRarity.GetColor(topValue.rare);
            }
            else {
                values[3] = ContainerTargetType.ScanEmpty.Value;
                colors[3] = HackTheme.TextDim;
            }

            int listCapacity = Math.Min(used, MaxPanelRows - IndexedHeadRows);
            if (listCapacity <= 0) return;

            Span<int> slots = stackalloc int[Chest.maxItems];
            int count = CollectSlotsByValueDesc(slots);

            //最后一行留给截断提示
            bool truncated = count > listCapacity;
            int shown = truncated ? listCapacity - 1 : Math.Min(count, listCapacity);

            Chest chest = ResolveChest();
            for (int i = 0; i < shown; i++) {
                Item item = chest.item[slots[i]];
                labels[IndexedHeadRows + i] = $"[{i + 1:D2}]";
                values[IndexedHeadRows + i] = item.stack > 1
                    ? $"{item.Name} x{item.stack}"
                    : item.Name;
                colors[IndexedHeadRows + i] = ItemRarity.GetColor(item.rare);
            }
            if (truncated) {
                int rest = count - shown;
                labels[IndexedHeadRows + shown] = "[..]";
                values[IndexedHeadRows + shown]
                    = ContainerTargetType.ScanRest.Format(rest);
                colors[IndexedHeadRows + shown] = HackTheme.TextDim;
            }
        }

        #endregion

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<ContainerTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                Rectangle bounds = GetContainerWorldBounds(anchorX, anchorY);
                return new Vector2(
                    Math.Max(bounds.Width, 32) * 0.6f + 26f,
                    Math.Max(bounds.Height, 32) * 0.6f + 26f);
            }
        }

        public string LockFrameTitle => IsValid ? GetContainerName() : string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            if (!IsValid) return false;
            if (Chest.IsLocked(anchorX, anchorY)) {
                text = ContainerTargetType.LockStateLocked.Value;
                color = HackTheme.Danger;
                return true;
            }
            if (IndexPreread.IsIndexed(anchorX, anchorY)) {
                //选中态直接给最值钱那件，这就是索引预读的"标出"呈现
                Item top = FindTopValueItem();
                text = top != null
                    ? ContainerTargetType.StateIndexed.Value + " > " + top.Name
                    : ContainerTargetType.StateIndexed.Value;
                color = HackTheme.AccentAlt;
                return true;
            }
            text = $"{CountOccupiedSlots()} / {Chest.maxItems}";
            color = HackTheme.Accent;
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            int casterIndex = caster?.whoAmI ?? Main.myPlayer;
            //容器没有专用入口，直接走统一权威入口（内部会做 CanApplyTo 校验）
            return HackEffectTracker.ApplyAuthorityEffect(hack, this, casterIndex,
                0, 0, 0f, 0) != null;
        }

        public bool TargetEquals(IHackTarget other) {
            return other is ContainerScannable c
                && c.anchorX == anchorX && c.anchorY == anchorY;
        }

        #endregion

        #region 箱体解析

        /// <summary>该格是否属于容器 tile（真箱与梳妆台；假箱刻意不算）</summary>
        public static bool IsContainerTile(int type) {
            if (type < 0 || type >= TileID.Sets.BasicChest.Length) return false;
            return TileID.Sets.BasicChest[type] || TileID.Sets.BasicDresser[type];
        }

        /// <summary>锚点格上是否立着一个有 chest 实体的容器</summary>
        public static bool IsContainerAnchorAt(int x, int y) {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY) {
                return false;
            }
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !IsContainerTile(tile.TileType)) return false;
            return Chest.FindChest(x, y) >= 0;
        }

        /// <summary>
        /// 悬停判定：命中格是容器类且能反查到 chest 实体。
        /// 输出的是归一化后的锚点座标
        /// </summary>
        public static bool TryGetScannableContainer(Vector2 worldPos,
            out int anchorX, out int anchorY) {
            anchorX = (int)(worldPos.X / 16f);
            anchorY = (int)(worldPos.Y / 16f);
            if (anchorX < 0 || anchorX >= Main.maxTilesX
                || anchorY < 0 || anchorY >= Main.maxTilesY) {
                return false;
            }
            Tile tile = Main.tile[anchorX, anchorY];
            if (!tile.HasTile || !IsContainerTile(tile.TileType)) return false;

            ResolveAnchor(ref anchorX, ref anchorY);
            return Chest.FindChest(anchorX, anchorY) >= 0;
        }

        //帧座标反推左上角，箱子(2x2)与梳妆台(3x2)共用一套 TileObjectData 算法
        private static void ResolveAnchor(ref int x, ref int y) {
            Tile tile = Main.tile[x, y];
            TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
            if (data == null) return;

            int frameWidth = data.CoordinateWidth + data.CoordinatePadding;
            int frameHeight = data.CoordinateHeights[0] + data.CoordinatePadding;
            int offsetX = tile.TileFrameX % (data.Width * frameWidth) / frameWidth;
            int offsetY = tile.TileFrameY % (data.Height * frameHeight) / frameHeight;
            x -= offsetX;
            y -= offsetY;
        }

        private static Rectangle GetContainerWorldBounds(int anchorX, int anchorY) {
            if (anchorX < 0 || anchorX >= Main.maxTilesX
                || anchorY < 0 || anchorY >= Main.maxTilesY) {
                return new Rectangle(anchorX * 16, anchorY * 16, 32, 32);
            }
            Tile tile = Main.tile[anchorX, anchorY];
            TileObjectData data = TileObjectData.GetTileData(tile.TileType, 0);
            int w = data?.Width ?? 2;
            int h = data?.Height ?? 2;
            return new Rectangle(anchorX * 16, anchorY * 16, w * 16, h * 16);
        }

        /// <summary>锚点反查 chest 实体，无则 null</summary>
        public Chest ResolveChest() {
            int index = Chest.FindChest(anchorX, anchorY);
            return index >= 0 ? Main.chest[index] : null;
        }

        /// <summary>锚点反查 <c>Main.chest</c> 槽位，无则 -1</summary>
        public int ResolveChestIndex() => Chest.FindChest(anchorX, anchorY);

        #endregion

        #region 内容统计

        public int CountOccupiedSlots() {
            Chest chest = ResolveChest();
            if (chest?.item == null) return 0;
            int count = 0;
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && !item.IsAir) count++;
            }
            return count;
        }

        public long SumContainedValue() {
            Chest chest = ResolveChest();
            if (chest?.item == null) return 0;
            long total = 0;
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item == null || item.IsAir) continue;
                total += (long)item.value * item.stack;
            }
            return total;
        }

        /// <summary>稀有度最高的一件（同稀有度取更值钱的）</summary>
        public Item FindTopRarityItem() {
            Chest chest = ResolveChest();
            if (chest?.item == null) return null;
            Item best = null;
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item == null || item.IsAir) continue;
                if (best == null || item.rare > best.rare
                    || item.rare == best.rare && item.value > best.value) {
                    best = item;
                }
            }
            return best;
        }

        /// <summary>单价×堆叠最高的一件</summary>
        public Item FindTopValueItem() {
            Chest chest = ResolveChest();
            if (chest?.item == null) return null;
            Item best = null;
            long bestWorth = -1;
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item == null || item.IsAir) continue;
                long worth = (long)item.value * item.stack;
                if (worth > bestWorth) {
                    bestWorth = worth;
                    best = item;
                }
            }
            return best;
        }

        //非空槽位按价值降序写进 span，返回条目数
        private int CollectSlotsByValueDesc(Span<int> slots) {
            Chest chest = ResolveChest();
            if (chest?.item == null) return 0;
            int count = 0;
            for (int i = 0; i < chest.item.Length && count < slots.Length; i++) {
                Item item = chest.item[i];
                if (item == null || item.IsAir) continue;
                slots[count++] = i;
            }
            //插入排序足够：条目至多 40
            for (int i = 1; i < count; i++) {
                int slot = slots[i];
                long worth = Worth(chest, slot);
                int j = i - 1;
                while (j >= 0 && Worth(chest, slots[j]) < worth) {
                    slots[j + 1] = slots[j];
                    j--;
                }
                slots[j + 1] = slot;
            }
            return count;

            static long Worth(Chest chest, int slot)
                => (long)chest.item[slot].value * chest.item[slot].stack;
        }

        private string GetContainerName() {
            Chest chest = ResolveChest();
            if (chest != null && !string.IsNullOrEmpty(chest.name)) return chest.name;
            Tile tile = Main.tile[anchorX, anchorY];
            return TileScannable.GetTileName(anchorX, anchorY, tile.TileType);
        }

        #endregion
    }
}
