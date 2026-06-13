using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 全身立绘演出基类——管理淡入/保持/淡出/自定义四个阶段的生命周期
    /// </summary>
    public abstract class FullBodyPortraitBase : VaultType<FullBodyPortraitBase>
    {
        /// <summary>
        /// 演出阶段
        /// </summary>
        public enum PerformancePhase
        {
            Inactive,
            FadeIn,
            Hold,
            FadeOut,
            /// <summary>
            /// 自定义演出，子类通过 <see cref="OnCustomPhaseUpdate"/> 驱动
            /// </summary>
            Custom
        }

        #region 属性

        public abstract string PortraitKey { get; }
        public bool Active { get; protected set; }

        /// <summary>
        /// 当前淡入淡出值(0~1)，FadeIn/FadeOut 阶段由基类线性驱动，
        /// Custom 阶段可由子类直接赋值
        /// </summary>
        public float CurrentFade { get; set; }

        /// <summary>
        /// 是否阻止对话框推进到下一句
        /// </summary>
        public bool BlockDialogueAdvance { get; set; }

        /// <summary>
        /// 是否阻止对话框关闭
        /// </summary>
        public bool BlockDialogueClose { get; set; }

        /// <summary>
        /// 拥有该立绘的对话框（可能为 null，表示未绑定或已解绑）
        /// </summary>
        public DialogueBoxBase OwnerDialogue;
        protected Vector2 position;
        protected float scale = 1f;
        protected float rotation;
        protected Color drawColor = Color.White;
        protected int timer;
        protected PerformancePhase currentPhase = PerformancePhase.Inactive;
        protected int dialogueIndex;

        #endregion

        #region 演出配置

        /// <summary>
        /// 淡入持续帧数
        /// </summary>
        protected virtual float FadeInDuration => 60f;

        /// <summary>
        /// 淡出持续帧数
        /// </summary>
        protected virtual float FadeOutDuration => 45f;

        protected float phaseProgress;

        #endregion

        #region 生命周期

        protected sealed override void VaultRegister() {
            DialogueBoxBase.RegisterFullBodyPortrait(this);
        }

        public sealed override void VaultSetup() {
            SetStaticDefaults();
        }

        /// <summary>
        /// 初始化立绘
        /// </summary>
        public virtual void Initialize(DialogueBoxBase dialogue) {
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

        /// <summary>
        /// 开始立绘演出，等待对话框展开后淡入
        /// </summary>
        public virtual void StartPerformance() {
            phaseProgress = 0f;
            CurrentFade = 0f;
            currentPhase = PerformancePhase.FadeIn;
        }

        /// <summary>
        /// 结束立绘演出，开始淡出
        /// </summary>
        public virtual void EndPerformance() {
            if (currentPhase != PerformancePhase.Custom) {
                currentPhase = PerformancePhase.FadeOut;
                phaseProgress = 0f;
            }
        }

        public virtual void Update() {
            if (!Active) return;

            timer++;

            switch (currentPhase) {
                case PerformancePhase.FadeIn:
                    // 等对话框展开后再计淡入
                    if (OwnerDialogue != null && OwnerDialogue.showProgress < 1f) break;
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

        /// <summary>
        /// 绘制立绘
        /// </summary>
        /// <param name="spriteBatch">精灵批次</param>
        /// <param name="dialogueAlpha">对话框当前透明度</param>
        public virtual void Draw(SpriteBatch spriteBatch, float dialogueAlpha) {
            if (!Active) return;
            OnDraw(spriteBatch, MathHelper.Clamp(dialogueAlpha * CurrentFade, 0f, 1f));
        }

        #endregion

        #region 对话联动

        /// <summary>
        /// 当对话推进时调用(由对话框触发)
        /// </summary>
        public virtual void OnDialogueAdvance() {
            dialogueIndex++;
        }

        /// <summary>
        /// 当对话完成时调用
        /// </summary>
        public virtual void OnDialogueComplete() { }

        #endregion

        #region 控制方法

        /// <summary>
        /// 进入自定义演出阶段
        /// </summary>
        protected void EnterCustomPhase() {
            currentPhase = PerformancePhase.Custom;
            phaseProgress = 0f;
        }

        /// <summary>
        /// 跳过淡入，直接进入 Hold 阶段并完全显示
        /// 适用于子场景中立绘已经在前一个场景显示过，不需要重新淡入的情况
        /// </summary>
        public void SkipFadeIn() {
            CurrentFade = 1f;
            currentPhase = PerformancePhase.Hold;
            phaseProgress = 0f;
        }

        /// <summary>
        /// 强制结束演出并停用
        /// </summary>
        protected void ForceDeactivate() {
            Active = false;
            currentPhase = PerformancePhase.Inactive;
            CurrentFade = 0f;
            BlockDialogueAdvance = false;
            BlockDialogueClose = false;
            OnDeactivate();
        }

        #endregion

        #region 钩子方法

        protected virtual void OnInitialize() { }
        protected virtual void OnUpdate() { }
        protected abstract void OnDraw(SpriteBatch spriteBatch, float alpha);
        protected virtual void OnDeactivate() { }
        /// <summary>
        /// Custom 阶段每帧更新，子类需自行驱动 <see cref="CurrentFade"/>
        /// </summary>
        protected virtual void OnCustomPhaseUpdate() { }

        #endregion
    }
}
