using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 爬藤怪法杖重铸：左键保留原版垂直咒火墙，只做数值行加成
    /// </summary>
    internal class GsClingerStaff : GsMorphScheme
    {
        public override int TargetItemID => ItemID.ClingerStaff;

        protected override string GsDescFallback =>
            "Reforged: still plants the classic cursed flame wall";
        protected override float BaseDamageMult => 1.12f;
    }
}
