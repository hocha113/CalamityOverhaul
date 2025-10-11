using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishVoodoo : FishSkill
    {
        public override int UnlockFishID => ItemID.GuideVoodooFish;
    }

    internal class FishVoodooPlayer : ModPlayer
    {
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            base.ModifyHurt(ref modifiers);
        }
    }
}
