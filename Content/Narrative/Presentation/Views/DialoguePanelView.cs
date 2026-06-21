using InnoVault.Narrative.Presentation.Dialogue;
using InnoVault.Narrative.Runtime;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.Narrative.Presentation.Views
{
    /// <summary>Narrative 对话面板——承载 ADV 全身立绘生命周期</summary>
    internal sealed class DialoguePanelView : NarrativeDialogueViewBase<DialoguePanelView>, INarrativePanelAnchor
    {
        private static readonly System.Collections.Generic.Dictionary<Type, FullBodyPortraitBase> TypeToPortraits = new();
        private static readonly System.Collections.Generic.Dictionary<string, FullBodyPortraitBase> NameToPortraits = new(StringComparer.Ordinal);

        private FullBodyPortraitBase _activeFullBodyPortrait;
        private string _portraitLineKey = string.Empty;
        private bool _portraitLineKeyInitialized;

        float INarrativePanelAnchor.ShowProgress => MotionProgress;

        Rectangle INarrativePanelAnchor.GetPanelRect() => PanelRect;

        /// <summary>显示全身立绘</summary>
        public bool ShowFullBodyPortrait<T>() where T : FullBodyPortraitBase => ShowFullBodyPortrait(typeof(T));

        public bool ShowFullBodyPortrait(Type type) {
            if (!TypeToPortraits.TryGetValue(type, out FullBodyPortraitBase portrait)) {
                return false;
            }

            StartPerformance(portrait);
            return true;
        }

        public void HideFullBodyPortrait() => _activeFullBodyPortrait?.EndPerformance();

        public FullBodyPortraitBase GetActiveFullBodyPortrait() => _activeFullBodyPortrait;

        internal static void RegisterFullBodyPortrait(FullBodyPortraitBase portrait) {
            TypeToPortraits[portrait.GetType()] = portrait;
            if (!string.IsNullOrWhiteSpace(portrait.Name)) {
                NameToPortraits[portrait.Name] = portrait;
            }
        }

        private void StartPerformance(FullBodyPortraitBase portrait) {
            if (_activeFullBodyPortrait != null && _activeFullBodyPortrait != portrait) {
                _activeFullBodyPortrait.EndPerformance();
            }

            _activeFullBodyPortrait = portrait;
            if (!portrait.Active) {
                portrait.Initialize(this);
            }
            portrait.StartPerformance();
        }

        public override void Sync(NarrativeSession active) {
            if (active != null && active.DialogueVisible) {
                Open();
                return;
            }

            if (_activeFullBodyPortrait is { BlockDialogueClose: true }) {
                return;
            }

            Close();
        }

        public override void Update() {
            base.Update();
            NarrativeSession session = NarrativeRunner.Active;
            if (session != null && session.DialogueVisible) {
                StoryDialogueLayoutUtil.RefreshWrappedLines(Layout, session.Line);
            }
            UpdateFullBodyPortrait();
            NotifyPortraitLineAdvance();
            BindSessionBlockers();
        }

        protected override void HandleInput(NarrativeSession session) {
            if (_activeFullBodyPortrait is { BlockDialogueAdvance: true }) {
                return;
            }

            base.HandleInput(session);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float alpha = Layout.Alpha;
            _activeFullBodyPortrait?.Draw(spriteBatch, alpha);
            base.Draw(spriteBatch);
        }

        private void UpdateFullBodyPortrait() {
            if (_activeFullBodyPortrait == null) {
                return;
            }

            _activeFullBodyPortrait.Update();
            if (!_activeFullBodyPortrait.Active) {
                _activeFullBodyPortrait = null;
            }
        }

        private void NotifyPortraitLineAdvance() {
            NarrativeSession session = NarrativeRunner.Active;
            if (session == null || !session.DialogueVisible || _activeFullBodyPortrait == null) {
                return;
            }

            LinePresentation line = session.Line;
            string lineKey = $"{line.Speaker}:{line.Text}";
            if (!_portraitLineKeyInitialized) {
                _portraitLineKey = lineKey;
                _portraitLineKeyInitialized = true;
                return;
            }

            if (lineKey == _portraitLineKey) {
                return;
            }

            _portraitLineKey = lineKey;
            _activeFullBodyPortrait.OnDialogueAdvance();
        }

        private void BindSessionBlockers() {
            NarrativeSession session = NarrativeRunner.Active;
            if (session == null) {
                return;
            }

            session.BlocksAdvance = IsAdvanceBlocked;
            session.BlocksCompletion = IsCompletionBlocked;
        }

        private bool IsAdvanceBlocked() => _activeFullBodyPortrait is { BlockDialogueAdvance: true };

        private bool IsCompletionBlocked() => _activeFullBodyPortrait is { BlockDialogueClose: true };

        private void ResetPortraitTracking() {
            _portraitLineKey = string.Empty;
            _portraitLineKeyInitialized = false;
        }

        public override void Open() {
            ResetPortraitTracking();
            base.Open();
        }

        public override void Close() {
            NarrativeSession session = NarrativeRunner.Active;
            if (session?.Phase == NarrativeSessionPhase.Completed) {
                _activeFullBodyPortrait?.OnDialogueComplete();
            }

            if (_activeFullBodyPortrait is { BlockDialogueClose: true }) {
                return;
            }

            HideFullBodyPortrait();
            ResetPortraitTracking();
            base.Close();
        }
    }
}
