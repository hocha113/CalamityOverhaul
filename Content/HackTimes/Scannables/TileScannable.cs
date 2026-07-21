using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>物块扫描 + IHackTarget</summary>
    internal class TileScannable : IHackTarget
    {
        private readonly int tileX;
        private readonly int tileY;

        public int TileCoordX => tileX;
        public int TileCoordY => tileY;

        public TileScannable(int tileX, int tileY) {
            this.tileX = tileX;
            this.tileY = tileY;
        }

        public Vector2 WorldCenter => new(tileX * 16f + 8f, tileY * 16f + 8f);

        public bool IsValid {
            get {
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return false;
                Tile tile = Main.tile[tileX, tileY];
                return tile.HasTile;
            }
        }

        public bool IsHackable => true;

        public int ScanRowCount => 5;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            if (!IsValid) return;
            Tile tile = Main.tile[tileX, tileY];
            int type = tile.TileType;

            labels[0] = HackTime.TileScanName.Value;
            values[0] = GetTileName(tileX, tileY, type);
            colors[0] = HackTheme.TextBright;

            labels[1] = HackTime.TileScanClass.Value;
            values[1] = GetTileClass(type);
            colors[1] = GetTileClassColor(type);

            labels[2] = HackTime.TileScanSize.Value;
            TileObjectData data = TileObjectData.GetTileData(type, 0);
            if (data != null) {
                values[2] = $"{data.Width} x {data.Height}";
                colors[2] = HackTheme.Accent;
            }
            else {
                values[2] = "1 x 1";
                colors[2] = HackTheme.TextDim;
            }

            labels[3] = HackTime.TileScanHardness.Value;
            values[3] = GetHardnessText(type);
            colors[3] = HackTheme.TextBright;

            labels[4] = HackTime.TileScanStatus.Value;
            values[4] = GetStatusText(tile, type);
            colors[4] = GetStatusColor(tile, type);
        }

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<TileTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                Rectangle bounds = GetTileWorldBounds(tileX, tileY);
                return new Vector2(
                    Math.Max(bounds.Width, 32) * 0.6f + 28f,
                    Math.Max(bounds.Height, 32) * 0.6f + 28f);
            }
        }

        public string LockFrameTitle {
            get {
                if (!IsValid) return string.Empty;
                Tile tile = Main.tile[tileX, tileY];
                return GetTileName(tileX, tileY, tile.TileType);
            }
        }

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            if (!IsValid) return false;
            Tile tile = Main.tile[tileX, tileY];
            int type = tile.TileType;
            text = GetTileClass(type);
            color = GetTileClassColor(type);
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            int casterIndex = caster?.whoAmI ?? Main.myPlayer;
            return HackEffectTracker.ApplyTileEffect(hack, tileX, tileY, casterIndex) != null;
        }

        public bool TargetEquals(IHackTarget other) {
            return other is TileScannable t && t.tileX == tileX && t.tileY == tileY;
        }

        #endregion

        /// <summary>显示名，MapHelper → ModTile → 打表 → 掉落物</summary>
        internal static string GetTileName(int x, int y, int type) {
            MapTile mapTile = MapHelper.CreateMapTile(x, y, 255);
            if (mapTile.Type > 0) {
                string mapName = Lang.GetMapObjectName(mapTile.Type);
                //纯数字地图名视为无效
                if (!string.IsNullOrEmpty(mapName) && !IsNumericOnly(mapName)) return mapName;
            }

            if (type >= TileID.Count) {
                ModTile modTile = TileLoader.GetTile(type);
                if (modTile != null) return modTile.Name;
            }

            Tile tile = Main.tile[x, y];
            if (TileNameFallbackRegistry.TryGetName(tile, type, out string fallbackName)) return fallbackName;

            int dropId = tile.GetTileDrop(x, y);
            if (dropId > 0) {
                string itemName = VaultUtils.GetLocalizedItemName(dropId).Value;
                if (!string.IsNullOrEmpty(itemName)) return itemName;
            }

            return GetGenericFallbackName(type);
        }

        private static bool IsNumericOnly(string s) {
            foreach (char c in s) {
                if (c < '0' || c > '9') return false;
            }
            return s.Length > 0;
        }

        private static string GetGenericFallbackName(int type) {
            return IsMultiTileObject(type)
                ? HackTime.TileScanMiscPile.Value
                : HackTime.TileScanMisc.Value;
        }

        private static bool IsMultiTileObject(int type) {
            TileObjectData data = TileObjectData.GetTileData(type, 0);
            return data != null && (data.Width > 1 || data.Height > 1);
        }

        internal static string GetTileClass(int type) {
            if (IsCraftingStation(type)) return HackTime.TileScanCrafting.Value;
            if (IsContainer(type)) return HackTime.TileScanContainer.Value;
            if (IsLightSource(type)) return HackTime.TileScanLight.Value;
            if (IsFurniture(type)) return HackTime.TileScanFurniture.Value;
            return HackTime.TileScanBlock.Value;
        }

        internal static Color GetTileClassColor(int type) {
            if (IsCraftingStation(type)) return HackTheme.Uploading;
            if (IsContainer(type)) return HackTheme.AccentAlt;
            if (IsLightSource(type)) return new Color(200, 200, 80);
            if (IsFurniture(type)) return HackTheme.Accent;
            return HackTheme.TextDim;
        }

        private static string GetHardnessText(int type) {
            if (Main.tileDungeon[type]) return HackTime.TileScanDungeon.Value;
            if (type == TileID.LihzahrdBrick || type == TileID.LihzahrdAltar)
                return HackTime.TileScanLihzahrd.Value;

            int minPick = GetMinPickPower(type);
            if (minPick >= 200) return HackTime.TileScanHardnessExtreme.Value;
            if (minPick >= 100) return HackTime.TileScanHardnessHigh.Value;
            if (minPick > 0) return HackTime.TileScanHardnessNormal.Value;
            return HackTime.TileScanHardnessLow.Value;
        }

        private static string GetStatusText(Tile tile, int type) {
            if (IsLightSource(type)) {
                //帧 X 开关
                bool isOn = tile.TileFrameX < 66 || Main.tileFrameImportant[type] && tile.TileFrameX == 0;
                return isOn ? HackTime.TileScanActive.Value : HackTime.TileScanInactive.Value;
            }
            if (IsContainer(type)) return HackTime.TileScanSealed.Value;
            if (IsCraftingStation(type)) return HackTime.TileScanOnline.Value;
            return HackTime.TileScanIntact.Value;
        }

        private static Color GetStatusColor(Tile tile, int type) {
            if (IsLightSource(type)) {
                bool isOn = tile.TileFrameX < 66 || Main.tileFrameImportant[type] && tile.TileFrameX == 0;
                return isOn ? HackTheme.Accent : HackTheme.Danger;
            }
            if (IsContainer(type)) return HackTheme.AccentAlt;
            if (IsCraftingStation(type)) return HackTheme.Accent;
            return HackTheme.TextBright;
        }

        #region 物块类型判定

        private static bool IsCraftingStation(int type) {
            return type == TileID.WorkBenches || type == TileID.Furnaces
                || type == TileID.Anvils || type == TileID.MythrilAnvil
                || type == TileID.AdamantiteForge || type == TileID.Hellforge
                || type == TileID.Bottles || type == TileID.AlchemyTable
                || type == TileID.TinkerersWorkbench || type == TileID.Loom
                || type == TileID.Kegs || type == TileID.CookingPots
                || type == TileID.Sawmill || type == TileID.HeavyWorkBench
                || type == TileID.DemonAltar || type == TileID.ImbuingStation
                || type == TileID.Solidifier || type == TileID.Blendomatic
                || type == TileID.MeatGrinder || type == TileID.Extractinator
                || type == TileID.LunarCraftingStation
                || type == TileID.LihzahrdAltar || type == TileID.DyeVat
                || type == TileID.GlassKiln || type == TileID.BoneWelder
                || type == TileID.SteampunkBoiler
                || type == TileID.HoneyDispenser || type == TileID.IceMachine
                || type == TileID.LivingLoom || type == TileID.SkyMill
                || type == TileID.Autohammer || type == TileID.CrystalBall;
        }

        private static bool IsContainer(int type) {
            return type == TileID.Containers || type == TileID.Containers2
                || type == TileID.FakeContainers || type == TileID.FakeContainers2
                || type == TileID.Dressers || type == TileID.Pigronata
                || type == TileID.Mannequin || type == TileID.Womannequin
                || type == TileID.DisplayDoll || type == TileID.HatRack;
        }

        private static bool IsLightSource(int type) {
            return type == TileID.Torches || type == TileID.Candles
                || type == TileID.Chandeliers || type == TileID.HangingLanterns
                || type == TileID.Lamps || type == TileID.Candelabras
                || type == TileID.Campfire || type == TileID.FireflyinaBottle
                || type == TileID.LightningBuginaBottle
                || type == TileID.ChineseLanterns
                || type == TileID.DiscoBall || type == TileID.WaterCandle
                || type == TileID.PeaceCandle;
        }

        private static bool IsFurniture(int type) {
            return Main.tileFrameImportant[type]
                && !IsCraftingStation(type) && !IsContainer(type) && !IsLightSource(type);
        }

        private static int GetMinPickPower(int type) {
            if (type == TileID.Meteorite) return 50;
            if (type == TileID.Demonite || type == TileID.Crimtane) return 55;
            if (type == TileID.Ebonstone || type == TileID.Crimstone
                || type == TileID.Pearlstone || type == TileID.Hellstone) return 65;
            if (type == TileID.Cobalt || type == TileID.Palladium) return 100;
            if (type == TileID.Mythril || type == TileID.Orichalcum) return 110;
            if (type == TileID.Adamantite || type == TileID.Titanium) return 150;
            if (type == TileID.Chlorophyte) return 200;
            if (type == TileID.LihzahrdBrick) return 210;
            return 0;
        }

        #endregion

        public static bool TryGetScannableTile(Vector2 worldPos, out int outX, out int outY) {
            outX = (int)(worldPos.X / 16f);
            outY = (int)(worldPos.Y / 16f);

            if (outX < 0 || outX >= Main.maxTilesX || outY < 0 || outY >= Main.maxTilesY) {
                return false;
            }

            Tile tile = Main.tile[outX, outY];
            return tile.HasTile;
        }

        public static bool IsTreeTile(int type) {
            return type == TileID.Trees
                || type == TileID.PalmTree
                || type == TileID.VanityTreeSakura
                || type == TileID.VanityTreeYellowWillow
                || type == TileID.TreeAsh
                || type == TileID.MushroomTrees;
        }

        /// <summary>整棵树视觉包围盒</summary>
        public static Rectangle GetTreeFullBounds(int x, int y, int type) {
            int topY = y;
            while (topY - 1 >= 0) {
                Tile t = Main.tile[x, topY - 1];
                if (!t.HasTile || t.TileType != type) break;
                topY--;
            }
            int botY = y;
            while (botY + 1 < Main.maxTilesY) {
                Tile t = Main.tile[x, botY + 1];
                if (!t.HasTile || t.TileType != type) break;
                botY++;
            }
            //树冠上扩 80px，分枝左右 40px
            const int canopyUp = 80;
            const int branchSide = 40;
            int px = x * 16 - branchSide;
            int py = topY * 16 - canopyUp;
            int w = 16 + branchSide * 2;
            int h = (botY - topY + 1) * 16 + canopyUp;
            return new Rectangle(px, py, w, h);
        }

        /// <summary>包围盒，多格/树特例</summary>
        public static Rectangle GetTileWorldBounds(int x, int y) {
            if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                return new Rectangle(x * 16, y * 16, 16, 16);

            Tile tile = Main.tile[x, y];
            if (!tile.HasTile) return new Rectangle(x * 16, y * 16, 16, 16);

            int type = tile.TileType;
            if (IsTreeTile(type)) {
                return GetTreeFullBounds(x, y, type);
            }

            TileObjectData data = TileObjectData.GetTileData(type, 0);
            if (data == null) return new Rectangle(x * 16, y * 16, 16, 16);

            //帧坐标反推多格偏移
            int frameWidth = data.CoordinateWidth + data.CoordinatePadding;
            int frameHeight = data.CoordinateHeights[0] + data.CoordinatePadding;
            int offsetX = tile.TileFrameX % (data.Width * frameWidth) / frameWidth;
            int offsetY = tile.TileFrameY % (data.Height * frameHeight) / frameHeight;

            int topLeftX = x - offsetX;
            int topLeftY = y - offsetY;

            return new Rectangle(
                topLeftX * 16,
                topLeftY * 16,
                data.Width * 16,
                data.Height * 16);
        }
    }
}
