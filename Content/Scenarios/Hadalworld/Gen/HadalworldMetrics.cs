namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen
{
    //垂直分带,自上而下镜像真实大洋分带命名
    internal enum HadalZone
    {
        Sky,      //天空缓冲带+海面以上空气
        Sunlit,   //日光带:浅海,海沟入口
        Twilight, //暮光带:大陆坡,残光尽头
        Midnight, //午夜带:主沟壑与溶洞群
        Abyssal,  //深渊带:深渊平原与巨型竖井
        Hadal     //超深渊带:海沟V形底(含封底基岩)
    }

    //深渊子世界生成/氛围共享契约(三路并行的锚,brief见父会话项目文件夹)
    //本文件由父会话统一维护:各路只读常量、只在运行期给SpawnTile赋值,
    //不得编辑本文件;数值调整诉求写进各自报告统一议价
    //分带比例为暂定值,待B路地形预览图定稿后统一调整
    internal static class HadalworldMetrics
    {
        internal const int Width = 2200;
        //5100=150的整倍数:原版WorldSections按150行分段向上取整,高度不整除时
        //末段会读Main.Map越界(DrawToMap_Section崩溃),宽同理须被200整除(2200✓)
        internal const int Height = 5100;

        //天空缓冲带[0,SkyRows),海面以上留白
        internal const int SkyRows = 60;
        //海面行:此行以下为水世界主体,生成期直写静水
        internal const int SeaLevelRow = 100;

        //五大分带下界行(半开区间[上界,下界)),上界=上一带下界
        internal const int SunlitBottom = 500;
        internal const int TwilightBottom = 1300;
        internal const int MidnightBottom = 2700;
        internal const int AbyssalBottom = 4100;

        //原版地狱判定线UnderworldLayer=Height-200=4900(地狱音乐/背景/深度计标签),
        //镜像DungeonworldMetrics的"层带避开"裁决:最深可玩点压在4780行以上,
        //以下保持实心封底,避免超深渊带误触地狱判定(余量120行)
        internal const int DeepestPlayableRow = 4780;
        internal const int BedrockTopRow = DeepestPlayableRow;

        //worldSurface放日光带中部让浅海吃到天光,rockLayer放暮光带顶附近
        //(背景切换/地下判定,OnLoad时写入;数值可议,见DungeonworldMetrics头注释排查法)
        internal const int WorldSurfaceRow = 300;
        internal const int RockLayerRow = 520;

        //出生海沟气穴房间的物块坐标:B路生成期开凿后写入本槽位与Main.spawnTileX/Y,
        //A路只消费;默认值对应管线占位生成的海面石台
        internal static Point SpawnTile = new(Width / 2, 96);

        /// <summary>世界坐标y(像素)→归一化深度[0,1],海面=0,世界底=1,供氛围做连续渐变</summary>
        internal static float DepthFraction(float worldPosY) {
            float row = worldPosY / 16f;
            float frac = (row - SeaLevelRow) / (Height - SeaLevelRow);
            return frac < 0f ? 0f : frac > 1f ? 1f : frac;
        }

        /// <summary>行→分带,封底基岩归超深渊带</summary>
        internal static HadalZone GetZone(int row) {
            if (row < SeaLevelRow) {
                return HadalZone.Sky;
            }
            if (row < SunlitBottom) {
                return HadalZone.Sunlit;
            }
            if (row < TwilightBottom) {
                return HadalZone.Twilight;
            }
            if (row < MidnightBottom) {
                return HadalZone.Midnight;
            }
            if (row < AbyssalBottom) {
                return HadalZone.Abyssal;
            }
            return HadalZone.Hadal;
        }
    }
}
