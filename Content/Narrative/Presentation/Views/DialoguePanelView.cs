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
        private NarrativeSession _activeFullBodyPortraitSession;

        float INarrativePanelAnchor.ShowProgress => MotionProgress;

        Rectangle INarrativePanelAnchor.GetPanelRect() => PanelRect;

        public static T GetPortraits<T>() where T : FullBodyPortraitBase => TypeToPortraits[typeof(T)] as T;

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
            _activeFullBodyPortraitSession = NarrativeRunner.Active;
            if (!portrait.Active) {
                portrait.Initialize(this);
            }
            portrait.StartPerformance();
        }

        public override void Sync(NarrativeSession active) {
            if (active != null && active.DialogueVisible) {
                ClearStaleFullBodyPortrait(active);
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
                _activeFullBodyPortraitSession = null;
            }
        }

        private void ClearStaleFullBodyPortrait(NarrativeSession active) {
            if (_activeFullBodyPortrait == null || ReferenceEquals(_activeFullBodyPortraitSession, active)) {
                return;
            }

            _activeFullBodyPortrait.AbortPerformance();
            _activeFullBodyPortrait = null;
            _activeFullBodyPortraitSession = null;
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

        public override void Open() {
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
            base.Close();
        }
    }
}
