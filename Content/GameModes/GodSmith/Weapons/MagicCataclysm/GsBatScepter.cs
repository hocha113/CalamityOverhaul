using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>蝙蝠权杖重铸：命中积「夜幕」，满层右键以自身为锚引爆「万蝠临渊」灾变</summary>
    internal class GsBatScepter : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.BatScepter;

        protected override string GsDescFallback =>
            "Reforged: hits build Nightfall; at full charge, right click to call the bat deluge\n" +
            "A phantom moon rises, waves of bats dive at your foes, then circle you as a guard ring";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsBatSwarmDirector>();

        protected override Color AccentColor => new(150, 110, 200);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item32;
    }
}
