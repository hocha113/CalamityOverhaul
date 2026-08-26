using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>贝特西之怒重铸：命中积「龙焰」，满层右键在光标区召来「龙王孽焰」灾变</summary>
    internal class GsBetsysWrath : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.ApprenticeStaffT3;

        protected override string GsDescFallback =>
            "Reforged: hits build Dragonfire; at full charge, right click to invoke the Dragon's Wrath\n" +
            "Betsy's shade dives three times trailing flame curtains, leaving a cursed pyre bed";

        public override int ChargePerHit => 4;

        public override int CataclysmManaCost => 50;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsDragonWrathDirector>();

        protected override Color AccentColor => new(255, 150, 60);

        protected override SoundStyle TriggerSound => SoundID.DD2_BetsyScream;
    }
}
