using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Everyday
{
    internal class DyeProtest : ADVScenarioBase
    {
        public override void SetStaticDefaults() {
            Localized("R1", "比目鱼");
            Localized("L1", "老实说，我并不喜欢那些鲜艳的颜色");
            Localized("L2", "洗掉好吗？放染缸里，然后用那个水桶");
        }

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait("R1", ADVAsset.Helen_solemnADV);
            AddLineFromKey("R1", "L1");
            AddLineFromKey("R1", "L2");
        }

        protected override ScenarioPolicy ConfigurePolicy() => new() {
            IsCompleted = save => save.Get<HalibutADVData>().DyeProtest,
            MarkCompleted = save => save.Get<HalibutADVData>().DyeProtest = true,
            CanTrigger = (save, player) => {
                var halibutPlayer = player.GetOverride<HalibutPlayer>();
                if (!halibutPlayer.HeldHalibut) {
                    return false;
                }
                Item item = player.GetItem();
                return item.type == HalibutOverride.ID && item.CWR().DyeItemID > ItemID.None;
            },
            BlockedBy = ScenarioBlockers.Boss | ScenarioBlockers.ActiveScenario,
        };
    }
}
