using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>夜明重铸：命中积「曦光」，满层右键在光标上空展开「极光帷幕」灾变</summary>
    internal class GsNightglow : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.FairyQueenMagicItem;

        protected override string GsDescFallback =>
            "Reforged: hits build Dawnlight; at full charge, right click to unfurl an Aurora Curtain over your cursor\n" +
            "The curtain sears foes within and rains light lances below";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 50;

        protected override float PassiveDamageBonus => 0.05f;

        protected override int DirectorType => ModContent.ProjectileType<GsAuroraCurtainDirector>();

        protected override Color AccentColor => new(140, 230, 210);

        protected override SoundStyle TriggerSound => SoundID.Item84;

        protected override void ModifyTriggerParams(Item item, Player player, ref Vector2 anchor, ref float ai1, ref float ai2) {
            //帷幕挂在光标上空，光矛自帘心落向帘下
            anchor.Y -= 200f;
        }
    }
}
