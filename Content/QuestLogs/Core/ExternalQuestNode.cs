using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.Core
{
    /// <summary>外模运行时节点。ID 由调用方传入，文案不走 QuestLogs.hjson</summary>
    [Autoload(false)]
    internal sealed class ExternalQuestNode : QuestNode
    {
        internal const int ProgressScale = 100;

        private readonly string _id;
        private readonly bool _countsTowardCompletionist;
        private LocalizedText _title = LocalizedText.Empty;
        private LocalizedText _summary = LocalizedText.Empty;
        private LocalizedText _detail = LocalizedText.Empty;
        private Func<Player, bool> _completeBool;
        private Func<Player, float> _completeFloat;
        private Func<Player, bool> _unlock;

        public override string Name => _id;
        public override string ID => _id;
        public override bool CountsTowardCompletionist => _countsTowardCompletionist;

        internal ExternalQuestNode(string id, bool countsTowardCompletionist) {
            _id = id;
            _countsTowardCompletionist = countsTowardCompletionist;
        }

        internal void BindTexts(LocalizedText title, LocalizedText summary, LocalizedText detail) {
            _title = title ?? LocalizedText.Empty;
            _summary = summary ?? LocalizedText.Empty;
            _detail = detail ?? LocalizedText.Empty;
        }

        internal void BindPredicates(Func<Player, bool> completeBool, Func<Player, float> completeFloat, Func<Player, bool> unlock) {
            _completeBool = completeBool;
            _completeFloat = completeFloat;
            _unlock = unlock;
        }

        public override void SetStaticDefaults() {
            DisplayName = _title;
            Description = _summary;
            DetailedDescription = _detail;
            if (Objectives.Count == 0) {
                Objectives.Add(new QuestObjective {
                    Description = _summary,
                    RequiredProgress = ProgressScale
                });
            }
        }

        public override void VaultSetup() {
            try {
                SetStaticDefaults();
            }
            catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[ExternalQuestNode:VaultSetup] {ex.Message}");
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
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return false;
            }
            try {
                return _unlock(player);
            }
            catch (Exception ex) {
                CWRMod.Instance.Logger.Warn($"[ExternalQuestNode] unlock `{_id}`: {ex.Message}");
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

            if (_completeBool != null || _completeFloat != null) {
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
                    CWRMod.Instance.Logger.Warn($"[ExternalQuestNode] complete `{_id}`: {ex.Message}");
                    return;
                }
                Objectives[0].RequiredProgress = ProgressScale;
                Objectives[0].CurrentProgress = (int)(progress * ProgressScale);
            }

            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }
}
