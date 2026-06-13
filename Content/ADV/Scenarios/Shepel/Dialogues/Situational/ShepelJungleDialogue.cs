using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Situational
{
    //丛林生物群落情境对话，硬模式时解锁第4变体
    internal class ShepelJungleDialogue : ShepelSituationalDialogueBase
    {
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VHard_Line1 { get; private set; }
        public static LocalizedText VHard_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "丛林的生物信号密度是所有地表区域里最高的，我的扫描模块有些应付不过来。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "主人在这里打架，背景噪音对我来说真的很嘈杂。不是抱怨，只是说明情况。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "这片区域的菌丝网络走向挺特别的，某些连接模式和我的神经路由有点相似。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "纯技术性描述，不是在夸它。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "丛林湿度持续刷新记录，某几个外部传感器不太开心。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "功能正常，只是汇报一下状态。");
            VHard_Line1 = this.GetLocalization(nameof(VHard_Line1),
                () => "主人，进入困难模式之后丛林的生物信号混乱程度上升了不止一个量级。");
            VHard_Line2 = this.GetLocalization(nameof(VHard_Line2),
                () => "植物会主动攻击这件事，我依然觉得在设计上有些过激。请注意周围。");
        }

        protected override bool CheckConditions(Player player, ADVSave save)
            => player.ZoneJungle;

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            int total = Main.hardMode ? 4 : 3;
            int v = data.JungleVariantSeed % total;
            data.JungleVariantSeed++;
            switch (v) {
                case 0: BuildV0(); break;
                case 1: BuildV1(); break;
                case 2: BuildV2(); break;
                default: BuildHard(); break;
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
            Add(RoleName.Value, V1_Line2.Value, onComplete: Complete);
        }

        private void BuildV2() {
            Add(RoleName.Value, V2_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V2_Line2.Value, onComplete: Complete);
        }

        private void BuildHard() {
            Add(RoleName.Value, VHard_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Shocked));
            Add(RoleName.Value, VHard_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Serious),
                onComplete: Complete);
        }
    }
}
