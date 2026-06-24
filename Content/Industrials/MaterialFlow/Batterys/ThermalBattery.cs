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
        //熔腔在电池本地坐标(0~1)中的范围，依据美术开窗测得，可微调
        private static readonly Vector2 ChamberMin = new(0.21f, 0.20f);
        private static readonly Vector2 ChamberMax = new(0.71f, 0.82f);
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

        //熔核绘制在墙体之后、物块之前（PreTileDraw 层），让金属外壳的透明窗口把熔核透出
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            Effect effect = EffectLoader.ThermalBatteryCore?.Value;
            if (effect == null) {
                return;//着色器缺失时交由 Tile.PreDraw 的 CPU 回退接管
            }

            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            float activity = MathHelper.Clamp(activeTime / 60f, 0f, 1f);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(1f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(Width, Height));
            effect.Parameters["uFill"]?.SetValue(ratio);
            effect.Parameters["uActivity"]?.SetValue(activity);
            effect.Parameters["uChamberMin"]?.SetValue(ChamberMin);
            effect.Parameters["uChamberMax"]?.SetValue(ChamberMax);

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            drawPos.X += 4;
            Rectangle dest = new((int)drawPos.X, (int)drawPos.Y, Width - 4, Height);

            //切到 Immediate 应用熔核着色器，绘制后恢复 PreTileDraw 批次(Main.Transform)
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.Transform);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, dest, Color.White);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
