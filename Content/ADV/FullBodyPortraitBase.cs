using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 全身立绘演出基类
    /// 用于制作复杂特效的全身立绘，支持变色、淡入淡出和演出效果
    /// 可以控制对话框的推进行为
    /// </summary>
    public abstract class FullBodyPortraitBase : VaultType<FullBodyPortraitBase>
    {
        #region 演出阶段枚举

        /// <summary>
        /// 演出阶段枚举，子类可继承扩展
        /// </summary>
        public enum PerformancePhase
        {
            /// <summary>
            /// 未激活
            /// </summary>
            Inactive,
            /// <summary>
            /// 等待对话框准备
            /// </summary>
            WaitingDialogue,
            /// <summary>
            /// 淡入阶段
            /// </summary>
            FadeIn,
            /// <summary>
            /// 保持显示
            /// </summary>
            Hold,
            /// <summary>
            /// 淡出阶段
            /// </summary>
            FadeOut,
            /// <summary>
            /// 自定义演出(子类扩展)
            /// </summary>
            Custom
        }

        #endregion

        #region 基础属性

        /// <summary>
        /// 立绘标识符
        /// </summary>
        public abstract string PortraitKey { get; }

        /// <summary>
        /// 立绘是否激活
        /// </summary>
        public bool Active { get; protected set; }

        /// <summary>
        /// 目标淡入淡出值(0到1)
        /// </summary>
        public float TargetFade { get; set; }

        /// <summary>
        /// 当前淡入淡出值(0到1)
        /// </summary>
        public float CurrentFade { get; protected set; }

        /// <summary>
        /// 淡入淡出速度
        /// </summary>
        protected virtual float FadeSpeed => 0.08f;

        /// <summary>
        /// 是否阻止对话框推进到下一句
        /// </summary>
        public bool BlockDialogueAdvance { get; protected set; }

        /// <summary>
        /// 是否阻止对话框关闭
        /// </summary>
        public bool BlockDialogueClose { get; protected set; }

        /// <summary>
        /// 所属的对话框实例
        /// </summary>
        protected DialogueBoxBase ownerDialogue;

        /// <summary>
        /// 立绘位置
        /// </summary>
        protected Vector2 position;

        /// <summary>
        /// 立绘缩放
        /// </summary>
        protected float scale = 1f;

        /// <summary>
        /// 立绘旋转
        /// </summary>
        protected float rotation;

        /// <summary>
        /// 绘制颜色
        /// </summary>
        protected Color drawColor = Color.White;

        /// <summary>
        /// 内部计时器，用于动画
        /// </summary>
        protected int timer;

        /// <summary>
        /// 当前演出阶段
        /// </summary>
        protected PerformancePhase currentPhase = PerformancePhase.Inactive;

        /// <summary>
        /// 当前对话索引(用于追踪对话进度)
        /// </summary>
        protected int dialogueIndex;

        /// <summary>
        /// 是否启用自动对话联动
        /// </summary>
        protected virtual bool AutoDialogueSync => true;

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

        /// <summary>
        /// 阶段进度计时器
        /// </summary>
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
        /// <param name="dialogue">所属的对话框</param>
        public virtual void Initialize(DialogueBoxBase dialogue) {
            ownerDialogue = dialogue;
            Active = true;
            TargetFade = 0f;
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
        /// 开始立绘演出
        /// </summary>
        public virtual void StartPerformance() {
            currentPhase = PerformancePhase.WaitingDialogue;
            phaseProgress = 0f;
            TargetFade = 1f;
            OnStartPerformance();
        }

        /// <summary>
        /// 结束立绘演出
        /// </summary>
        public virtual void EndPerformance() {
            TargetFade = 0f;
            if (currentPhase != PerformancePhase.Custom) {
                currentPhase = PerformancePhase.FadeOut;
                phaseProgress = 0f;
            }
            OnEndPerformance();
        }

        /// <summary>
        /// 更新立绘状态
        /// </summary>
        public virtual void Update() {
            if (!Active) {
                return;
            }

            timer++;

            //更新演出阶段
            UpdatePhase();

            //淡入淡出
            if (CurrentFade < TargetFade) {
                CurrentFade += FadeSpeed;
                if (CurrentFade > TargetFade) {
                    CurrentFade = TargetFade;
                }
            }
            else if (CurrentFade > TargetFade) {
                CurrentFade -= FadeSpeed;
                if (CurrentFade < TargetFade) {
                    CurrentFade = TargetFade;
                }
            }

            //淡出完成后停用
            if (CurrentFade <= 0f && TargetFade <= 0f && currentPhase == PerformancePhase.FadeOut) {
                Active = false;
                currentPhase = PerformancePhase.Inactive;
                OnDeactivate();
                return;
            }

            OnUpdate();
        }

        /// <summary>
        /// 更新演出阶段
        /// </summary>
        protected virtual void UpdatePhase() {
            switch (currentPhase) {
                case PerformancePhase.WaitingDialogue:
                    if (ownerDialogue != null && ownerDialogue.showProgress >= 1f) {
                        TransitionToPhase(PerformancePhase.FadeIn);
                    }
                    break;

                case PerformancePhase.FadeIn:
                    phaseProgress++;
                    if (phaseProgress >= FadeInDuration) {
                        TransitionToPhase(PerformancePhase.Hold);
                    }
                    else {
                        TargetFade = phaseProgress / FadeInDuration;
                    }
                    break;

                case PerformancePhase.Hold:
                    TargetFade = 1f;
                    break;

                case PerformancePhase.FadeOut:
                    phaseProgress++;
                    TargetFade = Math.Max(0f, 1f - phaseProgress / FadeOutDuration);
                    break;

                case PerformancePhase.Custom:
                    OnCustomPhaseUpdate();
                    break;
            }
        }

        /// <summary>
        /// 切换到指定演出阶段
        /// </summary>
        protected virtual void TransitionToPhase(PerformancePhase newPhase) {
            var oldPhase = currentPhase;
            currentPhase = newPhase;
            phaseProgress = 0f;
            OnPhaseTransition(oldPhase, newPhase);
        }

        /// <summary>
        /// 绘制立绘
        /// </summary>
        /// <param name="spriteBatch">精灵批次</param>
        /// <param name="dialogueAlpha">对话框当前透明度</param>
        public virtual void Draw(SpriteBatch spriteBatch, float dialogueAlpha) {
            if (!Active || CurrentFade <= 0.01f) {
                return;
            }

            OnDraw(spriteBatch, dialogueAlpha * CurrentFade);
        }

        #endregion

        #region 对话联动

        /// <summary>
        /// 当对话推进时调用(由对话框自动触发)
        /// </summary>
        public virtual void OnDialogueAdvance() {
            dialogueIndex++;
            OnDialogueAdvanceInternal(dialogueIndex);
        }

        /// <summary>
        /// 对话推进内部处理，子类重写此方法实现立绘切换等
        /// </summary>
        protected virtual void OnDialogueAdvanceInternal(int index) { }

        /// <summary>
        /// 当对话完成时调用
        /// </summary>
        public virtual void OnDialogueComplete() {
            OnDialogueCompleteInternal();
        }

        /// <summary>
        /// 对话完成内部处理
        /// </summary>
        protected virtual void OnDialogueCompleteInternal() { }

        #endregion

        #region 控制方法

        /// <summary>
        /// 设置是否阻止对话推进
        /// </summary>
        /// <param name="block">是否阻止</param>
        protected void SetBlockAdvance(bool block) {
            BlockDialogueAdvance = block;
        }

        /// <summary>
        /// 设置是否阻止对话关闭
        /// </summary>
        /// <param name="block">是否阻止</param>
        protected void SetBlockClose(bool block) {
            BlockDialogueClose = block;
        }

        /// <summary>
        /// 进入自定义演出阶段
        /// </summary>
        protected void EnterCustomPhase() {
            TransitionToPhase(PerformancePhase.Custom);
        }

        /// <summary>
        /// 退出自定义演出阶段
        /// </summary>
        /// <param name="nextPhase">下一阶段</param>
        protected void ExitCustomPhase(PerformancePhase nextPhase = PerformancePhase.Hold) {
            if (currentPhase == PerformancePhase.Custom) {
                TransitionToPhase(nextPhase);
            }
        }

        /// <summary>
        /// 强制结束演出并停用
        /// </summary>
        protected void ForceDeactivate() {
            Active = false;
            currentPhase = PerformancePhase.Inactive;
            CurrentFade = 0f;
            TargetFade = 0f;
            SetBlockAdvance(false);
            SetBlockClose(false);
            OnDeactivate();
        }

        #endregion

        #region 震动效果

        private float shakeIntensity;
        private int shakeDuration;
        private int shakeTimer;

        /// <summary>
        /// 播放震动效果
        /// </summary>
        /// <param name="intensity">震动强度</param>
        /// <param name="duration">持续时间(帧)</param>
        protected void PlayShake(float intensity, int duration) {
            shakeIntensity = intensity;
            shakeDuration = duration;
            shakeTimer = 0;
        }

        /// <summary>
        /// 获取震动偏移
        /// </summary>
        protected Vector2 GetShakeOffset() {
            if (shakeTimer >= shakeDuration) {
                return Vector2.Zero;
            }

            shakeTimer++;
            float progress = 1f - shakeTimer / (float)shakeDuration;
            float offsetX = Main.rand.NextFloat(-shakeIntensity, shakeIntensity) * progress;
            float offsetY = Main.rand.NextFloat(-shakeIntensity, shakeIntensity) * progress;
            return new Vector2(offsetX, offsetY);
        }

        #endregion

        #region 钩子方法，供子类重写

        /// <summary>
        /// 初始化时调用
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// 开始演出时调用
        /// </summary>
        protected virtual void OnStartPerformance() { }

        /// <summary>
        /// 结束演出时调用
        /// </summary>
        protected virtual void OnEndPerformance() { }

        /// <summary>
        /// 每帧更新时调用
        /// </summary>
        protected virtual void OnUpdate() { }

        /// <summary>
        /// 绘制时调用
        /// </summary>
        /// <param name="spriteBatch">精灵批次</param>
        /// <param name="alpha">最终透明度(已包含淡入淡出)</param>
        protected abstract void OnDraw(SpriteBatch spriteBatch, float alpha);

        /// <summary>
        /// 停用时调用
        /// </summary>
        protected virtual void OnDeactivate() { }

        /// <summary>
        /// 阶段切换时调用
        /// </summary>
        protected virtual void OnPhaseTransition(PerformancePhase oldPhase, PerformancePhase newPhase) { }

        /// <summary>
        /// 自定义阶段更新(需要子类实现)
        /// </summary>
        protected virtual void OnCustomPhaseUpdate() { }

        #endregion
    }
}
