using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// 掉落物扫描数据实现
    /// <br/>用于分析世界中的 Item 实体，不默认提供可上传协议
    /// </summary>
    internal class ItemScannable : IHackTarget
    {
        public int ItemIndex { get; }

        public ItemScannable(int itemIndex) {
            ItemIndex = itemIndex;
        }

        public Vector2 WorldCenter {
            get {
                if (!IsValid) return Vector2.Zero;
                return Main.item[ItemIndex].Center;
            }
        }

        public bool IsValid {
            get {
                if (ItemIndex < 0 || ItemIndex >= Main.maxItems) return false;
                return ItemTargetType.IsScannableItem(Main.item[ItemIndex]);
            }
        }

        public bool IsHackable => false;

        public int ScanRowCount => 10;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            if (!IsValid) return;
            Item item = Main.item[ItemIndex];

            labels[0] = HackTime.ItemScanName.Value;
            values[0] = GetItemName(item);
            colors[0] = HackTheme.TextBright;

            labels[1] = HackTime.ItemScanClass.Value;
            values[1] = GetItemClass(item);
            colors[1] = GetItemClassColor(item);

            labels[2] = HackTime.ItemScanStack.Value;
            values[2] = $"{item.stack:N0} / {item.maxStack:N0}";
            colors[2] = item.stack >= item.maxStack ? HackTheme.AccentAlt : HackTheme.Accent;

            labels[3] = HackTime.ItemScanValue.Value;
            values[3] = FormatCoinValue(item.value * item.stack);
            colors[3] = item.value > 0 ? HackTheme.Uploading : HackTheme.TextDim;

            labels[4] = HackTime.ItemScanRarity.Value;
            values[4] = GetRarityText(item);
            colors[4] = GetRarityColor(item.rare);

            labels[5] = HackTime.ItemScanCombat.Value;
            values[5] = GetCombatText(item);
            colors[5] = item.damage > 0 || item.defense > 0 ? HackTheme.Danger : HackTheme.TextDim;

            labels[6] = HackTime.ItemScanUtility.Value;
            values[6] = GetUtilityText(item);
            colors[6] = GetUtilityColor(item);

            labels[7] = HackTime.ItemScanPrefix.Value;
            values[7] = item.prefix > 0 ? $"#{item.prefix}" : HackTime.ItemScanNone.Value;
            colors[7] = item.prefix > 0 ? HackTheme.AccentAlt : HackTheme.TextDim;

            labels[8] = HackTime.ItemScanTypeId.Value;
            values[8] = $"ItemID {item.type}";
            colors[8] = HackTheme.TextDim;

            labels[9] = HackTime.ItemScanPosition.Value;
            values[9] = $"{(int)item.Center.X}, {(int)item.Center.Y}";
            colors[9] = HackTheme.TextDim;
        }

        public HackTargetType TargetType => HackTargetType.Get<ItemTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                if (!IsValid) return Vector2.Zero;
                Item item = Main.item[ItemIndex];
                return new Vector2(
                    Math.Max(item.width, 16) * 0.6f + 24f,
                    Math.Max(item.height, 16) * 0.6f + 24f);
            }
        }

        public string LockFrameTitle => IsValid ? GetItemName(Main.item[ItemIndex]) : string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            if (!IsValid) return false;
            Item item = Main.item[ItemIndex];
            text = item.stack > 1 ? $"x{item.stack:N0}" : GetItemClass(item);
            color = item.stack > 1 ? HackTheme.Accent : GetItemClassColor(item);
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) => false;

        public bool TargetEquals(IHackTarget other) {
            return other is ItemScannable i && i.ItemIndex == ItemIndex;
        }

        private static string GetItemName(Item item) {
            string name = VaultUtils.GetLocalizedItemName(item.type).Value;
            if (!string.IsNullOrEmpty(name)) return name;
            return $"Item #{item.type}";
        }

        private static string GetItemClass(Item item) {
            if (item.damage > 0) return HackTime.ItemScanWeapon.Value;
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0) return HackTime.ItemScanTool.Value;
            if (item.defense > 0) return HackTime.ItemScanArmor.Value;
            if (item.accessory) return HackTime.ItemScanAccessory.Value;
            if (item.ammo > 0) return HackTime.ItemScanAmmo.Value;
            if (item.consumable) return HackTime.ItemScanConsumable.Value;
            if (item.createTile >= TileID.Dirt || item.createWall >= 0) return HackTime.ItemScanPlaceable.Value;
            if (item.material) return HackTime.ItemScanMaterial.Value;
            if (item.questItem) return HackTime.ItemScanQuest.Value;
            return HackTime.ItemScanMisc.Value;
        }

        private static Color GetItemClassColor(Item item) {
            if (item.damage > 0) return HackTheme.Danger;
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0) return HackTheme.Uploading;
            if (item.defense > 0 || item.accessory) return HackTheme.AccentAlt;
            if (item.consumable || item.ammo > 0) return HackTheme.Accent;
            return HackTheme.TextDim;
        }

        private static string GetCombatText(Item item) {
            if (item.damage > 0) return $"{item.damage} DMG / {item.knockBack:F1} KB";
            if (item.defense > 0) return $"{item.defense} DEF";
            return HackTime.ItemScanNone.Value;
        }

        private static string GetUtilityText(Item item) {
            if (item.pick > 0) return $"Pick {item.pick}%";
            if (item.axe > 0) return $"Axe {item.axe * 5}%";
            if (item.hammer > 0) return $"Hammer {item.hammer}%";
            if (item.fishingPole > 0) return $"Fishing {item.fishingPole}%";
            if (item.bait > 0) return $"Bait {item.bait}%";
            if (item.healLife > 0) return $"Heal {item.healLife} HP";
            if (item.healMana > 0) return $"Mana {item.healMana}";
            if (item.buffType > 0) return $"Buff {item.buffType}";
            if (item.useTime > 0) return $"Use {item.useTime}f";
            return HackTime.ItemScanNone.Value;
        }

        private static Color GetUtilityColor(Item item) {
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0) return HackTheme.Uploading;
            if (item.fishingPole > 0 || item.bait > 0) return HackTheme.AccentAlt;
            if (item.healLife > 0 || item.healMana > 0 || item.buffType > 0) return HackTheme.Accent;
            return HackTheme.TextDim;
        }

        private static string GetRarityText(Item item) {
            return item.rare switch {
                -1 => "Gray",
                0 => "White",
                1 => "Blue",
                2 => "Green",
                3 => "Orange",
                4 => "Light Red",
                5 => "Pink",
                6 => "Light Purple",
                7 => "Lime",
                8 => "Yellow",
                9 => "Cyan",
                10 => "Red",
                11 => "Purple",
                _ => $"Rarity {item.rare}"
            };
        }

        private static Color GetRarityColor(int rarity) {
            return rarity switch {
                -1 => new Color(130, 130, 130),
                0 => Color.White,
                1 => new Color(150, 150, 255),
                2 => new Color(150, 255, 150),
                3 => new Color(255, 200, 150),
                4 => new Color(255, 150, 150),
                5 => new Color(255, 150, 255),
                6 => new Color(210, 160, 255),
                7 => new Color(150, 255, 10),
                8 => new Color(255, 255, 10),
                9 => new Color(5, 200, 255),
                10 => new Color(255, 40, 100),
                11 => new Color(180, 40, 255),
                _ => HackTheme.TextBright
            };
        }

        private static string FormatCoinValue(int copper) {
            if (copper <= 0) return HackTime.ItemScanNoValue.Value;

            int platinum = copper / 1000000;
            copper %= 1000000;
            int gold = copper / 10000;
            copper %= 10000;
            int silver = copper / 100;
            copper %= 100;

            if (platinum > 0) return $"{platinum}p {gold}g {silver}s";
            if (gold > 0) return $"{gold}g {silver}s {copper}c";
            if (silver > 0) return $"{silver}s {copper}c";
            return $"{copper}c";
        }
    }
}
