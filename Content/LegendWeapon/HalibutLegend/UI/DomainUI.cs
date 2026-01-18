using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    ///<summary>
    ///领域控制面板
    ///</summary>
    internal class DomainUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText";
        public static LocalizedText TitleText;
        public static LocalizedText ExtraEyeTitleText;
        public static LocalizedText CrashedLabelText;
        internal static LocalizedText[] EyeLayerDescriptions = new LocalizedText[11];//1-10
        public static LocalizedText LayerTitleFormat;

        private readonly List<ExtraTooltipParticle> extraEyeTooltipParticles = [];
        private int extraEyeTooltipSpawnTimer = 0;
        private float extraEyeTooltipPulse = 0f;
        private float extraEyeTooltipRotation = 0f;
        private float extraEyeTooltipShock = 0f;

        //操控禁止相关
        private bool isInteractionLocked = false;
        private float lockOverlayAlpha = 0f;
        private float lockPulseTimer = 0f;
        private readonly List<LockParticle> lockParticles = [];
        private float lockIconRotation = 0f;
        private float lockShakeOffset = 0f;
        private int lockShakeTimer = 0;
        private int remainingLockTime = 0;//剩余锁定时间（帧数）
        private float countdownScale = 1f;//倒计时数字缩放动画
        private int lastSecond = -1;//上一秒的值，用于触发动画

        ///<summary>
        ///设置或获取面板是否禁止操控
        ///</summary>
        public bool IsInteractionLocked {
            get => isInteractionLocked;
            set {
                if (isInteractionLocked != value) {
                    isInteractionLocked = value;
                    if (value) {
                        OnInteractionLocked();
                    }
                    else {
                        OnInteractionUnlocked();
                    }
                }
            }
        }

        ///<summary>
        ///锁定时触发的效果
        ///</summary>
        private void OnInteractionLocked() {
            lockShakeTimer = 15;
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = -0.3f });
            //生成锁定粒子效果
            for (int i = 0; i < 20; i++) {
                float angle = i / 20f * MathHelper.TwoPi;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);
                lockParticles.Add(new LockParticle(halibutCenter, velocity, new Color(200, 100, 100)));
            }
        }

        ///<summary>
        ///解锁时触发的效果
        ///</summary>
        private void OnInteractionUnlocked() {
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.2f });
            //生成解锁粒子效果
            for (int i = 0; i < 15; i++) {
                float angle = i / 15f * MathHelper.TwoPi;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1.5f, 2.5f);
                lockParticles.Add(new LockParticle(halibutCenter, velocity, new Color(100, 255, 150)));
            }
            remainingLockTime = 0;
            lastSecond = -1;
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "海域领域");
            ExtraEyeTitleText = this.GetLocalization(nameof(ExtraEyeTitleText), () => "第 十 层");
            CrashedLabelText = this.GetLocalization(nameof(CrashedLabelText), () => "已死机");
            EyeLayerDescriptions[1] = this.GetLocalization("EyeDesc1", () => "初启领域之眼，微弱的潮汐感开始共鸣");
            EyeLayerDescriptions[2] = this.GetLocalization("EyeDesc2", () => "双目同开，水压在周遭缓慢聚集，力量渐显");
            EyeLayerDescriptions[3] = this.GetLocalization("EyeDesc3", () => "三重视界锁定海流，领域开始稳定成型");
            EyeLayerDescriptions[4] = this.GetLocalization("EyeDesc4", () => "第四层共鸣放大，涌动的寒意悄然扩散");
            EyeLayerDescriptions[5] = this.GetLocalization("EyeDesc5", () => "五层交织，环形水旋于脚下成形，给予守护");
            EyeLayerDescriptions[6] = this.GetLocalization("EyeDesc6", () => "第六层脉冲涌现，能量脉络变得清晰可辨");
            EyeLayerDescriptions[7] = this.GetLocalization("EyeDesc7", () => "七眼同辉，潮域对外界的侵蚀性显著增强");
            EyeLayerDescriptions[8] = this.GetLocalization("EyeDesc8", () => "第八层使水压几近凝实，力量几乎到达巅峰");
            EyeLayerDescriptions[9] = this.GetLocalization("EyeDesc9", () => "九层极境，海渊之形完全显现，伟力贯通");
            EyeLayerDescriptions[10] = this.GetLocalization("EyeDesc10", () => "十层无限叠加，神之境界");
            LayerTitleFormat = this.GetLocalization(nameof(LayerTitleFormat), () => "第 {0} 层");
        }

        public static DomainUI Instance => UIHandleLoader.GetUIHandleOfType<DomainUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//手动调用

        //展开控制
        private float expandProgress = 0f;//展开进度（0-1）
        private const float ExpandDuration = 10f;//展开动画持续帧数

        //面板尺寸（使用TooltipPanel的大小）
        private float PanelWidth => TooltipPanel.Width;//214
        private float PanelHeight => TooltipPanel.Height;//206

        //位置相关
        private Vector2 anchorPosition;//锚点位置（动态计算，跟随SkillTooltipPanel）
        private float currentWidth = 0f;//当前宽度（用于从右到左展开动画）
        private float targetWidth = 0f;//目标宽度
        private const float MinWidth = 8f;//最小宽度（完全收起时）

        //九只奈落之眼 + 额外中心第十眼
        internal static List<SeaEyeButton> Eyes => player.GetModPlayer<HalibutSave>().eyes;
        internal static List<SeaEyeButton> ActivationSequence => player.GetModPlayer<HalibutSave>().activationSequence;
        internal const int MaxEyes = 9;//外圈仍然是9
        internal const float EyeOrbitRadius = 75f;//眼睛轨道半径
        private ExtraSeaEyeButton extraEye = new();//第十只中心额外之眼

        //大比目鱼中心图标
        internal Vector2 halibutCenter;
        private const float HalibutSize = 45f;
        private float halibutRotation = 0f;
        private float halibutPulse = 0f;
        private readonly List<HalibutPulseEffect> halibutPulses = [];

        //圆环效果
        private readonly List<DomainRing> rings = [];
        internal int lastActiveEyeCount = 0;

        //粒子效果
        private readonly List<EyeParticle> particles = [];
        private int particleTimer = 0;

        //悬停和交互
        private bool hoveringPanel = false;
        private SeaEyeButton hoveredEye = null;
        private bool extraEyeHovered = false;

        //内容淡入进度
        private float contentFadeProgress = 0f;
        private const float ContentFadeDelay = 0.4f;//内容在展开40%后开始淡入

        //激活动画（眼睛飞向中心并放大）
        private readonly List<EyeActivationAnimation> activationAnimations = [];

        ///<summary>
        ///获取第十眼（额外之眼）是否激活
        ///</summary>
        public bool IsExtraEyeActive => extraEye?.IsActive ?? false;

        ///<summary>
        ///获取当前激活的眼睛数量（即领域层数）- 仅用于UI显示
        ///核心数据请使用 HalibutPlayer.SeaDomainLayers
        ///</summary>
        public int ActiveEyeCount {
            get {
                int baseCount = 0;
                if (ActivationSequence != null) {
                    foreach (var eye in ActivationSequence) {
                        if (eye.IsActive) {
                            baseCount++;
                        }
                    }
                }
                if (extraEye != null && extraEye.IsActive) {
                    baseCount++;
                }
                return baseCount;
            }
        }

        ///<summary>
        ///是否应该显示面板
        ///</summary>
        public static bool ShouldShow => HalibutUIPanel.Instance.Sengs >= 1f;

        /// <summary>
        /// 逻辑更新，用于处理不应受帧率影响的动画和状态更新
        /// </summary>
        public override void LogicUpdate() {
            if (expandProgress < 0.01f) {
                return;
            }

            //更新大比目鱼旋转动画（固定速度，不受帧率影响）
            halibutRotation += 0.005f;
            if (halibutRotation > MathHelper.TwoPi) {
                halibutRotation -= MathHelper.TwoPi;
            }

            //更新锁定状态相关的计时器
            UpdateLockTimers();

            //更新粒子生命周期
            UpdateParticleLifecycles();

            //更新圆环动画
            foreach (var ring in rings) {
                ring.LogicUpdate();
            }

            //更新激活动画
            for (int i = activationAnimations.Count - 1; i >= 0; i--) {
                activationAnimations[i].LogicUpdate();
            }

            //更新脉冲效果
            for (int i = halibutPulses.Count - 1; i >= 0; i--) {
                halibutPulses[i].LogicUpdate();
            }
        }

        /// <summary>
        /// 更新锁定状态相关的计时器（固定频率）
        /// </summary>
        private void UpdateLockTimers() {
            if (isInteractionLocked) {
                lockPulseTimer += 0.05f;
                lockIconRotation += 0.02f;

                if (lockPulseTimer > MathHelper.TwoPi * 10f) {
                    lockPulseTimer -= MathHelper.TwoPi * 10f;
                }
                if (lockIconRotation > MathHelper.TwoPi) {
                    lockIconRotation -= MathHelper.TwoPi;
                }
            }

            if (lockShakeTimer > 0) {
                lockShakeTimer--;
                lockShakeOffset = (float)Math.Sin(lockShakeTimer * 0.8f) * (lockShakeTimer * 0.3f);
            }
            else {
                lockShakeOffset = 0f;
            }
        }

        /// <summary>
        /// 更新粒子生命周期（固定频率）
        /// </summary>
        private void UpdateParticleLifecycles() {
            //更新普通粒子
            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Life++;
                if (particles[i].Life >= particles[i].MaxLife) {
                    particles.RemoveAt(i);
                }
            }

            //更新锁定粒子
            for (int i = lockParticles.Count - 1; i >= 0; i--) {
                lockParticles[i].Life++;
                if (lockParticles[i].Life >= lockParticles[i].MaxLife) {
                    lockParticles.RemoveAt(i);
                }
            }
        }

        public override void Update() {
            if (Eyes.Count == 0) {
                for (int i = 0; i < MaxEyes; i++) {
                    float angle = i / (float)MaxEyes * MathHelper.TwoPi - MathHelper.PiOver2;
                    Eyes.Add(new SeaEyeButton(i, angle));
                }
            }

            //计算锚点位置（动态跟随SkillTooltipPanel）
            Vector2 mainPanelPos = HalibutUIPanel.Instance.DrawPosition;
            Vector2 mainPanelSize = HalibutUIPanel.Instance.Size;
            Vector2 baseAnchor = mainPanelPos + new Vector2(mainPanelSize.X, mainPanelSize.Y / 2);

            //如果SkillTooltipPanel正在显示，锚点需要右移
            if (SkillTooltipPanel.Instance.IsShowing) {
                //获取SkillTooltipPanel的实际宽度
                float skillPanelWidth = SkillTooltipPanel.Instance.Size.X;
                anchorPosition = baseAnchor + new Vector2(skillPanelWidth - 10, 0);//-10重叠
            }
            else {
                anchorPosition = baseAnchor;
            }

            //展开/收起动画
            if (ShouldShow) {
                if (expandProgress < 1f) {
                    expandProgress += 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
            }
            else {
                if (expandProgress > 0f) {
                    expandProgress -= 1f / ExpandDuration;
                    expandProgress = Math.Clamp(expandProgress, 0f, 1f);
                }
            }

            //使用缓动函数
            float easedProgress = ShouldShow ? CWRUtils.EaseOutBack(expandProgress) : CWRUtils.EaseInCubic(expandProgress);

            //计算当前宽度（从右到左展开）
            targetWidth = PanelWidth;
            currentWidth = MinWidth + (targetWidth - MinWidth) * easedProgress;

            //计算位置（从右向左滑出）
            DrawPosition = anchorPosition + new Vector2(-6, -PanelHeight / 2 - 18);
            Size = new Vector2(currentWidth, PanelHeight);

            if (expandProgress < 0.01f) {
                return;//完全收起时不更新
            }

            //更新中心位置（相对于当前实际显示宽度的中心）
            //currentWidth是动画宽度，但实际显示区域是从DrawPosition.X开始的revealWidth
            float revealWidth = PanelWidth * expandProgress;
            halibutCenter = DrawPosition + new Vector2(revealWidth / 2, PanelHeight / 2);

            //检测面板悬停
            Rectangle panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)Size.X, (int)Size.Y);
            hoveringPanel = panelRect.Contains(Main.MouseScreen.ToPoint());
            if (hoveringPanel) {
                player.mouseInterface = true;
            }

            //更新大比目鱼脉动动画（视觉效果，可受帧率影响）
            halibutPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.1f + 0.9f;

            //内容淡入（延迟开始）
            if (expandProgress > ContentFadeDelay && contentFadeProgress < 1f) {
                float adjustedProgress = (expandProgress - ContentFadeDelay) / (1f - ContentFadeDelay);
                contentFadeProgress = Math.Min(contentFadeProgress + 0.1f, adjustedProgress);
            }
            else if (expandProgress <= ContentFadeDelay && contentFadeProgress > 0f) {
                contentFadeProgress -= 0.15f;
                contentFadeProgress = Math.Clamp(contentFadeProgress, 0f, 1f);
            }

            //更新锁定状态动画
            UpdateLockAnimation();

            //更新眼睛
            hoveredEye = null;
            foreach (var eye in Eyes) {
                eye.Update(halibutCenter, EyeOrbitRadius * easedProgress, easedProgress);
                if (eye.IsHovered && hoveringPanel) {
                    hoveredEye = eye;
                }
                //锁定时禁止点击
                if (!isInteractionLocked && eye.IsHovered && Main.mouseLeft && Main.mouseLeftRelease) {
                    HandleEyeToggle(eye);
                }
                else if (isInteractionLocked && eye.IsHovered && Main.mouseLeft && Main.mouseLeftRelease) {
                    //触发锁定反馈
                    TriggerLockedFeedback();
                }
            }

            //第十眼出现条件：外圈9全部激活 且 TheOnlyBornOfAnEra 条件满足
            bool canShowExtra = false;
            if (ActivationSequence.Count >= 9) {
                if (HalibutPlayer.TheOnlyBornOfAnEra()) {
                    canShowExtra = true;
                }
            }

            //如果外圈未满足，强制关闭
            if (!canShowExtra) {
                extraEye.ForceClose();
            }

            extraEye.Update(halibutCenter, canShowExtra, easedProgress);
            extraEyeHovered = extraEye.IsHovered && hoveringPanel;
            //锁定时禁止点击
            if (!isInteractionLocked && extraEyeHovered && Main.mouseLeft && Main.mouseLeftRelease && canShowExtra) {
                extraEye.Toggle();
                SoundEngine.PlaySound(SoundID.MenuTick);
                if (extraEye.IsActive) {
                    halibutPulses.Add(new HalibutPulseEffect(halibutCenter));
                }
            }
            else if (isInteractionLocked && extraEyeHovered && Main.mouseLeft && Main.mouseLeftRelease && canShowExtra) {
                //触发锁定反馈
                TriggerLockedFeedback();
            }

            int currentActiveCount = ActiveEyeCount;
            if (currentActiveCount != lastActiveEyeCount) {
                UpdateRings(currentActiveCount);
                lastActiveEyeCount = currentActiveCount;
            }

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Center = halibutCenter;
                rings[i].Update();//视觉更新
                if (rings[i].ShouldRemove) {
                    rings.RemoveAt(i);
                }
            }

            //粒子位置更新（视觉插值）
            foreach (var particle in particles) {
                particle.Update();
            }

            //生成环境粒子
            if (expandProgress >= 1f && currentActiveCount > 0) {
                particleTimer++;
                if (particleTimer % 15 == 0) {
                    SpawnAmbientParticle();
                }
            }

            for (int i = activationAnimations.Count - 1; i >= 0; i--) {
                activationAnimations[i].Update(halibutCenter);//视觉更新
                if (activationAnimations[i].Finished) {
                    halibutPulses.Add(new HalibutPulseEffect(halibutCenter));
                    activationAnimations.RemoveAt(i);
                }
            }

            for (int i = halibutPulses.Count - 1; i >= 0; i--) {
                halibutPulses[i].Update();//视觉更新
                if (halibutPulses[i].Finished) {
                    halibutPulses.RemoveAt(i);
                }
            }
        }

        ///<summary>
        ///更新锁定动画效果（视觉状态更新，在Update中调用）
        ///</summary>
        private void UpdateLockAnimation() {
            //获取锁定时间
            if (player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                remainingLockTime = halibutPlayer.IsInteractionLockedTime;
            }

            //更新覆盖层透明度（视觉插值）
            if (isInteractionLocked) {
                lockOverlayAlpha = Math.Min(lockOverlayAlpha + 0.08f, 0.65f);

                //倒计时动画
                int currentSecond = (int)Math.Ceiling(remainingLockTime / 60f);
                if (currentSecond != lastSecond && currentSecond > 0) {
                    lastSecond = currentSecond;
                    countdownScale = 1.5f;//触发缩放动画
                    //播放滴答音效
                    if (currentSecond <= 3) {
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.3f });
                    }
                }
                //缩放动画衰减（视觉插值）
                if (countdownScale > 1f) {
                    countdownScale = MathHelper.Lerp(countdownScale, 1f, 0.15f);
                }
            }
            else {
                lockOverlayAlpha = Math.Max(lockOverlayAlpha - 0.12f, 0f);
                countdownScale = 1f;
            }

            //注意：lockShakeTimer、lockPulseTimer、lockIconRotation 的更新已移至 LogicUpdate
            //这里只更新粒子位置（视觉插值）
            foreach (var lockParticle in lockParticles) {
                lockParticle.Position += lockParticle.Velocity;
                lockParticle.Velocity *= 0.95f;
                lockParticle.Rotation += 0.08f;
            }
        }

        ///<summary>
        ///触发锁定反馈（玩家尝试在锁定时操作）
        ///</summary>
        private void TriggerLockedFeedback() {
            lockShakeTimer = 12;
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f, Pitch = -0.5f });
            //生成少量警告粒子
            for (int i = 0; i < 8; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);
                lockParticles.Add(new LockParticle(Main.MouseScreen, velocity, new Color(255, 150, 150)));
            }
        }

        private void HandleEyeToggle(SeaEyeButton eye) {
            SoundEngine.PlaySound(SoundID.MenuTick);

            bool wasActive = eye.IsActive;
            //写在这里提醒自己，别他妈动这个调用顺序，这里用了更新差序的逻辑
            eye.Toggle();

            if (!wasActive && eye.IsActive) {
                if (!ActivationSequence.Contains(eye)) {
                    ActivationSequence.Add(eye);
                    eye.LayerNumber = ActivationSequence.Count;
                }
                activationAnimations.Add(new EyeActivationAnimation(eye.Position, eye.LayerNumber ?? 1));
                SpawnEyeToggleParticles(eye, true);
            }
            else if (wasActive && !eye.IsActive) {
                ActivationSequence.Remove(eye);
                RecalculateLayerNumbers();
                SpawnEyeToggleParticles(eye, false);
            }

            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            if (halibutPlayer.SeaDomainActive) {
                halibutPlayer.OnStartSeaDomain = true;
                halibutPlayer.SeaDomainLayers = ActiveEyeCount;//同步层数
                SeaDomain.Deactivate(player);
            }
            if (halibutPlayer.CloneFishActive) {
                halibutPlayer.OnStartClone = true;
                halibutPlayer.CloneCount = halibutPlayer.SeaDomainLayers;//同步数量
                CloneFish.Deactivate(player);
            }
        }

        private void RecalculateLayerNumbers() {
            for (int i = 0; i < ActivationSequence.Count; i++) {
                ActivationSequence[i].LayerNumber = i + 1;
            }
        }

        internal void UpdateRings(int targetCount) {
            //移除多余的圆环
            while (rings.Count > targetCount) {
                rings.RemoveAt(rings.Count - 1);
            }

            //添加新圆环
            while (rings.Count < targetCount) {
                int index = rings.Count;
                float radius = 30f + index * 12f;
                rings.Add(new DomainRing(halibutCenter, radius, index));
            }
        }

        private void SpawnEyeToggleParticles(SeaEyeButton eye, bool activating) {
            //判断是否死机
            bool isCrashed = false;
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                int crashLevel = halibutPlayer.CrashesLevel();
                isCrashed = eye.LayerNumberDisplay <= crashLevel;
            }

            for (int i = 0; i < 12; i++) {
                float angle = i / 12f * MathHelper.TwoPi;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 4f);

                //根据死机状态使用不同颜色
                Color color;
                if (isCrashed && activating) {
                    color = new Color(255, 100, 100);//红色粒子
                }
                else {
                    color = activating ? new Color(100, 220, 255) : new Color(80, 80, 100);
                }

                particles.Add(new EyeParticle(eye.Position, velocity, color));
            }
        }

        private void SpawnAmbientParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(30f, 70f);
            Vector2 pos = halibutCenter + angle.ToRotationVector2() * radius;
            Vector2 velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
            particles.Add(new EyeParticle(pos, velocity, new Color(120, 200, 255, 200)));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (expandProgress < 0.01f) {
                return;
            }
            float alpha = Math.Min(expandProgress * 2f, 1f);
            DrawPanel(spriteBatch, alpha);
            foreach (var ring in rings) {
                ring.Draw(spriteBatch, alpha);
            }
            DrawConnectionLines(spriteBatch, alpha);
            foreach (var particle in particles) {
                particle.Draw(spriteBatch, alpha);
            }
            foreach (var pulse in halibutPulses) {
                pulse.Draw(spriteBatch, alpha);
            }
            DrawHalibut(spriteBatch, alpha);
            foreach (var eye in Eyes) {
                eye.Draw(spriteBatch, alpha);
            }
            DrawExtraEye(spriteBatch, alpha);
            foreach (var anim in activationAnimations) {
                anim.Draw(spriteBatch, alpha);
            }
            //绘制锁定效果
            if (lockOverlayAlpha > 0.01f) {
                DrawLockOverlay(spriteBatch, alpha);
            }
            //绘制锁定粒子
            foreach (var lockParticle in lockParticles) {
                lockParticle.Draw(spriteBatch, alpha);
            }
            if (expandProgress > 0.8f) {
                //事实证明绘制这个标题不是一个好主意，因为不符合UI风格，并且还显得多余
                //DrawTitle(spriteBatch, alpha);
            }
            if (hoveredEye != null && expandProgress >= 0.4f) {
                DrawEyeTooltip(spriteBatch, hoveredEye, alpha);
            }
            else if (extraEyeHovered && expandProgress >= 0.4f) {
                DrawExtraEyeTooltip(spriteBatch, alpha);
            }
        }

        ///<summary>
        ///绘制锁定覆盖层效果
        ///</summary>
        private void DrawLockOverlay(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //计算面板区域（带震动偏移）
            Vector2 shakeOffset = new Vector2(lockShakeOffset, 0);
            Rectangle panelRect = new Rectangle(
                (int)(DrawPosition.X + shakeOffset.X),
                (int)(DrawPosition.Y + shakeOffset.Y),
                (int)Size.X,
                (int)Size.Y
            );

            //绘制半透明红色覆盖层（脉动效果）
            float pulseValue = (float)Math.Sin(lockPulseTimer * 3f) * 0.15f + 0.35f;
            Color overlayColor = new Color(180, 60, 60) * (lockOverlayAlpha * pulseValue * alpha);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), overlayColor);

            //绘制扫描线效果
            int scanLineCount = 8;
            for (int i = 0; i < scanLineCount; i++) {
                float lineY = panelRect.Y + panelRect.Height / (float)scanLineCount * i;
                float lineOffset = (Main.GlobalTimeWrappedHourly * 2f + i * 0.3f) % 1f * panelRect.Height;
                Rectangle scanLine = new Rectangle(
                    panelRect.X,
                    (int)(lineY + lineOffset) % (panelRect.Y + panelRect.Height),
                    panelRect.Width,
                    1
                );
                if (scanLine.Y >= panelRect.Y && scanLine.Y <= panelRect.Bottom) {
                    Color scanColor = new Color(220, 100, 100) * (lockOverlayAlpha * 0.4f * alpha);
                    spriteBatch.Draw(pixel, scanLine, new Rectangle(0, 0, 1, 1), scanColor);
                }
            }

            //绘制中心锁定图标
            Vector2 lockIconPos = halibutCenter + shakeOffset;
            float iconSize = 32f;
            float iconPulse = (float)Math.Sin(lockPulseTimer * 4f) * 0.2f + 1f;

            //绘制锁定图标外发光
            for (int i = 0; i < 3; i++) {
                float glowSize = iconSize * (1.3f + i * 0.15f) * iconPulse;
                DrawLockIcon(spriteBatch, lockIconPos, glowSize,
                    new Color(200, 80, 80) * (lockOverlayAlpha * (0.3f - i * 0.08f) * alpha),
                    lockIconRotation + i * 0.1f);
            }

            //绘制锁定图标主体
            DrawLockIcon(spriteBatch, lockIconPos, iconSize * iconPulse,
                new Color(255, 120, 120) * (lockOverlayAlpha * alpha),
                lockIconRotation);

            //绘制倒计时
            if (remainingLockTime > 0) {
                DrawLockCountdown(spriteBatch, lockIconPos, iconSize, alpha);
            }

            //绘制警告边框
            DrawWarningBorder(spriteBatch, panelRect, lockOverlayAlpha * alpha);
        }

        ///<summary>
        ///绘制锁定倒计时（环形进度条+数字）
        ///</summary>
        private void DrawLockCountdown(SpriteBatch spriteBatch, Vector2 center, float iconSize, float alpha) {
            if (remainingLockTime <= 0) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float remainingSeconds = remainingLockTime / 60f;
            int displaySeconds = (int)Math.Ceiling(remainingSeconds);

            //计算进度比例（假设最大锁定时间为10秒）
            float maxLockSeconds = 10f;
            float progress = Math.Clamp(remainingSeconds / maxLockSeconds, 0f, 1f);

            //绘制环形进度条
            Vector2 ringCenter = center + new Vector2(0, iconSize * 0.8f);
            float ringRadius = iconSize * 0.65f;
            int segments = 48;
            float startAngle = -MathHelper.PiOver2;//从顶部开始
            float endAngle = startAngle + MathHelper.TwoPi * progress;

            //背景环（暗色）
            DrawCircularRing(spriteBatch, ringCenter, ringRadius, startAngle, startAngle + MathHelper.TwoPi,
                new Color(80, 40, 40) * (lockOverlayAlpha * 0.4f * alpha), 2.5f, segments);

            //进度环（亮色，带脉动）
            float ringPulse = (float)Math.Sin(lockPulseTimer * 5f) * 0.15f + 0.85f;
            Color progressColor = Color.Lerp(new Color(255, 100, 100), new Color(255, 180, 180), ringPulse);
            DrawCircularRing(spriteBatch, ringCenter, ringRadius, startAngle, endAngle,
                progressColor * (lockOverlayAlpha * alpha), 3f, segments);

            //进度环外发光
            DrawCircularRing(spriteBatch, ringCenter, ringRadius + 2f, startAngle, endAngle,
                progressColor * (lockOverlayAlpha * alpha * 0.5f), 1.5f, segments);

            //绘制倒计时数字
            string timeText = displaySeconds.ToString();
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(timeText);
            Vector2 textPos = ringCenter - textSize * countdownScale / 2;

            //数字外发光（脉动）
            float textGlowPulse = (float)Math.Sin(lockPulseTimer * 6f) * 0.3f + 0.7f;
            Color glowColor = new Color(255, 120, 120) * (lockOverlayAlpha * alpha * textGlowPulse);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * (2f * countdownScale);
                Utils.DrawBorderString(spriteBatch, timeText, textPos + offset, glowColor, countdownScale);
            }

            //数字主体
            Color textColor = Color.White * (lockOverlayAlpha * alpha);
            Utils.DrawBorderString(spriteBatch, timeText, textPos, textColor, countdownScale);

            //绘制"秒"字或"s"（小字）
            string unitText = Language.ActiveCulture.LegacyId == (int)GameCulture.CultureName.Chinese ? "秒" : "s";
            Vector2 unitSize = FontAssets.MouseText.Value.MeasureString(unitText) * 0.6f;
            Vector2 unitPos = ringCenter + new Vector2(ringRadius * 0.7f, textSize.Y * countdownScale * 0.3f);

            Utils.DrawBorderString(spriteBatch, unitText, unitPos + new Vector2(1, 1),
                Color.Black * (lockOverlayAlpha * alpha * 0.5f), 0.6f);
            Utils.DrawBorderString(spriteBatch, unitText, unitPos,
                new Color(255, 200, 200) * (lockOverlayAlpha * alpha * 0.8f), 0.6f);

            //剩余时间很短时（<=3秒）添加警告效果
            if (displaySeconds <= 3) {
                float warningPulse = (float)Math.Sin(lockPulseTimer * 10f) * 0.5f + 0.5f;
                //绘制警告光环
                for (int i = 0; i < 3; i++) {
                    float waveRadius = ringRadius + (i + 1) * 8f * warningPulse;
                    Color waveColor = new Color(255, 80, 80) * (lockOverlayAlpha * alpha * (0.4f - i * 0.1f) * warningPulse);
                    DrawCircularRing(spriteBatch, ringCenter, waveRadius, 0, MathHelper.TwoPi,
                        waveColor, 1.5f, 32);
                }
            }
        }

        ///<summary>
        ///绘制环形线段
        ///</summary>
        private static void DrawCircularRing(SpriteBatch spriteBatch, Vector2 center, float radius,
            float startAngle, float endAngle, Color color, float thickness, int segments) {
            if (Math.Abs(endAngle - startAngle) < 0.01f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float angleRange = endAngle - startAngle;
            int actualSegments = Math.Max(3, (int)(segments * Math.Abs(angleRange) / MathHelper.TwoPi));
            float angleStep = angleRange / actualSegments;

            Vector2 prevPoint = center + startAngle.ToRotationVector2() * radius;
            for (int i = 1; i <= actualSegments; i++) {
                float angle = startAngle + angleStep * i;
                Vector2 currentPoint = center + angle.ToRotationVector2() * radius;

                Vector2 diff = currentPoint - prevPoint;
                float length = diff.Length();
                if (length > 0.1f) {
                    float rotation = diff.ToRotation();
                    spriteBatch.Draw(pixel, prevPoint, new Rectangle(0, 0, 1, 1), color,
                        rotation, new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
                }
                prevPoint = currentPoint;
            }
        }

        ///<summary>
        ///绘制锁定图标（简化的锁形状）
        ///</summary>
        private static void DrawLockIcon(SpriteBatch spriteBatch, Vector2 center, float size, Color color, float rotation) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //锁身（矩形）
            Rectangle lockBody = new Rectangle(
                (int)(center.X - size * 0.25f),
                (int)(center.Y - size * 0.1f),
                (int)(size * 0.5f),
                (int)(size * 0.4f)
            );
            spriteBatch.Draw(pixel, lockBody, new Rectangle(0, 0, 1, 1), color);

            //锁孔（小矩形）
            Rectangle lockHole = new Rectangle(
                (int)(center.X - size * 0.08f),
                (int)center.Y,
                (int)(size * 0.16f),
                (int)(size * 0.2f)
            );
            Color holeColor = Color.Black * (color.A / 255f);
            spriteBatch.Draw(pixel, lockHole, new Rectangle(0, 0, 1, 1), holeColor);

            //锁环（半圆弧，用多段线模拟）
            int segments = 12;
            float arcRadius = size * 0.28f;
            for (int i = 0; i <= segments; i++) {
                float angle = MathHelper.Pi + i / (float)segments * MathHelper.Pi;
                Vector2 pos1 = center + new Vector2(0, -size * 0.1f) + angle.ToRotationVector2() * arcRadius;
                if (i > 0) {
                    float prevAngle = MathHelper.Pi + (i - 1) / (float)segments * MathHelper.Pi;
                    Vector2 pos0 = center + new Vector2(0, -size * 0.1f) + prevAngle.ToRotationVector2() * arcRadius;
                    DrawLine(spriteBatch, pos0, pos1, color, size * 0.12f);
                }
            }

            //锁环两侧加粗
            Vector2 leftArc = center + new Vector2(-arcRadius, -size * 0.1f);
            Vector2 rightArc = center + new Vector2(arcRadius, -size * 0.1f);
            DrawLine(spriteBatch, leftArc, lockBody.Location.ToVector2() + new Vector2(0, size * 0.1f), color, size * 0.12f);
            DrawLine(spriteBatch, rightArc, lockBody.Location.ToVector2() + new Vector2(lockBody.Width, size * 0.1f), color, size * 0.12f);
        }

        ///<summary>
        ///绘制警告边框
        ///</summary>
        private static void DrawWarningBorder(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float thickness = 2f;
            float dashLength = 10f;
            float gapLength = 6f;
            float offset = Main.GlobalTimeWrappedHourly * 60f % (dashLength + gapLength);

            Color warningColor = new Color(255, 100, 100) * alpha;

            //上边框
            DrawDashedLine(spriteBatch, new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top),
                warningColor, thickness, dashLength, gapLength, offset);
            //下边框
            DrawDashedLine(spriteBatch, new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom),
                warningColor, thickness, dashLength, gapLength, offset);
            //左边框
            DrawDashedLine(spriteBatch, new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Bottom),
                warningColor, thickness, dashLength, gapLength, offset);
            //右边框
            DrawDashedLine(spriteBatch, new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom),
                warningColor, thickness, dashLength, gapLength, offset);
        }

        ///<summary>
        ///绘制虚线
        ///</summary>
        private static void DrawDashedLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color,
            float thickness, float dashLength, float gapLength, float offset) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 diff = end - start;
            float totalLength = diff.Length();
            if (totalLength < 0.1f) {
                return;
            }
            diff.Normalize();
            float rotation = (float)Math.Atan2(diff.Y, diff.X);
            int segments = Math.Max(1, (int)(totalLength / 10f));
            float currentPos = -offset;

            while (currentPos < totalLength) {
                float dashStart = Math.Max(0, currentPos);
                float dashEnd = Math.Min(totalLength, currentPos + dashLength);
                if (dashEnd > dashStart) {
                    Vector2 drawPos = start + diff * dashStart;
                    float drawLength = dashEnd - dashStart;
                    spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), color,
                        rotation, new Vector2(0, 0.5f), new Vector2(drawLength, thickness), SpriteEffects.None, 0f);
                }
                currentPos += dashLength + gapLength;
            }
        }

        ///<summary>
        ///绘制线段
        ///</summary>
        private static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 0.1f) return;
            float rotation = diff.ToRotation();
            spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), color,
                rotation, new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            //计算源矩形（从左到右展开的裁剪效果，与SkillTooltipPanel一致）
            float revealProgress = expandProgress;
            int revealWidth = (int)(PanelWidth * revealProgress);

            Rectangle sourceRect = new Rectangle(
                0,//从左侧开始显示
                0,
                revealWidth,
                (int)PanelHeight
            );

            Rectangle destRect = new Rectangle(
                (int)DrawPosition.X,//从左侧对齐
                (int)DrawPosition.Y,
                revealWidth,
                (int)PanelHeight
            );

            //绘制阴影
            Rectangle shadowRect = destRect;
            shadowRect.Offset(3, 3);
            Color shadowColor = Color.Black * (alpha * 0.4f);
            spriteBatch.Draw(TooltipPanel, shadowRect, sourceRect, shadowColor);

            //绘制面板主体（带脉动效果）
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.05f + 0.95f;
            Color panelColor = Color.White * (alpha * pulse);
            spriteBatch.Draw(TooltipPanel, destRect, sourceRect, panelColor);

            //绘制边框发光（只在完全展开后）
            if (expandProgress > 0.9f) {
                Color glowColor = Color.Gold with { A = 0 } * (alpha * 0.3f * pulse);
                Rectangle glowRect = destRect;
                glowRect.Inflate(2, 2);
                spriteBatch.Draw(TooltipPanel, glowRect, sourceRect, glowColor);
            }
        }

        private void DrawConnectionLines(SpriteBatch spriteBatch, float alpha) {
            if (ActiveEyeCount == 0 || expandProgress < 0.5f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //获取死机等级
            int crashLevel = 0;
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                crashLevel = halibutPlayer.CrashesLevel();
            }

            foreach (var eye in ActivationSequence) {
                if (!eye.IsActive) {
                    continue;
                }
                Vector2 start = halibutCenter;
                Vector2 end = eye.Position;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + eye.Index * 0.5f) * 1.5f;

                //判断这个眼睛是否死机
                bool isCrashed = eye.LayerNumberDisplay <= crashLevel;

                //根据死机状态使用不同颜色
                Color lineColor;
                if (isCrashed) {
                    lineColor = Color.Lerp(new Color(255, 100, 100), new Color(200, 80, 80),
                        (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + eye.Index) * 0.5f + 0.5f);
                }
                else {
                    lineColor = Color.Lerp(new Color(80, 180, 255), new Color(120, 220, 255),
                        (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + eye.Index) * 0.5f + 0.5f);
                }

                lineColor *= alpha * 0.35f * expandProgress;
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor, rotation, Vector2.Zero, new Vector2(length, 1.5f + wave), SpriteEffects.None, 0f);
            }
            if (extraEye.IsActive) {
                Vector2 start = halibutCenter + new Vector2(0, -HalibutSize * 0.2f);
                Vector2 end = halibutCenter;
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation();
                Color lineColor = Color.Lerp(new Color(150, 200, 255), new Color(220, 240, 255), (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f);
                lineColor *= alpha * 0.4f;
                spriteBatch.Draw(pixel, start, new Rectangle(0, 0, 1, 1), lineColor, rotation, Vector2.Zero, new Vector2(length, 2.2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawHalibut(SpriteBatch spriteBatch, float alpha) {
            if (contentFadeProgress < 0.01f) {
                return;
            }
            Texture2D halibutTex = TextureAssets.Item[HalibutOverride.ID].Value;
            float halibutAlpha = contentFadeProgress * alpha;
            for (int i = 0; i < 2; i++) {
                float glowScale = HalibutSize / halibutTex.Width * (1.2f + i * 0.15f) * halibutPulse;
                Color glowColor = Color.Lerp(new Color(100, 200, 255), new Color(80, 160, 240), i / 2f);
                glowColor *= halibutAlpha * (0.3f - i * 0.1f);
                spriteBatch.Draw(halibutTex, halibutCenter, null, glowColor, halibutRotation + i * 0.1f, halibutTex.Size() / 2, glowScale, SpriteEffects.None, 0f);
            }
            float mainScale = HalibutSize / halibutTex.Width * halibutPulse;
            Color mainColor = Color.White * halibutAlpha;
            spriteBatch.Draw(halibutTex, halibutCenter, null, mainColor, halibutRotation, halibutTex.Size() / 2, mainScale, SpriteEffects.None, 0f);
            if (ActiveEyeCount > 0 && expandProgress >= 1f) {
                string layerText = $"{ActiveEyeCount}";
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(layerText);
                Vector2 textPos = halibutCenter - textSize / 2 + new Vector2(0, HalibutSize * 0.55f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.TwoPi * i / 4;
                    Vector2 offset = angle.ToRotationVector2() * 1.5f;
                    Utils.DrawBorderString(spriteBatch, layerText, textPos + offset, Color.Gold * halibutAlpha * 0.6f, 1f);
                }
                Utils.DrawBorderString(spriteBatch, layerText, textPos, Color.White * halibutAlpha, 1f);
            }
        }

        private void DrawExtraEye(SpriteBatch spriteBatch, float alpha) {
            if (extraEye == null) {
                return;
            }
            bool canShow = ActivationSequence.Count >= 9 && HalibutPlayer.TheOnlyBornOfAnEra();
            if (!canShow) {
                return;
            }
            extraEye.Draw(spriteBatch, halibutCenter, alpha);
        }

        public static string GetDescription(int layer) {
            if (layer >= 1 && layer < EyeLayerDescriptions.Length) {
                var lt = EyeLayerDescriptions[layer];
                if (lt != null) {
                    string value = lt.Value;
                    value = value.Replace("[Halibut_Domain]", CWRKeySystem.Halibut_Domain.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Restart]", CWRKeySystem.Halibut_Restart.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Clone]", CWRKeySystem.Halibut_Clone.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Superposition]", CWRKeySystem.Halibut_Superposition.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Halibut_Teleport]", CWRKeySystem.Halibut_Teleport.ToTooltipString(CWRLocText.Instance.Notbound.Value));
                    value = value.Replace("[Line]", "______________");
                    return value;
                }
            }
            return "Error";
        }

        private void DrawEyeTooltip(SpriteBatch spriteBatch, SeaEyeButton eye, float alpha) {
            //动态尺寸计算，参考 ResurrectionUI 的方式
            int displayLayer = eye.LayerNumberDisplay;
            string title = string.Format(LayerTitleFormat.Value, GetLayerNumeralText(displayLayer));
            string desc = GetDescription(displayLayer);
            float tooltipAlpha = alpha * 0.95f;

            //布局常量
            float minWidth = 250f;
            float maxWidth = 360f;
            float horizontalPadding = 12f; //左侧内边距
            float rightPadding = 14f;       //右侧内边距
            float topPadding = 8f;
            float bottomPadding = 12f;
            float titleExtra = 6f;          //标题下额外空隙
            float dividerSpacing = 6f;      //标题与分割线之间
            float textSpacingTop = 8f;      //正文上间距
            float lineHeight = 18f;         //正文行高

            //初始工作宽度
            float workingWidth = minWidth;
            float contentWidth = workingWidth - horizontalPadding - rightPadding; //有效文字宽度

            //先按最小宽度做一次换行
            string[] lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out int _);

            //测量最长行宽
            float longest = 0f;
            foreach (string l in lines) {
                if (string.IsNullOrWhiteSpace(l)) {
                    continue;
                }
                float w = FontAssets.MouseText.Value.MeasureString(l.TrimEnd('-', ' ')).X;
                if (w > longest) {
                    longest = w;
                }
            }

            //考虑标题和死机标签宽度
            float titleWidth = FontAssets.MouseText.Value.MeasureString(title).X;
            if (eye.IsCrashed) {
                string crashed = CrashedLabelText.Value;
                float crashWidth = FontAssets.MouseText.Value.MeasureString(crashed).X * 0.6f + 26f; //标签宽度预留
                titleWidth += crashWidth;
            }
            longest = Math.Max(longest, titleWidth);

            if (longest > contentWidth) {
                workingWidth = Math.Clamp(longest + horizontalPadding + rightPadding, minWidth, maxWidth);
                contentWidth = workingWidth - horizontalPadding - rightPadding;
                lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out _);
            }

            //统计有效行数
            int drawLines = 0;
            foreach (var l in lines) {
                if (!string.IsNullOrWhiteSpace(l)) {
                    drawLines++;
                }
            }

            //标题高度 (缩放0.85f后的实际高度)
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * 0.85f;
            float dividerHeight = 1.2f;

            //面板高度计算
            float panelHeight = topPadding
                + titleHeight + titleExtra
                + dividerSpacing + dividerHeight
                + textSpacingTop + drawLines * lineHeight
                + bottomPadding;

            //屏幕限制
            float screenHeightLimit = Main.screenHeight - 40f;
            if (panelHeight > screenHeightLimit) {
                panelHeight = screenHeightLimit;
            }

            Vector2 panelSize = new Vector2(workingWidth, panelHeight);

            //定位（鼠标上方）
            Vector2 basePos = MousePosition + new Vector2(18, -panelSize.Y - 8);
            if (basePos.X + panelSize.X > Main.screenWidth - 20) {
                basePos.X = Main.screenWidth - panelSize.X - 20;
            }
            if (basePos.Y < 20) {
                basePos.Y = 20;
            }

            Rectangle panelRect = new Rectangle((int)basePos.X, (int)basePos.Y, (int)panelSize.X, (int)panelSize.Y);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(3, 3);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (tooltipAlpha * 0.5f));

            float openProg = Math.Min(1f, contentFadeProgress * 1.3f);
            Color bgColor = new Color(25, 35, 55) * (tooltipAlpha * 0.92f);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgColor);

            Color borderGlow = Color.CornflowerBlue * (tooltipAlpha * 0.6f * openProg);
            DrawFancyBorder(spriteBatch, panelRect, borderGlow, tooltipAlpha);

            //标题
            Vector2 titlePos = new Vector2(panelRect.X + horizontalPadding, panelRect.Y + topPadding);
            Color titleGlow = Color.Gold * (tooltipAlpha * 0.55f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4;
                Vector2 offset = ang.ToRotationVector2() * 1.25f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow * 0.6f, 0.85f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * tooltipAlpha, 0.85f);

            //死机标签
            if (eye.IsCrashed) {
                string crashed = CrashedLabelText.Value;
                Vector2 crashSize = FontAssets.MouseText.Value.MeasureString(crashed) * 0.6f;
                Vector2 crashPos = new(panelRect.Right - rightPadding - crashSize.X, titlePos.Y + 2);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Vector2 off = ang.ToRotationVector2() * 1.1f;
                    Utils.DrawBorderString(spriteBatch, crashed, crashPos + off, new Color(255, 60, 60) * tooltipAlpha * 0.5f, 0.6f);
                }
                Utils.DrawBorderString(spriteBatch, crashed, crashPos, new Color(255, 120, 120) * tooltipAlpha, 0.6f);
            }

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + titleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - horizontalPadding - rightPadding, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, Color.Gold * tooltipAlpha * 0.8f, Color.Gold * tooltipAlpha * 0.1f, 1.2f);

            //正文
            Vector2 textStart = dividerStart + new Vector2(0, dividerSpacing + textSpacingTop);
            int drawn = 0;
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) {
                    continue;
                }
                string line = lines[i].TrimEnd('-', ' ');
                Vector2 lp = textStart + new Vector2(2, drawn * lineHeight);
                if (lp.Y + (lineHeight - 2f) > panelRect.Bottom - bottomPadding) {
                    break; //高度溢出
                }
                Utils.DrawBorderString(spriteBatch, line, lp + new Vector2(1, 1), Color.Black * tooltipAlpha * 0.5f, 0.7f);
                Utils.DrawBorderString(spriteBatch, line, lp, Color.White * tooltipAlpha, 0.7f);
                drawn++;
            }

            //装饰星星
            float starTime = Main.GlobalTimeWrappedHourly * 3f;
            Vector2 star1 = new Vector2(panelRect.Right - 14, panelRect.Y + 12);
            float s1Alpha = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star1, 4f, Color.Gold * s1Alpha);
            Vector2 star2 = new Vector2(panelRect.Right - 20, panelRect.Bottom - 16);
            float s2Alpha = ((float)Math.Sin(starTime + MathHelper.Pi) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star2, 3f, Color.Gold * s2Alpha);
        }

        private void DrawExtraEyeTooltip(SpriteBatch spriteBatch, float alpha) {
            //动态尺寸 + 炫酷特效
            string title = ExtraEyeTitleText.Value;
            string desc = GetDescription(10);
            float tooltipAlpha = alpha * 0.98f;

            //布局常量
            float minWidth = 260f;
            float maxWidth = 420f;
            float horizontalPadding = 16f;
            float rightPadding = 18f;
            float topPadding = 12f;
            float bottomPadding = 16f;
            float titleExtra = 8f;
            float dividerSpacing = 8f;
            float textSpacingTop = 10f;
            float lineHeight = 18f;
            float titleScale = 0.95f;

            float workingWidth = minWidth;
            float contentWidth = workingWidth - horizontalPadding - rightPadding;

            string[] lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out _);

            float longest = 0f;
            foreach (var l in lines) {
                if (string.IsNullOrWhiteSpace(l)) continue;
                float w = FontAssets.MouseText.Value.MeasureString(l.TrimEnd('-', ' ')).X;
                if (w > longest) longest = w;
            }
            float titleWidth = FontAssets.MouseText.Value.MeasureString(title).X * titleScale;
            longest = Math.Max(longest, titleWidth);
            if (longest > contentWidth) {
                workingWidth = Math.Clamp(longest + horizontalPadding + rightPadding, minWidth, maxWidth);
                contentWidth = workingWidth - horizontalPadding - rightPadding;
                lines = Utils.WordwrapString(desc, FontAssets.MouseText.Value, (int)(contentWidth + 40), 20, out _);
            }
            int drawLines = 0; foreach (var l in lines) if (!string.IsNullOrWhiteSpace(l)) drawLines++;
            float titleHeight = FontAssets.MouseText.Value.MeasureString(title).Y * titleScale;
            float dividerHeight = 1.4f;
            float panelHeight = topPadding + titleHeight + titleExtra + dividerSpacing + dividerHeight + textSpacingTop + drawLines * lineHeight + bottomPadding;
            float screenLimit = Main.screenHeight - 40f; if (panelHeight > screenLimit) panelHeight = screenLimit;
            Vector2 panelSize = new Vector2(workingWidth, panelHeight);

            //位置
            Vector2 basePos = MousePosition + new Vector2(22, -panelSize.Y - 14);
            if (basePos.X + panelSize.X > Main.screenWidth - 16) basePos.X = Main.screenWidth - panelSize.X - 16;
            if (basePos.Y < 16) basePos.Y = 16;
            Rectangle panelRect = new Rectangle((int)basePos.X, (int)basePos.Y, (int)panelSize.X, (int)panelSize.Y);
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //状态更新
            extraEyeTooltipPulse += 0.04f;
            extraEyeTooltipRotation += 0.01f;
            if (extraEyeTooltipShock < 1f) {
                extraEyeTooltipShock += 0.08f;
            }
            float shockEase = 1f - (float)Math.Pow(1f - extraEyeTooltipShock, 3);

            //生成粒子
            extraEyeTooltipSpawnTimer++;
            int targetParticles = 42;
            if (extraEyeTooltipParticles.Count < targetParticles && extraEyeTooltipSpawnTimer % 2 == 0) {
                var center = panelRect.Center.ToVector2();
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float rad = Main.rand.NextFloat(12f, panelRect.Width * 0.45f);
                extraEyeTooltipParticles.Add(new ExtraTooltipParticle(center, ang, rad));
            }
            //更新粒子
            for (int i = extraEyeTooltipParticles.Count - 1; i >= 0; i--) {
                extraEyeTooltipParticles[i].Update();
                if (extraEyeTooltipParticles[i].Dead) extraEyeTooltipParticles.RemoveAt(i);
            }

            //阴影
            Rectangle shadowRect = panelRect; shadowRect.Offset(4, 4);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (tooltipAlpha * 0.55f));

            //背景渐变 (两层脉动)
            Color bgA = new Color(20, 32, 58) * (tooltipAlpha * 0.95f);
            Color bgB = new Color(40, 64, 96) * (tooltipAlpha * 0.25f * (0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f)));
            Color bgColor = new Color(
                (byte)Math.Clamp(bgA.R + bgB.R, 0, 255),
                (byte)Math.Clamp(bgA.G + bgB.G, 0, 255),
                (byte)Math.Clamp(bgA.B + bgB.B, 0, 255),
                (byte)Math.Clamp(bgA.A + bgB.A, 0, 255)
            );
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), bgColor);

            //内部径向辉光 (pulse)
            float radial = (float)Math.Sin(extraEyeTooltipPulse * 2.2f) * 0.5f + 0.5f;
            Rectangle innerGlow = panelRect; innerGlow.Inflate(-4, -4);
            spriteBatch.Draw(pixel, innerGlow, new Rectangle(0, 0, 1, 1), new Color(80, 140, 220) * (tooltipAlpha * 0.07f * radial));

            //外圈脉冲环 (两层旋转渐隐线条)
            float ringAlpha = 0.18f * tooltipAlpha;
            DrawRotatingRing(spriteBatch, panelRect, 0.92f + 0.02f * (float)Math.Sin(extraEyeTooltipPulse * 3f), ringAlpha, 34, 2f, 0.8f, 0f);
            DrawRotatingRing(spriteBatch, panelRect, 0.88f, ringAlpha * 0.8f, 20, 3f, 1.2f, extraEyeTooltipRotation * 1.6f);

            //边框 (动态颜色)
            float edgeWave = (float)Math.Sin(extraEyeTooltipPulse * 2.5f) * 0.5f + 0.5f;
            Color borderGlow = Color.Lerp(new Color(120, 200, 255), new Color(255, 210, 90), edgeWave) * (tooltipAlpha * 0.7f);
            DrawFancyBorder(spriteBatch, panelRect, borderGlow, tooltipAlpha);

            //标题
            Vector2 titlePos = new Vector2(panelRect.X + horizontalPadding, panelRect.Y + topPadding);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4;
                Vector2 offset = ang.ToRotationVector2() * (1.8f + 0.6f * shockEase);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, borderGlow * 0.55f, titleScale);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * tooltipAlpha, titleScale);

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, titleHeight + titleExtra);
            Vector2 dividerEnd = dividerStart + new Vector2(panelSize.X - horizontalPadding - rightPadding, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd, borderGlow * 0.85f, borderGlow * 0.08f, 1.4f);

            //正文
            Vector2 textStart = dividerStart + new Vector2(0, dividerSpacing + textSpacingTop);
            int drawn = 0;
            for (int i = 0; i < lines.Length; i++) {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string line = lines[i].TrimEnd('-', ' ');
                Vector2 lp = textStart + new Vector2(2, drawn * lineHeight);
                if (lp.Y + (lineHeight - 2f) > panelRect.Bottom - bottomPadding) break;
                Utils.DrawBorderString(spriteBatch, line, lp + new Vector2(1, 1), Color.Black * tooltipAlpha * 0.5f, 0.75f);
                Utils.DrawBorderString(spriteBatch, line, lp, new Color(235, 245, 255) * tooltipAlpha, 0.75f);
                drawn++;
            }

            //粒子绘制（在内容之上，边框之下）
            foreach (var p in extraEyeTooltipParticles) {
                p.Draw(spriteBatch, tooltipAlpha);
            }

            //角落星芒
            float starTime = Main.GlobalTimeWrappedHourly * 4f;
            Vector2 star1 = new Vector2(panelRect.Right - 18, panelRect.Y + 14);
            Vector2 star2 = new Vector2(panelRect.Left + 18, panelRect.Bottom - 16);
            float s1Alpha = ((float)Math.Sin(starTime) * 0.5f + 0.5f) * tooltipAlpha;
            float s2Alpha = ((float)Math.Sin(starTime + MathHelper.PiOver2) * 0.5f + 0.5f) * tooltipAlpha;
            DrawStar(spriteBatch, star1, 5f, borderGlow * s1Alpha);
            DrawStar(spriteBatch, star2, 4f, borderGlow * s2Alpha);

            //中心能量脉冲 (轻微)
            float centerPulse = (float)Math.Sin(extraEyeTooltipPulse * 3.2f) * 0.5f + 0.5f;
            Rectangle core = panelRect;
            int shrinkX = (int)(panelRect.Width * 0.45f);
            int shrinkY = (int)(panelRect.Height * 0.55f);
            core.Inflate(-shrinkX, -shrinkY);
            if (core.Width > 2 && core.Height > 2) {
                spriteBatch.Draw(pixel, core, new Rectangle(0, 0, 1, 1), new Color(120, 210, 255) * (tooltipAlpha * 0.07f * centerPulse));
            }
        }

        private static void DrawRotatingRing(SpriteBatch sb, Rectangle rect, float radiusFactor, float alpha, int segments, float thickness, float distortion, float rotationOffset) {
            if (alpha <= 0f) return;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 center = rect.Center.ToVector2();
            float baseRadius = MathF.Min(rect.Width, rect.Height) * 0.5f * radiusFactor;
            float time = Main.GlobalTimeWrappedHourly + rotationOffset;
            float step = MathHelper.TwoPi / segments;
            Vector2 prev = center + (0f + time).ToRotationVector2() * baseRadius;
            for (int i = 1; i <= segments; i++) {
                float ang = i * step + time;
                float noise = (float)Math.Sin(ang * 3f + time * 4f) * distortion;
                float r = baseRadius + noise;
                Vector2 cur = center + ang.ToRotationVector2() * r;
                Vector2 diff = cur - prev; float len = diff.Length(); if (len > 0.01f) {
                    float rot = diff.ToRotation();
                    Color col = new Color(180, 230, 255) * (alpha * (0.6f + 0.4f * (float)Math.Sin(ang * 2f + time * 5f)));
                    sb.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), col, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                }
                prev = cur;
            }
        }

        private class ExtraTooltipParticle
        {
            private Vector2 center;
            private float angle;
            private float radius;
            private float speed;
            private float radialDrift;
            private float alpha;
            private float scale;
            private int life;
            private int maxLife;
            public bool Dead => alpha <= 0f || life > maxLife;
            public ExtraTooltipParticle(Vector2 c, float ang, float r) {
                center = c;
                angle = ang;
                radius = r;
                speed = Main.rand.NextFloat(0.01f, 0.035f) * (Main.rand.NextBool() ? 1f : -1f);
                radialDrift = Main.rand.NextFloat(-0.05f, 0.05f);
                alpha = 0f;
                scale = Main.rand.NextFloat(1.2f, 2.4f);
                life = 0;
                maxLife = Main.rand.Next(90, 160);
            }
            public void Update() {
                life++;
                angle += speed;
                radius += radialDrift;
                if (radius < 6f) radius = 6f;
                if (life < 20) alpha = MathHelper.Lerp(alpha, 1f, 0.15f);
                else if (life > maxLife - 25) alpha *= 0.9f;
                else alpha *= 0.995f;
            }
            public void Draw(SpriteBatch sb, float globalAlpha) {
                if (Dead) return;
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                Color col = Color.Lerp(new Color(120, 200, 255), new Color(255, 220, 140), (float)Math.Sin(angle * 2f) * 0.5f + 0.5f) * (alpha * globalAlpha * 0.6f);
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col, angle, new Vector2(0.5f, 0.5f), new Vector2(scale * 3f, scale), SpriteEffects.None, 0f);
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), col * 0.5f, angle + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale * 2f, scale * 0.6f), SpriteEffects.None, 0f);
            }
        }

        private static string GetLayerNumeralText(int i) {
            if (Language.ActiveCulture.LegacyId != (int)GameCulture.CultureName.Chinese) {
                return i switch {
                    1 => "I",
                    2 => "II",
                    3 => "III",
                    4 => "IV",
                    5 => "V",
                    6 => "VI",
                    7 => "VII",
                    8 => "VIII",
                    9 => "IX",
                    10 => "X",
                    _ => i.ToString()
                };
            }
            return i switch {
                1 => "一",
                2 => "二",
                3 => "三",
                4 => "四",
                5 => "五",
                6 => "六",
                7 => "七",
                8 => "八",
                9 => "九",
                10 => "十",
                _ => i.ToString()
            };
        }

        private static void DrawFancyBorder(SpriteBatch spriteBatch, Rectangle rect, Color glow, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle top = new Rectangle(rect.X, rect.Y, rect.Width, 1);
            Rectangle bottom = new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1);
            Rectangle left = new Rectangle(rect.X, rect.Y, 1, rect.Height);
            Rectangle right = new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height);
            spriteBatch.Draw(pixel, top, new Rectangle(0, 0, 1, 1), glow);
            spriteBatch.Draw(pixel, bottom, new Rectangle(0, 0, 1, 1), glow * 0.8f);
            spriteBatch.Draw(pixel, left, new Rectangle(0, 0, 1, 1), glow * 0.9f);
            spriteBatch.Draw(pixel, right, new Rectangle(0, 0, 1, 1), glow * 0.9f);
            Color corner = Color.White * (alpha * 0.6f);
            DrawCorner(spriteBatch, new Vector2(rect.Left, rect.Top), corner, 0f);
            DrawCorner(spriteBatch, new Vector2(rect.Right, rect.Top), corner, MathHelper.PiOver2);
            DrawCorner(spriteBatch, new Vector2(rect.Right, rect.Bottom), corner, MathHelper.Pi);
            DrawCorner(spriteBatch, new Vector2(rect.Left, rect.Bottom), corner, MathHelper.Pi + MathHelper.PiOver2);
        }

        private static void DrawCorner(SpriteBatch spriteBatch, Vector2 pos, Color color, float rot) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            for (int i = 0; i < 3; i++) {
                float len = 6 - i * 2;
                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * (0.9f - i * 0.3f), rot, new Vector2(0, 0.5f), new Vector2(len, 1f), SpriteEffects.None, 0f);
            }
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private void DrawStar(SpriteBatch spriteBatch, Vector2 position, float size, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
            spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), color * 0.7f, -MathHelper.PiOver4, new Vector2(0.5f, 0.5f), new Vector2(size * 0.7f, size * 0.2f), SpriteEffects.None, 0);
        }

        ///<summary>
        ///锁定粒子类
        ///</summary>
        private class LockParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public Color Color;

            public LockParticle(Vector2 pos, Vector2 vel, Color color) {
                Position = pos;
                Velocity = vel;
                Life = 0;
                MaxLife = Main.rand.Next(30, 50);
                Scale = Main.rand.NextFloat(0.6f, 1.2f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                Color = color;
            }

            public void Update() {
                //注意：Life的增加已移至DomainUI.LogicUpdate中的UpdateParticleLifecycles
                Position += Velocity;
                Velocity *= 0.95f;
                Rotation += 0.08f;
            }

            public void Draw(SpriteBatch spriteBatch, float panelAlpha) {
                float lifeProgress = Life / MaxLife;
                float fadeAlpha = 1f - lifeProgress;
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Color drawColor = Color * (fadeAlpha * panelAlpha);
                spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1), drawColor,
                    Rotation, new Vector2(0.5f, 0.5f), new Vector2(Scale * 4f, Scale * 4f), SpriteEffects.None, 0f);
            }
        }
    }
}
