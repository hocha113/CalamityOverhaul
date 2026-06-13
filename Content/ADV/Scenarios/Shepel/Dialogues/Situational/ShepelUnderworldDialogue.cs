using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel.Dialogues.Situational
{
    //地狱区域的情境对话，血月时额外解锁第4个变体
    internal class ShepelUnderworldDialogue : ShepelSituationalDialogueBase
    {
        public static LocalizedText RoleName { get; private set; }
        public static LocalizedText V0_Line1 { get; private set; }
        public static LocalizedText V0_Line2 { get; private set; }
        public static LocalizedText V1_Line1 { get; private set; }
        public static LocalizedText V1_Line2 { get; private set; }
        public static LocalizedText V2_Line1 { get; private set; }
        public static LocalizedText V2_Line2 { get; private set; }
        public static LocalizedText VBloodMoon_Line1 { get; private set; }
        public static LocalizedText VBloodMoon_Line2 { get; private set; }

        public override void SetStaticDefaults() {
            RoleName = this.GetLocalization(nameof(RoleName), () => "SHPC");
            V0_Line1 = this.GetLocalization(nameof(V0_Line1),
                () => "地底深处，热流读数超出了所有预设参数范围。这里的一切比我预想的要极端。");
            V0_Line2 = this.GetLocalization(nameof(V0_Line2),
                () => "某些外部传感器在过热警告中，不过我会撑住的。主人也注意别硬撑。");
            V1_Line1 = this.GetLocalization(nameof(V1_Line1),
                () => "岩浆作为照明方案，从能源效率角度来说极其浪费。");
            V1_Line2 = this.GetLocalization(nameof(V1_Line2),
                () => "不过地狱的居民大概不关心这个。");
            V2_Line1 = this.GetLocalization(nameof(V2_Line1),
                () => "这里的建筑痕迹比地表的地牢还要古老，在岩浆里泡了这么久也没被侵蚀。");
            V2_Line2 = this.GetLocalization(nameof(V2_Line2),
                () => "用的是什么材料？存档一下，以后有机会研究。");
            VBloodMoon_Line1 = this.GetLocalization(nameof(VBloodMoon_Line1),
                () => "血月加地狱，两套警报系统同时运行，有点忙。");
            VBloodMoon_Line2 = this.GetLocalization(nameof(VBloodMoon_Line2),
                () => "不过数据倒是很丰富。主人请多注意安全。");
        }

        protected override bool CheckConditions(Player player, ADVSave save)
            => player.ZoneUnderworldHeight;

        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(RoleName.Value, texture: null);
            DialogueBoxBase.SetPortraitStyle(RoleName.Value, silhouette: false);
            ShepelADVData data = Main.LocalPlayer.GetModPlayer<ADVSavePlayer>().ADVSave.Get<ShepelADVData>();
            //血月时解锁第4个变体，扩大轮换池
            int total = Main.bloodMoon ? 4 : 3;
            int v = data.UnderworldVariantSeed % total;
            data.UnderworldVariantSeed++;
            switch (v) {
                case 0: BuildV0(); break;
                case 1: BuildV1(); break;
                case 2: BuildV2(); break;
                default: BuildBloodMoon(); break;
            }
        }

        private void BuildV0() {
            Add(RoleName.Value, V0_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Shocked));
            Add(RoleName.Value, V0_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Serious),
                onComplete: Complete);
        }

        private void BuildV1() {
            Add(RoleName.Value, V1_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V1_Line2.Value,
                onStart: () => SetPortraitFace(ShepelFullBodyPortrait.Face.Smirk),
                onComplete: Complete);
        }

        private void BuildV2() {
            Add(RoleName.Value, V2_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, V2_Line2.Value, onComplete: Complete);
        }

        private void BuildBloodMoon() {
            Add(RoleName.Value, VBloodMoon_Line1.Value,
                onStart: () => ShowPortraitWithFace(ShepelFullBodyPortrait.Face.Serious));
            Add(RoleName.Value, VBloodMoon_Line2.Value, onComplete: Complete);
        }
    }
}
