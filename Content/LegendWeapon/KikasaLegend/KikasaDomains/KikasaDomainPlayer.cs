using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>血湖领域玩家态。权威状态机，网络只转播表现形态</summary>
    public class KikasaDomainPlayer : ModPlayer
    {
        public KikasaDomainPhase Phase { get; private set; } = KikasaDomainPhase.Closed;

        /// <summary>当前阶段帧计数</summary>
        public int PhaseTimer { get; private set; }

        /// <summary>着色器累计时间（秒），兼作撕纸遮罩噪声时基</summary>
        public float EffectTime { get; private set; }

        /// <summary>撕开覆盖进度 0~1，Opening 撕开 Closing 长回，稳态 1</summary>
        public float SpreadProgress { get; private set; }

        /// <summary>血湖上涨原始量 0~1，Opening 涨 Closing 退；观感经 <see cref="RiseProgress"/> 缓速</summary>
        public float RiseT { get; private set; }

        /// <summary>撕裂原点（世界坐标），开域帧取玩家中心</summary>
        public Vector2 OriginWorldPos { get; private set; }

        /// <summary>血湖水面世界 Y，开域帧取玩家脚底；空中开域就悬湖，领域本是异空间</summary>
        public float LakeWorldY { get; private set; }

        /// <summary>在场平滑系数 0~1，驱动光照/滤镜/天空垫底</summary>
        public float PresenceSmooth { get; private set; }

        /// <summary>浸润压暗包络 0~1，撕开后随覆盖退场</summary>
        public float SoakDim { get; private set; }

        /// <summary>水面泡沫/波动增强 0~1，涨水最烈、静水微澜、退水再起</summary>
        public float FoamBoost { get; private set; }

        /// <summary>涨水观感进度：前快后慢，水逼近脚底时减速（与入雨演出同曲线）</summary>
        public float RiseProgress => 1f - MathF.Pow(1f - RiseT, 1.6f);

        /// <summary>域是否处于任意激活阶段（含开合过渡）</summary>
        public bool AnyActive => Phase != KikasaDomainPhase.Closed;

        /// <summary>调色是否需要执行</summary>
        public bool GradeVisible => AnyActive;

        private long lastCommandFrame = -1;
        private int resyncTimer;
        //稳态偶发水声计时

        private int ambienceTimer;
        //触脚确认拍只放一次

        private bool contactDone;
        //涨水途中的两记水涌拍

        private bool riseBeatNear;
        private bool riseBeatFar;

        //==================== 输入 ====================

        /// <summary>持鬼伞按 <see cref="CWRKeySystem.Legend_Domain"/> 开阖；骇客时停不受理</summary>
        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.dead) {
                return;
            }
            if (HackTime.Active) {
                return;
            }
            Item item = Player.GetItem();
            bool holding = item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
            if (holding && CWRKeySystem.Legend_Domain.JustPressed) {
                KikasaDomain.TryToggle(Player, out _);
            }
        }

        //==================== 网络形态 ====================

        /// <summary>只含施术者掷过骰、别处推不出来的量；包络各端本地自算</summary>
        internal void WriteNetworkState(BinaryWriter writer) {
            writer.Write((byte)Phase);
            writer.Write((ushort)Math.Clamp(PhaseTimer, 0, ushort.MaxValue));
            writer.Write(SpreadProgress);
            writer.Write(RiseT);
            writer.Write(OriginWorldPos.X);
            writer.Write(OriginWorldPos.Y);
            writer.Write(LakeWorldY);
        }

        /// <summary>先读满整份负载再校验，脏包只做丢弃，不留半套状态</summary>
        internal void ReadNetworkState(BinaryReader reader) {
            byte phase = reader.ReadByte();
            int phaseTimer = reader.ReadUInt16();
            float spread = reader.ReadSingle();
            float rise = reader.ReadSingle();
            Vector2 origin = new(reader.ReadSingle(), reader.ReadSingle());
            float lakeY = reader.ReadSingle();

            if (phase > (byte)KikasaDomainPhase.Closing
                || !float.IsFinite(spread) || !float.IsFinite(rise)
                || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y)
                || !float.IsFinite(lakeY)) {
                return;
            }

            Phase = (KikasaDomainPhase)phase;
            PhaseTimer = phaseTimer;
            SpreadProgress = MathHelper.Clamp(spread, 0f, 1f);
            RiseT = MathHelper.Clamp(rise, 0f, 1f);
            OriginWorldPos = origin;
            LakeWorldY = lakeY;
            //触脚拍可从水位推回，中途加入者跨过 1 时照常触发
            contactDone = RiseT >= 0.999f;
            riseBeatNear = RiseT >= 0.75f;
            riseBeatFar = RiseT >= 0.4f;
        }

        /// <summary>命令被本机受理后立刻转播一份，让同场的人跟上同一拍</summary>
        private void BroadcastCommand() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            resyncTimer = KikasaDomainNet.ResyncInterval;
            KikasaDomainNet.SendSnapshot(Player);
        }

        //==================== 命令 ====================

        internal bool OpenDomain() {
            //收域中途反悔，原地续开：撕口从等值覆盖处再撕，锚点保持原样

            if (Phase == KikasaDomainPhase.Closing) {
                if (!ConsumeCommandGate()) {
                    return false;
                }
                Phase = KikasaDomainPhase.Opening;
                PhaseTimer = KikasaDomain.SoakFrames
                    + (int)(InvertOpenSpread(SpreadProgress) * KikasaDomain.TearFrames);
                ambienceTimer = 600;
                BroadcastCommand();
                return true;
            }
            if (Phase != KikasaDomainPhase.Closed || !ConsumeCommandGate()) {
                return false;
            }
            Phase = KikasaDomainPhase.Opening;
            PhaseTimer = 0;
            SpreadProgress = 0f;
            RiseT = 0f;
            OriginWorldPos = Player.Center;
            LakeWorldY = Player.Bottom.Y;
            contactDone = false;
            riseBeatNear = false;
            riseBeatFar = false;
            FoamBoost = 0f;
            ambienceTimer = 600;
            BroadcastCommand();
            return true;
        }

        internal bool CloseDomain() {
            if (Phase == KikasaDomainPhase.Closed || Phase == KikasaDomainPhase.Closing) {
                return false;
            }
            if (!ConsumeCommandGate()) {
                return false;
            }
            bool interrupt = Phase == KikasaDomainPhase.Opening;
            float p = SpreadProgress;
            Phase = KikasaDomainPhase.Closing;
            //开到一半收=撕口从当前覆盖原路合回；水位从当前高度退落

            PhaseTimer = interrupt
                ? (int)(InvertCloseSpread(p) * KikasaDomain.CloseFrames)
                : 0;
            if (IsLocalVisual) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 2 }, Player.Center);
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.45f, Pitch = -0.85f, MaxInstances = 2 }, Player.Center);
            }
            BroadcastCommand();
            return true;
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
        private bool IsLocalVisual => !Main.dedServ && ReferenceEquals(KikasaDomain.Viewed, this);

        /// <summary>屏震落在观看者身上而非施术者：队友开的域，震的是在场的人</summary>
        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);

        //==================== 推进 ====================

        internal void UpdateLocal() {
            if (Phase == KikasaDomainPhase.Closed) {
                PresenceSmooth = MathHelper.Lerp(PresenceSmooth, 0f, 0.05f);
                if (PresenceSmooth < 0.003f) PresenceSmooth = 0f;
                SoakDim = 0f;
                FoamBoost = 0f;
                return;
            }

            EffectTime += 1f / 60f;
            PhaseTimer++;

            //稳态每两秒重播一份形态，中途加入、丢包与漂移都靠它自愈
            if (Player.whoAmI == Main.myPlayer
                && Main.netMode == NetmodeID.MultiplayerClient
                && --resyncTimer <= 0) {
                resyncTimer = KikasaDomainNet.ResyncInterval;
                KikasaDomainNet.SendSnapshot(Player);
            }

            switch (Phase) {
                case KikasaDomainPhase.Opening: UpdateOpening(); break;
                case KikasaDomainPhase.Open: UpdateOpen(); break;
                case KikasaDomainPhase.Closing: UpdateClosing(); break;
            }

            UpdatePresence();
            UpdateMusicCap();
            UpdateFoam();

            //浸润压暗走包络，撕开后随覆盖退场，中断收域时平滑离场
            float dimTarget = 0f;
            if (Phase == KikasaDomainPhase.Opening) {
                dimTarget = PhaseTimer <= KikasaDomain.SoakFrames
                    ? PhaseTimer / (float)KikasaDomain.SoakFrames
                    : 1f - SpreadProgress;
            }
            SoakDim = MathHelper.Lerp(SoakDim, dimTarget, 0.25f);
            if (SoakDim < 0.004f && dimTarget <= 0f) {
                SoakDim = 0f;
            }
        }

        //浸润→撕开；血湖与撕口同窗推进

        private void UpdateOpening() {
            int t = PhaseTimer;

            if (t == 1 && IsLocalVisual) {
                //湿气渗上来的起手

                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = -0.9f, MaxInstances = 2 }, Player.Center);
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = -0.9f, MaxInstances = 2 }, Player.Center);
            }

            if (t > KikasaDomain.SoakFrames) {
                int st = t - KikasaDomain.SoakFrames;
                float raw = MathHelper.Clamp(st / (float)KikasaDomain.TearFrames, 0f, 1f);
                SpreadProgress = OpenSpreadCurve(raw);

                if (st == 1 && IsLocalVisual) {
                    //破纸撕开

                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.85f, Pitch = -0.7f, MaxInstances = 2 }, Player.Center);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 2 }, Player.Center);
                    ShakeViewer(3f);
                    KikasaDomainDeco.BurstScraps(this, 16);
                }
                //吞没段起步、纸幅加速离场的第二记撕裂

                if (st == (int)(KikasaDomain.TearFrames * 0.55f) && IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.55f, MaxInstances = 2 }, Player.Center);
                    ShakeViewer(2f);
                }
                //撕裂前沿持续掉纸屑

                if (IsLocalVisual && st % 3 == 0 && raw < 0.9f) {
                    KikasaDomainDeco.BurstScraps(this, 3);
                }

                if (raw >= 1f) {
                    Phase = KikasaDomainPhase.Open;
                    PhaseTimer = 0;
                    ambienceTimer = Main.rand.Next(480, 840);
                    if (IsLocalVisual) {
                        //落定的一声闷沉水鼓

                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.45f, Pitch = -0.85f, MaxInstances = 1 }, Player.Center);
                    }
                }
            }

            AdvanceRise();
        }

        private void UpdateOpen() {
            SpreadProgress = 1f;
            //续开时水位可能未满，继续涨

            AdvanceRise();

            //死寂血湖里偶尔一声水滴

            if (--ambienceTimer <= 0) {
                ambienceTimer = Main.rand.Next(480, 900);
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 2 }, Player.Center);
                }
            }
        }

        private void UpdateClosing() {
            float f = MathHelper.Clamp(PhaseTimer / (float)KikasaDomain.CloseFrames, 0f, 1f);
            SpreadProgress = CloseSpreadCurve(f);
            RiseT = MathF.Max(RiseT - 1f / KikasaDomain.DrainFrames, 0f);
            //水位退过阈值就解锁对应节拍，中途反悔续开时涨水拍能重新响
            if (RiseT < 0.95f) contactDone = false;
            if (RiseT < 0.70f) riseBeatNear = false;
            if (RiseT < 0.35f) riseBeatFar = false;

            //水被抽走的一记吞咽

            if (PhaseTimer == (int)(KikasaDomain.CloseFrames * 0.55f) && IsLocalVisual) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = 0.05f, MaxInstances = 2 }, Player.Center);
            }

            if (PhaseTimer >= KikasaDomain.CloseFrames) {
                Phase = KikasaDomainPhase.Closed;
                PhaseTimer = 0;
                SpreadProgress = 0f;
                RiseT = 0f;
                contactDone = false;
                riseBeatNear = false;
                riseBeatFar = false;
                if (IsLocalVisual) {
                    //纸面合拢的湿闷收尾

                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.3f, Pitch = -0.95f, MaxInstances = 1 }, Player.Center);
                }
            }
        }

        //血湖上涨与确认拍；Opening/Open 共用（续开时水位可能未满）

        private void AdvanceRise() {
            if (Phase == KikasaDomainPhase.Opening && PhaseTimer <= KikasaDomain.RiseStartFrame) {
                return;
            }
            if (RiseT >= 1f) {
                return;
            }
            RiseT = MathF.Min(RiseT + 1f / KikasaDomain.RiseFrames, 1f);

            Vector2 lakeAt = new(Player.Center.X, LakeWorldY);
            if (!riseBeatFar && RiseT >= 0.4f) {
                riseBeatFar = true;
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 2 }, lakeAt);
                }
            }
            if (!riseBeatNear && RiseT >= 0.75f) {
                riseBeatNear = true;
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.45f, MaxInstances = 2 }, lakeAt);
                }
            }
            if (!contactDone && RiseT >= 1f) {
                //水面触脚确认拍

                contactDone = true;
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.75f, Pitch = -0.15f, MaxInstances = 2 }, lakeAt);
                    ShakeViewer(3.5f);
                    KikasaDomainDeco.SplashAt(lakeAt, 12);
                    KikasaDomainDeco.RippleAt(lakeAt, 1.4f);
                }
            }
        }

        private void UpdatePresence() {
            float target = AnyActive ? SpreadProgress : 0f;
            float rate = target > PresenceSmooth ? 0.08f : 0.06f;
            PresenceSmooth = MathHelper.Lerp(PresenceSmooth, target, rate);
            if (PresenceSmooth < 0.003f && target <= 0f) PresenceSmooth = 0f;
        }

        private void UpdateFoam() {
            float target;
            if (Phase == KikasaDomainPhase.Closing) {
                target = 0.65f;
            }
            else if (!contactDone && AnyActive) {
                target = RiseT > 0f ? 1f : 0f;
            }
            else {
                //静水微澜

                target = 0.15f;
            }
            FoamBoost = MathHelper.Lerp(FoamBoost, target, 0.06f);
        }

        //各阶段压低音乐；不直接归零由引擎自然回升

        private void UpdateMusicCap() {
            if (!IsLocalVisual || Main.gameMenu) {
                return;
            }
            float cap = Phase switch {
                KikasaDomainPhase.Opening => MathHelper.Lerp(1f, 0.5f, SpreadProgress),
                KikasaDomainPhase.Open => 0.5f,
                KikasaDomainPhase.Closing => MathHelper.Lerp(1f, 0.5f, SpreadProgress),
                _ => 1f,
            };
            //boss 在场时音乐让位给战斗曲

            if (Main.CurrentFrameFlags.AnyActiveBossNPC) {
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

        //开域撕纸三段：爆冲(0→0.32)→滞行(0.32→0.46、撕口读秒)→吞没(0.46→1 加速离场)
        //分段端点斜率相接，肉眼无折点；与鬼切墨浪同构，材质区分在前沿层不在曲线

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

        //收域合回三段：扫入(1→0.50)→滞行(0.50→0.30)→合尽(0.30→0)

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

        /// <summary>掉线的人不该在别人屏幕上留一片血湖</summary>
        public override void PlayerDisconnect() => ResetDomain();

        internal void ResetDomain() {
            Phase = KikasaDomainPhase.Closed;
            PhaseTimer = 0;
            EffectTime = 0f;
            SpreadProgress = 0f;
            RiseT = 0f;
            PresenceSmooth = 0f;
            SoakDim = 0f;
            FoamBoost = 0f;
            contactDone = false;
            riseBeatNear = false;
            riseBeatFar = false;
            lastCommandFrame = -1;
        }
    }
}
