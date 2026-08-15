using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Rooms
{
    //====================================================================
    //房间外壳修饰:让"实心矩形盖章+挖空"出来的房间不再是纯矩形。
    //L3~L6 四层共用同一段 StampAndCarve,天花一律笔直、四角一律直角——
    //站在房里第一眼看到的就是这条平顶线,是本世界最明显的程序化痕迹。
    //
    //三件事全部走 STRUCTURES §3.2-6 的允许面,且全部是"加砖"不是"减砖":
    //  1.内角收拱   ——顶两内角补斜切砖(F24),拱起脚
    //  2.天花阶梯收分——两端每2~4列收1行,穹顶语法的平房版
    //  3.拱肋       ——长房每8~14列垂一格肋,打断平顶长直线
    //
    //为什么不做地板/天花侵蚀(§3.2-6 另一条允许面):
    //  ·地板下抠会掏空后续家具的底锚,P80 FurnitureAudit 直接报悬空;
    //  ·天花上抠会把2格厚的外壳削到1格,正是§3.2-5点名的"单格缝隙退化温床"。
    //  两条都是减砖,与既有约束冲突;silhouette 变化用加砖就够,不硬凑第三种手法。
    //
    //调用时机:紧跟 StampAndCarve,几何冻结之前(§3.1-3装修单向性)。
    //房间若在此之后自行重挖内膛(书塔等),本文件的加砖被覆盖=无害空转。
    //====================================================================
    internal static class RoomShell
    {
        //门限:低于这个尺寸的房修了也看不出,反而吃掉本就紧张的净空
        private const int ArchMinWidth = 6;
        private const int ArchMinHeight = 5;
        private const int SetbackMinWidth = 16;
        private const int SetbackMinHeight = 8;
        private const int RibMinWidth = 24;
        private const int RibMinHeight = 9;

        /// <summary>外壳修饰总入口,随机走 <see cref="WorldGen.genRand"/>(F22)</summary>
        internal static void Dress(RoomNode room, ushort brick) {
            UnifiedRandom rand = WorldGen.genRand;
            int left = room.InteriorLeft;
            int right = room.InteriorRight;
            int top = room.InteriorTop;
            int floor = room.FloorTop;
            int w = right - left;
            int h = floor - top;

            if (w < ArchMinWidth || h < ArchMinHeight) {
                return;
            }
            if (w >= SetbackMinWidth && h >= SetbackMinHeight) {
                //收分先做,再把拱脚落在收分的内端——顺序反了会被收分的填砖盖掉
                (int innerL, int innerR, int depth) = CeilingSetback(left, right, top, brick, rand);
                CornerArch(innerL, innerR + 1, top + depth, brick);
            }
            else {
                CornerArch(left, right, top, brick);
            }
            if (w >= RibMinWidth && h >= RibMinHeight) {
                CeilingRibs(left, right, top, brick, rand);
            }
        }

        //顶两内角各补一格斜切砖:左角实心偏右上、右角实心偏左上,拱自两侧起脚
        //(斜切对偶取向对齐 L1Rooms.HangingPillar 与 L7Content 的既有用法)
        private static void CornerArch(int left, int right, int top, ushort brick) {
            TileBrush.SetSloped(left, top, brick, SlopeType.SlopeUpRight);
            TileBrush.SetSloped(right - 1, top, brick, SlopeType.SlopeUpLeft);
        }

        //两端阶梯收分:自房两端向内,每段2~4列填1行,共收1~2行——
        //"每2+列变1行"是§3.2-6对轮廓变化的唯一许可形状(锯齿噪声禁用)。
        //返回(左内端列, 右内端列, 最后一级的填厚),供拱脚接在收分肩上
        private static (int InnerLeft, int InnerRight, int Depth) CeilingSetback(
            int left, int right, int top, ushort brick, UnifiedRandom rand) {
            int depth = rand.Next(1, 3);
            int cursorL = left;
            int cursorR = right - 1;
            int lastStep = depth;
            for (int step = depth; step >= 1; step--) {
                int run = rand.Next(2, 5);
                for (int i = 0; i < run; i++) {
                    //左右两端同步内收,收分对称才读得出"穹顶"而不是"塌了一角"
                    FillColumn(cursorL + i, top, step, brick, left, right);
                    FillColumn(cursorR - i, top, step, brick, left, right);
                }
                cursorL += run;
                cursorR -= run;
                lastStep = step;
                if (cursorL >= cursorR) {
                    break;
                }
            }
            return (System.Math.Min(cursorL, right - 1), System.Math.Max(cursorR, left), lastStep);
        }

        //拱肋:长房天花每8~14列垂一格,把平顶长直线打断成开间
        //顶锚家具撞上肋会放置失败→跳过+记日志(F9),不是错误
        private static void CeilingRibs(int left, int right, int top, ushort brick, UnifiedRandom rand) {
            int spacing = rand.Next(8, 15);
            //起点相位随机,免得所有房的肋都对齐在同一列上
            for (int x = left + rand.Next(4, spacing); x < right - 3; x += spacing) {
                TileBrush.SetSolid(x, top, brick);
            }
        }

        private static void FillColumn(int x, int top, int rows, ushort brick, int left, int right) {
            if (x < left || x >= right) {
                return;
            }
            for (int i = 0; i < rows; i++) {
                TileBrush.SetSolid(x, top + i, brick);
            }
        }
    }
}
