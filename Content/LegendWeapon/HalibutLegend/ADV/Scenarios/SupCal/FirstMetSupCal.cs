using CalamityMod.NPCs.CalClone;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.SupCal
{
    internal class FirstMetSupCal : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FirstMetSupCal);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Rolename3 { get; private set; }

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "至尊灾厄");
            Rolename3 = this.GetLocalization(nameof(Rolename3), () => "比目鱼");
        }
        
        protected override void Build() {
            // 注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: true);
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.SupCalADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(Rolename3.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(Rolename3.Value, silhouette: false);

            // 添加对话
            Add(Rolename1.Value, "没想到你这么快就杀掉了我的'妹妹'");
            Add(Rolename1.Value, "兵贵神速不是吗?");
            Add(Rolename3.Value, "你是......我对你有印象");
            Add(Rolename3.Value, "你是那个焚烧掉了一半海域的女巫");
            Add(Rolename2.Value, "哈?!呵呵，竟然有人...或者鱼认得我，你们倒也算有趣");
            Add(Rolename3.Value, "......你，为什么还活着?我记得你已经在上世纪就已经死了");
            Add(Rolename2.Value, "呵呵呵，连这事都有听说过吗?和你这条有趣的鱼解释一下也无妨，我的意识早已经熔铸进硫磺火中，这具躯体不过是被火焰操纵的尸体");
            Add(Rolename3.Value, "......活人的意识，非人的躯体，依靠媒介行走世间，你成为了异类?!");
            Add(Rolename2.Value, "你的层次太低，理解不了我现在的状态");
            Add(Rolename2.Value, "况且我来这里也不是为了这事儿的......");

            AddWithChoices(Rolename2.Value, "那么，你的选择是？", new List<Choice> {
                new Choice("开战", () => {
                    // 选择后继续对话
                    Add(Rolename2.Value, "这么好战的吗？那么便让我来称量称量你吧");
                    // 继续推进场景
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value,
                        "这么好战的吗？那么便让我来称量称量你吧",
                        onFinish: () => Complete()
                    );
                }),
                new Choice("撤退", () => {
                    Add(Rolename2.Value, "......真是杂鱼呢，那么给你一个见面礼，我们下次见");
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value,
                        "......真是杂鱼呢，那么给你一个见面礼，我们下次见",
                        onFinish: () => Complete()
                    );
                }),
            });
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.FirstMetSupCal) {
                return;
            }
            if (!FirstMetSupCalNPC.Spawned) {
                return;
            }

            if (ScenarioManager.Start<FirstMetSupCal>()) {
                save.FirstMetSupCal = true;
                FirstMetSupCalNPC.Spawned = false;
            }
        }
    }

    internal class FirstMetSupCalNPC : GlobalNPC
    {
        public static bool Spawned = false;
        public override bool SpecialOnKill(NPC npc) {
            if (npc.type == ModContent.NPCType<CalamitasClone>()) {
                Spawned = true;
            }
            return false;
        }
    }
}
