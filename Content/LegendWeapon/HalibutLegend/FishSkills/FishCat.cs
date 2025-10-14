using CalamityOverhaul.Common;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishCat : FishSkill
    {
        public static SoundStyle Sound => CWRSound.Hajm;//要使用到的弹跳碰撞音效
        public override int UnlockFishID => ItemID.Catfish;
        public override int DefaultCooldown => 90;
    }
}
