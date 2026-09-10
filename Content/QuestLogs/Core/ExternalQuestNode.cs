using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    /// <summary>
    /// 外模运行时节点。ID 由调用方传入，文案不走 QuestLogs.hjson。<br/>
    /// 不参与 Autoload，由 <see cref="QuestNode.TryRegisterRuntime"/> 入表；
    /// 除显式登记为章目枢纽外，永不充当"起点"根，避免抢占章目第 0 条与教程目标
    /// </summary>
    [Autoload(false)]
    internal sealed class ExternalQuestNode : QuestNode
    {
        /// <summary>手动进度 / 浮点谓词的默认刻度</summary>
        internal const int DefaultProgressScale = 100;

        /// <summary>谓词连续抛出这么多次后停用，避免每帧刷日志</summary>
        private const int MaxPredicateFailures = 3;

        private readonly string _id;
        private readonly bool _countsTowardCompletionist;
        private readonly bool _chapterHub;
        private readonly int _chapterOrder;
        private LocalizedText _title = LocalizedText.Empty;
        private LocalizedText _summary = LocalizedText.Empty;
        private LocalizedText _detail = LocalizedText.Empty;
        private LocalizedText _objective;
        private Func<Player, bool> _completeBool;
        private Func<Player, float> _completeFloat;
        private Func<Player, bool> _unlock;
        private int _requiredProgress = DefaultProgressScale;
        private int _completeFailures;
        private int _unlockFailures;

        public override string Name => _id;
        public override string ID => _id;
        public override bool CountsTowardCompletionist => _countsTowardCompletionist;
        /// <summary>外部节点不做无父根：无父也不进章目、不排第 0 条</summary>
        public override bool IsChapterRoot => false;
        public override bool IsChapterHub => _chapterHub;
        public override int ChapterOrder => _chapterOrder;

        /// <summary>是否绑定了完成谓词；绑定后手动进度每帧会被覆盖</summary>
        internal bool HasCompletePredicate => _completeBool != null || _completeFloat != null;

        internal ExternalQuestNode(string id, bool countsTowardCompletionist, bool chapterHub, int chapterOrder) {
            _id = id;
            _countsTowardCompletionist = countsTowardCompletionist;
            _chapterHub = chapterHub;
            _chapterOrder = chapterOrder;
        }

        internal void BindTexts(LocalizedText title, LocalizedText summary, LocalizedText detail, LocalizedText objective) {
            _title = title ?? LocalizedText.Empty;
            _summary = summary ?? LocalizedText.Empty;
            _detail = detail ?? LocalizedText.Empty;
            _objective = objective;
        }

        /// <summary>
        /// 绑定谓词。布尔谓词目标刻度为 1（详情页不画分数）；
        /// 浮点谓词与手动进度用 <paramref name="progressMax"/>，非正数回落默认刻度
        /// </summary>
        internal void BindPredicates(Func<Player, bool> completeBool, Func<Player, float> completeFloat, Func<Player, bool> unlock, int progressMax) {
            _completeBool = completeBool;
            _completeFloat = completeFloat;
            _unlock = unlock;
            if (completeBool != null && completeFloat == null) {
                _requiredProgress = 1;
            }
            else {
                _requiredProgress = progressMax > 0 ? progressMax : DefaultProgressScale;
            }
        }

        public override void SetStaticDefaults() {
            DisplayName = _title;
            Description = _summary;
            DetailedDescription = _detail;
            if (Objectives.Count == 0) {
                Objectives.Add(new QuestObjective {
                    Description = _objective ?? _summary,
                    RequiredProgress = _requiredProgress
                });
            }
        }

        public override void VaultSetup() {
            try {
                SetStaticDefaults();
            }
            catch (Exception ex) {
                CWRMod.Instance?.Logger.Error($"[ExternalQuestNode:VaultSetup] `{_id}`: {ex.Message}");
            }
            InitializeRewards();
            for (int i = 0; i < Objectives.Count; i++) {
                Objectives[i].Initialize(this, i);
            }
            PostSetup();
        }

        protected override bool HiddenTriggerMet() {
            if (_unlock == null) {
                return true;
            }
            //坏掉的门保持关闭：不能因为谓词抛异常就放行
            if (_unlockFailures >= MaxPredicateFailures) {
                return false;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return false;
            }
            try {
                return _unlock(player);
            }
            catch (Exception ex) {
                if (++_unlockFailures >= MaxPredicateFailures) {
                    CWRMod.Instance?.Logger.Error($"[ExternalQuestNode] unlock `{_id}` threw {MaxPredicateFailures} times, gate stays closed: {ex.Message}");
                }
                return false;
            }
        }

        public override void UpdateByPlayer() {
            if (Objectives.Count == 0) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            if (HasCompletePredicate) {
                float progress = 0f;
                try {
                    if (_completeFloat != null) {
                        progress = MathHelper.Clamp(_completeFloat(player), 0f, 1f);
                    }
                    else if (_completeBool(player)) {
                        progress = 1f;
                    }
                }
                catch (Exception ex) {
                    if (++_completeFailures >= MaxPredicateFailures) {
                        //谓词坏了就冻住，节点留在图上但不再推进；只记一次
                        CWRMod.Instance?.Logger.Error($"[ExternalQuestNode] complete `{_id}` threw {MaxPredicateFailures} times, disabled: {ex.Message}");
                        _completeBool = null;
                        _completeFloat = null;
                    }
                    return;
                }
                Objectives[0].RequiredProgress = _requiredProgress;
                //加一点余量抵消 float 乘法误差，避免 1f 变成 required-1
                Objectives[0].CurrentProgress = (int)(progress * _requiredProgress + 0.001f);
            }

            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
