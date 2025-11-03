using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Gifts
{
    internal class HellGift : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(HellGift);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        protected override void Build() {
            
        }
        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (!NPC.downedMoonlord) {
                return;//月球领主后才触发
            }
            if (!halibutPlayer.Player.ZoneUnderworldHeight) {
                return;//必须在地狱区域
            }
            if (save.HellGift) {
                return;//已经获得过奖励
            }
            if (!halibutPlayer.Player.TryGetModPlayer<HalibutSave>(out var halibutSave)) {
                return;//没有获取到HalibutSave
            }
            int fishVoodooID = FishSkill.GetT<FishVoodoo>().ID;
            if (halibutSave.unlockSkills.Any(fish => fish.ID == fishVoodooID)) {
                return;//已经解锁过替死娃娃技能
            }
            if (StartScenario()) {
                save.HellGift = true;//标记已经获得奖励
            }
        }
    }
}
