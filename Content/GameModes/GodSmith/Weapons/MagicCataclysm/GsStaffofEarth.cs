using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>大地法杖重铸：命中积「地怒」，满层右键在光标处地面掀起「造山」灾变</summary>
    internal class GsStaffofEarth : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.StaffofEarth;

        protected override string GsDescFallback =>
            "Reforged: hits build Earthwrath; at full charge, right click to raise the Orogeny at your cursor\n" +
            "Rock pillars erupt in waves amid flying rubble, leaving a magma vein bed behind";

        public override int ChargePerHit => 5;

        public override int CataclysmManaCost => 55;

        protected override float PassiveDamageBonus => 0.10f;

        protected override int DirectorType => ModContent.ProjectileType<GsOrogenyDirector>();

        protected override Color AccentColor => new(255, 140, 52);

        protected override SoundStyle TriggerSound => SoundID.Item14;
    }
}
