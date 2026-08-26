using CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers
{
    /// <summary>
    /// 回收机瓦片:3×3,占位期整机程序化绘制(魔法像素拼装,零贴图),
    /// 专属贴图到位后换标准帧绘制
    /// </summary>
    internal class RecyclerTile : ModTile
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
            AddMapEntry(new Color(96, 116, 104), VaultUtils.GetLocalizedItemName<Recycler>());

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
            player.cursorItemIconID = ModContent.ItemType<Recycler>();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out RecyclerTP tp)) {
                return;
            }
            if (tp.RecData?.IsWorking == true) {
                r = 0.14f;
                g = 0.26f;
                b = 0.20f;
            }
        }

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var topLeft)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(topLeft, out RecyclerTP tp)) {
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
            if (!TileProcessorLoader.ByPositionGetTP(point, out RecyclerTP tp)) {
                return false;
            }

            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }

            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color light = Lighting.GetColor(i + 1, j + 1);
            bool powered = tp.RecData != null && tp.RecData.UEvalue >= tp.RecData.UEPerTick;
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

            //底座与机壳:回收机走冷绿钢
            Box(0, 42, 48, 6, Mul(new Color(34, 38, 36)));
            Box(3, 8, 42, 36, Mul(new Color(62, 72, 66)));
            Box(5, 10, 38, 32, Mul(new Color(48, 56, 52)));

            //拆解台面
            Box(10, 26, 28, 3, Mul(new Color(84, 94, 88)));

            //台上被拆装备:真实贴图躺台 + 稀有度色底光(工位收尾泵脉冲)
            bool hasInput = tp.RecData?.InputItem != null && !tp.RecData.InputItem.IsAir;
            if (hasInput) {
                Color rare = ItemRarity.GetColor(tp.RecData.InputItem.rare);
                SvgPathPen.SoftDot(spriteBatch, basePos + new Vector2(24f, 24f), 11f,
                    rare, 0.14f + 0.32f * tp.RarityPulse);
                Main.instance.LoadItem(tp.RecData.InputItem.type);
                VaultUtils.SimpleDrawItem(spriteBatch, tp.RecData.InputItem.type,
                    basePos + new Vector2(24f, 21.5f), 12, 1f, 0f, Mul(new Color(235, 235, 235)));
            }

            //拆解臂:门架 + 工位编舞(移位→下压→切割驻留→抬升)
            bool working = tp.RecData?.IsWorking == true;
            float headX = 14f + tp.ArmX01 * 14f;
            float armDrop = tp.ArmDrop01 * 5f;
            Box(12, 12, 3, 14, Mul(new Color(74, 84, 78)));
            Box(33, 12, 3, 14, Mul(new Color(74, 84, 78)));
            Box(12, 12, 24, 3, Mul(new Color(88, 98, 90)));
            Box(headX, 13 + armDrop, 6, 6, Mul(new Color(130, 140, 126)));
            //臂尖
            Box(headX + 2, 19 + armDrop, 2, 3, Mul(new Color(150, 158, 142)));
            //切割驻留:接触点炽亮闪烁
            if (tp.CutGlow > 0.05f) {
                float flicker = 0.35f + 0.22f * MathF.Sin(Main.GlobalTimeWrappedHourly * 43f);
                SvgPathPen.SoftDot(spriteBatch, basePos + new Vector2(headX + 3f, 23f), 5.5f,
                    new Color(255, 214, 150), tp.CutGlow * flicker);
            }

            //分选斗:右下出锭口
            Box(36, 34, 8, 8, Mul(new Color(42, 48, 44)));
            Box(37, 35, 6, 2, Mul(new Color(96, 104, 92)));

            //状态灯:统一警示语言(黄呼吸=堵料,红呼吸=缺电),工作=薄荷绿闪,待机=暗
            Color lamp;
            if (tp.VisualAlert != ProcAlert.None) {
                lamp = ProcessingChainVFX.LampColor(tp.VisualAlert, Color.White);
            }
            else if (working) {
                float blink = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
                lamp = new Color(120, 230, 170) * blink;
            }
            else {
                lamp = new Color(60, 68, 62);
            }
            Box(40, 11, 3, 3, lamp);

            return false;
        }
    }
}
