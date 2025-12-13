using CalamityOverhaul.Content.UIs;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class Spectrometer : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Spectrometer";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(10, 3));
        }
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
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<SpectrometerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }
        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient(ItemID.DyeVat).
                AddIngredient(CWRID.Item_DubiousPlating, 15).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 15).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class SpectrometerTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/SpectrometerTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(87, 72, 81), VaultUtils.GetLocalizedItemName<Spectrometer>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(1, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool RightClick(int i, int j) {
            if (TileProcessorLoader.AutoPositionGetTP(i, j, out SpectrometerTP spectrometer)) {
                spectrometer.RightClick(Main.LocalPlayer);
            }
            return base.RightClick(i, j);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<Spectrometer>());

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out SpectrometerTP spectrometer)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            //根据TP中更新的frame变量来决定绘制物块的哪一帧动画
            int frameYPos = t.TileFrameY + spectrometer.frame * 54; //每个物块帧的高度是3格*18像素=54
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal class SpectrometerTP : BaseDyeTP
    {
        public override int TargetTileID => ModContent.TileType<SpectrometerTile>();
        public override int TargetItem => ModContent.ItemType<Spectrometer>();
        public override bool CanDrop => true;
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        public override BaseDyeMachineUI DyeMachineUI => SpectrometerUI.Instance;
        internal int frame;
        internal int workTime;
        public override void UpdateDyeMachine() {
            if (workTime > 0) {
                VaultUtils.ClockFrame(ref frame, 10, 2);
                workTime--;
            }
        }
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            Item item = null;
            if (BeDyedItem.type > ItemID.None) {
                item = BeDyedItem;
            }
            if (ResultDyedItem.type > ItemID.None) {
                item = ResultDyedItem;
            }

            if (item == null) {
                return;
            }

            float time = Main.GlobalTimeWrappedHourly;
            Vector2 drawPosition = CenterInWorld - Main.screenPosition;
            Color baseColor = Lighting.GetColor(PosInWorld.ToTileCoordinates());

            //机械感扫描：使用锯齿波代替正弦，制造扫描上下走动感
            float saw = time * 2f % 2f; //范围0~2
            float animOffsetY = (saw < 1f ? saw : 2f - saw) * 6f - 3f; //上下往返，范围-3~3

            //脉冲缩放：机械感更生硬，使用abs(sin)制造周期性收缩
            float scale = 1f + MathF.Abs(MathF.Sin(time * 5f)) * 0.07f;

            float rotation = 0;

            //水平偏移，模拟机器臂扫描
            float animOffsetX = MathF.Round(MathF.Sin(time * 4f) * 2f);

            Vector2 itemDrawPos = drawPosition + new Vector2(animOffsetX, animOffsetY);
            if (workTime <= 0) {
                scale = 1f;
                itemDrawPos = drawPosition;
            }

            //颜色脉冲，偏冷光
            float pulse = (MathF.Sin(time * 6f) + 1f) * 0.5f; //0~1
            Color drawColor = Color.Lerp(baseColor, Color.Cyan, pulse * 0.4f);

            if (DyeSlotItem.type > ItemID.None) {
                item.BeginDyeEffectForWorld(DyeSlotItem.type);
            }
            VaultUtils.SimpleDrawItem(spriteBatch, item.type, itemDrawPos, Width / 2, scale, rotation, drawColor);
            if (DyeSlotItem.type > ItemID.None) {
                item.EndDyeEffectForWorld();
            }
        }
        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}