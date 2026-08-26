using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>星云奥秘重铸：命中积「奥秘」，满层右键以自身为心旋开「星云漩臂」灾变</summary>
    internal class GsNebulaArcanum : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.NebulaArcanum;

        protected override string GsDescFallback =>
            "Reforged: hits build Arcanum; at full charge, right click to spin up the Nebula Spiral\n" +
            "Three nebula arms orbit you, grinding foes and dragging the lesser ones inward";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 55;

        protected override float PassiveDamageBonus => 0.05f;

        protected override int DirectorType => ModContent.ProjectileType<GsNebulaSpiralDirector>();

        protected override Color AccentColor => new(160, 90, 240);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item84;
    }
}
