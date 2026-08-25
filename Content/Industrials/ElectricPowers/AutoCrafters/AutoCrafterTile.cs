using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.AutoCrafters
{
    /// <summary>
    /// 自动合成台瓦片:3×3,占位期整机程序化绘制(魔法像素拼装,零贴图),
    /// 钉选产物以全息投影浮在台面上方,专属贴图到位后换标准帧绘制
    /// </summary>
    internal class AutoCrafterTile : ModTile
    {
        public const int TileWidth = 3;
        public const int TileHeight = 3;
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(96, 106, 130), VaultUtils.GetLocalizedItemName<AutoCrafter>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Origin = new Point16(1, 2);
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide,
                TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<AutoCrafter>();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out AutoCrafterTP tp)) {
                return;
            }
            if (tp.CrafterData != null && tp.CrafterData.PinnedResultType > 0) {
                r = 0.12f;
                g = 0.18f;
                b = 0.30f;
            }
        }

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var topLeft)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(topLeft, out AutoCrafterTP tp)) {
                return false;
            }

            tp.RightClickByTile(false);
            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            //整机只在左上角那格画一次
            if (point.X != i || point.Y != j) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out AutoCrafterTP tp)) {
                return false;
            }

            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }

            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color light = Lighting.GetColor(i + 1, j + 1);
            var data = tp.CrafterData;
            bool powered = data != null && data.UEvalue >= data.CraftCost;
            if (!powered) {
                light.R /= 2;
                light.G /= 2;
                light.B /= 2;
                light.A = 255;
            }

            void Box(float x, float y, float w, float h, Color color) {
                spriteBatch.Draw(px, basePos + new Vector2(x, y), new Rectangle(0, 0, 1, 1),
                    color, 0f, Vector2.Zero, new Vector2(w, h), SpriteEffects.None, 0f);
            }
            Color Mul(Color c) => new Color(
                c.R * light.R / 255, c.G * light.G / 255, c.B * light.B / 255, (byte)255);

            //底座与立柱:装配台是敞开的门架结构
            Box(0, 42, 48, 6, Mul(new Color(36, 38, 44)));
            Box(4, 30, 40, 12, Mul(new Color(58, 62, 74)));
            Box(6, 32, 36, 8, Mul(new Color(46, 50, 60)));
            //台面
            Box(2, 28, 44, 3, Mul(new Color(84, 90, 106)));
            //门架立柱与横梁
            Box(6, 4, 3, 24, Mul(new Color(66, 70, 84)));
            Box(39, 4, 3, 24, Mul(new Color(66, 70, 84)));
            Box(6, 2, 36, 3, Mul(new Color(78, 84, 100)));

            //装配头:横梁下的滑块,工作时左右巡行
            bool working = data != null && data.CraftProgress > 0;
            float headPhase = working ? MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f : 0.5f;
            float headX = 10f + headPhase * 24f;
            Box(headX, 5, 6, 5, Mul(new Color(104, 112, 130)));
            Box(headX + 2, 10, 2, 4, Mul(new Color(126, 134, 152)));

            //全息投影:钉选产物浮在台面上方,呼吸明灭
            if (powered && data.PinnedResultType > 0) {
                float holo = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
                Main.instance.LoadItem(data.PinnedResultType);
                VaultUtils.SimpleDrawItem(spriteBatch, data.PinnedResultType,
                    basePos + new Vector2(24f, 19f - 1.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f)),
                    16, 1f, 0f, new Color(130, 200, 255) * holo);
                //投影束
                Box(22, 22, 4, 6, new Color(120, 190, 255) * (holo * 0.30f));
            }

            //状态灯:工作=蓝呼吸,缺电=红,待机=暗
            Color lamp;
            if (!powered) {
                lamp = new Color(150, 40, 30);
            }
            else if (working) {
                float blink = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
                lamp = new Color(120, 190, 255) * blink;
            }
            else {
                lamp = new Color(58, 62, 72);
            }
            Box(43, 33, 3, 3, lamp);

            return false;
        }
    }
}
