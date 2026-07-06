using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains
{
    /// <summary>翻转子阶段</summary>
    public enum OniFlipStage : byte
    {
        None,
        /// <summary>死寂：风停、花瓣冻结、音乐掐掉</summary>
        PreSilence,
        /// <summary>负片闪 + 全屏刀痕 + 日月化眼</summary>
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

        /// <summary>墨水覆盖进度 0~1，Opening 爆扩 Closing 吸回，稳态 1</summary>
        public float SpreadProgress { get; private set; }

        /// <summary>里世界平滑系数 0~1，驱动光照/滤镜</summary>
        public float UraSmooth { get; private set; }

        /// <summary>表世界错位帧脉冲 0~1，数帧内衰减</summary>
        public float AnomalyPulse { get; private set; }

        //====== 鬼眼 ======
        /// <summary>眼睛世界坐标，开/收域锚点兼墨水扩散原点</summary>
        public Vector2 EyeWorldPos { get; private set; }
        /// <summary>眼睛整体可见度 0~1</summary>
        public float EyeIntensity { get; private set; }
        /// <summary>睁眼程度 0闭~1全开</summary>
        public float EyeOpenAmount { get; private set; }
        /// <summary>勾玉环累计旋转（弧度）</summary>
        public float EyeSpin { get; private set; }
        /// <summary>虹膜爆闪 0~1</summary>
        public float EyeFlash { get; private set; }
        /// <summary>消散进度 0~1</summary>
        public float EyeDissolve { get; private set; }

        public bool EyeVisible => EyeIntensity > 0.003f;

        //====== 翻转专用 ======
        public OniFlipStage FlipStage { get; private set; } = OniFlipStage.None;
        public bool FlipToUra { get; private set; }
        /// <summary>负片闪强度 0~1</summary>
        public float NegativeFlash { get; private set; }
        /// <summary>纸层剥落进度 0~1</summary>
        public float PeelProgress { get; private set; }
        /// <summary>全屏刀痕角度（弧度，屏幕空间），每次随机</summary>
        public float FlipSlashAngle { get; private set; }
        /// <summary>两半滑移不对称占比 0.32~0.68</summary>
        public float PeelBias { get; private set; } = 0.5f;
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
        //收域阖眼音效只放一次
        private bool closeClickPlayed;

        /// <summary>域是否处于任意激活阶段（含开合过渡）</summary>
        public bool AnyActive => Phase != OniDomainPhase.Closed;

        /// <summary>调色是否需要执行</summary>
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
            EyeWorldPos = Player.Center + new Vector2(0f, -150f);
            EyeIntensity = 0f;
            EyeOpenAmount = 0f;
            EyeSpin = Main.rand.NextFloat(MathHelper.TwoPi);
            EyeFlash = 0f;
            EyeDissolve = 0f;
            anomalyTimer = SetAnomalyInterval();
            ambienceTimer = 600;

            if (IsLocalVisual) {
                //低鸣，有什么东西在头顶成形
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.32f, Pitch = -0.9f, MaxInstances = 1 }, Player.Center);
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
            closeClickPlayed = false;
            //吸回锚点重锚到玩家当前位置上方
            EyeWorldPos = Player.Center + new Vector2(0f, -150f);
            EyeIntensity = 0f;
            EyeOpenAmount = 1f;
            EyeDissolve = 0f;
            OniDomainDeco.NotifyClosing();
            //收域：过去归还给过去
            OniOmokage.BurnAll();

            if (IsLocalVisual) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.35f, Pitch = -0.7f, MaxInstances = 1 }, Player.Center);
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
            //刀痕完全随机：左右倾向与陡缓都掷骰
            float lean = Main.rand.NextBool() ? 1f : -1f;
            FlipSlashAngle = lean * Main.rand.NextFloat(0.35f, 1.22f);
            //两半不对称滑移
            PeelBias = Main.rand.NextFloat(0.32f, 0.68f);
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
            if (EyeFlash > 0f) {
                EyeFlash = MathF.Max(EyeFlash - 0.16f, 0f);
            }
        }

        //鬼眼开域：浮现→睁眼→勾玉狂旋→爆域（圈内即表世界）
        private void UpdateOpening() {
            int t = PhaseTimer;
            int tOpenEnd = OniDomain.EyeEmergeFrames + OniDomain.EyeOpenFrames;
            int tBurst = tOpenEnd + OniDomain.EyeBurstFrames;

            if (t <= OniDomain.EyeEmergeFrames) {
                //浮现：闭眼轮廓渐显微颤，灵体向眼汇聚
                EyeIntensity = t / (float)OniDomain.EyeEmergeFrames;
                EyeOpenAmount = 0.025f + 0.02f * MathF.Sin(t * 0.55f);
                if (IsLocalVisual) {
                    if (t == 6 || t == 20) {
                        //心跳
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.25f, Pitch = -0.95f, MaxInstances = 2 }, Player.Center);
                    }
                    if (t % 2 == 0) {
                        OniDomainDeco.SpawnEyeConverge(EyeWorldPos, 2);
                    }
                }
                return;
            }

            if (t <= tOpenEnd) {
                //睁眼：数帧内猛然撑开
                float f = (t - OniDomain.EyeEmergeFrames) / (float)OniDomain.EyeOpenFrames;
                EyeIntensity = 1f;
                EyeOpenAmount = f * f * (3f - 2f * f);
                EyeSpin += 0.05f;
                if (t == OniDomain.EyeEmergeFrames + 1 && IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 1 }, Player.Center);
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = -0.55f, MaxInstances = 1 }, Player.Center);
                    Player.CWR().GetScreenShake(3f);
                }
                return;
            }

            if (t <= tBurst) {
                //勾玉加速狂旋
                float f = (t - tOpenEnd) / (float)OniDomain.EyeBurstFrames;
                EyeOpenAmount = 1f;
                EyeSpin += MathHelper.Lerp(0.08f, 0.5f, f * f);
                if (t == tBurst) {
                    //爆域
                    EyeFlash = 1f;
                    if (IsLocalVisual) {
                        SoundEngine.PlaySound(CWRSound.Thunder with { Volume = 0.55f, Pitch = -0.25f, MaxInstances = 1 }, Player.Center);
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.7f, MaxInstances = 1 }, Player.Center);
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 1 }, Player.Center);
                        Player.CWR().GetScreenShake(7f);
                    }
                }
                return;
            }

            //墨浪爆扩：缓出曲线前 0.3s 走完七成屏幕
            //眼睛保持半实体悬在天上看着你，消散大头留给 Omote 里的余韵衰减
            int st = t - tBurst;
            float raw = MathHelper.Clamp(st / (float)OniDomain.OpenSpreadFrames, 0f, 1f);
            float inv = 1f - raw;
            SpreadProgress = 1f - inv * inv * inv;
            EyeDissolve = raw * 0.55f;
            EyeIntensity = 1f - raw * 0.6f;
            EyeSpin += MathHelper.Lerp(0.5f, 0.06f, raw);
            if (IsLocalVisual && st % 3 == 0 && raw < 0.85f) {
                OniDomainDeco.SpawnEyeScatter(EyeWorldPos, 2);
            }

            if (raw >= 1f) {
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
            DecayEyeLeftover();

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
            DecayEyeLeftover();
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
                        //快门：入里瞬间把屏内敌人的"过去"钉成面影；回表则全部烧散
                        if (FlipToUra) {
                            if (OniOmokage.AutoShutterOnFlip) {
                                OniOmokage.ImprintVisible();
                            }
                        }
                        else {
                            OniOmokage.BurnAll();
                        }
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
            DecayEyeLeftover();

            //远处太鼓心跳
            if (--ambienceTimer <= 0) {
                ambienceTimer = Main.rand.Next(480, 780);
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.22f, Pitch = -0.9f, MaxInstances = 1 }, Player.Center);
                }
            }
        }

        //收域对称化：眼睛重现→墨水吸回眼中→阖眼
        private void UpdateClosing() {
            int t = PhaseTimer;
            int c0 = OniDomain.CloseEyeFrames;
            int c1 = c0 + OniDomain.CloseRetractFrames;
            int c2 = c1 + OniDomain.CloseBlinkFrames;

            if (t <= c0) {
                //眼睛重现，已睁开
                EyeIntensity = t / (float)c0;
                EyeOpenAmount = 1f;
                EyeSpin -= 0.04f;
                SpreadProgress = 1f;
                return;
            }

            if (t <= c1) {
                //墨水吸回：缓入，先慢后疾冲进眼里
                float f = (t - c0) / (float)OniDomain.CloseRetractFrames;
                SpreadProgress = 1f - f * f * f;
                EyeIntensity = 1f;
                EyeSpin -= MathHelper.Lerp(0.04f, 0.28f, f);
                if (t == c0 + 1 && IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = -0.45f, MaxInstances = 1 }, Player.Center);
                }
                if (IsLocalVisual && t % 2 == 0) {
                    //墨水化灵体被吸入
                    OniDomainDeco.SpawnEyeConverge(EyeWorldPos, 2);
                }
                return;
            }

            //阖眼
            float bf = (t - c1) / (float)OniDomain.CloseBlinkFrames;
            SpreadProgress = 0f;
            EyeOpenAmount = MathHelper.Clamp(1f - bf * 1.8f, 0f, 1f);
            if (!closeClickPlayed && EyeOpenAmount <= 0f) {
                closeClickPlayed = true;
                if (IsLocalVisual) {
                    //归鞘咔 + 尾铃
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 1 }, Player.Center);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.28f, Pitch = 0.2f, MaxInstances = 1 }, Player.Center);
                    OniDomainDeco.SpawnEyeScatter(EyeWorldPos, 6);
                }
            }
            EyeIntensity = 1f - MathF.Max(bf - 0.55f, 0f) / 0.45f;

            if (t >= c2) {
                Phase = OniDomainPhase.Closed;
                PhaseTimer = 0;
                WorldIsUra = false;
                SpreadProgress = 0f;
                EyeIntensity = 0f;
                EyeOpenAmount = 0f;
                PaperValid = false;
            }
        }

        //稳态里眼睛的余韵：继续消散成灵体，勾玉惯性转着淡出
        private void DecayEyeLeftover() {
            if (EyeIntensity <= 0f) {
                return;
            }
            EyeIntensity = MathF.Max(EyeIntensity - 0.028f, 0f);
            EyeDissolve = MathF.Min(EyeDissolve + 0.030f, 1f);
            EyeSpin += 0.05f;
            if (IsLocalVisual && EyeIntensity > 0.1f && PhaseTimer % 4 == 0) {
                OniDomainDeco.SpawnEyeScatter(EyeWorldPos, 1);
            }
        }

        private void UpdateUraSmooth() {
            float target = 0f;
            if (WorldIsUra && Phase != OniDomainPhase.Closing) {
                target = 1f;
            }
            else if (WorldIsUra && Phase == OniDomainPhase.Closing) {
                //收域时跟随墨水吸回回明
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
                        ? MathHelper.Lerp(0.4f, 0f, flipStageTimer / (float)Math.Max(preSilenceDuration - 10, 1))
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
            OniOmokage.Clear();
            Phase = OniDomainPhase.Closed;
            PhaseTimer = 0;
            WorldIsUra = false;
            EffectTime = 0f;
            SpreadProgress = 0f;
            UraSmooth = 0f;
            AnomalyPulse = 0f;
            EyeIntensity = 0f;
            EyeOpenAmount = 0f;
            EyeSpin = 0f;
            EyeFlash = 0f;
            EyeDissolve = 0f;
            FlipStage = OniFlipStage.None;
            NegativeFlash = 0f;
            PeelProgress = 0f;
            PeelBias = 0.5f;
            PendingPaperCapture = false;
            PaperValid = false;
            flipStageTimer = 0;
            lastCommandFrame = -1;
            closeClickPlayed = false;
        }
    }
}
