using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Situational
{
    //雪地生物群落情境对话，3个普通变体循环
    internal class ShepelSnowBiomeDialogue : ShepelSituationalDialogueBase
    {
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "低温区域。理论上低温对电子系统有益，但我并没有感受到性能提升。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "大概因为主人产生的战斗数据太多，省下的算力被瞬间占满了。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "冰晶的光折射数据非常漂亮，这不是评估报告，只是个人看法。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "若不是还有敌人要对付，这里其实挺适合待着的。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "雪地的环境噪音是所有地表区域里最低的。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "主人在这里的时候，我的传感器误报率也低了不少。不知道是不是因为这个原因。");
        }

        protected override bool CheckConditions(Player player, ADVSave save)
            => player.ZoneSnow;

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            int v = data.SnowVariantSeed % 3;
            data.SnowVariantSeed++;
            switch (v) {
                case 0: BuildV0(); break;
                case 1: BuildV1(); break;
                default: BuildV2(); break;
            }
        }

        private void BuildV0() {
            Add(RoleName.Value, V0_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V0_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }

        private void BuildV1() {
            Add(RoleName.Value, V1_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V1_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Happy),
                onComplete: Complete);
        }

        private void BuildV2() {
            Add(RoleName.Value, V2_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V2_Line2.Value, onComplete: Complete);
        }
    }
}
