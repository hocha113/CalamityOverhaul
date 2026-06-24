using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys
{
    internal class ThermalBattery : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBattery";
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
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Green;
            Item.createTile = ModContent.TileType<ThermalBatteryTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = ThermalBatteryTP._maxUEValue;
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 15).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 15).
                AddIngredient(ItemID.Glass, 50).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 15).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 5).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.Glass, 50).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 15).
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 5).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    internal class ThermalBatteryTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryTile";
        public const int Width = 3;
        public const int Height = 4;
        public const int OriginOffsetX = 1;
        public const int OriginOffsetY = 1;
        public const int SheetSquare = 18;
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/ThermalBatteryTile")]
        private static Asset<Texture2D> tileAsset = null;
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/ThermalBatteryFull")]
        private static Asset<Texture2D> tileFullAsset = null;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AnimationFrameHeight = 72;

            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<ThermalBattery>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<ThermalBattery>();//当玩家鼠标悬停在物块之上时，显示该物品的材质
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out ThermalBatteryTP thermal)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += thermal.frame * (Height * SheetSquare);
            Texture2D tex = tileAsset.Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            //着色器可用时由 ThermalBatteryTP.Draw 绘制熔核，这里只画金属外壳
            bool shaderCore = EffectLoader.ThermalBatteryCore?.Value != null;
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, thermal.fullLoad ? t.TileFrameY : frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                if (!shaderCore) {
                    //回退：旧的内部发光贴图随电量做透明度渐变
                    Texture2D glow = tileFullAsset.Value;
                    spriteBatch.Draw(glow, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                        , thermal.drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
                }
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class ThermalBatteryTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ThermalBatteryTile>();
        public override int TargetItem => ModContent.ItemType<ThermalBattery>();
        internal int frame;
        internal Color drawColor;
        internal float oldUEValue;
        internal int activeTime;
        internal const float _maxUEValue = 8000;
        public override float MaxUEValue => _maxUEValue;
        internal bool fullLoad;
        //熔核着色器用：平滑后的电量比例与初始化标记
        internal float displayRatio;
        private bool ratioInited;
        //熔核着色器逻辑分辨率(电池像素尺寸 3x4 格)，与熔腔标定基准一致，整批共享
        internal static readonly Vector2 CoreResolution = new(ThermalBatteryTile.Width * 16, ThermalBatteryTile.Height * 16);
        //熔腔在电池本地坐标(0~1)中的范围，依据美术开窗测得，可微调
        internal static readonly Vector2 ChamberMin = new(0.21f, 0.20f);
        internal static readonly Vector2 ChamberMax = new(0.71f, 0.82f);
        public override void UpdateMachine() {
            fullLoad = MachineData.UEvalue >= MaxUEValue;
            if (--activeTime > 0 || fullLoad) {
                VaultUtils.ClockFrame(ref frame, 5, 5);
            }

            float ratio = MachineData.UEvalue / MaxUEValue;
            drawColor = Color.White * ratio;
            if (!ratioInited) {
                displayRatio = ratio;
                ratioInited = true;
            }
            else {
                displayRatio = MathHelper.Lerp(displayRatio, ratio, 0.1f);
            }
            if (oldUEValue != MachineData.UEvalue) {
                activeTime = 60;
                oldUEValue = MachineData.UEvalue;
            }
        }

        /// <summary>
        /// 熔核单实例绘制：由 <see cref="ThermalBatteryCoreDraw"/> 合批调用（墙后物块前，金属外壳透明窗口透出）。
        /// 共享着色器参数由合批器统一设置；逐电池数据走顶点色：r=电量比例, g=充能活跃度
        /// </summary>
        internal void DrawCore(SpriteBatch spriteBatch) {
            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            float activity = MathHelper.Clamp(activeTime / 60f, 0f, 1f);
            Vector2 drawPos = PosInWorld - Main.screenPosition;
            drawPos.X += 4;
            Rectangle dest = new((int)drawPos.X, (int)drawPos.Y, Width - 4, Height);
            Color data = new((byte)(ratio * 255f), (byte)(activity * 255f), (byte)0, (byte)255);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, dest, data);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }

    /// <summary>
    /// 热能电池熔核合批绘制：屏内所有热能电池共用一次着色器批次（墙后物块前），消除逐电池切批次开销
    /// </summary>
    internal class ThermalBatteryCoreDraw : GlobalTileProcessor
    {
        public override bool PreTileDrawEverything(SpriteBatch spriteBatch) {
            MachineShaderBatch.DrawBatch(spriteBatch, EffectLoader.ThermalBatteryCore, SamplerState.LinearClamp,
                static tp => tp is ThermalBatteryTP,
                static effect => {
                    effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    effect.Parameters["uAlpha"]?.SetValue(1f);
                    effect.Parameters["uResolution"]?.SetValue(ThermalBatteryTP.CoreResolution);
                    effect.Parameters["uChamberMin"]?.SetValue(ThermalBatteryTP.ChamberMin);
                    effect.Parameters["uChamberMax"]?.SetValue(ThermalBatteryTP.ChamberMax);
                },
                tp => ((ThermalBatteryTP)tp).DrawCore(spriteBatch));
            return true;
        }
    }
}
