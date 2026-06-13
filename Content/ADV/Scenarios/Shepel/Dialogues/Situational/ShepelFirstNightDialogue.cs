using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Situational
{
    //夜晚情境对话，4个变体循环
    internal class ShepelFirstNightDialogue : ShepelSituationalDialogueBase
    {
        public override int DialoguePriority => 40;

        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText V3_Line1 { get; private set; }
        public static LocalizedText V3_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "夜幕落下了，夜间的威胁密度远超白天，请注意周围。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "我的视野不依赖光照，我看得清。主人呢，看得清吗？");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "今晚的星象数据比较完整，我正在运行例行分析。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "没什么要紧的，只是习惯了有事做。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "夜晚安静了许多，相对的。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "越安静的时候越需要警惕，主人。");
            V3_Line1 = this.GetLocalization(nameof(V3_Line1),
                () => "都这么晚了，主人有计划今晚去哪吗。");
            V3_Line2 = this.GetLocalization(nameof(V3_Line2),
                () => "只是问问，不是催。");
        }

        protected override bool CheckConditions(Player player, ADVSave save)
            => !Main.dayTime;

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            int v = data.NightVariantSeed % 4;
            data.NightVariantSeed++;
            switch (v) {
                case 0: BuildV0(); break;
                case 1: BuildV1(); break;
                case 2: BuildV2(); break;
                default: BuildV3(); break;
            }
        }

        private void BuildV0() {
            Add(RoleName.Value, V0_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V0_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Happy),
                onComplete: Complete);
        }

        private void BuildV1() {
            Add(RoleName.Value, V1_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V1_Line2.Value, onComplete: Complete);
        }

        private void BuildV2() {
            Add(RoleName.Value, V2_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V2_Line2.Value, onComplete: Complete);
        }

        private void BuildV3() {
            Add(RoleName.Value, V3_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Smirk));
            Add(RoleName.Value, V3_Line2.Value, onComplete: Complete);
        }
    }
}
