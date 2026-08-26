using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using System;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.Panorama
{
    /// <summary>
    /// 湖心景布局：全屏血湖夜景剖面的分区几何。色板一律走
    /// <see cref="KikasaHudTheme"/> 的双形态访问器，不另立一套。
    /// 分区即空间位置，湖上住两鬼、水线摆三席、册在浅水、藏在湖底。
    /// 坐标全部按 UI 空间实时计算，禁止直读 Main.screenWidth/Height
    /// </summary>
    internal static class KikasaPanoramaTheme
    {
        //====== 纵向分层（uv.y，与 KikasaPanorama.fx 的常量必须一致） ======

        /// <summary>满水位水线 uv.y</summary>
        public const float WaterFullUv = 0.40f;

        /// <summary>空湖水线 uv.y（水缩到屏底，湖床全露）</summary>
        public const float WaterEmptyUv = 0.88f;

        /// <summary>当前水面 uv：涨水进度映射到画内水位</summary>
        public static float WaterUv(float riseProgress)
            => MathHelper.Lerp(WaterEmptyUv, WaterFullUv, MathHelper.Clamp(riseProgress, 0f, 1f));

        //====== UI 空间访问器（转发 KikasaHudTheme，调用方少一次 using） ======

        public static float UIScreenW => KikasaHudTheme.UIScreenW;
        public static float UIScreenH => KikasaHudTheme.UIScreenH;
        public static Vector2 UIMouse => KikasaHudTheme.UIMouse;

        //====== 上带·两鬼区 ======

        /// <summary>题头中心</summary>
        public static Vector2 TitlePos => new(UIScreenW * 0.5f, 40f);

        /// <summary>恶犬（鬼梦之鬼）站点：左岸，脚跟踩着满水位的岸沿</summary>
        public static Vector2 HoundPos => new(
            MathF.Max(150f, UIScreenW * 0.155f),
            UIScreenH * WaterFullUv - HoundHeight * 0.52f);

        /// <summary>恶犬身高（像素）</summary>
        public static float HoundHeight => MathF.Min(120f, UIScreenH * 0.15f);

        /// <summary>恶犬热区（狼身外扩一圈）</summary>
        public static Rectangle HoundHit {
            get {
                Vector2 c = HoundPos;
                float h = HoundHeight;
                return new Rectangle((int)(c.X - h * 0.95f), (int)(c.Y - h * 0.62f),
                    (int)(h * 1.9f), (int)(h * 1.24f));
            }
        }

        /// <summary>金焰（鬼火之鬼）浮点：右侧水上</summary>
        public static Vector2 WispPos => new(
            MathF.Min(UIScreenW - 150f, UIScreenW * 0.845f),
            UIScreenH * WaterFullUv - 64f);

        /// <summary>金焰热区</summary>
        public static Rectangle WispHit {
            get {
                Vector2 c = WispPos;
                return new Rectangle((int)(c.X - 58f), (int)(c.Y - 66f), 116, 132);
            }
        }

        //====== 天带·祈雨绳 ======

        /// <summary>祈雨绳符位数（唯一来源 <see cref="KikasaTalismanStore.SlotCount"/>）</summary>
        public static int TalisSlotCount => KikasaTalismanStore.SlotCount;

        /// <summary>绳锚高度：题头之下、两鬼之上的天带中段</summary>
        private static float TalisRopeY => MathHelper.Clamp(UIScreenH * 0.105f, 76f, 112f);

        /// <summary>绳左锚点</summary>
        public static Vector2 TalisRopeLeft => new(UIScreenW * 0.31f, TalisRopeY);

        /// <summary>绳右锚点</summary>
        public static Vector2 TalisRopeRight => new(UIScreenW * 0.69f, TalisRopeY);

        /// <summary>绳中段下垂深度</summary>
        public static float TalisRopeSag => MathHelper.Clamp(UIScreenH * 0.045f, 22f, 44f);

        /// <summary>符纸吊线长度（绳到符顶）</summary>
        public const float TalisCordLen = 10f;

        /// <summary>符纸尺寸，随屏高微缩</summary>
        public static Vector2 TalisStripSize => new(26f, MathHelper.Clamp(UIScreenH * 0.062f, 44f, 60f));

        /// <summary>第 i 个符位在绳上的参数 u（居中散开）</summary>
        public static float TalisSlotU(int index) {
            int count = TalisSlotCount;
            return count <= 1 ? 0.5f : 0.5f + (index - (count - 1) * 0.5f) * 0.24f;
        }

        /// <summary>绳上参数 u 处的静态垂弧点（风摆动画在绘制侧叠加）</summary>
        public static Vector2 TalisRopePoint(float u) {
            Vector2 l = TalisRopeLeft;
            Vector2 r = TalisRopeRight;
            return Vector2.Lerp(l, r, u) + new Vector2(0f, MathF.Sin(u * MathHelper.Pi) * TalisRopeSag);
        }

        /// <summary>符纸身心（吊线下方符身中点），涟漪/定妆锚点</summary>
        public static Vector2 TalisStripCenter(int index)
            => TalisRopePoint(TalisSlotU(index)) + new Vector2(0f, TalisCordLen + TalisStripSize.Y * 0.5f);

        /// <summary>符位命中矩形：吊线+符身整段，两侧各放 8px</summary>
        public static Rectangle TalisSlotHit(int index) {
            Vector2 p = TalisRopePoint(TalisSlotU(index));
            Vector2 size = TalisStripSize;
            return new Rectangle((int)(p.X - size.X * 0.5f - 8f), (int)p.Y,
                (int)(size.X + 16f), (int)(TalisCordLen + size.Y + 10f));
        }

        //====== 祈雨绳·候选扇（多行网格：绘制/命中/引线全部同源） ======

        /// <summary>候选扇迷你符尺寸</summary>
        public static readonly Vector2 TalisFanSize = new(20f, 40f);

        /// <summary>候选扇横向间距</summary>
        public const float TalisFanGap = 52f;

        /// <summary>候选扇行距：符身 40px + 行间留白，末行摘下位的小注也不压行</summary>
        public const float TalisFanRowGap = 62f;

        /// <summary>
        /// 候选扇固定列数：换符/选符两入口共用同一布局。
        /// 旧版按项数均摊行宽，摘下位那 +1 项会把 9×3 重排成 7×4，肌肉记忆断裂（反馈八·#24）；
        /// 现在列数恒定、末行不满不重排，同一张符在两入口位置一致。
        /// 容量按全符 27+摘下位=28 取 7 列（9 列装不下 28）
        /// </summary>
        public const int TalisFanCols = 7;

        /// <summary>本屏实际列数：极窄屏按可用宽收列（两侧 40px 钳边），正常屏恒为 <see cref="TalisFanCols"/></summary>
        private static int TalisFanColsFit()
            => Math.Clamp((int)((UIScreenW - 80f) / TalisFanGap) + 1, 3, TalisFanCols);

        /// <summary>候选扇行数：项数超出一行容量后增行</summary>
        public static int TalisFanRowCount(int itemCount) {
            int cols = TalisFanColsFit();
            return itemCount <= 0 ? 0 : (itemCount + cols - 1) / cols;
        }

        /// <summary>
        /// 网格原点（首行中心）：水平锚定被点符位并按行宽钳回屏内；
        /// 垂直先落符身下方，行数多时向上钳、极矮屏保底不顶出题头
        /// </summary>
        private static Vector2 TalisFanOrigin(int slotIndex, int itemCount) {
            Vector2 strip = TalisStripCenter(slotIndex);
            float half = (TalisFanColsFit() - 1) * 0.5f * TalisFanGap;
            float cx = MathHelper.Clamp(strip.X, 40f + half, UIScreenW - 40f - half);
            float cy = strip.Y + TalisStripSize.Y * 0.5f + 54f;
            float rowSpan = (TalisFanRowCount(itemCount) - 1) * TalisFanRowGap;
            //底钳：末行连同摘下位小注、换行后的规则衬底块（最高两行）都收在屏底之上；
            //顶钳后手兜底，保交互优先
            cy = MathF.Min(cy, UIScreenH - 150f - rowSpan);
            cy = MathF.Max(cy, 56f);
            return new Vector2(cx, cy);
        }

        /// <summary>候选扇第 item 张的中心：定列铺开，末行不满不重排，同一张符两入口位置一致</summary>
        public static Vector2 TalisFanPos(int slotIndex, int itemIndex, int itemCount) {
            int cols = TalisFanColsFit();
            Vector2 origin = TalisFanOrigin(slotIndex, itemCount);
            return new Vector2(
                origin.X + (itemIndex % cols - (cols - 1) * 0.5f) * TalisFanGap,
                origin.Y + itemIndex / cols * TalisFanRowGap);
        }

        /// <summary>候选扇命中矩形（几何与 <see cref="TalisFanPos"/> 严格同源）</summary>
        public static Rectangle TalisFanHit(int slotIndex, int itemIndex, int itemCount) {
            Vector2 c = TalisFanPos(slotIndex, itemIndex, itemCount);
            return new Rectangle((int)(c.X - TalisFanSize.X * 0.5f - 6f),
                (int)(c.Y - TalisFanSize.Y * 0.5f - 6f),
                (int)(TalisFanSize.X + 12f), (int)(TalisFanSize.Y + 12f));
        }

        /// <summary>符位到扇的引线终点：网格首行上缘中点，留 8px 气口</summary>
        public static Vector2 TalisFanTopAnchor(int slotIndex, int itemCount) {
            Vector2 origin = TalisFanOrigin(slotIndex, itemCount);
            return new Vector2(origin.X, origin.Y - TalisFanSize.Y * 0.5f - 8f);
        }

        //====== 中带·编成区 ======

        /// <summary>三席影位中心：水线上等距横列</summary>
        public static Vector2 SeatPos(int index) {
            float cx = UIScreenW * 0.5f;
            float spacing = MathF.Min(190f, UIScreenW * 0.13f);
            return new Vector2(cx + (index - 1) * spacing, UIScreenH * WaterFullUv + 34f);
        }

        /// <summary>席位沉影的适配尺寸</summary>
        public const float SeatFit = 58f;

        /// <summary>席位命中半径</summary>
        public const float SeatHitR = 40f;

        /// <summary>收集册条带的纵向中心</summary>
        public static float RosterY => UIScreenH * 0.565f;

        /// <summary>册条目适配尺寸</summary>
        public const float RosterFit = 38f;

        /// <summary>册条目命中半径</summary>
        public const float RosterHitR = 24f;

        /// <summary>
        /// 册条目横向中心：条目数多时间距自适应收窄（两侧留边），
        /// 绘制与命中共用这一份布局
        /// </summary>
        public static float RosterX(int index, int count) {
            float margin = MathF.Max(120f, UIScreenW * 0.10f);
            float avail = UIScreenW - margin * 2f;
            float spacing = count <= 1 ? 0f : MathF.Min(54f, avail / (count - 1));
            float total = spacing * (count - 1);
            return UIScreenW * 0.5f - total * 0.5f + index * spacing;
        }

        //====== 下带·湖藏区 ======

        /// <summary>湖藏栅格顶缘 y</summary>
        public static float VaultTop => UIScreenH * 0.655f;

        /// <summary>每行格数（容量 40 = 8×5，正好铺满不用滚动）</summary>
        public const int VaultCols = 8;

        /// <summary>湖藏格横向间距</summary>
        public static float VaultSpacingX => MathF.Min(60f, UIScreenW * 0.042f);

        /// <summary>湖藏格纵向间距：按屏高压缩，底部给页脚留位</summary>
        public static float VaultSpacingY {
            get {
                float span = UIScreenH - 56f - VaultTop;
                return MathF.Min(56f, span / 5f);
            }
        }

        /// <summary>湖藏格适配尺寸</summary>
        public const float VaultFit = 34f;

        /// <summary>第 i 件沉物的中心</summary>
        public static Vector2 VaultCell(int index) {
            int row = index / VaultCols;
            int col = index % VaultCols;
            float startX = UIScreenW * 0.5f - (VaultCols - 1) * VaultSpacingX * 0.5f;
            return new Vector2(startX + col * VaultSpacingX, VaultTop + 18f + row * VaultSpacingY);
        }

        //====== 页脚 ======

        /// <summary>批注行（盘内回执）基线 y</summary>
        public static float NoteY => UIScreenH - 66f;

        /// <summary>页脚提示行基线 y</summary>
        public static float FooterY => UIScreenH - 36f;

        /// <summary>异相位呼吸波</summary>
        public static float Breath(float time, float seed, float speed = 2f)
            => MathF.Sin(time * speed + seed * 17.39f) * 0.5f + 0.5f;
    }
}
