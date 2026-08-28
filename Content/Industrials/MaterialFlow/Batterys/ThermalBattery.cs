using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using Microsoft.Xna.Framework.Graphics;
using System;
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
            CreateRecipe().
            AddIngredient<CircuitBoard>(15).
            AddIngredient(ItemID.Glass, 50).
            AddRecipeGroup(CWRCrafted.TinBarGroup, 15).
            AddRecipeGroup(CWRCrafted.GoldBarGroup, 5).
            AddTile(TileID.Anvils).
            Register();

        }
    }

    internal class ThermalBatteryTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryTile";
        public const int Width = 4;
        public const int Height = 4;
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<ThermalBattery>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(2, 3);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 20];
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
    }

    internal class ThermalBatteryTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ThermalBatteryTile>();
        public override int TargetItem => ModContent.ItemType<ThermalBattery>();
        internal float oldUEValue;
        internal int activeTime;
        internal const float _maxUEValue = 8000;
        public override float MaxUEValue => _maxUEValue;
        //熔核显示比例与初始化标记
        internal float displayRatio;
        private bool ratioInited;
        //熔核窗中心(画布像素),对齐贴图观察窗
        internal static readonly Vector2 CoreCenter = new(30f, 25f);
        public override void UpdateMachine() {
            if (activeTime > 0) {
                activeTime--;
            }

            float ratio = MachineData.UEvalue / MaxUEValue;
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

        /// <summary>熔核窗辉光:贴图已画实心熔核,辉光只做电量反馈,亮度随电量、充放时呼吸</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            if (glow == null || ratio <= 0.02f) {
                return;
            }
            float activity = MathHelper.Clamp(activeTime / 60f, 0f, 1f);
            float breath = 1f + 0.14f * activity * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.2f + Position.X * 0.7f);
            Vector2 core = PosInWorld + CoreCenter - Main.screenPosition;
            Color c = new Color(255, 150, 60) with { A = 0 };
            float size = 28f + 16f * ratio;
            spriteBatch.Draw(glow, core, null, c * ((0.20f + 0.45f * ratio) * breath), 0f,
                glow.Size() * 0.5f, size / glow.Width, SpriteEffects.None, 0f);
            //近满电时窗心追加一层白热
            if (ratio > 0.96f) {
                spriteBatch.Draw(glow, core, null, new Color(255, 230, 180, 0) * 0.35f * breath, 0f,
                    glow.Size() * 0.5f, size * 0.6f / glow.Width, SpriteEffects.None, 0f);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
