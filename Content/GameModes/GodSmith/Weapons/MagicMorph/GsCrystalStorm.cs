using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 水晶风暴重铸：保留原版高速连射，只做数值行加成
    /// </summary>
    internal class GsCrystalStorm : GsMorphScheme
    {
        public override int TargetItemID => ItemID.CrystalStorm;

        protected override string GsDescFallback =>
            "Reforged: keeps the classic crystal storm";
        protected override float BaseDamageMult => 1.05f;
    }
}
