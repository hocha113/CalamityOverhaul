using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>月耀重铸：命中积「月相」，满层右键在光标区升起「月蚀审判」灾变</summary>
    internal class GsLunarFlare : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.LunarFlareBook;

        protected override string GsDescFallback =>
            "Reforged: hits build Moonphase; at full charge, right click to raise the Lunar Eclipse\n" +
            "An eclipse disk pours twelve woven flare falls, every third one backed by a phantom moonbeam";

        public override int ChargePerHit => 2;

        public override int CataclysmManaCost => 60;

        protected override float PassiveDamageBonus => 0.05f;

        protected override int DirectorType => ModContent.ProjectileType<GsLunarEclipseDirector>();

        protected override Color AccentColor => new(150, 220, 235);

        protected override SoundStyle TriggerSound => SoundID.Item4;

        protected override void ModifyTriggerParams(Item item, Player player, ref Vector2 anchor, ref float ai1, ref float ai2) {
            //审判区落在光标处，蚀盘由 director 悬于其上空
            anchor.Y -= 30f;
        }
    }
}
