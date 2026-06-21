using CalamityOverhaul.Content.Narrative.Presentation.Views;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.Narrative.Presentation
{
    public abstract class FullBodyPortraitBase : VaultType<FullBodyPortraitBase>
    {
        public enum PerformancePhase
        {
            Inactive,
            FadeIn,
            Hold,
            FadeOut,
            Custom
        }

        public abstract string PortraitKey { get; }
        public bool Active { get; protected set; }
        public float CurrentFade { get; set; }
        public bool BlockDialogueAdvance { get; set; }
        public bool BlockDialogueClose { get; set; }
        public INarrativePanelAnchor OwnerDialogue;

        protected Vector2 position;
        protected float scale = 1f;
        protected float rotation;
        protected Color drawColor = Color.White;
        protected int timer;
        protected PerformancePhase currentPhase = PerformancePhase.Inactive;
        protected int dialogueIndex;
        protected float phaseProgress;

        protected virtual float FadeInDuration => 60f;
        protected virtual float FadeOutDuration => 45f;

        protected sealed override void VaultRegister() {
            DialoguePanelView.RegisterFullBodyPortrait(this);
        }

        public sealed override void VaultSetup() {
            SetStaticDefaults();
        }

        public virtual void Initialize(INarrativePanelAnchor dialogue) {
            OwnerDialogue = dialogue;
            Active = true;
            CurrentFade = 0f;
            timer = 0;
            dialogueIndex = 0;
            phaseProgress = 0f;
            BlockDialogueAdvance = false;
            BlockDialogueClose = false;
            currentPhase = PerformancePhase.Inactive;
            OnInitialize();
        }

        public virtual void StartPerformance() {
            phaseProgress = 0f;
            CurrentFade = 0f;
            currentPhase = PerformancePhase.FadeIn;
        }

        public virtual void EndPerformance() {
            if (currentPhase != PerformancePhase.Custom) {
                currentPhase = PerformancePhase.FadeOut;
                phaseProgress = 0f;
            }
        }

        public virtual void Update() {
            if (!Active) {
                return;
            }

            timer++;
            switch (currentPhase) {
                case PerformancePhase.FadeIn:
                    if (OwnerDialogue != null && OwnerDialogue.ShowProgress < 1f) {
                        break;
                    }
                    phaseProgress++;
                    if (phaseProgress >= FadeInDuration) {
                        CurrentFade = 1f;
                        currentPhase = PerformancePhase.Hold;
                        phaseProgress = 0f;
                    }
                    else {
                        CurrentFade = phaseProgress / FadeInDuration;
                    }
                    break;
                case PerformancePhase.Hold:
                    CurrentFade = 1f;
                    break;
                case PerformancePhase.FadeOut:
                    phaseProgress++;
                    CurrentFade = Math.Max(0f, 1f - phaseProgress / FadeOutDuration);
                    if (CurrentFade <= 0f) {
                        Active = false;
                        currentPhase = PerformancePhase.Inactive;
                        OnDeactivate();
                        return;
                    }
                    break;
                case PerformancePhase.Custom:
                    OnCustomPhaseUpdate();
                    break;
            }

            OnUpdate();
        }

        public virtual void Draw(SpriteBatch spriteBatch, float dialogueAlpha) {
            if (!Active) {
                return;
            }

            OnDraw(spriteBatch, MathHelper.Clamp(dialogueAlpha * CurrentFade, 0f, 1f));
        }

        public virtual void OnDialogueAdvance() {
            dialogueIndex++;
        }

        public virtual void OnDialogueComplete() { }

        protected void EnterCustomPhase() {
            currentPhase = PerformancePhase.Custom;
            phaseProgress = 0f;
        }

        public void SkipFadeIn() {
            CurrentFade = 1f;
            currentPhase = PerformancePhase.Hold;
            phaseProgress = 0f;
        }

        protected void ForceDeactivate() {
            Active = false;
            currentPhase = PerformancePhase.Inactive;
            CurrentFade = 0f;
            BlockDialogueAdvance = false;
            BlockDialogueClose = false;
            OnDeactivate();
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnUpdate() { }
        protected abstract void OnDraw(SpriteBatch spriteBatch, float alpha);
        protected virtual void OnDeactivate() { }
        protected virtual void OnCustomPhaseUpdate() { }
    }
}
