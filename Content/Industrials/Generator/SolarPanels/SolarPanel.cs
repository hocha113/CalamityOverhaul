using CalamityOverhaul.Content.Items.Materials;
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

        #region 状态覆层:掠光带 + 遮挡警示灯(板体为贴图)

        /// <summary>镜面高光带色(初代冷白,MK2 覆写成圣辉金白)</summary>
        protected virtual Color GlintColor => new(214, 236, 255);

        /// <summary>
        /// 贴图上的状态覆层:高光带随 <see cref="Main.time"/> 太阳角自东向西扫、
        /// 正午顶缘日光线;遮挡时亮红色警示灯,阻塞可读。
        /// 失效(夜/日食)的哑光暗化在瓦片 PreDraw 里完成
        /// </summary>
        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 origin = PosInWorld - Main.screenPosition;
            float env = EnvFactor;
            //受光斜面:贴图 art 底对齐(顶部留 8px 空),面板斜面取 art 上部 28px
            Rectangle face = new((int)origin.X + 8, (int)origin.Y + Height - 38, Width - 16, 28);

            bool glintOn = env > 0.01f && Main.dayTime && skyExposed;
            if (glintOn) {
                //太阳角驱动的高光带中心:清晨在东缘,正午居中,黄昏在西缘
                float dayProg = (float)(Main.time / Main.dayLength);
                float bandX = MathHelper.Lerp(face.Left + 10, face.Right - 10, dayProg);
                float slant = MathHelper.Lerp(5.2f, -5.2f, dayProg);
                Color glint = GlintColor with { A = 0 };
                for (int seg = 0; seg < 3; seg++) {
                    int segY = face.Y + 2 + seg * 8;
                    //钳进面板内,晨昏两端掠光不越出边框
                    int segX = Math.Clamp((int)(bandX + slant * (seg - 1)), face.Left + 8, face.Right - 10);
                    spriteBatch.Draw(px, new Rectangle(segX - 2, segY, 6, 8), glint * (env * 0.6f));
                    spriteBatch.Draw(px, new Rectangle(segX - 8, segY, 4, 8), glint * (env * 0.25f));
                    spriteBatch.Draw(px, new Rectangle(segX + 6, segY, 4, 8), glint * (env * 0.25f));
                }
                //正午满功率:顶缘压一道日光线
                if (env > 0.8f) {
                    spriteBatch.Draw(px, new Rectangle(face.X + 4, face.Y - 2, face.Width - 8, 2),
                        new Color(255, 240, 190, 0) * ((env - 0.8f) / 0.2f * 0.8f));
                }
            }

            //遮挡警示灯:头顶被封死时红灯慢闪,阻塞状态一眼可读
            if (!skyExposed) {
                float blink = MathF.Sin(Main.GlobalTimeWrappedHourly * 4.4f) > 0f ? 1f : 0.25f;
                Rectangle lamp = new(face.Right - 10, face.Y + 4, 4, 4);
                spriteBatch.Draw(px, lamp, new Color(220, 40, 40) * blink);
                spriteBatch.Draw(px, lamp, new Color(255, 90, 90, 0) * (blink * 0.6f));
            }
        }

        /// <summary>失效哑光的瓦片绘制:夜/日食/遮挡时机身减半暗化,全系状态语言</summary>
        internal static bool DrawDimmablePanelTile(int i, int j, SpriteBatch spriteBatch, BaseSolarPanelTP tp, int tileType) {
            MachineTileDraw.DrawCell(i, j, spriteBatch, tileType, 3, 16, tp.EnvFactor <= 0.01f);
            return false;
        }

        #endregion

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }

    internal class SolarPanel : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/SolarPanel";

        /// <summary>系列色调:日光蓝,用于 UI 点缀</summary>
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
            Item.createTile = ModContent.TileType<SolarPanelTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 1000;
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient(ItemID.CrystalShard, 10).
            AddIngredient(ItemID.Glass, 30).
            AddRecipeGroup(CWRCrafted.MythrilBarGroup, 5).
            AddIngredient<CircuitBoard>(10).
            AddTile(TileID.MythrilAnvil).
            Register();

        }
    }

    internal class SolarPanelTile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/SolarPanelTile";
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
            TileObjectData.newTile.Width = 5;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(2, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
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
            if (!TileProcessorLoader.ByPositionGetTP(point, out SolarPanelTP tp)) {
                return false;
            }
            return BaseSolarPanelTP.DrawDimmablePanelTile(i, j, spriteBatch, tp, Type);
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
