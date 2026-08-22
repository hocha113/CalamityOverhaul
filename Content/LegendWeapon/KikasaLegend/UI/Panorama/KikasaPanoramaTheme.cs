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

        /// <summary>湖力条：题头下方居中，一条读数解释两道门（入梦线/自燃线）</summary>
        public static Rectangle VigorBarRect {
            get {
                float w = MathF.Min(400f, UIScreenW * 0.28f);
                return new Rectangle((int)(UIScreenW * 0.5f - w * 0.5f), 84, (int)w, 8);
            }
        }

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
