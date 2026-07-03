using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniDomains
{
    /// <summary>翻转子阶段</summary>
    public enum OniFlipStage : byte
    {
        None,
        /// <summary>死寂：风停、花瓣冻结、音乐掐掉</summary>
        PreSilence,
        /// <summary>负片闪 + 全屏刀痕</summary>
        Flash,
        /// <summary>纸层剥落</summary>
        Peel,
        /// <summary>落定</summary>
        Settle
    }

    /// <summary>鬼域每玩家状态机，仅本地玩家实际推进</summary>
    public class OniDomainPlayer : ModPlayer
    {
        public OniDomainPhase Phase { get; private set; } = OniDomainPhase.Closed;

        /// <summary>当前调色世界，翻转在捕获帧切换</summary>
        public bool WorldIsUra { get; private set; }

        /// <summary>当前阶段帧计数</summary>
        public int PhaseTimer { get; private set; }

        /// <summary>着色器累计时间（秒）</summary>
        public float EffectTime { get; private set; }

        /// <summary>墨水覆盖进度 0~1，Opening 涨潮 Closing 退潮，稳态 1</summary>
        public float SpreadProgress { get; private set; }

        /// <summary>开域裂口世界坐标，墨水扩散原点</summary>
        public Vector2 SlashWorldPos { get; private set; }

        /// <summary>开域白线强度 0~1</summary>
        public float SlashLineIntensity { get; private set; }

        /// <summary>里世界平滑系数 0~1，驱动光照/天空/灯笼</summary>
        public float UraSmooth { get; private set; }

        /// <summary>表世界错位帧脉冲 0~1，数帧内衰减</summary>
        public float AnomalyPulse { get; private set; }

        //====== 翻转专用 ======
        public OniFlipStage FlipStage { get; private set; } = OniFlipStage.None;
        public bool FlipToUra { get; private set; }
        /// <summary>负片闪强度 0~1</summary>
        public float NegativeFlash { get; private set; }
        /// <summary>纸层剥落进度 0~1</summary>
        public float PeelProgress { get; private set; }
        /// <summary>全屏刀痕角度（弧度，屏幕空间）</summary>
        public float FlipSlashAngle { get; private set; }
        /// <summary>渲染线待办：捕获当前帧作纸层</summary>
        public bool PendingPaperCapture { get; internal set; }
        /// <summary>纸层内容有效（捕获成功且分辨率未变）</summary>
        public bool PaperValid { get; internal set; }

        private int flipStageTimer;
        private int preSilenceDuration;
        private long lastCommandFrame = -1;

        //WorldFreezeSystem reason 标签
        private const string FreezeReason = "OniDomainFlip";
        //本次翻转是否由本机挂了时停
        private bool flipFreezeHeld;

        //环境音计时
        private int ambienceTimer;
        //错位帧计时
        private int anomalyTimer;

        /// <summary>域是否处于任意激活阶段（含开合过渡）</summary>
        public bool AnyActive => Phase != OniDomainPhase.Closed;

        /// <summary>调色是否需要执行（含收尾淡出）</summary>
        public bool GradeVisible => AnyActive;

        //====== 对外命令 ======

        internal bool OpenDomain() {
            if (Phase != OniDomainPhase.Closed || !ConsumeCommandGate()) {
                return false;
            }
            Phase = OniDomainPhase.Opening;
            PhaseTimer = 0;
            WorldIsUra = false;
            SpreadProgress = 0f;
            SlashLineIntensity = 0f;
            SlashWorldPos = Player.Center + new Vector2(0f, -120f);
            anomalyTimer = SetAnomalyInterval();
            ambienceTimer = 600;

            if (IsLocalVisual) {
                //世界被划开的第一声：轻的金属泛音 + 极低的回响
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.42f, Pitch = -0.35f, MaxInstances = 1 }, Player.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.30f, Pitch = -0.9f, MaxInstances = 1 }, Player.Center);
            }
            return true;
        }

        internal bool CloseDomain() {
            if (Phase == OniDomainPhase.Closed || Phase == OniDomainPhase.Closing || Phase == OniDomainPhase.Flipping) {
                return false;
            }
            if (!ConsumeCommandGate()) {
                return false;
            }
            Phase = OniDomainPhase.Closing;
            PhaseTimer = 0;
            FlipStage = OniFlipStage.None;
            NegativeFlash = 0f;
            PeelProgress = 0f;
            PaperValid = false;
            //退潮锚点重锚到玩家当前位置，开域后可能已走远
            SlashWorldPos = Player.Center + new Vector2(0f, -120f);
            OniDomainDeco.NotifyClosing();

            if (IsLocalVisual) {
                //墨水退潮
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.45f, MaxInstances = 1 }, Player.Center);
            }
            return true;
        }

        internal bool FlipDomain() {
            if (Phase != OniDomainPhase.Omote && Phase != OniDomainPhase.Ura) {
                return false;
            }
            if (!ConsumeCommandGate()) {
                return false;
            }
            FlipToUra = !WorldIsUra;
            Phase = OniDomainPhase.Flipping;
            PhaseTimer = 0;
            FlipStage = OniFlipStage.PreSilence;
            flipStageTimer = 0;
            preSilenceDuration = FlipToUra ? OniDomain.PreSilenceToUra : OniDomain.PreSilenceToOmote;
            NegativeFlash = 0f;
            PeelProgress = 0f;
            //斜向刀痕，围绕 -35° 随机摆动
            FlipSlashAngle = MathHelper.ToRadians(-35f) + Main.rand.NextFloat(-0.22f, 0.22f);
            OniDomainDeco.NotifyFreeze();

            //翻转仪式全程时停：世界屏息，纸层揭开后恢复。多人下静态快照体系会失同步，单人才挂
            if (VaultUtils.isSinglePlayer && Player.whoAmI == Main.myPlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
                flipFreezeHeld = true;
                if (Main.LocalPlayer.Alives()) {
                    //预填飞行时间，防首次进入快照被零值覆盖
                    WorldFreezePlayer freezePlayer = Main.LocalPlayer.GetModPlayer<WorldFreezePlayer>();
                    freezePlayer.frozenWingTime = Main.LocalPlayer.wingTime;
                    freezePlayer.frozenRocketTime = Main.LocalPlayer.rocketTime;
                }
            }
            return true;
        }

        private void ReleaseFlipFreeze() {
            if (!flipFreezeHeld) {
                return;
            }
            WorldFreezeSystem.Deactivate(FreezeReason);
            flipFreezeHeld = false;
        }

        //同帧防重入
        private bool ConsumeCommandGate() {
            long frame = (long)Main.GameUpdateCount;
            if (lastCommandFrame == frame) {
                return false;
            }
            lastCommandFrame = frame;
            return true;
        }

        private bool IsLocalVisual => !Main.dedServ && Player.whoAmI == Main.myPlayer;

        //====== 状态机推进，仅本地玩家由 OniDomainSystem 调用 ======

        internal void UpdateLocal() {
            if (Phase == OniDomainPhase.Closed) {
                UraSmooth = MathHelper.Lerp(UraSmooth, 0f, 0.05f);
                if (UraSmooth < 0.003f) UraSmooth = 0f;
                return;
            }

            EffectTime += 1f / 60f;
            PhaseTimer++;

            switch (Phase) {
                case OniDomainPhase.Opening: UpdateOpening(); break;
                case OniDomainPhase.Omote: UpdateOmote(); break;
                case OniDomainPhase.Flipping: UpdateFlipping(); break;
                case OniDomainPhase.Ura: UpdateUra(); break;
                case OniDomainPhase.Closing: UpdateClosing(); break;
            }

            UpdateUraSmooth();
            UpdateMusicCap();

            if (AnomalyPulse > 0f) {
                AnomalyPulse = MathF.Max(AnomalyPulse - 0.25f, 0f);
            }
        }

        private void UpdateOpening() {
            if (PhaseTimer <= OniDomain.SlashRevealFrames) {
                SlashLineIntensity = PhaseTimer / (float)OniDomain.SlashRevealFrames;
                if (PhaseTimer == OniDomain.SlashRevealFrames && IsLocalVisual) {
                    //墨从裂口渗出
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 1 }, Player.Center);
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.22f, Pitch = -0.85f, MaxInstances = 1 }, Player.Center);
                }
                return;
            }

            int spreadT = PhaseTimer - OniDomain.SlashRevealFrames;
            SpreadProgress = MathHelper.Clamp(spreadT / (float)OniDomain.OpenSpreadFrames, 0f, 1f);
            //白线在墨水铺开时熄灭
            SlashLineIntensity = MathHelper.Clamp(1f - spreadT / 40f, 0f, 1f);

            if (SpreadProgress >= 1f) {
                Phase = OniDomainPhase.Omote;
                PhaseTimer = 0;
                if (IsLocalVisual) {
                    //落定风铃
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.35f, Pitch = 0.15f, MaxInstances = 1 }, Player.Center);
                }
            }
        }

        private void UpdateOmote() {
            SpreadProgress = 1f;
            SlashLineIntensity = 0f;

            //低频错位帧
            if (--anomalyTimer <= 0) {
                AnomalyPulse = 1f;
                anomalyTimer = SetAnomalyInterval();
            }

            //偶发远处风铃
            if (--ambienceTimer <= 0) {
                ambienceTimer = Main.rand.Next(720, 1200);
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.16f, Pitch = 0.3f, MaxInstances = 1 }, Player.Center);
                }
            }
        }

        private void UpdateFlipping() {
            SpreadProgress = 1f;
            flipStageTimer++;

            switch (FlipStage) {
                case OniFlipStage.PreSilence:
                    //倒数第 12 帧：死寂中唯一一声风铃
                    if (flipStageTimer == preSilenceDuration - 12 && FlipToUra && IsLocalVisual) {
                        SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.5f, Pitch = 0.45f, MaxInstances = 1 }, Player.Center);
                    }
                    if (flipStageTimer >= preSilenceDuration) {
                        FlipStage = OniFlipStage.Flash;
                        flipStageTimer = 0;
                        NegativeFlash = 1f;
                        if (IsLocalVisual) {
                            //斩 + 太鼓闷击
                            SoundEngine.PlaySound(CWRSound.SwiftSlice with { Volume = 0.75f, Pitch = -0.1f, MaxInstances = 1 }, Player.Center);
                            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.85f, Pitch = -0.7f, MaxInstances = 1 }, Player.Center);
                            Player.CWR().GetScreenShake(6f);
                        }
                    }
                    break;

                case OniFlipStage.Flash:
                    NegativeFlash = MathHelper.Clamp(1f - flipStageTimer / (float)OniDomain.FlashFrames, 0f, 1f);
                    if (flipStageTimer >= OniDomain.FlashFrames) {
                        //捕获旧世界画面作纸层，随后调色切至新世界
                        PendingPaperCapture = true;
                        WorldIsUra = FlipToUra;
                        FlipStage = OniFlipStage.Peel;
                        flipStageTimer = 0;
                        PeelProgress = 0f;
                        OniDomainDeco.NotifyPeelStart(FlipToUra);
                        if (IsLocalVisual) {
                            //纸帛撕裂感
                            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.75f, MaxInstances = 1 }, Player.Center);
                        }
                    }
                    break;

                case OniFlipStage.Peel:
                    PeelProgress = MathHelper.Clamp(flipStageTimer / (float)OniDomain.PeelFrames, 0f, 1f);
                    if (flipStageTimer >= OniDomain.PeelFrames) {
                        FlipStage = OniFlipStage.Settle;
                        flipStageTimer = 0;
                        PaperValid = false;
                        //纸层落尽，新世界开始呼吸
                        ReleaseFlipFreeze();
                    }
                    break;

                case OniFlipStage.Settle:
                    if (flipStageTimer >= OniDomain.SettleFrames) {
                        Phase = WorldIsUra ? OniDomainPhase.Ura : OniDomainPhase.Omote;
                        PhaseTimer = 0;
                        FlipStage = OniFlipStage.None;
                        ambienceTimer = Main.rand.Next(300, 600);
                        anomalyTimer = SetAnomalyInterval();
                    }
                    break;
            }
        }

        private void UpdateUra() {
            SpreadProgress = 1f;

            //远处太鼓心跳
            if (--ambienceTimer <= 0) {
                ambienceTimer = Main.rand.Next(480, 780);
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.22f, Pitch = -0.9f, MaxInstances = 1 }, Player.Center);
                }
            }
        }

        private void UpdateClosing() {
            SpreadProgress = MathHelper.Clamp(1f - PhaseTimer / (float)OniDomain.CloseFrames, 0f, 1f);

            //墨水退回裂口，白线短暂重现再熄灭
            float tail = 1f - SpreadProgress;
            SlashLineIntensity = tail > 0.75f ? MathHelper.Clamp((1f - tail) * 4f, 0f, 1f) : 0f;

            if (PhaseTimer >= OniDomain.CloseFrames) {
                Phase = OniDomainPhase.Closed;
                PhaseTimer = 0;
                WorldIsUra = false;
                SpreadProgress = 0f;
                SlashLineIntensity = 0f;
                PaperValid = false;
                if (IsLocalVisual) {
                    //归鞘咔 + 尾铃
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 1 }, Player.Center);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.28f, Pitch = 0.2f, MaxInstances = 1 }, Player.Center);
                }
            }
        }

        private void UpdateUraSmooth() {
            float target = 0f;
            if (WorldIsUra && Phase != OniDomainPhase.Closing) {
                target = 1f;
            }
            else if (WorldIsUra && Phase == OniDomainPhase.Closing) {
                //收域时跟随墨水退潮回明
                target = SpreadProgress;
            }
            float rate = target > UraSmooth ? 0.03f : 0.035f;
            UraSmooth = MathHelper.Lerp(UraSmooth, target, rate);
            if (UraSmooth < 0.003f && target <= 0f) UraSmooth = 0f;
        }

        //各阶段压低音乐；不直接归零由引擎自然回升
        private void UpdateMusicCap() {
            if (!IsLocalVisual || Main.gameMenu) {
                return;
            }
            float cap = 1f;
            switch (Phase) {
                case OniDomainPhase.Opening:
                    cap = MathHelper.Lerp(1f, 0.4f, SpreadProgress);
                    break;
                case OniDomainPhase.Omote:
                    cap = 0.4f;
                    break;
                case OniDomainPhase.Flipping:
                    cap = FlipStage == OniFlipStage.PreSilence
                        ? MathHelper.Lerp(0.4f, 0f, flipStageTimer / (float)Math.Max(preSilenceDuration - 20, 1))
                        : 0.08f;
                    break;
                case OniDomainPhase.Ura:
                    cap = 0.15f;
                    break;
                case OniDomainPhase.Closing:
                    cap = MathHelper.Lerp(1f, 0.4f, SpreadProgress);
                    break;
            }
            if (cap >= 1f) {
                return;
            }
            int music = Main.curMusic;
            if (music >= 0 && music < Main.musicFade.Length && Main.musicFade[music] > cap) {
                Main.musicFade[music] = MathHelper.Lerp(Main.musicFade[music], cap, 0.2f);
            }
        }

        private static int SetAnomalyInterval() => Main.rand.Next(420, 900);

        internal void ResetDomain() {
            ReleaseFlipFreeze();
            Phase = OniDomainPhase.Closed;
            PhaseTimer = 0;
            WorldIsUra = false;
            EffectTime = 0f;
            SpreadProgress = 0f;
            SlashLineIntensity = 0f;
            UraSmooth = 0f;
            AnomalyPulse = 0f;
            FlipStage = OniFlipStage.None;
            NegativeFlash = 0f;
            PeelProgress = 0f;
            PendingPaperCapture = false;
            PaperValid = false;
            flipStageTimer = 0;
            lastCommandFrame = -1;
        }
    }
}
