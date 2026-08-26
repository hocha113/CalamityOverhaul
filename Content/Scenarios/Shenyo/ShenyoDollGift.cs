using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Scenarios.OniRainWorlds;
using CalamityOverhaul.Content.Scenarios.Shenyo.Dolls;
using InnoVault.Narrative.Composition;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 邪恶地形初访赠礼:认识沈幽之后第一次踏进腐化/猩红,她现身送出替死娃娃
    /// (<see cref="ScapegoatDoll"/>)。开演即写完成位,与礼物线同规,防中途退出重复领取
    /// </summary>
    internal sealed class ShenyoDollGift : StoryScenario, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.Shenyo";

        public static LocalizedText L0 { get; private set; }
        public static LocalizedText L1 { get; private set; }
        public static LocalizedText L2 { get; private set; }
        public static LocalizedText L3 { get; private set; }

        public override StyleId DefaultStyle => NarrativeIds.Kikasa;

        public override void SetStaticDefaults() {
            L0 = this.GetLocalization(nameof(L0), () => "我想起有个东西值得给你");
            L1 = this.GetLocalization(nameof(L1), () => "这个娃娃揣好后可以救你一命");
            L2 = this.GetLocalization(nameof(L2), () => "缝制这个东西的人是个傻姑娘，给她暗恋的人做了一大堆，但那个人根本不可能用得上");
            L3 = this.GetLocalization(nameof(L3), () => "我当年用鬼钱和她换了不少，拿来当礼物送小辈");
        }

        protected override void Build(NarrativeComposer n) {
            n.Say(NarrativeIds.Shenyo, L0.Value, Voice[1],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Pensive))
             .SayReward(NarrativeIds.Shenyo, L1.Value, ModContent.ItemType<ScapegoatDoll>(), title: string.Empty,
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Smile))
             .Say(NarrativeIds.Shenyo, L2.Value, Voice[3],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Wry))
             .Say(NarrativeIds.Shenyo, L3.Value, Voice[4],
                    onEnter: ShenyoNarrativePortrait.FaceEnter(ShenyoFullBodyPortrait.Face.Lidded));
        }

        protected override NarrativePolicy ConfigurePolicy() => new() {
            IsCompleted = _ => ShenyoStorySync.EvilBiomeDollGift,
            CanTrigger = (_, player) => CanTriggerGift(player),
            //开演即写:中途退出算已领过,不重播不重发
            OnTriggered = _ => ShenyoStorySync.MarkEvilBiomeDollGift(),
        };

        private static bool CanTriggerGift(Player player) {
            if (Main.dedServ || player == null || !player.Alives()
                || player.whoAmI != Main.myPlayer) {
                return false;
            }
            if (!ShenyoStorySync.PostFirstMetIsComplete) {
                return false;
            }
            //身在鬼雨世界或进出过渡期间不插话
            if (OniRainWorldState.LocalIn || OniRainWorldTransition.Active
                || OniRainDescentTransition.Active || OniRainExitTransition.Active) {
                return false;
            }
            return player.ZoneCorrupt || player.ZoneCrimson;
        }

        protected override void OnStarted() => ShenyoNarrativePortrait.Show();

        protected override void OnCompleted() => ShenyoNarrativePortrait.Hide();
    }
}
