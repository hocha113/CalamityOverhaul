using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HalibutLegend
{
    internal class HalibutText : ModType, ILocalizedModType
    {
        public string LocalizationCategory => "Legend";
        protected override void Register() { }
        #region 字段内容
        public LocalizedText Greeting0 { get; private set; }
        public LocalizedText Greeting1 { get; private set; }
        public LocalizedText Greeting2 { get; private set; }
        public LocalizedText Greeting3 { get; private set; }
        public LocalizedText Greeting4 { get; private set; }
        public LocalizedText Greeting5 { get; private set; }
        public LocalizedText Greeting6 { get; private set; }
        public LocalizedText Greeting7 { get; private set; }
        public LocalizedText Greeting8 { get; private set; }
        public LocalizedText Greeting9 { get; private set; }
        public LocalizedText Greeting10 { get; private set; }
        public LocalizedText Greeting11 { get; private set; }
        public LocalizedText Greeting12 { get; private set; }
        public LocalizedText Greeting13 { get; private set; }
        #endregion
        #region Utils
        public static string GetTextKey(string key) => $"Mods.CalamityOverhaul.Legend.HalibutText.{key}";
        public static string GetTextValue(string key) => Language.GetTextValue($"Mods.CalamityOverhaul.Legend.HalibutText.{key}");
        public static LocalizedText GetText(string key) => Language.GetText($"Mods.CalamityOverhaul.Legend.HalibutText.{key}");
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
