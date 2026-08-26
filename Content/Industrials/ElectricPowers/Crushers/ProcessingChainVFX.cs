using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    /// <summary>加工链警示状态:None=正常/待机,Blocked=缺料或堵料,NoPower=缺电</summary>
    internal enum ProcAlert
    {
        None,
        Blocked,
        NoPower,
    }

    //=========================================================================
    // 加工链(粉碎机/回收机/自动合成台)共用表现层小件:
    // 统一警示灯语言、屏上判定、矿屑取色、颚板冲程曲线。
    // 纯客户端零网络;共置于 Crushers 文件夹沿用 ProcessingModules 先例
    //=========================================================================
    internal static class ProcessingChainVFX
    {
        /// <summary>统一警示呼吸包络,三台共享相位</summary>
        internal static float AlertBreath
            => 0.44f + 0.30f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f);

        /// <summary>缺料/堵料 = 黄呼吸</summary>
        internal static readonly Color BlockedAmber = new(255, 196, 64);
        /// <summary>缺电 = 红呼吸</summary>
        internal static readonly Color NoPowerRed = new(205, 52, 38);

        /// <summary>警示灯颜色;None 时返回给定的常规色</summary>
        internal static Color LampColor(ProcAlert alert, Color normal) => alert switch {
            ProcAlert.NoPower => NoPowerRed * AlertBreath,
            ProcAlert.Blocked => BlockedAmber * AlertBreath,
            _ => normal,
        };

        /// <summary>屏上判定(带余量),屏外不发粒子不做编舞</summary>
        internal static bool OnScreen(Vector2 worldPos, float margin = 200f) {
            Vector2 d = worldPos - Main.screenPosition;
            return d.X > -margin && d.Y > -margin
                && d.X < Main.screenWidth + margin && d.Y < Main.screenHeight + margin;
        }

        /// <summary>
        /// 颚板冲程曲线:30tick 一冲程(蓄压慢合20 → 应变驻留2 → 破碎快咬2 → 回程6)。
        /// 返回 0..1 闭合度;<paramref name="biteFrame"/> 在破碎瞬间(第24拍)为真
        /// </summary>
        internal static float JawCurve(int progress, out bool biteFrame) {
            int c = progress % 30;
            biteFrame = c == 24;
            if (c < 20) {
                float t = c / 20f;
                return 0.60f * t * t;
            }
            if (c < 22) {
                return 0.60f + (c - 20) * 0.01f;
            }
            if (c < 24) {
                return 0.62f + (c - 22) * 0.19f;
            }
            float r = (c - 24) / 6f;
            return 1f - r * (2f - r);
        }

        /// <summary>蓄压程度 0..1,喂机身微颤幅度</summary>
        internal static float JawPressure(int progress) {
            int c = progress % 30;
            return c < 22 ? c / 22f : 1f;
        }

        /// <summary>
        /// 矿种→(屑体色,矿彩亮缘)。白名单矿逐个取色,未知矿走灰岩兜底;
        /// 颜色是矿石视觉主色的手工近似,服务于岩屑 PRT 染色
        /// </summary>
        internal static (Color Body, Color Glint) OreChipColors(int itemType) {
            if (itemType == ItemID.CopperOre) { return (new Color(150, 82, 45), new Color(230, 140, 80)); }
            if (itemType == ItemID.TinOre) { return (new Color(140, 130, 100), new Color(215, 205, 160)); }
            if (itemType == ItemID.IronOre) { return (new Color(120, 95, 80), new Color(190, 160, 140)); }
            if (itemType == ItemID.LeadOre) { return (new Color(85, 90, 110), new Color(150, 155, 185)); }
            if (itemType == ItemID.SilverOre) { return (new Color(150, 155, 165), new Color(225, 230, 240)); }
            if (itemType == ItemID.TungstenOre) { return (new Color(110, 130, 105), new Color(180, 215, 160)); }
            if (itemType == ItemID.GoldOre) { return (new Color(170, 130, 50), new Color(255, 215, 90)); }
            if (itemType == ItemID.PlatinumOre) { return (new Color(145, 150, 170), new Color(220, 235, 255)); }
            if (itemType == ItemID.DemoniteOre) { return (new Color(90, 70, 140), new Color(160, 120, 255)); }
            if (itemType == ItemID.CrimtaneOre) { return (new Color(130, 40, 45), new Color(230, 80, 80)); }
            if (itemType == ItemID.Meteorite) { return (new Color(110, 60, 45), new Color(255, 120, 60)); }
            if (itemType == ItemID.Obsidian) { return (new Color(45, 38, 60), new Color(130, 110, 190)); }
            if (itemType == ItemID.Hellstone) { return (new Color(105, 50, 35), new Color(255, 140, 50)); }
            if (itemType == ItemID.CobaltOre) { return (new Color(45, 80, 150), new Color(90, 160, 255)); }
            if (itemType == ItemID.PalladiumOre) { return (new Color(150, 80, 55), new Color(255, 150, 95)); }
            if (itemType == ItemID.MythrilOre) { return (new Color(55, 120, 110), new Color(110, 230, 200)); }
            if (itemType == ItemID.OrichalcumOre) { return (new Color(150, 70, 110), new Color(255, 130, 200)); }
            if (itemType == ItemID.AdamantiteOre) { return (new Color(145, 45, 60), new Color(255, 90, 110)); }
            if (itemType == ItemID.TitaniumOre) { return (new Color(95, 100, 115), new Color(175, 190, 215)); }
            if (itemType == ItemID.ChlorophyteOre) { return (new Color(70, 120, 55), new Color(140, 235, 100)); }
            if (itemType == ItemID.LunarOre) { return (new Color(60, 130, 130), new Color(140, 255, 235)); }
            return (new Color(105, 100, 92), new Color(170, 162, 150));
        }
    }
}
