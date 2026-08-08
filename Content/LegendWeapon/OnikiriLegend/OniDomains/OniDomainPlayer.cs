using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.IO;
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
        /// <summary>死寂、风停、花瓣冻结、音乐掐掉</summary>
        PreSilence,
        /// <summary>负片闪 + 全屏刀痕 + 日月化眼</summary>
        Flash,
        /// <summary>纸层剥落</summary>
        Peel,
        /// <summary>落定</summary>
        Settle
    }

    /// <summary>领域玩家态/印记</summary>
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

        /// <summary>开域屏息压暗 0~1，中断收域时平滑退场</summary>
        public float OpenDim { get; private set; }

        /// <summary>爆域冲击环 0~1，贴浸染前沿，中断后自然衰减</summary>
        public float BurstGlow { get; private set; }

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
        /// <summary>渲染线待办、捕获当前帧作纸层</summary>
        public bool PendingPaperCapture { get; internal set; }
        /// <summary>纸层内容有效（捕获成功且分辨率未变）</summary>
        public bool PaperValid { get; internal set; }

        private int flipStageTimer;
        private int preSilenceDuration;
        private long lastCommandFrame = -1;
        private int resyncTimer;

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

        /// <summary>
        /// 领域形态。只含施术者掷过骰、别处推不出来的量；
        /// 其余（勾玉惯性、错位帧、屏息包络）各端本地自算，不占带宽
        /// </summary>
        internal void WriteNetworkState(BinaryWriter writer) {
            writer.Write((byte)Phase);
            writer.Write((ushort)Math.Clamp(PhaseTimer, 0, ushort.MaxValue));
            writer.Write(WorldIsUra);
            writer.Write(SpreadProgress);
            writer.Write(EyeWorldPos.X);
            writer.Write(EyeWorldPos.Y);
            writer.Write(EyeIntensity);
            writer.Write(EyeOpenAmount);
            writer.Write(EyeSpin);
            writer.Write(EyeDissolve);
            writer.Write((byte)FlipStage);
            writer.Write((ushort)Math.Clamp(flipStageTimer, 0, ushort.MaxValue));
            writer.Write((ushort)Math.Clamp(preSilenceDuration, 0, ushort.MaxValue));
            writer.Write(FlipToUra);
            writer.Write(FlipSlashAngle);
            writer.Write(PeelBias);
        }

        /// <summary>先读满整份负载再校验，脏包只做丢弃，不留半套状态</summary>
        internal void ReadNetworkState(BinaryReader reader) {
            byte phase = reader.ReadByte();
            int phaseTimer = reader.ReadUInt16();
            bool worldIsUra = reader.ReadBoolean();
            float spread = reader.ReadSingle();
            Vector2 eyePos = new(reader.ReadSingle(), reader.ReadSingle());
            float eyeIntensity = reader.ReadSingle();
            float eyeOpen = reader.ReadSingle();
            float eyeSpin = reader.ReadSingle();
            float eyeDissolve = reader.ReadSingle();
            byte flipStage = reader.ReadByte();
            int stageTimer = reader.ReadUInt16();
            int silence = reader.ReadUInt16();
            bool flipToUra = reader.ReadBoolean();
            float slashAngle = reader.ReadSingle();
            float peelBias = reader.ReadSingle();

            if (phase > (byte)OniDomainPhase.Closing
                || flipStage > (byte)OniFlipStage.Settle
                || !float.IsFinite(spread) || !float.IsFinite(eyePos.X)
                || !float.IsFinite(eyePos.Y) || !float.IsFinite(eyeIntensity)
                || !float.IsFinite(eyeOpen) || !float.IsFinite(eyeSpin)
                || !float.IsFinite(eyeDissolve) || !float.IsFinite(slashAngle)
                || !float.IsFinite(peelBias)) {
                return;
            }

            OniDomainPhase incoming = (OniDomainPhase)phase;
            OniFlipStage incomingStage = (OniFlipStage)flipStage;
            if (IsLocalVisual) {
                //在看这个域时，远端的收域与起翻要把本机装饰带到同一拍
                if (Phase != OniDomainPhase.Closed && incoming == OniDomainPhase.Closed) {
                    OniDomainDeco.NotifyClosing();
                }
                else if (FlipStage == OniFlipStage.None
                    && incomingStage == OniFlipStage.PreSilence) {
                    OniDomainDeco.NotifyFreeze();
                }
            }
            Phase = incoming;
            PhaseTimer = phaseTimer;
            WorldIsUra = worldIsUra;
            SpreadProgress = MathHelper.Clamp(spread, 0f, 1f);
            EyeWorldPos = eyePos;
            EyeIntensity = MathHelper.Clamp(eyeIntensity, 0f, 1f);
            EyeOpenAmount = MathHelper.Clamp(eyeOpen, 0f, 1f);
            EyeSpin = eyeSpin;
            EyeDissolve = MathHelper.Clamp(eyeDissolve, 0f, 1f);
            FlipStage = incomingStage;
            flipStageTimer = stageTimer;
            preSilenceDuration = silence;
            FlipToUra = flipToUra;
            FlipSlashAngle = slashAngle;
            PeelBias = MathHelper.Clamp(peelBias, 0.32f, 0.68f);
            if (Phase == OniDomainPhase.Closed) {
                PaperValid = false;
                PendingPaperCapture = false;
            }
        }

        /// <summary>命令被本机受理后立刻转播一份，让同场的人跟上同一拍</summary>
        private void BroadcastCommand() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            resyncTimer = OniDomainNet.ResyncInterval;
            OniDomainNet.SendSnapshot(Player);
        }

        internal bool OpenDomain() {
            //收域中途反悔，原地续开

            if (Phase == OniDomainPhase.Closing) {
                if (!ConsumeCommandGate()) {
                    return false;
                }
                ResumeOpenFromClosing();
                BroadcastCommand();
                return true;
            }
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
            BroadcastCommand();
            return true;
        }

        //眼已在场、跳过召唤仪式，反解开域曲线从等值覆盖处续扩，锚点保持原样圈心不跳

        private void ResumeOpenFromClosing() {
            float p = SpreadProgress;
            int ritual = OniDomain.EyeEmergeFrames + OniDomain.EyeOpenFrames + OniDomain.EyeBurstFrames;
            Phase = OniDomainPhase.Opening;
            PhaseTimer = ritual + (int)(InvertOpenSpread(p) * OniDomain.OpenSpreadFrames);
            EyeOpenAmount = 1f;
            anomalyTimer = SetAnomalyInterval();
            ambienceTimer = 600;
            closeClickPlayed = false;
            //再燃一闪，盖掉眼态接管的微跳

            if (p < 0.997f) {
                EyeFlash = MathF.Max(EyeFlash, 0.45f);
            }
        }

        internal bool CloseDomain() {
            if (Phase == OniDomainPhase.Closed || Phase == OniDomainPhase.Closing || Phase == OniDomainPhase.Flipping) {
                return false;
            }
            if (!ConsumeCommandGate()) {
                return false;
            }
            bool interrupt = Phase == OniDomainPhase.Opening;
            float p = SpreadProgress;
            Phase = OniDomainPhase.Closing;
            FlipStage = OniFlipStage.None;
            NegativeFlash = 0f;
            PeelProgress = 0f;
            PaperValid = false;
            closeClickPlayed = false;

            if (interrupt) {
                //开域中途反悔，锚点与眼态原样保留，墨浪从当前覆盖处原路吸回

                if (p <= 0.012f) {
                    //墨浪尚未出眼，跳过吸回直接阖眼(+1 跳过饱噬一闪)

                    PhaseTimer = OniDomain.CloseEyeFrames + OniDomain.CloseRetractFrames + 1;
                    SpreadProgress = 0f;
                }
                else {
                    PhaseTimer = OniDomain.CloseEyeFrames
                        + (int)(InvertCloseSpread(p) * OniDomain.CloseRetractFrames);
                }
            }
            else {
                //稳态收域，吸回锚点重锚到玩家当前位置上方(满屏覆盖下重锚不可见)

                PhaseTimer = 0;
                EyeWorldPos = Player.Center + new Vector2(0f, -150f);
                EyeIntensity = 0f;
                EyeOpenAmount = 1f;
                EyeDissolve = 0f;
            }
            if (IsLocalVisual) {
                OniDomainDeco.NotifyClosing();
            }
            //收域、过去归还给过去。面影是本机自己的过去，只有自己收域才烧

            if (Player.whoAmI == Main.myPlayer) {
                OniOmokage.BurnAll();
            }
            BroadcastCommand();
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
            //刀痕完全随机、左右倾向与陡缓都掷骰

            float lean = Main.rand.NextBool() ? 1f : -1f;
            FlipSlashAngle = lean * Main.rand.NextFloat(0.35f, 1.22f);
            //两半不对称滑移

            PeelBias = Main.rand.NextFloat(0.32f, 0.68f);
            if (IsLocalVisual) {
                OniDomainDeco.NotifyFreeze();
            }

            //翻转仪式全程时停、世界屏息，纸层揭开后恢复。多人下静态快照体系会失同步，单人才挂

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
            BroadcastCommand();
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

        /// <summary>本机此刻身处的是不是这个域。自己开的恒为真，队友的域进了范围也算</summary>
        private bool IsLocalVisual => !Main.dedServ && ReferenceEquals(OniDomain.Viewed, this);

        /// <summary>屏震落在观看者身上而非施术者：队友开的域，震的是在场的人</summary>
        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        internal void UpdateLocal() {
            if (Phase == OniDomainPhase.Closed) {
                UraSmooth = MathHelper.Lerp(UraSmooth, 0f, 0.05f);
                if (UraSmooth < 0.003f) UraSmooth = 0f;
                OpenDim = 0f;
                BurstGlow = 0f;
                return;
            }

            EffectTime += 1f / 60f;
            PhaseTimer++;

            //稳态每两秒重播一份形态，中途加入、丢包与漂移都靠它自愈
            if (Player.whoAmI == Main.myPlayer
                && Main.netMode == NetmodeID.MultiplayerClient
                && --resyncTimer <= 0) {
                resyncTimer = OniDomainNet.ResyncInterval;
                OniDomainNet.SendSnapshot(Player);
            }

            switch (Phase) {
                case OniDomainPhase.Opening: UpdateOpening(); break;
                case OniDomainPhase.Omote: UpdateOmote(); break;
                case OniDomainPhase.Flipping: UpdateFlipping(); break;
                case OniDomainPhase.Ura: UpdateUra(); break;
                case OniDomainPhase.Closing: UpdateClosing(); break;
            }

            UpdateUraSmooth();
            UpdateMusicCap();

            //屏息压暗走包络，中断收域时平滑退场而非瞬灭

            float dimTarget = 0f;
            if (Phase == OniDomainPhase.Opening) {
                int tBurst = OniDomain.EyeEmergeFrames + OniDomain.EyeOpenFrames + OniDomain.EyeBurstFrames;
                dimTarget = PhaseTimer <= tBurst ? PhaseTimer / (float)tBurst : 1f - SpreadProgress;
            }
            OpenDim = MathHelper.Lerp(OpenDim, dimTarget, 0.25f);
            if (OpenDim < 0.004f && dimTarget <= 0f) {
                OpenDim = 0f;
            }

            if (BurstGlow > 0f) {
                BurstGlow = MathF.Max(BurstGlow - 0.05f, 0f);
            }
            if (AnomalyPulse > 0f) {
                AnomalyPulse = MathF.Max(AnomalyPulse - 0.25f, 0f);
            }
            if (EyeFlash > 0f) {
                EyeFlash = MathF.Max(EyeFlash - 0.16f, 0f);
            }
        }

        //鬼眼开域、浮现→睁眼→勾玉狂旋→爆域（圈内即表世界）

        private void UpdateOpening() {
            int t = PhaseTimer;
            int tOpenEnd = OniDomain.EyeEmergeFrames + OniDomain.EyeOpenFrames;
            int tBurst = tOpenEnd + OniDomain.EyeBurstFrames;

            if (t <= OniDomain.EyeEmergeFrames) {
                //浮现、闭眼轮廓渐显微颤，灵体向眼汇聚

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
                //睁眼、数帧内猛然撑开

                float f = (t - OniDomain.EyeEmergeFrames) / (float)OniDomain.EyeOpenFrames;
                EyeIntensity = 1f;
                EyeOpenAmount = f * f * (3f - 2f * f);
                EyeSpin += 0.05f;
                if (t == OniDomain.EyeEmergeFrames + 1 && IsLocalVisual) {
                    SoundEngine.PlaySound(CWRSound.OutburstCC);
                    SoundEngine.PlaySound(CWRSound.OutburstRelease);
                    ShakeViewer(3f);
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
                    BurstGlow = 1f;
                    if (IsLocalVisual) {
                        SoundEngine.PlaySound(CWRSound.Thunder with { Volume = 0.55f, Pitch = -0.25f, MaxInstances = 1 }, Player.Center);
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.7f, MaxInstances = 1 }, Player.Center);
                        SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 1 }, Player.Center);
                        ShakeViewer(7f);
                    }
                }
                return;
            }

            //墨浪三段推进、爆冲后墨墙横陈滞行读秒，再加速吞没屏角

            //眼睛保持半实体悬在天上看着你，消散大头留给 Omote 里的余韵衰减

            int st = t - tBurst;
            float raw = MathHelper.Clamp(st / (float)OniDomain.OpenSpreadFrames, 0f, 1f);
            SpreadProgress = OpenSpreadCurve(raw);
            EyeDissolve = raw * 0.55f;
            EyeIntensity = 1f - raw * 0.6f;
            EyeSpin += MathHelper.Lerp(0.5f, 0.06f, raw);
            if (IsLocalVisual && st % 3 == 0 && raw < 0.85f) {
                OniDomainDeco.SpawnEyeScatter(EyeWorldPos, 2);
            }
            //吞没段起步、墨墙加速离场的浪声

            if (st == (int)(OniDomain.OpenSpreadFrames * 0.55f) && IsLocalVisual) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.55f, MaxInstances = 1 }, Player.Center);
                ShakeViewer(4f);
            }

            if (raw >= 1f) {
                //续开回到中断前的世界，全新开域恒为表

                Phase = WorldIsUra ? OniDomainPhase.Ura : OniDomainPhase.Omote;
                PhaseTimer = 0;
                if (IsLocalVisual) {
                    //落定风铃

                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.35f, Pitch = 0.15f, MaxInstances = 1 }, Player.Center);
                }
                if (Player.whoAmI == Main.myPlayer)
                    Tutorial.OnikiriTutorialEvents.FireDomainPhaseSettled(Phase);
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
                    //倒数第 12 帧、死寂中唯一一声风铃

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
                            ShakeViewer(6f);
                        }
                    }
                    break;

                case OniFlipStage.Flash:
                    NegativeFlash = MathHelper.Clamp(1f - flipStageTimer / (float)OniDomain.FlashFrames, 0f, 1f);
                    if (flipStageTimer >= OniDomain.FlashFrames) {
                        //捕获旧世界画面作纸层，随后调色切至新世界

                        WorldIsUra = FlipToUra;
                        FlipStage = OniFlipStage.Peel;
                        flipStageTimer = 0;
                        PeelProgress = 0f;
                        //纸层与装饰是本机屏幕的东西，不在场的域不该动它们

                        if (IsLocalVisual) {
                            PendingPaperCapture = true;
                            OniDomainDeco.NotifyPeelStart(FlipToUra);
                            //纸帛撕裂感

                            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.75f, MaxInstances = 1 }, Player.Center);
                        }
                        //快门、入里瞬间把屏内敌人的"过去"钉成面影；回表则全部烧散。
                        //面影是本机玩家自己的过去，队友翻转不动我的纸

                        if (Player.whoAmI == Main.myPlayer) {
                            if (FlipToUra) {
                                if (OniOmokage.AutoShutterOnFlip) {
                                    OniOmokage.ImprintVisible();
                                }
                            }
                            else {
                                OniOmokage.BurnAll();
                            }
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
                        if (Player.whoAmI == Main.myPlayer)
                            Tutorial.OnikiriTutorialEvents.FireDomainPhaseSettled(Phase);
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

        //收域对称化、眼睛重现→墨水吸回眼中→阖眼

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
                //墨水吸回三段、黑环扫入屏缘悬停读秒，再加速冲进眼里

                float f = (t - c0) / (float)OniDomain.CloseRetractFrames;
                SpreadProgress = CloseSpreadCurve(f);
                //渐升而非硬置，中断接管时从开域眼态平滑续走；吸回饱食、消散复原

                EyeIntensity = MathF.Min(1f, EyeIntensity + 0.09f);
                EyeDissolve = MathF.Max(EyeDissolve - 0.03f, 0f);
                EyeSpin -= MathHelper.Lerp(0.04f, 0.28f, f);
                if (t == c0 + 1 && IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = -0.45f, MaxInstances = 1 }, Player.Center);
                }
                //吸尽段起步、墨水加速灌回的抽吸声

                if (t == c0 + (int)(OniDomain.CloseRetractFrames * 0.58f) && IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0.05f, MaxInstances = 1 }, Player.Center);
                }
                if (IsLocalVisual && t % 2 == 0) {
                    //墨水化灵体被吸入

                    OniDomainDeco.SpawnEyeConverge(EyeWorldPos, 2);
                }
                return;
            }

            //墨浪吸尽、眼睛饱噬一闪

            if (t == c1 + 1) {
                EyeFlash = 1f;
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.7f, Pitch = -0.6f, MaxInstances = 1 }, Player.Center);
                    ShakeViewer(5f);
                    OniDomainDeco.SpawnEyeScatter(EyeWorldPos, 8);
                }
            }

            //阖眼、min 接管保证从中断眼态单调阖下，正常路径进场即 1 不受影响

            float bf = (t - c1) / (float)OniDomain.CloseBlinkFrames;
            SpreadProgress = 0f;
            EyeOpenAmount = MathF.Min(EyeOpenAmount, MathHelper.Clamp(1f - bf * 1.8f, 0f, 1f));
            if (!closeClickPlayed && EyeOpenAmount <= 0f) {
                closeClickPlayed = true;
                if (IsLocalVisual) {
                    //归鞘咔 + 尾铃

                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = -0.1f, MaxInstances = 1 }, Player.Center);
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.28f, Pitch = 0.2f, MaxInstances = 1 }, Player.Center);
                    OniDomainDeco.SpawnEyeScatter(EyeWorldPos, 6);
                }
            }
            EyeIntensity = MathF.Min(EyeIntensity, 1f - MathF.Max(bf - 0.55f, 0f) / 0.45f);

            if (t >= c2) {
                Phase = OniDomainPhase.Closed;
                PhaseTimer = 0;
                WorldIsUra = false;
                SpreadProgress = 0f;
                EyeIntensity = 0f;
                EyeOpenAmount = 0f;
                PaperValid = false;
                if (Player.whoAmI == Main.myPlayer)
                    Tutorial.OnikiriTutorialEvents.FireDomainPhaseSettled(OniDomainPhase.Closed);
            }
        }

        //开域墨浪三段：爆冲(0→0.32、起始斜率≈3.2)→滞行(0.32→0.46、墨墙横陈读秒)→吞没(0.46→1 加速离场)
        //前沿 dist≈progress*1.18 而屏角约 0.5，可视行程集中在 0~0.55，滞行段必须落在其中
        //分段端点斜率相接，肉眼无折点

        private static float OpenSpreadCurve(float x) {
            if (x < 0.18f) {
                float f = x / 0.18f;
                return (0.5719f - 0.2519f * f) * f;
            }
            if (x < 0.55f) {
                return 0.32f + (x - 0.18f) * (0.14f / 0.37f);
            }
            float g = (x - 0.55f) / 0.45f;
            return 0.46f + 0.1703f * g + 0.3697f * g * g * g;
        }

        //收域吸回三段：扫入(1→0.50)→滞行(0.50→0.30、黑环悬在屏缘)→吸尽(0.30→0 冲进眼里)

        private static float CloseSpreadCurve(float x) {
            if (x < 0.18f) {
                float f = x / 0.18f;
                return 1f - (0.91f - 0.41f * f) * f;
            }
            if (x < 0.58f) {
                return 0.50f - (x - 0.18f) * (0.20f / 0.40f);
            }
            float g = (x - 0.58f) / 0.42f;
            return 0.30f - 0.21f * g - 0.09f * g * g * g;
        }

        //单调曲线二分反解，中断反向时从等值覆盖率处接管

        private static float InvertOpenSpread(float target) {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 20; i++) {
                float mid = (lo + hi) * 0.5f;
                if (OpenSpreadCurve(mid) < target) {
                    lo = mid;
                }
                else {
                    hi = mid;
                }
            }
            return (lo + hi) * 0.5f;
        }

        private static float InvertCloseSpread(float target) {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 20; i++) {
                float mid = (lo + hi) * 0.5f;
                if (CloseSpreadCurve(mid) > target) {
                    lo = mid;
                }
                else {
                    hi = mid;
                }
            }
            return (lo + hi) * 0.5f;
        }

        //稳态里眼睛的余韵、继续消散成灵体，勾玉惯性转着淡出

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
            if (WorldIsUra) {
                //开/收过渡跟随墨水覆盖(含续开回里)，稳态与翻转恒 1

                bool transiting = Phase == OniDomainPhase.Opening || Phase == OniDomainPhase.Closing;
                target = transiting ? SpreadProgress : 1f;
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
            //boss 在场时音乐让位给战斗曲，只保留翻转仪式的死寂掐音

            if (Main.CurrentFrameFlags.AnyActiveBossNPC && Phase != OniDomainPhase.Flipping) {
                cap = MathF.Max(cap, 0.85f);
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

        /// <summary>掉线的人不该在别人屏幕上留一片里世界</summary>
        public override void PlayerDisconnect() => ResetDomain();

        internal void ResetDomain() {
            ReleaseFlipFreeze();
            if (Player.whoAmI == Main.myPlayer) {
                OniOmokage.Clear();
            }
            Phase = OniDomainPhase.Closed;
            PhaseTimer = 0;
            WorldIsUra = false;
            EffectTime = 0f;
            SpreadProgress = 0f;
            UraSmooth = 0f;
            AnomalyPulse = 0f;
            OpenDim = 0f;
            BurstGlow = 0f;
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
