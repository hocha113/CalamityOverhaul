using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Situational
{
    //地牢区域的情境对话，夜晚时额外解锁第4个变体
    internal class ShepelDungeonDialogue : ShepelSituationalDialogueBase
    {
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VNight_Line1 { get; private set; }
        public static LocalizedText VNight_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "地牢内部，残留能量扰乱了扫描信号，一直在处理噪声。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "总有一种被注视的感觉。也许是数据干扰，也许不是。主人多留意。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "地牢的设计者对采光需求明显没什么兴趣。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "走廊宽度、叉路密度，防守设计挺严密的。旧主人还是有想法的。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "书架……这里有书架，非常多。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "扫描了一部分，内容大多已经腐蚀。遗憾。");
            VNight_Line1 = this.GetLocalization(nameof(VNight_Line1),
                () => "地牢加上夜晚，主人选的时机真是……挺有品位的。");
            VNight_Line2 = this.GetLocalization(nameof(VNight_Line2),
                () => "没关系，我的视野不依赖光照。我会看着。");
        }

        protected override bool CheckConditions(Player player, ADVSave save)
            => player.ZoneDungeon;

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            //夜晚时解锁第4个变体
            int total = !Main.dayTime ? 4 : 3;
            int v = data.DungeonVariantSeed % total;
            data.DungeonVariantSeed++;
            switch (v) {
                case 0: BuildV0(); break;
                case 1: BuildV1(); break;
                case 2: BuildV2(); break;
                default: BuildNight(); break;
            }
        }

        private void BuildV0() {
            Add(RoleName.Value, V0_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V0_Line2.Value, onComplete: Complete);
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
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Sad),
                onComplete: Complete);
        }

        private void BuildNight() {
            Add(RoleName.Value, VNight_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Smirk));
            Add(RoleName.Value, VNight_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Serious),
                onComplete: Complete);
        }
    }
}
