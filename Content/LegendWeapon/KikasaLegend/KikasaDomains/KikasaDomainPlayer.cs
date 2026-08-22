using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps;
using CalamityOverhaul.Content.Scenarios.OniRainWorlds;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework.Input;
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

        /// <summary>撕开覆盖进度 0~1，Opening 撕开 Closing 长回，稳态 1；鬼梦推进需要写入故设 internal</summary>
        public float SpreadProgress { get; internal set; }

        /// <summary>血湖上涨原始量 0~1，Opening 涨 Closing 退；观感经 <see cref="RiseProgress"/> 缓速。
        /// 鬼梦里湖不存在（Dreaming 恒 0），归返时涌回，由 <see cref="KikasaDreamDirector"/> 写入</summary>
        public float RiseT { get; internal set; }

        /// <summary>撕裂/合拢原点（世界坐标）。开域帧取玩家中心；收域期间逐帧向玩家当前位置聚拢，
        /// 纸口以人为中心合回而不是钉死在开域点</summary>
        public Vector2 OriginWorldPos { get; private set; }

        /// <summary>血湖水面世界 Y，开域帧取玩家脚底；空中开域就悬湖，领域本是异空间</summary>
        public float LakeWorldY { get; private set; }

        /// <summary>在场平滑系数 0~1，驱动光照/滤镜/天空垫底</summary>
        public float PresenceSmooth { get; private set; }

        /// <summary>浸润压暗包络 0~1，撕开后随覆盖退场</summary>
        public float SoakDim { get; private set; }

        /// <summary>水面泡沫/波动增强 0~1，涨水最烈、静水微澜、退水再起</summary>
        public float FoamBoost { get; private set; }

        //==================== 鬼雨异化 ====================

        /// <summary>当前形态：false=血湖，true=鬼雨异化；翻转结算帧切换，收域归血湖</summary>
        public bool IsRainForm { get; private set; }

        /// <summary>本次翻转的目标方向</summary>
        public bool FlipToRain { get; private set; }

        /// <summary>鬼雨异化混合 0~1，驱动全部稳态视觉；结算后在白闪掩护下快速就位</summary>
        public float RainBlend { get; private set; }

        /// <summary>满幕雨帘密度 0~1：稳态吃 <see cref="RainBlend"/>，翻转期由节拍接管（前兆稀雨/退雨）</summary>
        public float RainCurtainDensity { get; private set; }

        /// <summary>沸腾强度 0~1，驱动水线搅动/气泡/蒸汽</summary>
        public float FlipBoil { get; private set; }

        /// <summary>沸腾上涌的水位抬升（屏幕 uv）：翻转层把黑水线抬到枢轴上方</summary>
        public float BoilRiseUv => 0.005f * FlipBoil;

        /// <summary>观感水面世界 Y：沸腾上涌期为抬高后的面（气泡/蒸汽从这里破水），
        /// 非翻转期恒等 <see cref="LakeWorldY"/>，鬼梦沸腾共用无感</summary>
        public float VisualLakeY => LakeWorldY
            - BoilRiseUv * Main.screenHeight / Main.GameViewMatrix.Zoom.Y;

        /// <summary>镜面预览向目标形态的靠拢 0~1，方向在消费端按 <see cref="FlipToRain"/> 换算</summary>
        public float FlipMix { get; private set; }

        /// <summary>倒转角（弧度），反向蓄势后 0→π</summary>
        public float FlipRollAngle { get; private set; }

        /// <summary>倒转角速度（弧度/帧），旋转拖影用</summary>
        public float FlipRollVelocity { get; private set; }

        /// <summary>结算后镜面向上吞没旧形态 0~1</summary>
        public float FlipSwallow { get; private set; }

        /// <summary>镜面调色增益，结算后让位真实异化氛围</summary>
        public float FlipGrade { get; private set; } = 1f;

        /// <summary>冷镜异样脉冲 0~1</summary>
        public float FlipGlimpse { get; private set; }

        /// <summary>异样涟漪环扩散 0~1</summary>
        public float FlipGlimpseRing { get; private set; }

        /// <summary>结算白闪 0~1</summary>
        public float FlipFlash { get; private set; }

        /// <summary>翻转期缝线辉光 0~1</summary>
        public float FlipSeamGlow { get; private set; }

        //==================== 鬼梦 ====================

        /// <summary>倒影恶犬是否已醒：湖镜里的人影被黑犬替换。领域激活期切换，收域清零</summary>
        public bool HoundReflection { get; internal set; }

        /// <summary>鬼梦在场混合 0~1：拉入结算后闪下就位、归返结算后退场，
        /// 驱动梦空/压光/湖面表现关停的交叉渐变（语义对齐 <see cref="RainBlend"/>）</summary>
        public float DreamBlend { get; internal set; }

        /// <summary>鬼梦沸腾强度 0~1，比异化翻转更烈</summary>
        public float DreamBoil { get; internal set; }

        /// <summary>镜面预览向梦侧/真实侧的靠拢 0~1，方向由当前相位决定</summary>
        public float DreamMix { get; internal set; }

        /// <summary>窥犬凝视 0~1：驻留段镜中黑犬双目自暗处亮起</summary>
        public float DreamGaze { get; internal set; }

        /// <summary>鬼梦倒转角（弧度），反向蓄势后 0→π</summary>
        public float DreamRollAngle { get; internal set; }

        /// <summary>鬼梦倒转角速度（弧度/帧），旋转拖影用</summary>
        public float DreamRollVelocity { get; internal set; }

        /// <summary>结算后镜面向上吞没旧世界 0~1</summary>
        public float DreamSwallow { get; internal set; }

        /// <summary>鬼梦镜面调色增益，结算后让位真实氛围</summary>
        public float DreamGrade { get; internal set; } = 1f;

        /// <summary>结算闪 0~1：拉入血红、归返暖白，色温在渲染端按相位取</summary>
        public float DreamFlash { get; internal set; }

        /// <summary>梦镜异样脉冲 0~1（错位双曝的那一下）</summary>
        public float DreamGlimpse { get; internal set; }

        /// <summary>异样涟漪环扩散 0~1</summary>
        public float DreamGlimpseRing { get; internal set; }

        /// <summary>鬼梦翻转期缝线辉光 0~1</summary>
        public float DreamSeamGlow { get; internal set; }

        //==================== 鬼火 ====================

        /// <summary>鬼火点燃态：满水血湖稳态自燃（门控在 <see cref="KikasaWisps.KikasaWisp"/>）。
        /// 鬼雨压制拍走完即清除（沸雨边免压制）；鬼梦只是湖暂时不在（包络退场、归返自动复燃，
        /// 点燃态保留）；收域清零</summary>
        public bool WispFireActive { get; internal set; }

        /// <summary>沸雨边（焰×潦）在效：鬼雨压不灭鬼火，改半强度蒸沸。
        /// owner 按沉影盘逐帧刷新并随快照同步，远端读不到盘</summary>
        public bool WispRainProof { get; internal set; }

        /// <summary>湖力 0~1：鬼火与鬼梦共饮的一汪水。鬼火燃着持续耗，火熄慢慢回；
        /// 拉入鬼梦当帧抽干。自燃要求满格、入梦要求过半，回涨就是这两门灵异的天然冷却</summary>
        public float LakeVigor { get; internal set; } = 1f;

        /// <summary>点燃处世界 X，燃沿从这里向两侧蔓延；收火时反向啃回原点</summary>
        public float WispOriginX { get; internal set; }

        /// <summary>鬼火在场包络 0~1，各端本地推进（燃起/退场的整体强度）</summary>
        public float WispT { get; internal set; }

        /// <summary>燃沿蔓延进度 0~1（乘半宽 4000 即已扫半径）</summary>
        public float WispSpread { get; internal set; }

        /// <summary>鬼雨压制熄灭进度 0~1：翻入鬼雨、世界落回视野后确定性推满，拍毕清除点燃态</summary>
        public float WispQuench { get; internal set; }

        /// <summary>喂给湖面调色的鬼火光强：压制中水里的金光先行冷却</summary>
        public float WispGlow => WispT * (1f - WispQuench * 0.6f);

        /// <summary>处于鬼梦相位（拉入/梦中/归返）任意一段</summary>
        public bool InDreamPhase => Phase == KikasaDomainPhase.DreamPull
            || Phase == KikasaDomainPhase.Dreaming
            || Phase == KikasaDomainPhase.DreamReturn;

        /// <summary>此刻画面处于梦侧：拉入结算后、梦中全程、归返结算前。
        /// 湖面物理与湖系表现按它关停，梦里没有那面湖</summary>
        public bool DreamWorldVisual =>
            Phase == KikasaDomainPhase.Dreaming
            || (Phase == KikasaDomainPhase.DreamPull && PhaseTimer >= KikasaDream.PullCommitFrame)
            || (Phase == KikasaDomainPhase.DreamReturn && PhaseTimer < KikasaDream.ReturnCommitFrame);

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
        //异化态远雷相对闪光的延迟帧数，光先于声

        private int thunderSoundDelay;
        //异化键未绑定时的原生中键边沿检测

        private bool previousMiddleDown;
        //触脚确认拍只放一次

        private bool contactDone;
        //涨水途中的两记水涌拍

        private bool riseBeatNear;
        private bool riseBeatFar;

        //WorldFreezeSystem reason：翻转/鬼梦进出专场共用，三相位互斥不会叠挂

        private const string FreezeReason = "KikasaDomainTransition";
        //本次专场是否由本机挂了时停

        private bool transitionFreezeHeld;

        //==================== 输入 ====================

        //异化键长按状态：hold 帧计数；swallowed=本次按住已被长按/归返消费，松手不再翻转
        private int mutateHold;
        private bool mutateWasDown;
        private bool mutateSwallowed;
        //倒影自动门控的稳定计数，防相位过渡里的抖动
        private int reflectionStable;

        /// <summary>长按异化键拉入鬼梦的判定帧数</summary>
        internal const int DreamHoldFrames = 30;

        /// <summary>此刻拉得动鬼梦：稳态满水、倒影既醒、湖力过半（与 PullDream 的门一致）</summary>
        internal bool DreamPullReady
            => Phase == KikasaDomainPhase.Open && RiseT >= 0.999f && HoundReflection
            && LakeVigor >= KikasaServants.KikasaEffigyBoard.DreamVigorNeed;

        /// <summary>
        /// 持鬼伞按 <see cref="CWRKeySystem.Legend_Domain"/> 开阖；
        /// <see cref="CWRKeySystem.Kikasa_DomainMutate"/>（默认中键，被清空绑定时回退原生中键）：
        /// 短按=开域/血雨翻转，长按=拉入鬼梦（满水且湖力过半即可，不看编成），梦中任按即归返。
        /// 倒影醒睡与鬼火燃熄随湖自动走，不再占键；骇客时停不受理
        /// </summary>
        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.dead) {
                mutateWasDown = false;
                mutateHold = 0;
                return;
            }
            //原生中键边沿逐帧维护，跨过时停/受理窗口也不留陈旧状态
            bool middleDown = Mouse.GetState().MiddleButton == ButtonState.Pressed;
            bool middleEdge = middleDown && !previousMiddleDown;
            previousMiddleDown = middleDown;

            //全屏地图上按键会在地图底下拉起全屏演出；输入被演出锁住时也不受理新命令
            if (HackTime.Active || Main.mapFullscreen || Main.blockInput) {
                mutateWasDown = false;
                mutateHold = 0;
                return;
            }
            Item item = Player.GetItem();
            bool holding = item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
            if (holding && CWRKeySystem.Legend_Domain.JustPressed) {
                KikasaDomain.TryToggle(Player, out _);
            }

            //异化键：默认 Mouse3；被清空绑定时回退原生中键
            bool unbound = CWRKeySystem.IsKeybindUnbound(CWRKeySystem.Kikasa_DomainMutate);
            bool mutateDown = unbound ? middleDown : CWRKeySystem.Kikasa_DomainMutate.Current;
            bool mutateEdge = unbound ? middleEdge : CWRKeySystem.Kikasa_DomainMutate.JustPressed;
            HandleMutateKey(holding, mutateDown, mutateEdge);

            //满水稳态则倒影自醒，醒睡不再占键
            UpdateReflectionGate();
        }

        /// <summary>
        /// 异化键的短按/长按分流：按下即计帧，捱过 <see cref="DreamHoldFrames"/> 且拉得动
        /// 就拉入鬼梦（消费本次按住）；松手时未被消费的短按才翻转。
        /// 梦中按下沿直接归返。短按因此有约半秒的松手延迟，翻转本身是 216 帧的仪式，吃得下
        /// </summary>
        private void HandleMutateKey(bool holding, bool down, bool edge) {
            bool eligible = (holding || AnyActive) && !Player.mouseInterface;
            if (edge && eligible) {
                if (Phase == KikasaDomainPhase.Dreaming) {
                    //梦中重按即归返，不吃长按
                    KikasaDomain.TryDreamPull(Player, out _);
                    mutateWasDown = true;
                    mutateSwallowed = true;
                    mutateHold = 0;
                    return;
                }
                mutateWasDown = true;
                mutateSwallowed = false;
                mutateHold = 0;
            }
            if (mutateWasDown && down) {
                mutateHold++;
                if (!mutateSwallowed && mutateHold == DreamHoldFrames) {
                    if (DreamPullReady) {
                        if (!KikasaDomain.TryDreamPull(Player, out _)) {
                            KikasaDreamSystem.Refuse(Player);
                        }
                        //无论受理与否都吞掉本次按住：到点的长按不能退化成翻转
                        mutateSwallowed = true;
                    }
                    else if (HoundReflection) {
                        //倒影醒着但条件差口气（湖力未半/正过渡）：拒一声并吞掉，防误翻
                        KikasaDreamSystem.Refuse(Player);
                        mutateSwallowed = true;
                    }
                    //倒影没醒（湖还没涨满）：长按视作按得久的短按，松手照旧翻转，没梦可入不设陷阱
                }
                //长按预兆：拉得动时湖面涟漪渐密，倒影在等你
                if (!mutateSwallowed && DreamPullReady && mutateHold >= 8 && mutateHold % 7 == 0
                    && IsLocalVisual) {
                    float k = mutateHold / (float)DreamHoldFrames;
                    KikasaDomainDeco.RippleAt(new Vector2(Player.Center.X, LakeWorldY),
                        0.35f + k * 0.5f);
                }
            }
            if (mutateWasDown && !down) {
                bool shortPress = !mutateSwallowed && mutateHold < DreamHoldFrames;
                mutateWasDown = false;
                mutateHold = 0;
                mutateSwallowed = false;
                if (shortPress && eligible) {
                    KikasaDomain.TryMutate(Player, out _);
                }
            }
        }

        /// <summary>
        /// 倒影自动门控：稳态满水即自醒，梦之门常开，不再看魇影编成。
        /// 醒着的倒影不因水位波动入睡（收域时在 UpdateLocal 里随域睡透）；
        /// 只在 Open 稳态切换（翻转/鬼梦里镜面正忙），连续 10 帧稳定才动手防抖
        /// </summary>
        private void UpdateReflectionGate() {
            if (Phase != KikasaDomainPhase.Open) {
                reflectionStable = 0;
                return;
            }
            if (HoundReflection || RiseT < 0.999f) {
                reflectionStable = 0;
                return;
            }
            if (++reflectionStable >= 10) {
                reflectionStable = 0;
                ToggleHoundReflection();
            }
        }

        /// <summary>湖面物理：移动应用前钳制。每端对所有玩家跑同一规则（状态源自同步快照），各端一致</summary>
        public override void PreUpdateMovement() => KikasaLakeSurface.ApplyStanding(Player);

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
            writer.Write(IsRainForm);
            writer.Write(FlipToRain);
            writer.Write(HoundReflection);
            writer.Write(WispFireActive);
            writer.Write(WispOriginX);
            writer.Write(WispRainProof);
            writer.Write(LakeVigor);
        }

        /// <summary>先读满整份负载再校验，脏包只做丢弃，不留半套状态</summary>
        internal void ReadNetworkState(BinaryReader reader) {
            byte phase = reader.ReadByte();
            int phaseTimer = reader.ReadUInt16();
            float spread = reader.ReadSingle();
            float rise = reader.ReadSingle();
            Vector2 origin = new(reader.ReadSingle(), reader.ReadSingle());
            float lakeY = reader.ReadSingle();
            bool rainForm = reader.ReadBoolean();
            bool flipToRain = reader.ReadBoolean();
            bool houndReflection = reader.ReadBoolean();
            bool wispFire = reader.ReadBoolean();
            float wispOriginX = reader.ReadSingle();
            bool wispRainProof = reader.ReadBoolean();
            float lakeVigor = reader.ReadSingle();

            if (phase > (byte)KikasaDomainPhase.DreamReturn
                || !float.IsFinite(spread) || !float.IsFinite(rise)
                || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y)
                || !float.IsFinite(lakeY) || !float.IsFinite(wispOriginX)
                || !float.IsFinite(lakeVigor)) {
                return;
            }

            Phase = (KikasaDomainPhase)phase;
            PhaseTimer = phaseTimer;
            SpreadProgress = MathHelper.Clamp(spread, 0f, 1f);
            RiseT = MathHelper.Clamp(rise, 0f, 1f);
            OriginWorldPos = origin;
            LakeWorldY = lakeY;
            IsRainForm = rainForm;
            FlipToRain = flipToRain;
            HoundReflection = houndReflection;
            //鬼火包络/蔓延/压制各端本地自算，只同步点燃态与原点；
            //沸雨边与湖力是 owner 才算得出的量，快照矫正远端漂移
            WispFireActive = wispFire;
            WispOriginX = wispOriginX;
            WispRainProof = wispRainProof;
            LakeVigor = MathHelper.Clamp(lakeVigor, 0f, 1f);
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
            if (Phase == KikasaDomainPhase.Closed || Phase == KikasaDomainPhase.Closing
                || Phase == KikasaDomainPhase.Flipping || InDreamPhase) {
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

        /// <summary>开始鬼雨异化翻转。仅 Open 稳态且满水位受理；入雨/深潜全屏演出期间不叠加第二套拷屏翻转</summary>
        internal bool FlipDomain(out bool busy) {
            busy = false;
            if (Phase != KikasaDomainPhase.Open) {
                busy = Phase != KikasaDomainPhase.Closed;
                return false;
            }
            if (RiseT < 0.999f || OniRainWorldTransition.Active || OniRainDescentTransition.Active) {
                busy = true;
                return false;
            }
            if (!ConsumeCommandGate()) {
                return false;
            }
            Phase = KikasaDomainPhase.Flipping;
            PhaseTimer = 0;
            FlipToRain = !IsRainForm;
            ZeroFlipEnvelopes();
            //施术者本机才有运镜；运镜失败不致命，演出照走
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                CutsceneDirector.Play<KikasaFlipCutscene>(Player);
            }
            //专场全程时停、世界屏息，落定后恢复。多人下静态快照体系会失同步，单人才挂
            HoldTransitionFreeze();
            BroadcastCommand();
            return true;
        }

        /// <summary>倒影恶犬开关。稳态受理；确认拍是一圈水纹与远处的一声低应</summary>
        internal bool ToggleHoundReflection() {
            if (!ConsumeCommandGate()) {
                return false;
            }
            HoundReflection = !HoundReflection;
            if (IsLocalVisual) {
                Vector2 lakeAt = new(Player.Center.X, LakeWorldY);
                KikasaDomainDeco.RippleAt(lakeAt, HoundReflection ? 1.3f : 0.9f);
                if (HoundReflection) {
                    //影子醒了
                    KikasaHoundVoice.Wuff(lakeAt + new Vector2(0f, 220f), 0.36f, -0.18f, 2);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.75f, Volume = 0.45f, MaxInstances = 2 }, lakeAt);
                }
                else {
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.5f, Volume = 0.4f, MaxInstances = 2 }, lakeAt);
                }
            }
            BroadcastCommand();
            return true;
        }

        /// <summary>鬼火点燃/收火。点燃把蔓延原点定在脚下；收火走包络反向啃回。
        /// 自燃门控在 <see cref="KikasaWisp"/>（满水稳态+湖力蓄满），这里只执行命令；确认拍只在观看端</summary>
        internal bool ToggleWispFire() {
            if (!ConsumeCommandGate()) {
                return false;
            }
            if (WispFireActive) {
                WispFireActive = false;
                if (IsLocalVisual) {
                    //收火：一口气把火吸回来的轻响
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = -0.9f, Volume = 0.4f, MaxInstances = 2 }, Player.Center);
                }
            }
            else {
                WispFireActive = true;
                WispOriginX = Player.Center.X;
                WispSpread = 0f;
                WispQuench = 0f;
                if (IsLocalVisual) {
                    Vector2 lakeAt = new(Player.Center.X, LakeWorldY);
                    //点燃：低沉的气声起火 + 脚下荡开一圈
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = -0.45f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    SoundEngine.PlaySound(SoundID.Item20 with { Pitch = -0.3f, Volume = 0.45f, MaxInstances = 2 }, lakeAt);
                    KikasaDomainDeco.RippleAt(lakeAt, 1.2f);
                    KikasaWispFX.IgniteBurst(lakeAt);
                }
            }
            BroadcastCommand();
            return true;
        }

        /// <summary>
        /// 鬼梦拉入/归返。Open 稳态 + 满水位 + 倒影已醒 + 湖力过半才拉得动，
        /// 受理帧把湖力整汪抽干（回涨即天然冷却）；Dreaming 里再按即归返；
        /// 与入雨/深潜全屏演出互斥，同 <see cref="FlipDomain"/> 的约定
        /// </summary>
        internal bool PullDream(out bool busy) {
            busy = false;
            if (Phase == KikasaDomainPhase.Dreaming) {
                if (!ConsumeCommandGate()) {
                    return false;
                }
                Phase = KikasaDomainPhase.DreamReturn;
                PhaseTimer = 0;
                ZeroDreamEnvelopes();
                if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                    CutsceneDirector.Play<KikasaDreamReturnCutscene>(Player);
                }
                HoldTransitionFreeze();
                BroadcastCommand();
                return true;
            }
            if (Phase != KikasaDomainPhase.Open) {
                busy = Phase != KikasaDomainPhase.Closed;
                return false;
            }
            if (RiseT < 0.999f || !HoundReflection
                || LakeVigor < KikasaServants.KikasaEffigyBoard.DreamVigorNeed
                || OniRainWorldTransition.Active || OniRainDescentTransition.Active) {
                busy = true;
                return false;
            }
            if (!ConsumeCommandGate()) {
                return false;
            }
            //梦把湖抽干：出梦后湖力得重新蓄，鬼火也得等它回满才再燃
            LakeVigor = 0f;
            Phase = KikasaDomainPhase.DreamPull;
            PhaseTimer = 0;
            ZeroDreamEnvelopes();
            //施术者本机才有运镜；运镜失败不致命，演出照走
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                CutsceneDirector.Play<KikasaDreamPullCutscene>(Player);
            }
            HoldTransitionFreeze();
            BroadcastCommand();
            return true;
        }

        /// <summary>拉入落定：进入梦中稳态，湖随之不见</summary>
        internal void DreamSettleToDreaming() {
            ReleaseTransitionFreeze();
            Phase = KikasaDomainPhase.Dreaming;
            PhaseTimer = 0;
            RiseT = 0f;
            ZeroDreamEnvelopes();
        }

        /// <summary>归返落定：回到血湖稳态，形态保持入梦前的模样</summary>
        internal void DreamSettleToOpen() {
            ReleaseTransitionFreeze();
            Phase = KikasaDomainPhase.Open;
            PhaseTimer = 0;
            RiseT = 1f;
            ambienceTimer = Main.rand.Next(240, 480);
            ZeroDreamEnvelopes();
        }

        /// <summary>中断落定（死亡等）：梦拽不住死人，直接回血湖，域保持打开</summary>
        internal void DreamAbort() {
            ReleaseTransitionFreeze();
            Phase = KikasaDomainPhase.Open;
            PhaseTimer = 0;
            RiseT = 1f;
            ambienceTimer = Main.rand.Next(240, 480);
            ZeroDreamEnvelopes();
        }

        internal void ZeroDreamEnvelopes() {
            DreamBoil = DreamMix = DreamGaze = DreamRollAngle = DreamRollVelocity = 0f;
            DreamSwallow = DreamFlash = DreamSeamGlow = 0f;
            DreamGlimpse = DreamGlimpseRing = 0f;
            DreamGrade = 1f;
        }

        /// <summary>
        /// 专场时停：NPC/弹幕/玩家钉住，状态机仍走 PostUpdateEverything。
        /// 镜像鬼切 <c>OniDomainFlip</c>：多人静态快照会失同步，只在单机挂
        /// </summary>
        private void HoldTransitionFreeze() {
            if (transitionFreezeHeld) {
                return;
            }
            if (!VaultUtils.isSinglePlayer || Player.whoAmI != Main.myPlayer) {
                return;
            }
            WorldFreezeSystem.Activate(FreezeReason);
            transitionFreezeHeld = true;
            if (Main.LocalPlayer.Alives()) {
                //预填飞行时间，防首次进入快照被零值覆盖
                WorldFreezePlayer freezePlayer = Main.LocalPlayer.GetModPlayer<WorldFreezePlayer>();
                freezePlayer.frozenWingTime = Main.LocalPlayer.wingTime;
                freezePlayer.frozenRocketTime = Main.LocalPlayer.rocketTime;
            }
        }

        private void ReleaseTransitionFreeze() {
            if (!transitionFreezeHeld) {
                return;
            }
            WorldFreezeSystem.Deactivate(FreezeReason);
            transitionFreezeHeld = false;
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
                //域关着不留异化残余，重开总是血湖
                IsRainForm = false;
                FlipToRain = false;
                RainBlend = 0f;
                RainCurtainDensity = 0f;
                ZeroFlipEnvelopes();
                //梦也一并醒透：倒影入睡、梦境退净
                HoundReflection = false;
                DreamBlend = 0f;
                ZeroDreamEnvelopes();
                //鬼火随域关熄；湖力随域关回满，重开总是一汪蓄好的湖
                WispFireActive = false;
                WispRainProof = false;
                WispT = 0f;
                WispSpread = 0f;
                WispQuench = 0f;
                LakeVigor = MathF.Min(LakeVigor + KikasaServants.KikasaEffigyBoard.VigorRefillPerFrame * 2f, 1f);
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
                case KikasaDomainPhase.Flipping: UpdateFlipping(); break;
                //鬼梦三相位的包络推进委托给导演，防本文件膨胀
                case KikasaDomainPhase.DreamPull: KikasaDreamDirector.UpdatePull(this); break;
                case KikasaDomainPhase.Dreaming: KikasaDreamDirector.UpdateDreaming(this); break;
                case KikasaDomainPhase.DreamReturn: KikasaDreamDirector.UpdateReturn(this); break;
            }

            //延迟雷声的公共泵：闪光由 NotifyThunder 先行，这里在任意阶段把声补到
            if (thunderSoundDelay > 0 && --thunderSoundDelay == 0 && IsLocalVisual) {
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Pitch = Main.rand.NextFloat(-1f, -0.75f),
                    Volume = Main.rand.NextFloat(0.24f, 0.4f),
                    MaxInstances = 3,
                }, Player.Center + new Vector2(Main.rand.NextFloat(-900f, 900f), -400f));
            }

            UpdateRainBlend();
            UpdateDreamBlend();
            UpdateRainCurtain();
            UpdatePresence();
            UpdateMusicCap();
            UpdateFoam();
            //鬼火包络/压制/灼烧扫描委托出去，防状态机文件膨胀（同 KikasaDreamDirector 之例）
            KikasaWisp.Update(this);

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
                }
                //吞没段起步、纸幅加速离场的第二记撕裂

                if (st == (int)(KikasaDomain.TearFrames * 0.55f) && IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.55f, MaxInstances = 2 }, Player.Center);
                    ShakeViewer(2f);
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

            //血湖偶尔一声水滴；异化态换成远雷（天幕先闪、雷声延迟，光先于声）

            if (--ambienceTimer <= 0) {
                if (RainBlend > 0.5f) {
                    ambienceTimer = Main.rand.Next(360, 720);
                    if (IsLocalVisual) {
                        KikasaDomainSky.NotifyThunder();
                        thunderSoundDelay = Main.rand.Next(15, 40);
                    }
                }
                else {
                    ambienceTimer = Main.rand.Next(480, 900);
                    if (IsLocalVisual) {
                        SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 2 }, Player.Center);
                    }
                }
            }
        }

        private void UpdateClosing() {
            //合拢原点逐帧向人聚拢：满覆盖下圆心移动不可见，等圆缩小到可见时已贴在身上，
            //纸口追着人合拢而不是钉死在开域点；各端从同步的玩家位置自算，无需入快照特殊处理
            OriginWorldPos = Vector2.Lerp(OriginWorldPos, Player.Center, 0.2f);
            Vector2 offset = Player.Center.To(OriginWorldPos);
            OriginWorldPos = Player.Center + offset.UnitVector() * MathHelper.Clamp(offset.Length(), 0, 600);

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

        //==================== 鬼雨异化翻转 ====================

        //节拍（60fps）：沸腾骤变 0-54 → 窥影驻留 54-94 → 倒转 94-184 → 落定 184-216
        //结算帧 139=倒转段时间过半（曲线上 θ≈60°），137-147 的近全白硬闪盖住形态切换；
        //包络全是 PhaseTimer 的确定性函数，远端从快照 timer 自算同一形状，
        //快照漂移最多错开一两帧节拍音

        private const int FlipGlimpseStart = KikasaDomain.FlipBoilEnd + 6;
        private const int FlipGlimpseFrames = 20;

        private void UpdateFlipping() {
            SpreadProgress = 1f;
            RiseT = 1f;
            int t = PhaseTimer;
            float prevRoll = FlipRollAngle;

            //沸腾：快速拉满，结算后随白闪退场
            float boilIn = Smooth01(t / (float)KikasaDomain.FlipBoilRamp);
            float boilOut = t < KikasaDomain.FlipCommitFrame ? 1f
                : 1f - Smooth01((t - KikasaDomain.FlipCommitFrame) / 40f);
            FlipBoil = boilIn * boilOut;

            //镜面预览向目标形态靠拢："猛地变色"，与沸腾同步的陡坡先撞到 0.78，
            //沸腾余下的时间与驻留段再慢慢浸到 0.92 后保持
            FlipMix = t <= KikasaDomain.FlipBoilEnd
                ? Smooth01(t / (float)KikasaDomain.FlipBoilRamp) * 0.78f
                : MathHelper.Lerp(0.78f, 0.92f, Smooth01(
                    (t - KikasaDomain.FlipBoilEnd)
                    / (float)(KikasaDomain.FlipDwellEnd - KikasaDomain.FlipBoilEnd)));

            //倒转角：反向蓄势一小口，再 0→π 先慢后快再慢
            if (t <= KikasaDomain.FlipDwellEnd) {
                FlipRollAngle = 0f;
            }
            else {
                float p = (t - KikasaDomain.FlipDwellEnd)
                    / (float)(KikasaDomain.FlipRollEnd - KikasaDomain.FlipDwellEnd);
                const float antic = 0.10f;
                FlipRollAngle = p < antic
                    ? -0.03f * MathHelper.Pi * Smooth01(p / antic)
                    : MathHelper.Lerp(-0.03f * MathHelper.Pi, MathHelper.Pi,
                        CubicInOut((p - antic) / (1f - antic)));
            }
            FlipRollVelocity = FlipRollAngle - prevRoll;

            //结算后镜面向上吞满全屏；调色让位给已切换的真实氛围
            FlipSwallow = t < KikasaDomain.FlipCommitFrame ? 0f
                : Smooth01((t - KikasaDomain.FlipCommitFrame) / 37f);
            FlipGrade = t < KikasaDomain.FlipCommitFrame ? 1f
                : 1f - Smooth01((t - KikasaDomain.FlipCommitFrame) / 41f);

            //冷镜异样一闪与荡开的涟漪环
            FlipGlimpse = t >= FlipGlimpseStart && t < FlipGlimpseStart + FlipGlimpseFrames
                ? MathF.Sin(MathHelper.Pi * (t - FlipGlimpseStart) / FlipGlimpseFrames) : 0f;
            FlipGlimpseRing = t >= FlipGlimpseStart && t < FlipGlimpseStart + FlipGlimpseFrames + 14
                ? MathHelper.Clamp((t - FlipGlimpseStart) / (float)(FlipGlimpseFrames + 14), 0f, 1f) : 0f;

            //结算白闪：短促起势，长尾退潮
            if (t >= KikasaDomain.FlipCommitFrame - 2 && t < KikasaDomain.FlipCommitFrame) {
                FlipFlash = (t - (KikasaDomain.FlipCommitFrame - 2)) / 2f;
            }
            else if (t >= KikasaDomain.FlipCommitFrame) {
                FlipFlash = MathHelper.Clamp(1f - (t - KikasaDomain.FlipCommitFrame) / 18f, 0f, 1f);
            }
            else {
                FlipFlash = 0f;
            }

            //缝线辉光，落定段消隐
            FlipSeamGlow = t <= KikasaDomain.FlipRollEnd ? 1f
                : 1f - Smooth01((t - KikasaDomain.FlipRollEnd)
                    / (float)(KikasaDomain.FlipTotalFrames - KikasaDomain.FlipRollEnd));

            //结算：白闪掩护下切形态（>= 加锁存，快照跳帧也不漏拍）
            if (t >= KikasaDomain.FlipCommitFrame && IsRainForm != FlipToRain) {
                IsRainForm = FlipToRain;
                if (IsLocalVisual) {
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -0.6f, Volume = 0.85f, MaxInstances = 3 }, Player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.7f, Volume = 0.6f, MaxInstances = 2 }, Player.Center);
                    ShakeViewer(9f);
                }
            }

            SpawnFlipFx();
            PlayFlipBeats();

            //玩家中途失效：直接落定到目标形态，域保持打开
            if (Player.dead) {
                SettleFlip();
                return;
            }

            if (t >= KikasaDomain.FlipTotalFrames) {
                //θ=π、吞满全屏、调色归零时输出等于输入，落定无跳变
                SettleFlip();
            }
        }

        private void SettleFlip() {
            ReleaseTransitionFreeze();
            IsRainForm = FlipToRain;
            Phase = KikasaDomainPhase.Open;
            PhaseTimer = 0;
            ambienceTimer = Main.rand.Next(240, 480);
            ZeroFlipEnvelopes();
        }

        private void ZeroFlipEnvelopes() {
            FlipBoil = FlipMix = FlipRollAngle = FlipRollVelocity = 0f;
            FlipSwallow = FlipGlimpse = FlipGlimpseRing = FlipFlash = FlipSeamGlow = 0f;
            FlipGrade = 1f;
        }

        /// <summary>翻转期相位粒子：沿水线的沸腾气泡与蒸汽、落定溅圈</summary>
        private void SpawnFlipFx() {
            if (!IsLocalVisual) {
                return;
            }
            int t = PhaseTimer;
            //镜面预览的目标侧混合，气泡颜色跟着先行变
            float coldMix = FlipToRain ? FlipMix : 1f - FlipMix;

            //沸腾开场缩短后每帧喷保量；驻留/倒转仍隔帧，其它段手感不动
            if (t < KikasaDomain.FlipCommitFrame && FlipBoil > 0.05f
                && (t <= KikasaDomain.FlipBoilEnd || t % 2 == 0)) {
                KikasaDomainDeco.BoilBurst(this, FlipBoil, coldMix);
            }
            //蒸汽同理：开场 %3 加密，其后仍 %5
            if (t < KikasaDomain.FlipCommitFrame && FlipBoil > 0.3f
                && t % (t <= KikasaDomain.FlipBoilEnd ? 3 : 5) == 0) {
                KikasaDomainDeco.BoilSteam(this, FlipBoil, coldMix);
            }
            //落定确认拍：脚下水花溅开一圈，世界是"落"回湖面的
            if (t == KikasaDomain.FlipRollEnd) {
                Vector2 lakeAt = new(Player.Center.X, LakeWorldY);
                KikasaDomainDeco.SplashAt(lakeAt, 16);
                KikasaDomainDeco.RippleAt(lakeAt, 1.6f);
            }
        }

        /// <summary>翻转节拍音与确认拍，全部落在观看者本机</summary>
        private void PlayFlipBeats() {
            if (!IsLocalVisual) {
                return;
            }
            Vector2 lakeAt = new(Player.Center.X, LakeWorldY);
            switch (PhaseTimer) {
                case 1:
                    //受理拍：天幕先无声地闪、湖面荡开第一圈大涟漪，雷声隔十几帧才砸到，凶兆先到
                    KikasaDomainSky.NotifyThunder();
                    thunderSoundDelay = Main.rand.Next(12, 22);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.9f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    KikasaDomainDeco.RippleAt(lakeAt, 1.5f);
                    ShakeViewer(2f);
                    break;
                case 11:
                    //水从湖底翻起来的第一记涌拍（原 18，随开场缩 40%）
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.7f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    break;
                case 29:
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.45f, Volume = 0.55f, MaxInstances = 2 }, lakeAt);
                    KikasaDomainDeco.RippleAt(lakeAt, 1.1f);
                    ShakeViewer(2.5f);
                    break;
                case 47:
                    //沸腾顶点，整面湖都在滚（原 78）
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.15f, Volume = 0.65f, MaxInstances = 2 }, lakeAt);
                    ShakeViewer(3f);
                    break;
                case FlipGlimpseStart + 4:
                    //冷镜异样：布被扯紧的闷吸声
                    SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = -0.9f, Volume = 0.42f, MaxInstances = 2 }, lakeAt);
                    break;
                case KikasaDomain.FlipDwellEnd:
                    //倒转起势
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.7f, Volume = 0.5f, MaxInstances = 2 }, lakeAt);
                    break;
                case KikasaDomain.FlipDwellEnd + 35:
                    //世界滚动的极低闷响（倒转段内相对位置不变）
                    SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -1f, Volume = 0.34f, MaxInstances = 3 }, Player.Center);
                    break;
                case KikasaDomain.FlipRollEnd - 15:
                    //新形态的水声落下来
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.35f, Volume = 0.55f, MaxInstances = 2 }, Player.Center);
                    break;
                case KikasaDomain.FlipRollEnd:
                    //落定一记压低的闷锣
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.9f, Volume = 0.4f, MaxInstances = 1 }, Player.Center);
                    ShakeViewer(4f);
                    break;
            }
        }

        //鬼雨混合：朝当前形态收敛；结算后的白闪窗口内快速就位，其余时间缓速

        private void UpdateRainBlend() {
            float target = IsRainForm ? 1f : 0f;
            float rate = Phase == KikasaDomainPhase.Flipping
                && PhaseTimer >= KikasaDomain.FlipCommitFrame ? 0.30f : 0.08f;
            RainBlend = MathHelper.Lerp(RainBlend, target, rate);
            if (RainBlend < 0.002f && target <= 0f) RainBlend = 0f;
            if (RainBlend > 0.998f && target >= 1f) RainBlend = 1f;
        }

        //鬼梦混合：朝当前世界侧收敛；结算闪窗口内快速就位，同鬼雨混合的语义

        private void UpdateDreamBlend() {
            float target = DreamWorldVisual ? 1f : 0f;
            bool flashWindow =
                (Phase == KikasaDomainPhase.DreamPull && PhaseTimer >= KikasaDream.PullCommitFrame)
                || (Phase == KikasaDomainPhase.DreamReturn && PhaseTimer >= KikasaDream.ReturnCommitFrame);
            float rate = flashWindow ? 0.30f : 0.08f;
            DreamBlend = MathHelper.Lerp(DreamBlend, target, rate);
            if (DreamBlend < 0.002f && target <= 0f) DreamBlend = 0f;
            if (DreamBlend > 0.998f && target >= 1f) DreamBlend = 1f;
        }

        //雨帘密度：稳态吃 RainBlend；正向翻转开场迅猛切入（缩短后加密度），
        //驻留起改走稀雨台阶、倒转段再加密；逆向沸腾退雨；收域随撕口合拢退场

        private void UpdateRainCurtain() {
            float density = RainBlend;
            if (Phase == KikasaDomainPhase.Closing) {
                density *= SpreadProgress;
            }
            else if (Phase == KikasaDomainPhase.Flipping) {
                if (FlipToRain) {
                    float pre = PhaseTimer <= KikasaDomain.FlipBoilEnd
                        ? 0.04f + FlipBoil * 0.10f
                        : PhaseTimer <= KikasaDomain.FlipDwellEnd ? 0.06f
                        : PhaseTimer < KikasaDomain.FlipCommitFrame
                            ? MathHelper.Lerp(0.06f, 0.2f,
                                (PhaseTimer - KikasaDomain.FlipDwellEnd)
                                / (float)(KikasaDomain.FlipCommitFrame - KikasaDomain.FlipDwellEnd))
                            : 0f;
                    density = MathF.Max(density, pre);
                }
                else {
                    //血还魂：雨在沸腾里被血气蒸散
                    density *= 1f - FlipBoil * 0.75f;
                }
            }
            else if (InDreamPhase) {
                //梦里无雨：拉入的沸腾把雨蒸散，归返结算后随 DreamBlend 退场自然回来
                density *= 1f - MathF.Max(DreamBlend,
                    Phase == KikasaDomainPhase.DreamPull ? DreamBoil * 0.75f : 0f);
            }
            RainCurtainDensity = density;
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
            if (Phase == KikasaDomainPhase.Flipping) {
                //沸腾把泡沫顶满
                target = 0.15f + FlipBoil;
            }
            else if (Phase == KikasaDomainPhase.Closing) {
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
                //倒转期音乐随沸腾压向死寂，雷声与水声接管
                KikasaDomainPhase.Flipping => MathHelper.Lerp(0.5f, 0.2f, FlipBoil),
                //拉入沿沸腾压向死寂；梦中维持低鸣，远吠与风声当主角
                KikasaDomainPhase.DreamPull => MathHelper.Lerp(0.5f, 0.15f, MathF.Max(DreamBoil, DreamBlend)),
                KikasaDomainPhase.Dreaming => 0.25f,
                KikasaDomainPhase.DreamReturn => 0.3f,
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

        //翻转包络的缓动，与入雨演出同源

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float CubicInOut(float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
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
            ReleaseTransitionFreeze();
            Phase = KikasaDomainPhase.Closed;
            PhaseTimer = 0;
            EffectTime = 0f;
            SpreadProgress = 0f;
            RiseT = 0f;
            PresenceSmooth = 0f;
            SoakDim = 0f;
            FoamBoost = 0f;
            IsRainForm = false;
            FlipToRain = false;
            RainBlend = 0f;
            RainCurtainDensity = 0f;
            thunderSoundDelay = 0;
            ZeroFlipEnvelopes();
            HoundReflection = false;
            DreamBlend = 0f;
            ZeroDreamEnvelopes();
            WispFireActive = false;
            WispRainProof = false;
            WispOriginX = 0f;
            WispT = 0f;
            WispSpread = 0f;
            WispQuench = 0f;
            LakeVigor = 1f;
            contactDone = false;
            riseBeatNear = false;
            riseBeatFar = false;
            lastCommandFrame = -1;
        }
    }
}
