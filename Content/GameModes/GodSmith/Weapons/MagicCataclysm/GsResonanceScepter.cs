using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>共鸣权杖重铸：命中积「共振」，满层右键在光标区展开「谐振崩解」灾变</summary>
    internal class GsResonanceScepter : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.PrincessWeapon;

        protected override string GsDescFallback =>
            "Reforged: hits build Resonance; at full charge, right click to collapse the harmonics at your cursor\n" +
            "Five standing waves cross the area and their nodes strike whatever lingers";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsResonanceCollapseDirector>();

        protected override Color AccentColor => new(255, 214, 130);

        protected override SoundStyle TriggerSound => SoundID.Item25;
    }
}
