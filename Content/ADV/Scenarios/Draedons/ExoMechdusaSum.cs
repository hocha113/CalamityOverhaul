using CalamityMod;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using CalamityMod.World;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    internal class ExoMechdusaSum : ADVScenarioBase, ILocalizedModType, IWorldInfo
    {
        public string LocalizationCategory => "ADV";
        public override string Key => nameof(ExoMechdusaSum);

        //角色名称本地化
        public static LocalizedText DraedonName { get; private set; }

        //介绍台词
        public static LocalizedText IntroLine1 { get; private set; }
        public static LocalizedText IntroLine2 { get; private set; }
        public static LocalizedText IntroLine3 { get; private set; }
        public static LocalizedText IntroLine4 { get; private set; }
        public static LocalizedText IntroLine5 { get; private set; }

        //选择提示
        public static LocalizedText SelectionPrompt { get; private set; }

        //机械选项文本
        public static LocalizedText ChoiceAres { get; private set; }
        public static LocalizedText ChoiceThanatos { get; private set; }
        public static LocalizedText ChoiceTwins { get; private set; }

        //Boss Rush模式文本
        public static LocalizedText BossRushLine { get; private set; }

        /// <summary>
        /// 是否启用简洁模式，如果是，跳过介绍直接选择机甲
        /// </summary>
        public static bool SimpleMode;

        //设置场景默认使用嘉登科技风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => DraedonDialogueBox.Instance;

        private const string red = " ";
        private const string alt = " " + " ";

        void IWorldInfo.OnWorldLoad() {
            //重置状态
        }

        public override void SetStaticDefaults() {
            DraedonName = this.GetLocalization(nameof(DraedonName), () => "嘉登");

            //介绍台词(对应原游戏的 DraedonIntroductionText 系列)
            IntroLine1 = this.GetLocalization(nameof(IntroLine1), () => "你知道吗？这一刻已经等了太久了");
            IntroLine2 = this.GetLocalization(nameof(IntroLine2), () => "我对一切未知感到着迷，但最让我着迷的莫过于你的本质");
            IntroLine3 = this.GetLocalization(nameof(IntroLine3), () => "我将会向你展示，我那些超越神明的造物");
            IntroLine4 = this.GetLocalization(nameof(IntroLine4), () => "而你，则将在战斗中向我展示你的本质");
            IntroLine5 = this.GetLocalization(nameof(IntroLine5), () => "现在，选择吧");

            SelectionPrompt = this.GetLocalization(nameof(SelectionPrompt), () => "做出你的选择");
            BossRushLine = this.GetLocalization(nameof(BossRushLine), () => "做出你的选择。你有20秒的时间");

            //机械选项
            ChoiceAres = this.GetLocalization(nameof(ChoiceAres), () => "战神阿瑞斯");
            ChoiceThanatos = this.GetLocalization(nameof(ChoiceThanatos), () => "死神塔纳托斯");
            ChoiceTwins = this.GetLocalization(nameof(ChoiceTwins), () => "双子阿尔忒弥斯与阿波罗");
        }

        protected override void OnScenarioStart() {
            DraedonEffect.IsActive = true;
            DraedonEffect.Send();
            ExoMechdusaSumRender.RegisterHoverEffects();//注册机甲选择悬停特效
        }

        protected override void OnScenarioComplete() {
            SimpleMode = false;
            DraedonEffect.IsActive = false;
            DraedonEffect.Send();
            ExoMechdusaSumRender.Cleanup();//清理机甲选择悬停特效
        }

        protected override void Build() {
            //注册嘉登立绘(使用科技风格的剪影效果)
            DialogueBoxBase.RegisterPortrait(DraedonName.Value, ADVAsset.Draedon2ADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + red, ADVAsset.Draedon2RedADV, silhouette: false);
            DialogueBoxBase.RegisterPortrait(DraedonName.Value + alt, ADVAsset.DraedonADV, silhouette: false);

            //检查是否为简洁模式
            bool simpleMode = CWRRef.GetBossRushActive() || SimpleMode;

            if (simpleMode) {
                //简洁模式，直接显示选择界面，时间紧迫，不等你嗷
                AddWithChoices(DraedonName.Value + red, BossRushLine.Value, [
                    new Choice(ChoiceAres.Value, () => SummonMech(ExoMech.Prime)),
                    new Choice(ChoiceThanatos.Value, () => SummonMech(ExoMech.Destroyer)),
                    new Choice(ChoiceTwins.Value, () => SummonMech(ExoMech.Twins))
                ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
            }
            else {
                //普通模式，就播放老逼登的完整介绍对话
                Add(DraedonName.Value + alt, IntroLine1.Value);
                Add(DraedonName.Value, IntroLine2.Value);
                Add(DraedonName.Value, IntroLine3.Value);
                Add(DraedonName.Value + red, IntroLine4.Value);

                //添加选择界面
                AddWithChoices(DraedonName.Value + red, IntroLine5.Value, [
                    new Choice(ChoiceAres.Value, () => SummonMech(ExoMech.Prime)),
                    new Choice(ChoiceThanatos.Value, () => SummonMech(ExoMech.Destroyer)),
                    new Choice(ChoiceTwins.Value, () => SummonMech(ExoMech.Twins))
                ], choiceBoxStyle: ADVChoiceBox.ChoiceBoxStyle.Draedon);
            }
        }

        private void SummonMech(ExoMech mechType) {
            //设置要召唤的机械类型
            CalamityWorld.DraedonMechToSummon = mechType;

            DoSummon(Main.LocalPlayer);

            //完成当前场景
            Complete();
        }

        public static void DoSummon(Player player) {
            switch (CalamityWorld.DraedonMechToSummon) {
                case ExoMech.Destroyer:
                    Vector2 thanatosSpawnPosition = player.Center + Vector2.UnitY * 2100f;
                    NPC thanatos = CalamityUtils.SpawnBossBetter(thanatosSpawnPosition, ModContent.NPCType<ThanatosHead>());
                    if (thanatos != null)
                        thanatos.velocity = thanatos.SafeDirectionTo(player.Center) * 40f;
                    break;

                case ExoMech.Prime:
                    Vector2 aresSpawnPosition = player.Center - Vector2.UnitY * 1400f;
                    CalamityUtils.SpawnBossBetter(aresSpawnPosition, ModContent.NPCType<AresBody>());
                    break;

                case ExoMech.Twins:
                    Vector2 artemisSpawnPosition = player.Center + new Vector2(-1100f, -1600f);
                    Vector2 apolloSpawnPosition = player.Center + new Vector2(1100f, -1600f);
                    CalamityUtils.SpawnBossBetter(artemisSpawnPosition, ModContent.NPCType<Artemis>());
                    CalamityUtils.SpawnBossBetter(apolloSpawnPosition, ModContent.NPCType<Apollo>());
                    break;
            }
        }
    }
}
