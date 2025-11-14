using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 至尊灾厄女巫全身立绘演出
    /// 用于女巫告别场景，带有红色淡入效果和最终燃烧消失演出
    /// </summary>
    internal class SupCalFullBodyPortrait : FullBodyPortraitBase
    {
        public override string PortraitKey => "SupremeCalamitasFullBody";

        //演出状态
        private enum PerformanceState
        {
            Inactive,         //未激活
            RedFadeIn,        //红色淡入
            Hold,             //保持显示
            BurningDissolve   //燃烧消失
        }

        private PerformanceState currentState = PerformanceState.Inactive;

        //红色淡入参数
        private float redFadeProgress = 0f;
        private const float RedFadeDuration = 80f; //红色淡入持续时间(帧)

        //燃烧消失参数
        private float burnProgress = 0f;
        private const float BurnDuration = 120f; //燃烧消失持续时间(帧)
        private float burnIntensity = 0f;
        private float burnFlicker = 0f;
        private float dissolveTimer = 0f;

        //颜色覆盖
        private Color currentTint = Color.White;

        protected override float FadeSpeed => 0.05f;

        protected override void OnInitialize() {
            currentState = PerformanceState.Inactive;
            redFadeProgress = 0f;
            burnProgress = 0f;
            burnIntensity = 0f;
            burnFlicker = 0f;
            dissolveTimer = 0f;
            currentTint = Color.White;
        }

        protected override void OnStartPerformance() {
            //启动红色淡入演出
            currentState = PerformanceState.RedFadeIn;
            redFadeProgress = 0f;
            SetBlockAdvance(false); //不阻止对话推进
        }

        protected override void OnEndPerformance() {
            //触发燃烧消失演出
            if (currentState != PerformanceState.BurningDissolve) {
                StartBurningDissolve();
            }
        }

        /// <summary>
        /// 启动燃烧消失演出
        /// </summary>
        public void StartBurningDissolve() {
            currentState = PerformanceState.BurningDissolve;
            burnProgress = 0f;
        }

        protected override void OnUpdate() {
            dissolveTimer += 0.08f;
            if (dissolveTimer > MathHelper.TwoPi) {
                dissolveTimer -= MathHelper.TwoPi;
            }

            switch (currentState) {
                case PerformanceState.Inactive:
                    break;

                case PerformanceState.RedFadeIn:
                    UpdateRedFadeIn();
                    break;

                case PerformanceState.Hold:
                    UpdateHold();
                    break;

                case PerformanceState.BurningDissolve:
                    UpdateBurningDissolve();
                    break;
            }

            scale = 1.4f;//这个大小看着毕竟合适
        }

        /// <summary>
        /// 更新红色淡入状态
        /// </summary>
        private void UpdateRedFadeIn() {
            redFadeProgress++;

            if (redFadeProgress >= RedFadeDuration) {
                //红色淡入完成，进入保持状态
                currentState = PerformanceState.Hold;
                redFadeProgress = RedFadeDuration;
            }

            //计算红色叠加
            float t = redFadeProgress / RedFadeDuration;
            float eased = CWRUtils.EaseInOutCubic(t);

            //从白色逐渐变为深红色
            Color redTint = new Color(255, 50, 50); //深红色
            currentTint = Color.Lerp(Color.White, redTint, eased * 0.6f);
        }

        /// <summary>
        /// 更新保持显示状态
        /// </summary>
        private void UpdateHold() {
            //这里常态为白色更好
            currentTint = Color.White;
        }

        /// <summary>
        /// 更新燃烧消失状态
        /// </summary>
        private void UpdateBurningDissolve() {
            burnProgress++;

            if (burnProgress >= BurnDuration) {
                //燃烧完成，关闭立绘
                Active = false;
                currentState = PerformanceState.Inactive;
                SetBlockAdvance(false);
                SetBlockClose(false);
                return;
            }

            float t = burnProgress / BurnDuration;

            //燃烧强度从0快速上升到峰值，然后缓慢下降
            if (t < 0.3f) {
                burnIntensity = CWRUtils.EaseOutCubic(t / 0.3f);
            }
            else {
                burnIntensity = 1f - CWRUtils.EaseInCubic((t - 0.3f) / 0.7f);
            }

            //火焰闪烁效果
            burnFlicker = (float)Math.Sin(timer * 0.25f) * 0.5f + 0.5f;

            //颜色从红色变为橙黄色再变为灰白色(灰烬)
            Color burnOrange = new Color(255, 150, 50);  //橙色
            Color burnYellow = new Color(255, 220, 100); //黄色
            Color ashGray = new Color(200, 200, 200);    //灰白色

            if (t < 0.5f) {
                currentTint = Color.Lerp(new Color(255, 50, 50), burnOrange, t / 0.5f);
            }
            else if (t < 0.8f) {
                float burnT = (t - 0.5f) / 0.3f;
                currentTint = Color.Lerp(burnOrange, burnYellow, burnT);
            }
            else {
                float ashT = (t - 0.8f) / 0.2f;
                currentTint = Color.Lerp(burnYellow, ashGray, ashT);
            }

            //整体透明度随着燃烧逐渐降低
            TargetFade = 1f - CWRUtils.EaseInQuad(t);
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha) {
            Texture2D portrait = ADVAsset.SupCalADV;
            if (portrait == null) {
                return;
            }

            position = ownerDialogue.GetPanelRect().Top() + new Vector2(-portrait.Width, -portrait.Height) * scale;//这样的的位置才合适，真正位于对话框上方

            //计算绘制参数
            Vector2 portraitSize = portrait.Size() * scale;
            Color drawColor = currentTint * alpha;

            //燃烧效果叠加
            if (currentState == PerformanceState.BurningDissolve && burnIntensity > 0.01f) {
                DrawBurningEffect(spriteBatch, position, portraitSize, alpha);
            }
            //绘制主立绘
            spriteBatch.Draw(portrait, position, null, drawColor, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制燃烧效果
        /// </summary>
        private void DrawBurningEffect(SpriteBatch spriteBatch, Vector2 pos, Vector2 size, float alpha) {
            Texture2D portrait = ADVAsset.SupCalADV;
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //火焰颜色叠加
            Color flameCore = new Color(255, 200, 100) * (alpha * burnIntensity * burnFlicker * 0.6f);
            Color flameOuter = new Color(255, 100, 50) * (alpha * burnIntensity * 0.4f);

            //外层火焰光晕
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + dissolveTimer * 2f;
                Vector2 offset = angle.ToRotationVector2() * 8f * burnIntensity;
                spriteBatch.Draw(portrait, pos + offset, null, flameOuter, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            //内层火焰核心
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f - dissolveTimer * 1.5f;
                Vector2 offset = angle.ToRotationVector2() * 4f * burnIntensity;
                spriteBatch.Draw(portrait, pos + offset, null, flameCore, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            //火星粒子效果（简化版）
            int sparkCount = (int)(burnIntensity * 12f);
            for (int i = 0; i < sparkCount; i++) {
                float sparkTimer = (timer + i * 15f) * 0.1f;
                float sparkX = pos.X + (float)Math.Sin(sparkTimer + i) * size.X * 0.5f + size.X * 0.5f;
                float sparkY = pos.Y + size.Y - (sparkTimer % 1f) * size.Y * 1.2f;

                if (sparkY < pos.Y - size.Y * 0.2f) {
                    continue;
                }

                Vector2 sparkPos = new Vector2(sparkX, sparkY);
                float sparkAlpha = 1f - (sparkTimer % 1f);
                Color sparkColor = Color.Lerp(new Color(255, 200, 100), new Color(255, 100, 50), sparkTimer % 1f);
                sparkColor *= alpha * sparkAlpha * burnIntensity * 0.8f;

                float sparkSize = 2f + (float)Math.Sin(sparkTimer * 3f) * 1.5f;
                spriteBatch.Draw(pixel, sparkPos, null, sparkColor, 0f, new Vector2(0.5f, 0.5f), sparkSize, SpriteEffects.None, 0f);
            }

            //底部燃烧发光
            Rectangle bottomGlow = new Rectangle((int)pos.X, (int)(pos.Y + size.Y * 0.7f), (int)size.X, (int)(size.Y * 0.3f));
            Color glowGradient = new Color(255, 150, 50) * (alpha * burnIntensity * 0.3f);
            spriteBatch.Draw(pixel, bottomGlow, new Rectangle(0, 0, 1, 1), glowGradient);
        }

        protected override void OnDeactivate() {
            currentState = PerformanceState.Inactive;
            SetBlockAdvance(false);
            SetBlockClose(false);
        }
    }
}
