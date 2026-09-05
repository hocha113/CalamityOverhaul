using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 战术霰弹枪重铸：[战术扇面] 原版 6 粒装药原样，每泵照常耗 1 发，族级重挫后坐
    /// </summary>
    internal class GsTacticalShotgun : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.TacticalShotgun;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: paints the spread cone so you know exactly what the six pellets cover";
        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeFan", EnName = "Tactical Fan",
            },
        ];

        //霰弹后坐：重挫大抬
        protected override float RecoilShift => 5.5f;
        protected override float RecoilKick => 0.07f;
    }
}
