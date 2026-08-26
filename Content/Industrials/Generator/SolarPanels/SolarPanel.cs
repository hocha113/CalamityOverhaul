using CalamityOverhaul.Common;
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
        /// <summary>镜面高光带色(初代冷白,MK2 覆写成圣辉金白)</summary>
        protected virtual Color GlintColor => new(214, 236, 255);

        /// <summary>
        /// 整机自绘,由瓦片 PreDraw 在左上格调用一次。<br/>
        /// 镀膜光伏玻璃的三条签名:高光带随 <see cref="Main.time"/> 太阳角自东向西扫、
        /// 正午满功率边框辉光、失效(夜/日食/遮挡)转哑光并入全系暗化语言;
        /// 雨天按天气系数退成微光。遮挡时亮红色警示灯,阻塞可读
        /// </summary>
        internal void DrawPanelBody(SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 origin = PosInWorld - Main.screenPosition;
            Color light = Lighting.GetColor(Position.X + 1, Position.Y);

            float env = EnvFactor;
            float weather = SampleWeatherFactor();
            bool producing = env > 0.01f;
            //失效哑光:全系"断电减半"暗化语言;只压 RGB 保 alpha,乘 float 会把面板变半透
            float dim = producing ? 1f : 0.5f;
            static Color DimRGB(Color c, float f)
                => new((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f), c.A);
            //雨天/日食去饱和:板面失去镜面感
            float matte = producing ? 1f - weather : 1f;

            //支腿:左右两根,撑起面板
            Color legColor = new Color(52, 44, 38).MultiplyRGB(light);
            spriteBatch.Draw(px, new Rectangle((int)origin.X + 8, (int)origin.Y + 18, 4, 14), legColor);
            spriteBatch.Draw(px, new Rectangle((int)origin.X + Width - 12, (int)origin.Y + 18, 4, 14), legColor);

            //面板体:上半的平板
            Rectangle body = new((int)origin.X, (int)origin.Y, Width, 18);
            spriteBatch.Draw(px, body, DimRGB(PanelColor.MultiplyRGB(light), dim));

            //太阳角驱动的高光带中心:清晨在东缘,正午居中,黄昏在西缘
            float dayProg = Main.dayTime ? (float)(Main.time / Main.dayLength) : 0f;
            float bandX = MathHelper.Lerp(body.Left + 5, body.Right - 5, dayProg);
            bool glintOn = producing && Main.dayTime && skyExposed;

            //电池格:2 行 × 6 列;靠近高光带的格子被点亮,雨天整排转灰哑
            for (int row = 0; row < 2; row++) {
                for (int col = 0; col < 6; col++) {
                    Rectangle cellRect = new(body.X + 3 + col * 10, body.Y + 3 + row * 7, 8, 5);
                    Color cell = CellColor;
                    if (matte > 0.01f) {
                        //哑光:向灰收拢,镜面身份让位给"湿玻璃"
                        float gray = (cell.R + cell.G + cell.B) / 3f / 255f;
                        cell = Color.Lerp(cell, new Color(gray * 0.75f, gray * 0.78f, gray * 0.82f), matte * 0.8f);
                    }
                    cell = DimRGB(cell.MultiplyRGB(light), dim);
                    spriteBatch.Draw(px, cellRect, cell);

                    if (glintOn) {
                        float dist = MathF.Abs(cellRect.Center.X - bandX);
                        float boost = MathF.Exp(-dist * dist / 260f) * env;
                        if (boost > 0.03f) {
                            spriteBatch.Draw(px, cellRect, GlintColor with { A = 0 } * (boost * 0.5f));
                        }
                    }
                }
            }

            //镜面高光带:三段错位读作斜向掠光,入射角随日头翻转
            if (glintOn) {
                float slant = MathHelper.Lerp(2.6f, -2.6f, dayProg);
                for (int seg = 0; seg < 3; seg++) {
                    int segY = body.Y + seg * 6;
                    //钳进面板内,晨昏两端掠光不越出边框
                    int segX = Math.Clamp((int)(bandX + slant * (seg - 1)), body.Left + 6, body.Right - 7);
                    Color glint = GlintColor with { A = 0 };
                    spriteBatch.Draw(px, new Rectangle(segX - 1, segY, 3, 6), glint * (env * 0.75f));
                    spriteBatch.Draw(px, new Rectangle(segX - 4, segY, 2, 6), glint * (env * 0.3f));
                    spriteBatch.Draw(px, new Rectangle(segX + 3, segY, 2, 6), glint * (env * 0.3f));
                }
            }

            //边框:正午满功率时通体镀亮,顶缘再压一道日光线
            Color frame = FrameColor;
            if (env > 0.8f) {
                float noon = (env - 0.8f) / 0.2f;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Position.X * 0.7f);
                frame = Color.Lerp(frame, new Color(255, 222, 150), noon * pulse);
            }
            frame = DimRGB(frame.MultiplyRGB(light), dim);
            spriteBatch.Draw(px, new Rectangle(body.X, body.Y, body.Width, 2), frame);
            spriteBatch.Draw(px, new Rectangle(body.X, body.Bottom - 2, body.Width, 2), frame);
            spriteBatch.Draw(px, new Rectangle(body.X, body.Y, 2, body.Height), frame);
            spriteBatch.Draw(px, new Rectangle(body.Right - 2, body.Y, 2, body.Height), frame);
            if (env > 0.8f) {
                spriteBatch.Draw(px, new Rectangle(body.X + 2, body.Y - 1, body.Width - 4, 1),
                    new Color(255, 240, 190, 0) * ((env - 0.8f) / 0.2f * 0.8f));
            }

            //遮挡警示灯:头顶被封死时红灯慢闪,阻塞状态一眼可读
            if (!skyExposed) {
                float blink = MathF.Sin(Main.GlobalTimeWrappedHourly * 4.4f) > 0f ? 1f : 0.25f;
                Rectangle lamp = new(body.Right - 6, body.Y + 3, 2, 2);
                spriteBatch.Draw(px, lamp, new Color(220, 40, 40) * blink);
                spriteBatch.Draw(px, lamp, new Color(255, 90, 90, 0) * (blink * 0.6f));
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
