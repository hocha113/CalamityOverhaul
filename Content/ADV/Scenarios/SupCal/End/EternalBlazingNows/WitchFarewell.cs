using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows.EternalBlazingNow;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 女巫告别场景
    /// </summary>
    internal class WitchFarewell : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public static bool Spwan;
        public override string Key => nameof(WitchFarewell);
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        //女巫告别独白
        public static LocalizedText FarewellLine1 { get; private set; }
        public static LocalizedText FarewellLine2 { get; private set; }
        public static LocalizedText FarewellLine3 { get; private set; }
        public static LocalizedText FarewellLine4 { get; private set; }
        public static LocalizedText FarewellLine5 { get; private set; }
        public static LocalizedText FarewellLine6 { get; private set; }
        public static LocalizedText FarewellLine7 { get; private set; }
        public static LocalizedText FarewellLine8 { get; private set; }
        public static LocalizedText FarewellLine9 { get; private set; }
        public static LocalizedText FarewellLine10 { get; private set; }
        public static LocalizedText FarewellLine11 { get; private set; }
        private static bool HasHalibut;
        public string LocalizationCategory => "ADV.EternalBlazingNow";

        void IWorldInfo.OnWorldLoad() {
            Spwan = false;
        }

        public override void SetStaticDefaults() {
            FarewellLine1 = this.GetLocalization(nameof(FarewellLine1), () => "这漫长的一生里，我见过无数次黎明与终焉");
            FarewellLine2 = this.GetLocalization(nameof(FarewellLine2), () => "火焰吞噬时代，也照亮新的开始。我原以为，这次也不会例外");
            FarewellLine3 = this.GetLocalization(nameof(FarewellLine3), () => "没想到，在最后的路上，会有人同行");
            FarewellLine4 = this.GetLocalization(nameof(FarewellLine4), () => "对我来说，这样的结局……已经足够了");
            FarewellLine5 = this.GetLocalization(nameof(FarewellLine5), () => "你的存在，证明这片大地还没有真正枯竭");
            FarewellLine6 = this.GetLocalization(nameof(FarewellLine6), () => "我相信，你会走得比我更远");
            FarewellLine7 = this.GetLocalization(nameof(FarewellLine7), () => "而我，也终于可以停下来了");
            FarewellLine8 = this.GetLocalization(nameof(FarewellLine8), () => "不必回头看。前面还有更重要的事情等着你");
            FarewellLine9 = this.GetLocalization(nameof(FarewellLine9), () => "就当我在这场漫长的旅途中，终于抵达了属于自己的地方");
            FarewellLine10 = this.GetLocalization(nameof(FarewellLine10), () => "那么到这里，就足够了");
            FarewellLine11 = this.GetLocalization(nameof(FarewellLine11), () => "去吧，杂鱼");
        }
        
        protected override void OnScenarioStart() {
            //开始火圈收缩效果
            EbnEffect.StartContraction();
            if (Main.LocalPlayer.HasHalibut()) {
                RemoveHalubutFromPlayer();
                HasHalibut = true;
            }
        }

        protected override void OnScenarioComplete() {
            //场景结束后完全关闭效果
            EbnEffect.IsActive = false;
            EbnEffect.ResetEffects();
            //淡入效果，等待红屏完全消失
            EbnEffect.StartEpilogueFadeIn();
            if (HasHalibut) {
                //触发比目鱼的收尾场景
                HelenEpilogue.Spwan = true;
                HasHalibut = false;
            }
        }

        private static void RemoveHalubutFromPlayer() {
            Player player = Main.LocalPlayer;
            //从背包中移除所有比目鱼
            for (int i = 0; i < player.inventory.Length; i++) {
                if (player.inventory[i].type == HalibutOverride.ID) {
                    player.inventory[i].TurnToAir();
                }
            }
        }

        protected override void Build() {
            //女巫的最后独白
            Add(Rolename2.Value, FarewellLine1.Value, onStart: TriggerRedScreen);
            Add(Rolename2.Value, FarewellLine2.Value);
            Add(Rolename2.Value, FarewellLine3.Value);
            Add(Rolename2.Value, FarewellLine4.Value);
            Add(Rolename2.Value, FarewellLine5.Value);
            Add(Rolename2.Value, FarewellLine6.Value);
            Add(Rolename2.Value, FarewellLine7.Value);
            Add(Rolename2.Value, FarewellLine8.Value);
            Add(Rolename2.Value, FarewellLine9.Value);
            Add(Rolename2.Value, FarewellLine10.Value);
            Add(Rolename3.Value, FarewellLine11.Value, onStart: Achievement, FinalFade);
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (Spwan && StartScenario()) {
                Spwan = false;
            }
        }

        private void TriggerRedScreen() {
            //触发红屏效果
            EbnEffect.StartRedScreen();
        }

        private void FinalFade() {
            //最终淡出
            EbnEffect.FinalFadeOut = true;
        }

        private static void Achievement() {
            AchievementToast.ShowAchievement(
                CWRAsset.icon_small.Value,
                AchievementTitle.Value,
                AchievementTooltip.Value,
                AchievementToast.ToastStyle.Brimstone
            );
            if (Main.LocalPlayer.TryGetADVSave(out var save)) {
                save.EternalBlazingNow = true;//标记达成永恒燃烧的现在结局
            }
        }
    }
}
