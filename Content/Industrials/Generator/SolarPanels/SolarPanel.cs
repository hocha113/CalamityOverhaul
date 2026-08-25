using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.SolarPanels
{
    /// <summary>
    /// 太阳能板共用逻辑:白天按正午峰值曲线发电,雨天衰减、日食归零,
    /// 头顶被实体块遮挡时停摆(列扫描节流缓存)。
    /// 昼夜/天气是同步的世界状态,各端确定性本地模拟,零额外网络
    /// </summary>
    public abstract class BaseSolarPanelTP : BaseGeneratorTP, IGeneratorReadout
    {
        /// <summary>正午满工况下的峰值输出(UE/tick)</summary>
        public abstract float PeakOutput { get; }

        /// <summary>露天扫描间隔(帧),镜像草药机的节流扫描纪律</summary>
        private const int ScanInterval = 300;
        private int scanTimer;
        /// <summary>头顶露天缓存;首帧扫描前先按露天算,300 帧内自愈</summary>
        private bool skyExposed = true;

        public override MachineModules.MachineModuleTarget ModuleHostKind
            => MachineModules.MachineModuleTarget.SolarPanel;
        public override int ModuleSlotCount => 2;

        #region 环境系数

        /// <summary>昼间曲线:正午 1,日出日落 0,夜间 0</summary>
        internal static float SampleDayCurve() {
            if (!Main.dayTime) {
                return 0f;
            }
            return MathF.Sin(MathF.PI * (float)(Main.time / Main.dayLength));
        }

        /// <summary>天气系数:日食 ×0,雨天 ×0.4,晴天 ×1</summary>
        internal static float SampleWeatherFactor() {
            if (Main.eclipse) {
                return 0f;
            }
            if (Main.raining) {
                return 0.4f;
            }
            return 1f;
        }

        /// <summary>综合环境系数 0..1(昼间曲线 × 天气 × 露天)</summary>
        internal float EnvFactor => skyExposed ? SampleDayCurve() * SampleWeatherFactor() : 0f;

        #endregion

        #region 读数板

        public GeneratorReadoutKind ReadoutKind => GeneratorReadoutKind.Solar;
        public float ConditionRatio => MathHelper.Clamp(EnvFactor, 0f, 1f);
        public bool ConditionOk => EnvFactor > 0.05f;
        public float OutputPerSecond => PeakOutput * EnvFactor * ModuleRack.GenOutputMult * 60f;

        #endregion

        public override void GeneratorUpdate() {
            if (--scanTimer <= 0) {
                scanTimer = ScanInterval;
                skyExposed = ScanSkyExposure();
            }

            float gain = PeakOutput * EnvFactor * ModuleRack.GenOutputMult;
            if (gain > 0f && MachineData.UEvalue < MaxUEValue) {
                MachineData.UEvalue += gain;
            }
        }

        /// <summary>逐列向上扫到天顶,遇实体块即遮挡;只读物块,并行阶段安全</summary>
        private bool ScanSkyExposure() {
            int tileWidth = Width / 16;
            for (int x = Position.X; x < Position.X + tileWidth; x++) {
                for (int y = Position.Y - 1; y >= 10; y--) {
                    if (WorldGen.SolidTile(x, y)) {
                        return false;
                    }
                }
            }
            return true;
        }

        #region 程序化绘制:面板体 + 支腿,待美术贴图后替换

        /// <summary>面板底色</summary>
        protected virtual Color PanelColor => new(30, 52, 92);
        /// <summary>电池格色</summary>
        protected virtual Color CellColor => new(58, 112, 205);
        /// <summary>边框金属色</summary>
        protected virtual Color FrameColor => new(148, 118, 62);

        /// <summary>整机自绘,由瓦片 PreDraw 在左上格调用一次</summary>
        internal void DrawPanelBody(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 origin = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.X + 1, Position.Y);

            //支腿:左右两根,撑起面板
            Color legColor = new Color(52, 44, 38).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle((int)origin.X + 8, (int)origin.Y + 18, 4, 14), legColor);
            spriteBatch.Draw(px, new Rectangle((int)origin.X + Width - 12, (int)origin.Y + 18, 4, 14), legColor);

            //面板体:上半的平板
            Rectangle body = new((int)origin.X, (int)origin.Y, Width, 18);
            spriteBatch.Draw(px, body, PanelColor.MultiplyRGB(light));

            //电池格:2 行 × 6 列
            Color cell = CellColor.MultiplyRGB(light);
            for (int row = 0; row < 2; row++) {
                for (int col = 0; col < 6; col++) {
                    Rectangle cellRect = new(body.X + 3 + col * 10, body.Y + 3 + row * 7, 8, 5);
                    spriteBatch.Draw(px, cellRect, cell);
                }
            }

            //边框
            Color frame = FrameColor.MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle(body.X, body.Y, body.Width, 2), frame);
            spriteBatch.Draw(px, new Rectangle(body.X, body.Bottom - 2, body.Width, 2), frame);
            spriteBatch.Draw(px, new Rectangle(body.X, body.Y, 2, body.Height), frame);
            spriteBatch.Draw(px, new Rectangle(body.Right - 2, body.Y, 2, body.Height), frame);

            //反光:环境系数越高越亮,发电状态一眼可读
            float env = EnvFactor;
            if (env > 0.01f) {
                spriteBatch.Draw(px, body, Color.White * (env * 0.28f));
            }
        }

        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }

    internal class SolarPanel : ModItem
    {
        /// <summary>贴图复用热能发电机,靠日光蓝色调区分;专属贴图见待美术清单</summary>
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGenerator";

        /// <summary>系列色调:日光蓝</summary>
        internal static readonly Color Tint = new(120, 185, 255);

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.color = Tint;
            Item.createTile = ModContent.TileType<SolarPanelTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1000;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.CrystalShard, 10).
                AddIngredient(ItemID.Glass, 30).
                AddRecipeGroup(CWRCrafted.MythrilBarGroup, 5).
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.CrystalShard, 10).
                AddIngredient(ItemID.Glass, 30).
                AddRecipeGroup(CWRCrafted.MythrilBarGroup, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
        }
    }

    internal class SolarPanelTile : BaseGeneratorTile
    {
        /// <summary>零贴图程序化绘制,占位魔法像素保证加载安全</summary>
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<SolarPanelTP>();
        public override int GeneratorUI => UIHandleLoader.GetUIHandleID<GeneratorReadoutUI>();
        public override int TargetItem => ModContent.ItemType<SolarPanel>();

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(58, 112, 205), VaultUtils.GetLocalizedItemName<SolarPanel>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide,
                TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            //整机只在左上格画一次
            if (point.X != i || point.Y != j) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out SolarPanelTP tp)) {
                return false;
            }
            tp.DrawPanelBody(spriteBatch);
            return false;
        }
    }

    internal class SolarPanelTP : BaseSolarPanelTP
    {
        public override int TargetTileID => ModContent.TileType<SolarPanelTile>();
        public override int TargetItem => ModContent.ItemType<SolarPanel>();
        public override float MaxUEValue => 1000 * ModuleRack.StorageMult;
        public override float PeakOutput => 0.8f;
    }
}
