using CalamityOverhaul.Content.ADV.DialogueBoxs;
using CalamityOverhaul.Content.ADV.DialogueBoxs.Styles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Shepel
{
    /// <summary>
    /// 响应式对话基类，子类由 <see cref="ADVScenarioBase.Instances"/> 反射发现
    /// </summary>
    internal abstract class ShepelReactiveDialogueBase : SHPCDialogueScenarioBase, ILocalizedModType
    {
        public new string LocalizationCategory => "ADV.Shepel";
        public override int DialoguePriority => 50;
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => SHPCDialogueBox.Instance;

        /// <summary>
        /// 本子类绑定的响应式事件，一类事件一个子类
        /// </summary>
        protected abstract ShepelReactiveEvent HandledEvent { get; }

        /// <summary>
        /// Boss 对话返回目标 NPC 类型；非 Boss 保持 -1 不过滤
        /// </summary>
        protected virtual int TargetBossNpcType => -1;

        protected override bool CheckConditions(Player player, ADVSave save) {
            ShepelADVData data = save.Get<ShepelADVData>();
            if (!ShepelReactiveEvents.HasFlag(data, HandledEvent)) return false;
            if (TargetBossNpcType != -1 && data.LastDefeatedBossNpcType != TargetBossNpcType) return false;
            return true;
        }

        /// <summary>
        /// 清除当前事件 bit，Build 开头调用
        /// </summary>
        protected void ConsumeEvent(ShepelADVData data)
            => ShepelReactiveEvents.ClearFlag(data, HandledEvent);

        /// <summary>
        /// 显示全身立绘并设初始表情
        /// </summary>
        protected static void ShowPortraitWithFace(ShepelFullBodyPortrait.Face face) {
            SHPCDialogueBox.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>();
            if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait) {
                portrait.SkipFadeIn();
                portrait.currentFace = face;
            }
        }

        /// <summary>
        /// 对话中途切换立绘表情
        /// </summary>
        protected static void SetPortraitFace(ShepelFullBodyPortrait.Face face) {
            if (SHPCDialogueBox.Instance?.GetActiveFullBodyPortrait() is ShepelFullBodyPortrait portrait)
                portrait.currentFace = face;
        }
    }
}
