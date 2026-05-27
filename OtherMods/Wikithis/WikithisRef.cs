using System.Collections.Generic;
using System.Text;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Wikithis
{
    internal class WikithisRef : ICWRLoader
    {
        private const string WikiBaseUrl = "https://calamity-overhaul.cc";

        public static bool Has => ModLoader.HasMod("Wikithis");

        void ICWRLoader.SetupData() {
            if (Main.dedServ || !Has) {
                return;
            }

            if (!ModLoader.TryGetMod("Wikithis", out Mod wikithis)) {
                return;
            }

            var englishIds = new List<int>();
            var englishUrls = new List<string>();
            var chineseIds = new List<int>();
            var chineseUrls = new List<string>();

            foreach (ModItem modItem in ModContent.GetContent<ModItem>()) {
                if (modItem.Mod != CWRMod.Instance || modItem.GetType().IsAbstract) {
                    continue;
                }

                string pathSegment = modItem is SHPCModuleItem
                    ? $"legend/shpc/modules/{ToSHPCModuleSlug(modItem.GetType().Name)}"
                    : $"items/{modItem.Name.ToLowerInvariant()}";

                englishIds.Add(modItem.Type);
                englishUrls.Add($"{WikiBaseUrl}/en/{pathSegment}/");
                chineseIds.Add(modItem.Type);
                chineseUrls.Add($"{WikiBaseUrl}/cn/{pathSegment}/");
            }

            if (englishIds.Count == 0) {
                return;
            }

            wikithis.Call("ReplaceItem", englishIds, englishUrls);
            wikithis.Call("ReplaceItem", chineseIds, chineseUrls, GameCulture.CultureName.Chinese);

            CWRMod.Instance.Logger.Info($"WikithisRef registered {englishIds.Count} Calamity Overhaul item wiki links.");
        }

        private static string ToSHPCModuleSlug(string className) {
            if (className.EndsWith("Module")) {
                className = className[..^"Module".Length];
            }

            var slug = new StringBuilder(className.Length + 8);
            for (int i = 0; i < className.Length; i++) {
                char c = className[i];
                if (char.IsUpper(c)) {
                    if (i > 0) {
                        slug.Append('-');
                    }
                    slug.Append(char.ToLowerInvariant(c));
                }
                else {
                    slug.Append(c);
                }
            }
            return slug.ToString();
        }
    }
}
