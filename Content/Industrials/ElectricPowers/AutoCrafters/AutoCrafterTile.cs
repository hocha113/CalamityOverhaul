using CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers;
using CalamityOverhaul.Content.UIs.UIEffect;
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

            //装配头:横梁下的滑块,作业时随进度双往返(阻塞冻在半路)
            bool working = data != null && data.CraftProgress > 0;
            float headX = 10f + tp.HeadX01 * 24f;
            Box(headX, 5, 6, 5, Mul(new Color(104, 112, 130)));
            Box(headX + 2, 10, 2, 4, Mul(new Color(126, 134, 152)));
            //头尖打印微光:只在进度真实推进时亮
            if (tp.AdvanceGlow > 0.08f) {
                float flick = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 37f);
                SvgPathPen.SoftDot(spriteBatch, basePos + new Vector2(headX + 3f, 14f), 4f,
                    new Color(150, 220, 255), 0.4f * tp.AdvanceGlow * flick);
            }

            //全息投影:钉选产物的蓝图 + 作业打印实体化
            if (powered && data.PinnedResultType > 0) {
                DrawHologram(spriteBatch, tp, data, basePos, px, working);
            }

            //状态灯:统一警示语言(黄呼吸=缺料/堵,红呼吸=缺电),工作=蓝闪,待机=暗
            Color lamp;
            if (tp.VisualAlert != ProcAlert.None) {
                lamp = ProcessingChainVFX.LampColor(tp.VisualAlert, Color.White);
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

        /// <summary>
        /// 全息投影:台面投影仪光锥 + 半透蓝青蓝图(伪3D自旋+故障错位)
        /// + 作业时自下而上真彩打印(打印线=扫描线) + 完成全彩定格。
        /// 配方失踪/贴图取不到时降级为线框占位,同一套光学语言
        /// </summary>
        private static void DrawHologram(SpriteBatch sb, AutoCrafterTP tp, AutoCrafterData data,
            Vector2 basePos, Texture2D px, bool working) {
            Rectangle src = new(0, 0, 1, 1);
            bool blocked = tp.VisualAlert == ProcAlert.Blocked;
            float breath = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f);
            Vector2 anchor = basePos + new Vector2(24f, 15f);

            //投影仪:台面发射器亮缝 + 上行光锥(锥体上宽下窄,越高越淡)
            Color coneCol = blocked ? new Color(190, 150, 70, 0) : new Color(110, 190, 255, 0);
            float coneMul = (blocked ? 0.5f : 1f) * (0.5f + 0.5f * breath);
            sb.Draw(px, basePos + new Vector2(21.5f, 27f), src,
                new Color(170, 225, 255, 0) * (0.55f * coneMul), 0f, Vector2.Zero,
                new Vector2(5f, 2f), SpriteEffects.None, 0f);
            sb.Draw(px, basePos + new Vector2(22f, 22f), src, coneCol * (0.13f * coneMul), 0f,
                Vector2.Zero, new Vector2(4f, 5f), SpriteEffects.None, 0f);
            sb.Draw(px, basePos + new Vector2(20f, 17f), src, coneCol * (0.09f * coneMul), 0f,
                Vector2.Zero, new Vector2(8f, 5f), SpriteEffects.None, 0f);
            sb.Draw(px, basePos + new Vector2(17f, 9f), src, coneCol * (0.05f * coneMul), 0f,
                Vector2.Zero, new Vector2(14f, 8f), SpriteEffects.None, 0f);

            //贴图有效性:配方失踪或类型越界(模组增删)走线框占位
            int type = data.PinnedResultType;
            bool texValid = !tp.PinMissing && type > 0 && type < TextureAssets.Item.Length
                && ContentSamples.ItemsByType.ContainsKey(type);
            Texture2D tex = null;
            if (texValid) {
                Main.instance.LoadItem(type);
                tex = TextureAssets.Item[type].Value;
                texValid = tex != null;
            }
            if (!texValid) {
                DrawHoloPlaceholder(sb, px, anchor);
                return;
            }

            Rectangle frame = Main.itemAnimations[type]?.GetFrame(tex) ?? tex.Frame();
            float fit = Math.Min(1f, 18f / Math.Max(frame.Width, frame.Height));
            //伪3D自旋:横向压扁+过零翻面;阻塞时冻住
            float cos = blocked ? 0.86f : MathF.Cos(Main.GlobalTimeWrappedHourly * 1.35f);
            float sx = MathF.Max(MathF.Abs(cos), 0.24f);
            SpriteEffects fxFlip = cos < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float bob = blocked ? 0f : 1.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f);
            Vector2 pos = anchor + new Vector2(0f, -bob);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 scale = new Vector2(fit * sx, fit);

            //底衬辉光(下层,小占比)
            SvgPathPen.SoftDot(sb, pos, 12f,
                blocked ? new Color(200, 160, 80) : new Color(90, 180, 255), 0.09f + 0.07f * breath);

            //半透蓝图体 + 加色层;阻塞转琥珀灰并随警示呼吸
            Color ghostBody, ghostAdd;
            if (blocked) {
                float ab = ProcessingChainVFX.AlertBreath;
                ghostBody = new Color(150, 128, 84, 90) * (0.35f + 0.25f * ab);
                ghostAdd = new Color(190, 150, 70, 0) * (0.22f * ab);
            }
            else {
                ghostBody = new Color(80, 150, 220, 96) * (0.5f + 0.2f * breath);
                ghostAdd = new Color(70, 170, 255, 0) * (0.16f + 0.36f * breath);
            }
            sb.Draw(tex, pos, frame, ghostBody, 0f, origin, scale, fxFlip, 0f);
            sb.Draw(tex, pos, frame, ghostAdd, 0f, origin, scale, fxFlip, 0f);

            //打印实体化:自下而上真彩填充,分界打印线;进度冻结画面即冻结
            float p01 = working
                ? MathHelper.Clamp(data.CraftProgress / (float)data.MaxCraftProgress, 0f, 1f) : 0f;
            int fillH = (int)(frame.Height * p01);
            if (working && fillH >= 1) {
                Rectangle solidSrc = new(frame.X, frame.Y + frame.Height - fillH, frame.Width, fillH);
                Vector2 subOffset = new(0f, (frame.Height - fillH) * 0.5f * fit);
                sb.Draw(tex, pos + subOffset, solidSrc, Color.White * (blocked ? 0.5f : 0.9f), 0f,
                    new Vector2(frame.Width * 0.5f, fillH * 0.5f), scale, fxFlip, 0f);

                //打印线:横贯全息宽度的亮线 + 分界行切片提亮
                float lineOffY = (frame.Height * 0.5f - fillH) * fit;
                float lineW = frame.Width * fit * sx + 6f;
                sb.Draw(px, pos + new Vector2(-lineW * 0.5f, lineOffY), src,
                    new Color(170, 235, 255, 0) * (blocked ? 0.15f : 0.30f + 0.35f * tp.AdvanceGlow),
                    0f, new Vector2(0f, 0.5f), new Vector2(lineW, 1.4f), SpriteEffects.None, 0f);
                int rowH = Math.Min(2, fillH);
                Rectangle rowSrc = new(frame.X, frame.Y + frame.Height - fillH, frame.Width, rowH);
                sb.Draw(tex, pos + new Vector2(0f, lineOffY + rowH * 0.5f * fit), rowSrc,
                    new Color(190, 245, 255, 0) * (blocked ? 0.2f : 0.7f), 0f,
                    new Vector2(frame.Width * 0.5f, rowH * 0.5f), scale, fxFlip, 0f);
            }

            //故障错位:约2.7秒一次,单帧一条横切片横移
            if (!blocked) {
                float cycle = Main.GlobalTimeWrappedHourly % 2.7f;
                if (cycle < 0.034f) {
                    int gh = Math.Max(2, frame.Height / 6);
                    int gy = (int)((frame.Height - gh) * (Main.GlobalTimeWrappedHourly * 37f % 1f));
                    Rectangle gsrc = new(frame.X, frame.Y + gy, frame.Width, gh);
                    float gdir = Main.GlobalTimeWrappedHourly * 53f % 1f > 0.5f ? 2.5f : -2.5f;
                    Vector2 gOffset = new(gdir, (gy + gh * 0.5f - frame.Height * 0.5f) * fit);
                    sb.Draw(tex, pos + gOffset, gsrc, new Color(120, 220, 255, 0) * 0.5f, 0f,
                        new Vector2(frame.Width * 0.5f, gh * 0.5f), scale, fxFlip, 0f);
                }
            }

            //完成定格:蓝图正面全彩闪现,随 CompleteFlash 退潮
            if (tp.CompleteFlash > 0) {
                float f = tp.CompleteFlash / 18f;
                Vector2 fullScale = new(fit, fit);
                sb.Draw(tex, pos, frame, Color.White * (0.45f + 0.5f * f), 0f, origin,
                    fullScale, SpriteEffects.None, 0f);
                sb.Draw(tex, pos, frame, new Color(200, 240, 255, 0) * (f * 0.8f), 0f, origin,
                    fullScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>全息占位:线框盒+惊叹号,琥珀闪烁——配方失踪/贴图缺失的降级形态</summary>
        private static void DrawHoloPlaceholder(SpriteBatch sb, Texture2D px, Vector2 anchor) {
            Rectangle src = new(0, 0, 1, 1);
            float flick = 0.45f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7.7f);
            //偶发断闪:全息信号不稳
            if (Main.GlobalTimeWrappedHourly % 1.9f < 0.05f) {
                flick *= 0.25f;
            }
            Color line = new Color(255, 196, 64, 0) * flick;

            void Edge(float x, float y, float w, float h)
                => sb.Draw(px, anchor + new Vector2(x, y), src, line, 0f, Vector2.Zero,
                    new Vector2(w, h), SpriteEffects.None, 0f);

            //16x14 线框
            Edge(-8f, -7f, 16f, 1f);
            Edge(-8f, 6f, 16f, 1f);
            Edge(-8f, -7f, 1f, 14f);
            Edge(7f, -7f, 1f, 14f);
            //中央惊叹号
            Edge(-0.75f, -4f, 1.5f, 5f);
            Edge(-0.75f, 2.5f, 1.5f, 1.5f);
        }
    }
}
