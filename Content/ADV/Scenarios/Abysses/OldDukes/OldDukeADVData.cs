namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    /// <summary>
    /// 老公爵交互状态枚举
    /// </summary>
    public enum OldDukeInteractionState
    {
        /// <summary>未遇见</summary>
        NotMet = 0,
        /// <summary>已遇见但未做选择</summary>
        Met = 1,
        /// <summary>接受合作</summary>
        AcceptedCooperation = 2,
        /// <summary>拒绝合作（可重新选择）</summary>
        DeclinedCooperation = 3,
        /// <summary>选择战斗（永久战斗）</summary>
        ChoseToFight = 4
    }

    /// <summary>

    /// 老公爵剧情线的存档数据

    /// </summary>
    public class OldDukeADVData : ADVDataModule
    {
        public int OldDukeInteraction;
        public bool OldDukeFirstCampsiteDialogueCompleted;
        public bool OldDukeFindFragmentsQuestTriggered;
        public bool OldDukeFindFragmentsQuestCompleted;

        public OldDukeInteractionState OldDukeState {
            get => (OldDukeInteractionState)OldDukeInteraction;
            set => OldDukeInteraction = (int)value;
        }

        public bool FirstMetOldDuke => OldDukeState != OldDukeInteractionState.NotMet;
        public bool OldDukeCooperationAccepted => OldDukeState == OldDukeInteractionState.AcceptedCooperation;
        public bool OldDukeCooperationDeclined => OldDukeState == OldDukeInteractionState.DeclinedCooperation;
        public bool OldDukeChoseToFight => OldDukeState == OldDukeInteractionState.ChoseToFight;
        public bool CanRetriggerOldDukeDialogue => OldDukeState == OldDukeInteractionState.DeclinedCooperation;
    }
}
