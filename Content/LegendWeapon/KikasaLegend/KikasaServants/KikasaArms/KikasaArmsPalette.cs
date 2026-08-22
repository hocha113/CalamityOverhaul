using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms
{
    /// <summary>
    /// 械奴共用血色板（随观看域鬼雨异化冷化，与湖系同族）：
    /// 枪奴/刀奴本体、派生弹幕与斩痕、PRT 统一在此取色
    /// </summary>
    internal static class KikasaArmsPalette
    {
        internal static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        internal static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        internal static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        internal static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        internal static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        /// <summary>枪口热闪/刃缘亮线的暖点缀，只作次要加色层</summary>
        internal static Color MuzzleHot => KikasaDomain.CoolTint(new(255, 190, 170), new(200, 220, 222));
    }
}
