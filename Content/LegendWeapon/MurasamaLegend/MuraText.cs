using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend
{
    internal class MuraText : ModType, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";
        protected override void Register() { }
        #region 字段内容
        public LocalizedText Subtest_Text0 { get; private set; }
        public LocalizedText Subtest_Text1 { get; private set; }
        public LocalizedText Subtest_Text2 { get; private set; }
        public LocalizedText Subtest_Text3 { get; private set; }
        public LocalizedText Subtest_Text4 { get; private set; }
        public LocalizedText Subtest_Text5 { get; private set; }
        public LocalizedText Subtest_Text6 { get; private set; }
        public LocalizedText Subtest_Text7 { get; private set; }
        public LocalizedText Subtest_Text8 { get; private set; }
        public LocalizedText Subtest_Text9 { get; private set; }
        public LocalizedText Subtest_Text10 { get; private set; }
        public LocalizedText World_Text0 { get; private set; }
        #endregion
        #region Utils
        public static string GetTextKey(string key) => $"Mods.CalamityOverhaul.Legend.MuraText.{key}";
        public static string GetTextValue(string key) => Language.GetTextValue($"Mods.CalamityOverhaul.Legend.MuraText.{key}");
        public static LocalizedText GetText(string key) => Language.GetText($"Mods.CalamityOverhaul.Legend.MuraText.{key}");
        #endregion
        public override void Load() {
            //使用反射进行属性的自动加载
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties) {
                if (property.PropertyType == typeof(LocalizedText)) {
                    property.SetValue(this, this.GetLocalization(property.Name));
                }
            }
        }
    }
}
