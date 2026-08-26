using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>暴雪法杖重铸：命中积「风雪」，满层右键在光标区降下「白灾」灾变</summary>
    internal class GsBlizzardStaff : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.BlizzardStaff;

        protected override string GsDescFallback =>
            "Reforged: hits build Snowsquall; at full charge, right click to call the Whiteout over your cursor\n" +
            "A giant blizzard hammers the area and leaves rime spikes on the ground";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 50;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsWhiteoutDirector>();

        protected override Color AccentColor => new(150, 210, 255);

        protected override SoundStyle TriggerSound => SoundID.Item30;
    }
}
