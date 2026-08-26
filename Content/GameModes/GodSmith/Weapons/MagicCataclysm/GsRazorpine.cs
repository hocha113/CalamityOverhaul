using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>剃刀松重铸：命中积「松脂」，满层右键以自身为心掀起「针叶风暴」灾变</summary>
    internal class GsRazorpine : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.Razorpine;

        protected override string GsDescFallback =>
            "Reforged: hits build Resin; at full charge, right click to raise the Needle Storm\n" +
            "A ring of needles tightens around you, then settles into a needle mat underfoot";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsPineStormDirector>();

        protected override Color AccentColor => new(122, 205, 118);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item66;
    }
}
