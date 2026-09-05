using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// S.D.M.G. 重铸：[海豚狂涛] 原版极速原样，族级高频轻挫后坐
    /// </summary>
    internal class GsSDMG : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.SDMG;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: keeps the legendary fire rate";
        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeDolphinTide", EnName = "Dolphin Tide",
            },
        ];

        //终局机枪后坐：高频轻挫
        protected override float RecoilShift => 2.8f;
        protected override float RecoilKick => 0.04f;
    }
}
