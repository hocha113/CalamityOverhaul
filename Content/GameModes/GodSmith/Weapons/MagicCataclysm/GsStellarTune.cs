using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>星籁重铸：命中积「乐章」，满层右键开演「星海终章」灾变</summary>
    internal class GsStellarTune : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.SparkleGuitar;

        protected override string GsDescFallback =>
            "Reforged: hits build Melody; at full charge, right click to open the Stellar Finale\n" +
            "Star chords play themselves at your foes on the beat while you dance a little faster";

        public override int ChargePerHit => 2;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsStellarFinaleDirector>();

        protected override Color AccentColor => new(255, 160, 220);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item26;
    }
}
