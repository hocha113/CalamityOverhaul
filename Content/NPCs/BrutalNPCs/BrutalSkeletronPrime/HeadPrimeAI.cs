using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    [Autoload]
    internal partial class HeadPrimeAI : CWRNPCOverride, ICWRLoader, ILocalizedModType
    {
        public string LocalizationCategory => "BrutalNPCs";

        public static LocalizedText SkeletronPrime_Text { get; private set; }

        public override void SetStaticDefaults() {
            SkeletronPrime_Text = this.GetLocalization(nameof(SkeletronPrime_Text),
                () => "别妄图用这愚蠢的东西杀死我!去死吧有机体!");
        }
    }
}
