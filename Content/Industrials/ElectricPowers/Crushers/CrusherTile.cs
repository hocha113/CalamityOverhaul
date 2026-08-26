using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    /// <summary>
    /// 粉碎机瓦片:3×3,占位期整机程序化绘制(魔法像素拼装,零贴图),
    /// 专属贴图到位后换标准帧绘制。零贴图先例:OldNetRelayTile
    /// </summary>
    internal class CrusherTile : ModTile
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
            AddMapEntry(new Color(122, 110, 96), VaultUtils.GetLocalizedItemName<Crusher>());

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
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Stone);
            return false;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override bool CanDrop(int i, int j) => false;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<Crusher>();
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out CrusherTP tp)) {
                return;
            }
            if (tp.CruData?.IsWorking == true) {
                r = 0.28f;
                g = 0.22f;
                b = 0.12f;
            }
        }

        public override bool RightClick(int i, int j) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var topLeft)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(topLeft, out CrusherTP tp)) {
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
            if (!TileProcessorLoader.ByPositionGetTP(point, out CrusherTP tp)) {
                return false;
            }

            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }

            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y)
                + offset + tp.offsetPos;
            Color light = Lighting.GetColor(i + 1, j + 1);
            bool powered = tp.CruData != null && tp.CruData.UEvalue >= tp.CruData.UEPerTick;
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

            //底座与机壳
            Box(0, 42, 48, 6, Mul(new Color(38, 36, 34)));
            Box(3, 12, 42, 32, Mul(new Color(74, 70, 66)));
            Box(5, 14, 38, 28, Mul(new Color(56, 53, 50)));

            //进料斗:顶部收口的三段梯形
            Box(8, 0, 32, 4, Mul(new Color(88, 82, 76)));
            Box(12, 4, 24, 4, Mul(new Color(80, 75, 70)));
            Box(16, 8, 16, 4, Mul(new Color(72, 68, 64)));

            //斗喉可见矿料:进料看得见
            bool hasInput = tp.CruData?.InputItem != null && !tp.CruData.InputItem.IsAir;
            if (hasInput) {
                Main.instance.LoadItem(tp.CruData.InputItem.type);
                VaultUtils.SimpleDrawItem(spriteBatch, tp.CruData.InputItem.type,
                    basePos + new Vector2(24f, 8f), 11, 1f, 0f, Mul(new Color(225, 225, 225)));
            }

            //破碎腔:凹腔 + 上颚(蓄压慢合→破碎快咬→回程)+ 下颚固定
            Box(12, 18, 24, 20, Mul(new Color(30, 28, 26)));
            bool working = tp.CruData?.IsWorking == true;
            float closure = working
                ? ProcessingChainVFX.JawCurve(tp.CruData.CrushProgress, out _) : 0f;
            float jawDrop = closure * 6f;
            Box(14, 20 + jawDrop, 20, 5, Mul(new Color(120, 112, 100)));
            Box(14, 32, 20, 5, Mul(new Color(104, 98, 88)));
            //颚齿:上下各三粒
            for (int k = 0; k < 3; k++) {
                Box(16 + k * 7, 25 + jawDrop, 3, 2, Mul(new Color(140, 130, 116)));
                Box(19 + k * 7, 30, 3, 2, Mul(new Color(126, 118, 104)));
            }
            //破碎瞬间腔心迸亮
            if (closure > 0.88f) {
                float f = (closure - 0.88f) / 0.12f;
                SvgPathPen.SoftDot(spriteBatch, basePos + new Vector2(24f, 29f), 14f,
                    new Color(255, 205, 130), 0.5f * f);
            }

            //出料口:底沿右侧
            Box(34, 38, 10, 4, Mul(new Color(46, 44, 42)));

            //状态灯:统一警示语言(黄呼吸=堵料,红呼吸=缺电),工作=琥珀闪,待机=暗
            Color lamp;
            if (tp.VisualAlert != ProcAlert.None) {
                lamp = ProcessingChainVFX.LampColor(tp.VisualAlert, Color.White);
            }
            else if (working) {
                float blink = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
                lamp = new Color(255, 170, 70) * blink;
            }
            else {
                lamp = new Color(70, 66, 60);
            }
            Box(40, 15, 3, 3, lamp);

            return false;
        }
    }
}
