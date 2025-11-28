using CalamityOverhaul.Content.ADV.DialogueBoxs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 全身立绘演出基类
    /// 用于制作带动画效果的全身立绘，支持变色、抖动等演出效果
    /// 可以控制对话框的推进行为
    /// </summary>
    public abstract class FullBodyPortraitBase
    {
        /// <summary>
        /// 立绘标识符
        /// </summary>
        public abstract string PortraitKey { get; }

        /// <summary>
        /// 立绘是否激活
        /// </summary>
        public bool Active { get; protected set; }

        /// <summary>
        /// 目标淡入淡出值(0-1)
        /// </summary>
        public float TargetFade { get; set; }

        /// <summary>
        /// 当前淡入淡出值(0-1)
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
        /// 关联的对话框实例
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
        /// 立绘颜色
        /// </summary>
        protected Color drawColor = Color.White;

        /// <summary>
        /// 内部计时器，可用于动画
        /// </summary>
        protected int timer;

        /// <summary>
        /// 初始化立绘
        /// </summary>
        /// <param name="dialogue">关联的对话框</param>
        public virtual void Initialize(DialogueBoxBase dialogue) {
            ownerDialogue = dialogue;
            Active = true;
            TargetFade = 0f;
            CurrentFade = 0f;
            timer = 0;
            BlockDialogueAdvance = false;
            BlockDialogueClose = false;
            OnInitialize();
        }

        /// <summary>
        /// 启动立绘演出
        /// </summary>
        public virtual void StartPerformance() {
            TargetFade = 1f;
            OnStartPerformance();
        }

        /// <summary>
        /// 结束立绘演出
        /// </summary>
        public virtual void EndPerformance() {
            TargetFade = 0f;
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
            if (CurrentFade <= 0f && TargetFade <= 0f) {
                Active = false;
                OnDeactivate();
                return;
            }

            OnUpdate();
        }

        /// <summary>
        /// 绘制立绘
        /// </summary>
        /// <param name="spriteBatch">绘制批次</param>
        /// <param name="dialogueAlpha">对话框当前透明度</param>
        public virtual void Draw(SpriteBatch spriteBatch, float dialogueAlpha) {
            if (!Active || CurrentFade <= 0.01f) {
                return;
            }

            OnDraw(spriteBatch, dialogueAlpha * CurrentFade);
        }

        /// <summary>
        /// 设置立绘阻止对话推进
        /// </summary>
        /// <param name="block">是否阻止</param>
        protected void SetBlockAdvance(bool block) {
            BlockDialogueAdvance = block;
        }

        /// <summary>
        /// 设置立绘阻止对话关闭
        /// </summary>
        /// <param name="block">是否阻止</param>
        protected void SetBlockClose(bool block) {
            BlockDialogueClose = block;
        }

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

        private float shakeIntensity;
        private int shakeDuration;
        private int shakeTimer;

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

        #region 虚方法供子类重写

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
        /// <param name="spriteBatch">绘制批次</param>
        /// <param name="alpha">最终透明度(已包含淡入淡出)</param>
        protected abstract void OnDraw(SpriteBatch spriteBatch, float alpha);

        /// <summary>
        /// 停用时调用
        /// </summary>
        protected virtual void OnDeactivate() { }

        #endregion
    }
}
