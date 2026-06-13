using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Situational
{
    //海洋区域情境对话，降雨时额外解锁第4个变体
    internal class ShepelOceanDialogue : ShepelSituationalDialogueBase
    {
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VRain_Line1 { get; private set; }
        public static LocalizedText VRain_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "海洋区域。水下的声学环境与地表截然不同。我的某些模块挺喜欢这个频率的。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "如果有机会，我想完整扫描一次海底地形。不着急，以后再说。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "海水的温度分层很明显，表层和下方的深渊相差几十度。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "深渊方向的信号比上次又弱了一些，不知道是什么在干扰。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "这片海洋其实挺大的，主人有没有想过往最深处探索一下。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "我不是在催，只是提一嘴。");
            VRain_Line1 = this.GetLocalization(nameof(VRain_Line1),
                () => "下雨加海洋，海面现在相当嘈杂。");
            VRain_Line2 = this.GetLocalization(nameof(VRain_Line2),
                () => "往下潜进去反而安静了，挺有意思的对比。");
        }

        protected override bool CheckConditions(Player player, ADVSave save)
            => player.ZoneBeach;

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            //降雨时解锁第4个变体
            int total = Main.raining ? 4 : 3;
            int v = data.OceanVariantSeed % total;
            data.OceanVariantSeed++;
            switch (v) {
                case 0: BuildV0(); break;
                case 1: BuildV1(); break;
                case 2: BuildV2(); break;
                default: BuildRain(); break;
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
            Add(RoleName.Value, V2_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }

        private void BuildRain() {
            Add(RoleName.Value, VRain_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, VRain_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }
    }
}
