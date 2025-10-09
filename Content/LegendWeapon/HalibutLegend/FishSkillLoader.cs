using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class FishSkillLoader : ModSystem
    {
        public static T GetT<T>() where T : FishSkill => FishSkill.TypeToInstance[typeof(T)] as T;
    }
}
