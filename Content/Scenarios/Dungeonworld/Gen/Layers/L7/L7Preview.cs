using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Layers.L7
{
    //====================================================================
    //L7免接线看样入口（镜像DungeonworldPreview/L1Content惯例）：
    //任意世界脚下就地盖倒吊教堂构图，不注册GenPass、不碰管线文件。
    //仅单人调试（联机不发tile同步）；触发TestItem片段见交付报告。
    //floorRow=渡桥/顶板行走线（玩家脚下）；centerX=倒吊教堂中线。
    //====================================================================
    internal static class L7Preview
    {
        /// <summary>
        /// 看样1：空腔+前庭+渡桥+倒吊教堂全套+空腔链束。
        /// 占地约230宽×220高（脚下向上50、向下约170）。请在平坦开阔处使用。
        /// 深渊剪影厅另调 <see cref="PreviewAbyss"/>（需再向下约190行）。
        /// </summary>
        internal static void PreviewShowcase(int centerX, int floorRow) {
            if (!WarnSinglePlayer()) {
                return;
            }
            int cathLeft = centerX - L7InvertedCathedral.ArtW / 2;
            int shaftLeft = cathLeft - L7Content.CathLeftOff;
            int bandTop = floorRow - L7Content.CathTopOff;
            if (!TryLocalSpine(floorRow, out int spineInteriorTop, out int bandBottom)) {
                return;
            }

            int voidL = shaftLeft + L7Content.VoidLeftOff;
            var strip = new Rectangle(voidL - 4, bandTop,
                shaftLeft + DungeonworldMetrics.ShaftWidth + 8 - (voidL - 4),
                bandBottom + 2 - bandTop);
            if (!StripFits(strip, "Showcase")) {
                return;
            }

            Solidify(strip);
            CarveFakeShaft(shaftLeft, bandTop, floorRow, spineInteriorTop);
            L7Content.BuildComposition(new OccupancyGrid(strip), new RoomGraph(),
                shaftLeft, bandTop, spineInteriorTop, includeAbyss: false);
            WorldGen.RangeFrame(strip.Left - 1, strip.Top - 1, strip.Right + 1, strip.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L7Preview] Showcase落成 center={centerX} deck={floorRow}"
                + $" origin={L7InvertedCathedral.LastOrigin} strip={strip}");
        }

        /// <summary>
        /// 看样2：只盖倒吊教堂+辖域链束+垂钟龛（快速看 FlipY 与冥紫变调）。
        /// 占地约200宽×180高。不含前庭/渡桥/深渊厅。
        /// </summary>
        internal static void PreviewCathedral(int centerX, int floorRow) {
            if (!WarnSinglePlayer()) {
                return;
            }
            int left = centerX - L7InvertedCathedral.ArtW / 2;
            int top = floorRow;
            int pad = 28;
            var strip = new Rectangle(left - pad, top - 40,
                L7InvertedCathedral.ArtW + pad * 2,
                L7InvertedCathedral.TotalDepth + 48);
            if (!StripFits(strip, "Cathedral")) {
                return;
            }

            Solidify(strip);
            //空腔：顶板之上留32行吊距，四周留空隙，底接到龛下
            TileBrush.CarveRect(strip.Left + 2, top - 32, strip.Right - 2,
                top + L7InvertedCathedral.TotalDepth + 8, L7Style.Wall);
            L7InvertedCathedral.Build(left, top);
            L7Style.ChainBundle(left + 2, 3, top - 32, 32);
            L7Style.ChainBundle(left + 8, 3, top - 32, 32);
            WorldGen.RangeFrame(strip.Left - 1, strip.Top - 1, strip.Right + 1, strip.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L7Preview] Cathedral落成 origin=({left},{top}) strip={strip}");
        }

        /// <summary>
        /// 看样3：教堂正下方盖深渊剪影厅（垂链末端+断拱残片+黑暗留白，全封闭）。
        /// floorRow=厅顶上一行（玩家站在"层脊地板"上往下看）；需脚下再留约190行。
        /// </summary>
        internal static void PreviewAbyss(int centerX, int floorRow) {
            if (!WarnSinglePlayer()) {
                return;
            }
            int cathLeft = centerX - L7InvertedCathedral.ArtW / 2;
            int top = floorRow + L7Content.AbyssTopOff;
            int bottom = floorRow + L7Content.AbyssBottomOff;
            var strip = new Rectangle(cathLeft + 6, floorRow,
                132, bottom + 2 - floorRow);
            if (!StripFits(strip, "Abyss")) {
                return;
            }

            Solidify(strip);
            L7Content.BuildAbyssSilhouetteAt(cathLeft, top, bottom);
            WorldGen.RangeFrame(strip.Left - 1, strip.Top - 1, strip.Right + 1, strip.Bottom + 1);
            CWRMod.Instance.Logger.Info(
                $"[L7Preview] Abyss落成 [{cathLeft + 10},{top})~[{cathLeft + 134},{bottom})");
        }

        //本地伪层脊：龛底再留16行空腔，再接脊内膛（与PlanAndBuild空腔预算同构）
        private static bool TryLocalSpine(int deckRow, out int spineInteriorTop, out int bandBottom) {
            int nicheBottom = deckRow + L7InvertedCathedral.TotalDepth;
            int voidBottom = nicheBottom + 16;
            spineInteriorTop = voidBottom + 2;
            int spineFloor = spineInteriorTop + DungeonworldMetrics.SpineClearance;
            bandBottom = spineFloor + DungeonworldMetrics.SpineReserveBelow;
            if (bandBottom + 8 >= Main.maxTilesY) {
                CWRMod.Instance.Logger.Error(
                    $"[L7Preview] 本地层脊底{bandBottom}贴近世界底{Main.maxTilesY}，换更高处看样");
                return false;
            }
            return true;
        }

        private static void CarveFakeShaft(int shaftLeft, int bandTop, int deckRow, int spineInteriorTop) {
            int shaftRight = shaftLeft + DungeonworldMetrics.ShaftWidth;
            int spineFloor = spineInteriorTop + DungeonworldMetrics.SpineClearance;
            TileBrush.CarveRect(shaftLeft, bandTop + 2, shaftRight, spineFloor, L7Style.Wall);
            for (int y = bandTop + 5; y < spineFloor; y += DungeonworldMetrics.ShaftStepRows) {
                TileBrush.PlatformRow(shaftLeft, shaftRight, y, L7Style.PlatformFrameY);
            }
            //行走线与前庭东隧道齐平，保证能踏入竖井
            TileBrush.PlatformRow(shaftLeft, shaftRight, deckRow, L7Style.PlatformFrameY);
        }

        private static void Solidify(Rectangle rect) {
            for (int x = rect.Left; x < rect.Right; x++) {
                for (int y = rect.Top; y < rect.Bottom; y++) {
                    TileBrush.SetSolid(x, y, L7Style.Brick);
                }
            }
        }

        private static bool StripFits(Rectangle strip, string tag) {
            if (WorldGen.InWorld(strip.Left, strip.Top, 8)
                && WorldGen.InWorld(strip.Right - 1, strip.Bottom - 1, 8)) {
                return true;
            }
            CWRMod.Instance.Logger.Error(
                $"[L7Preview] {tag}条带{strip}超出世界边界，换平坦开阔处");
            return false;
        }

        private static bool WarnSinglePlayer() {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                return true;
            }
            CWRMod.Instance.Logger.Warn("[L7Preview] 看样入口仅单人调试用,联机不发tile同步");
            return false;
        }
    }
}
