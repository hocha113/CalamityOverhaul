using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTeleports;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 悬伞:普攻持有体,持伞常驻(由 CWRItem.heldProjType 的持有生成机制维持)。
    /// 常态悬在玩家背肩上方随行(Idle);检测到近敌且玩家未主动攻击时,
    /// 自行往目标方向倾靠过去按放缓节拍抛洒墨滴(AutoRain);
    /// 按住左键飞到头顶悬点绕柄自旋,按节拍自伞缘甩出大墨滴(<see cref="KikasaInkDrop"/>),
    /// 每拍出手前有一记反向蓄势(与领域倒转同一套动作语法);右键倒撑蓄墨(Flip/Pour)。
    /// 所有转移实时直入无前后摇:攻击态间直接变形,归位途中任意帧可再入,
    /// 只有墨瀑倾覆本体(猛倾+冲刷)不可打断。
    /// 鬼域传送(<see cref="KikasaTeleports.KikasaTeleport"/>)期间由本伞亲自执行:
    /// 检测到所有者的水舞台弹幕即入 Teleport 态跟拍,扎水→隐没→彼岸破水,
    /// 全程只有这一把伞;传送是本体位移,优先级高于倾覆锁。
    /// 状态机由所有者的原版同步控制位驱动,各端自走,无自定义网络包;
    /// 自动索敌的交战/换锁只在所有者端决断,经 State 与 ai[0] 的 netUpdate 补包收敛
    /// </summary>
    internal class KikasaRainUmbrella : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序与几何 ====================

        /// <summary>初次部署上浮帧数(刚拿起伞,从手中升到随行位)</summary>
        public const int RiseFrames = 16;

        /// <summary>出手前反向蓄势帧数</summary>
        public const int WindupFrames = 4;

        /// <summary>每波墨滴基数;域外/血湖用此值,鬼雨形态再加 2,再加栏位加成</summary>
        public const int DropsPerVolley = 1;

        /// <summary>收伞回手帧数</summary>
        public const int RecallFrames = 18;

        /// <summary>倾覆编舞:猛倾帧数/持续冲刷帧数/甩干回正帧数</summary>
        public const int PourTiltFrames = 5;
        public const int PourHoldFrames = 38;
        public const int PourShakeFrames = 12;

        /// <summary>悬点:玩家中心上方高度</summary>
        private const float HoverHeight = 92f;

        /// <summary>伞缘半径,墨滴的甩出点</summary>
        private const float RimRadius = 34f;

        /// <summary>光标搜敌半径与保底搜敌半径</summary>
        private const float CursorSeekRange = 520f;
        private const float FallbackSeekRange = 1100f;

        /// <summary>自动索敌半径与交战节奏:确认延迟/脱战再交战冷却/扫描步进(帧)</summary>
        private const float AutoSeekRange = 620f;
        private const int AutoEngageDelay = 40;
        private const int AutoReengageCooldown = 30;
        private const int AutoScanCadence = 8;

        /// <summary>自动攻击节拍放缓倍率:闲时自卫的火力让位于手动指挥</summary>
        private const float AutoRainTempoMul = 1.5f;

        /// <summary>常态随行锚点:背肩侧偏与悬高</summary>
        private const float IdleBackOffset = 30f;
        private const float IdleHeight = 58f;

        private enum UmbrellaState : byte { Rise, Hover, Recall, Flip, Pour, Idle, AutoRain, Teleport }

        /// <summary>自动索敌目标通道:存 whoAmI+1,0=无锁;随生成包与 netUpdate 补包同步</summary>
        private ref float AutoTargetAi => ref Projectile.ai[0];

        /// <summary>当前自动锁定目标,-1=无</summary>
        private int AutoTargetWho => (int)Projectile.ai[0] - 1;

        private UmbrellaState State {
            get => (UmbrellaState)Projectile.ai[1];
            set {
                if ((UmbrellaState)Projectile.ai[1] != value) {
                    Projectile.ai[1] = (float)value;
                    Projectile.ai[2] = 0f;
                    Projectile.netUpdate = true;
                }
            }
        }

        private ref float StateTimer => ref Projectile.ai[2];

        /// <summary>攻击态(含自动交战):祭符等"撑伞期间"语义读这里,常驻的闲伞不算</summary>
        internal bool IsRaining => IsAttackState(State);

        private static bool IsAttackState(UmbrellaState state)
            => state is UmbrellaState.Hover or UmbrellaState.Flip
            or UmbrellaState.Pour or UmbrellaState.AutoRain;

        /// <summary>该玩家的常驻伞此刻是否在攻击态:"撑伞期间"语义的对外口径(伞常驻后不能再看在场数)</summary>
        internal static bool OwnerIsRaining(Player owner) {
            int type = ModContent.ProjectileType<KikasaRainUmbrella>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == owner.whoAmI && proj.type == type
                    && proj.ModProjectile is KikasaRainUmbrella umbrella) {
                    return umbrella.IsRaining;
                }
            }
            return false;
        }

        private Player Owner => Main.player[Projectile.owner];

        //会话与自动索敌的端本地量:交战决断只在所有者端跑,旁观端跟同步状态走
        private UmbrellaState prevState;
        private int autoScanTimer;
        private int idleTime;
        private int manualCooldown;

        //表现状态:各端本地自走的连续量,不需同步
        private float spinPhase;
        private float spinSpeed;
        private float lean;
        private float bobPhase;
        private float visualScale;
        private Vector2 recallFrom;
        /// <summary>湿度:收伞时水膜退场</summary>
        private float wetness = 1f;
        /// <summary>节拍挤压:回拉蓄势时伞面绷紧</summary>
        private float beatSquash;
        /// <summary>甩雨后坐:出手拍向上一弹</summary>
        private float recoil;
        //鬼眼(锁定 telegraph 驱动)
        private float eyeOpen;
        private float eyeGlow;
        private Vector2 eyeLook = new(0f, 1f);

        /// <summary>伞面鬼眼锚点(帧内归一 uv)与半径,素材校准点</summary>
        private static readonly Vector2 EyeCenter = new(0.5f, 0.34f);
        private const float EyeRadius = 0.2f;

        /// <summary>当前视线目标,-1=无(眼睑垂着)</summary>
        private int gazeTarget = -1;
        private int blinkTimer = 150;

        //倒撑重击表现
        /// <summary>翻转进度 0=正撑 1=倒扣</summary>
        private float flipT;
        /// <summary>倾覆侧倾角(带符号)</summary>
        private float pourTilt;
        /// <summary>倾覆朝向:+1 右 -1 左</summary>
        private float pourDirSign = 1f;
        /// <summary>倾覆瞄准角(出手瞬间锁定,跟光标)</summary>
        private float pourAim = MathHelper.PiOver2;
        /// <summary>释放瞬间锁定的蓄力档</summary>
        private float pourFill;

        //鬼域传送跟拍
        /// <summary>传送隐显乘子:没入水中=0,常态=1,乘进伞体与伞下鬼的绘制</summary>
        private float teleportFade = 1f;
        /// <summary>扎水拍只放一次</summary>
        private bool teleportPlunged;
        /// <summary>破水拍只放一次</summary>
        private bool teleportPopped;

        //唤雨符:每 AI 帧解析一次的档与派发器快照(空绳零开销),绘制线程复用上一帧
        private KikasaTalismanProfile talismanProfile = KikasaTalismanProfile.Identity;
        private KikasaTalismanHookRunner talismanHooks;

        /// <summary>
        /// 蓄墨满帧:伞下鬼越多蓄得越快(90→56),口径在 <see cref="KikasaOverride"/>；
        /// 沛符再除蓄墨速率倍率,下限护住换挡拍的可读性
        /// </summary>
        private int CurrentChargeFrames {
            get {
                int frames = KikasaOverride.GetChargeFullFrames(KikasaOverride.GetSlotCount(Owner));
                float rate = talismanProfile.ChargeRateMul;
                return Math.Max((int)MathF.Round(frames / MathF.Max(rate, 0.01f)), 18);
            }
        }

        /// <summary>撑伞上浮帧数:霎符缩时经倍率折入,下限护住演出可读性</summary>
        private int CurrentRiseFrames
            => Math.Max((int)MathF.Round(RiseFrames * talismanProfile.RiseFramesMul), 2);

        /// <summary>蓄墨水位(表现):Flip 期随蓄力涨,Pour 期排空</summary>
        private float ChargeFill => State switch {
            UmbrellaState.Flip => MathHelper.Clamp(StateTimer / (float)CurrentChargeFrames, 0f, 1f),
            UmbrellaState.Pour => pourFill * (1f - MathHelper.Clamp(
                (StateTimer - PourTiltFrames) / (float)PourHoldFrames, 0f, 1f)),
            _ => 0f,
        };

        /// <summary>
        /// 一波墨雨节拍的完整解:周期/滴数/错拍/是否齐掷波。
        /// 滴数=域形态基数+每 3 格栏位一滴;周期随栏位缩短,再乘唤雨符节拍倍率
        /// (霖加密/沛放缓),但至少给出手窗留 4 帧回稳;
        /// S≥<see cref="KikasaOverride.TierGhostVolley"/> 时每第 4 波为齐掷波
        /// 出手拍全鬼同帧各掷一滴,不再占用错拍窗口。
        /// 唤雨符节奏挂钩在基准解之后叠改(霎三连/雹自造齐掷/澍雩时窗等),护栏再钳一次
        /// </summary>
        private void SolveVolleyRhythm(Player owner, out int period, out int dropCount,
            out int stagger, out bool ghostVolley) {
            int slots = KikasaOverride.GetSlotCount(owner);
            dropCount = VolleyDropCount(owner) + KikasaOverride.GetDropBonus(slots);
            stagger = KikasaOverride.GetDropStagger(slots);
            float tempoMul = talismanProfile.RainTempoMul;
            if (State == UmbrellaState.AutoRain) {
                //自动档放缓节拍:闲时自卫的火力让位于手动指挥
                tempoMul *= AutoRainTempoMul;
            }
            period = Math.Max((int)MathF.Round(KikasaOverride.GetVolleyPeriod(slots) * tempoMul),
                WindupFrames + dropCount * stagger + 4);
            ghostVolley = slots >= KikasaOverride.TierGhostVolley
                && (int)(StateTimer / period) % 4 == 3;

            if (!talismanHooks.IsEmpty) {
                KikasaVolleyRhythm rhythm = new() {
                    Period = period,
                    DropCount = dropCount,
                    Stagger = stagger,
                    GhostVolley = ghostVolley,
                };
                talismanHooks.ModifyVolleyRhythm(Projectile, ref rhythm);
                dropCount = Math.Max(rhythm.DropCount, 0);
                stagger = Math.Max(rhythm.Stagger, 1);
                ghostVolley = rhythm.GhostVolley;
                //出手窗+回稳帧的护栏在挂钩之后再钳一次,防节拍窗溢出周期
                period = Math.Max(rhythm.Period, WindupFrames + dropCount * stagger + 4);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Player owner = Owner;
            if (owner?.active != true || owner.dead
                || owner.HeldItem?.type != ModContent.ItemType<KikasaItem>()) {
                //持有条件破裂:人已不在持伞,直接谢幕不走收伞弧线
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;
            Projectile.velocity = Vector2.Zero;
            if (Main.myPlayer == Projectile.owner) {
                //持有生成只带基伤,逐帧补活伤(等级表/召唤加成/前缀);滴在所有者端按此出伤
                Projectile.damage = owner.GetWeaponDamage(owner.HeldItem);
            }

            //唤雨符快照:一帧一解,后续节拍/滴生成/挂钩全部复用(符位表在玩家身上)
            talismanProfile = KikasaTalismanCombat.Resolve(owner);
            talismanHooks = KikasaTalismanHooks.For(owner);

            bobPhase += 0.07f;
            UpdateLean(owner);
            if (manualCooldown > 0) {
                manualCooldown--;
            }

            if (StateTimer == 0f && State == UmbrellaState.Rise) {
                //初次部署拍:伞骨闷扫+一层薄水
                KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.62f, -0.22f, 2);
                KikasaInk.Play(SoundID.SplashWeak, Projectile.Center, 0.4f, -0.15f, 2);
            }

            //会话切拍:非攻击态⇄攻击态的边沿=一场雨的开拍/收拍,各端从状态变化同拍推得
            //(自动脱战只在所有者端决断转移,旁观端经补包在这里补齐起点/挂钩/演出)
            UmbrellaState state = State;
            if (state != prevState) {
                if (IsAttackState(state) && !IsAttackState(prevState)) {
                    OnAttackSessionStart();
                }
                else if (!IsAttackState(state) && IsAttackState(prevState)) {
                    //脱离攻击记冷却:自动索敌稍候再接手,松手的意图先被尊重
                    manualCooldown = AutoReengageCooldown;
                    OnAttackSessionEnd();
                }
                if (state == UmbrellaState.Recall) {
                    //远端经补包进归位时,本地兜底一个弧线起点
                    recallFrom = Projectile.Center;
                }
                idleTime = 0;
                autoScanTimer = 0;
                prevState = state;
            }

            //失能/入梦不销毁:常驻的伞只是收工归位,攻击态一律打回
            //(梦里 sustain 位还挂着也不许倒撑蓄下去,禁弹面只兜生成,姿态这里管)
            if ((owner.CCed || KikasaDream.DreamWorldAt(owner.Center)) && IsAttackState(State)) {
                BeginRecall();
            }

            //鬼域传送夺伞:各端看到所有者的水舞台弹幕即入态跟拍,无自定义包;
            //传送是本体位移,优先级高于倾覆锁
            if (State != UmbrellaState.Teleport
                && KikasaTeleportProj.FindFor(Projectile.owner) != null) {
                State = UmbrellaState.Teleport;
            }

            switch (State) {
                case UmbrellaState.Rise:
                    UpdateRise(owner);
                    break;
                case UmbrellaState.Hover:
                    UpdateHover(owner);
                    break;
                case UmbrellaState.Recall:
                    UpdateRecall(owner);
                    break;
                case UmbrellaState.Flip:
                    UpdateFlip(owner);
                    break;
                case UmbrellaState.Pour:
                    UpdatePour(owner);
                    break;
                case UmbrellaState.Idle:
                    UpdateIdle(owner);
                    break;
                case UmbrellaState.AutoRain:
                    UpdateAutoRain(owner);
                    break;
                case UmbrellaState.Teleport:
                    UpdateTeleport(owner);
                    break;
            }
            StateTimer++;
        }

        //==================== 状态推进 ====================

        private Vector2 HoverAnchor(Player owner)
            => owner.MountedCenter + new Vector2(owner.velocity.X * 2.2f,
                -HoverHeight * talismanProfile.HoverHeightMul + MathF.Sin(bobPhase) * 6f - recoil);

        /// <summary>初次部署:刚拿起伞,从手中升到随行位;攻击输入任意帧截断直入攻击态</summary>
        private void UpdateRise(Player owner) {
            float t = MathHelper.Clamp(StateTimer / (float)CurrentRiseFrames, 0f, 1f);
            //EaseOutBack:上浮带过冲再回落定位
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float e = 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * (t - 1f) * (t - 1f);
            Projectile.Center = Vector2.Lerp(owner.MountedCenter, IdleAnchor(owner), e);
            visualScale = MathHelper.Lerp(0.55f, 0.8f, MathHelper.Clamp(t * 1.4f, 0f, 1f));

            //自旋从零加速起转
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.25f, 0.09f);
            spinPhase += spinSpeed;

            if (!Main.dedServ) {
                //撑满拍:一圈水膜甩出去
                if (StateTimer == 1f) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 dir = (MathHelper.TwoPi * i / 10f).ToRotationVector2();
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            Projectile.Center + dir * RimRadius * 0.5f,
                            dir * Main.rand.NextFloat(2.4f, 4f) - Vector2.UnitY * 1.2f,
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(16, 26));
                    }
                }
                //加速段甩出螺旋墨珠:旋转要被看见
                else if ((int)StateTimer % 2 == 0) {
                    float xOff = MathF.Cos(spinPhase) * RimRadius * visualScale;
                    Vector2 rim = Projectile.Center + new Vector2(xOff, 2f);
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(rim,
                        new Vector2(MathF.Sign(xOff) * Main.rand.NextFloat(1.5f, 3f), -Main.rand.NextFloat(0.5f, 1.5f)),
                        KikasaInk.InkBody, Main.rand.NextFloat(0.28f, 0.44f))?.Configure(Main.rand.Next(14, 22));
                }
            }

            if (TryEnterCommandedAttack(owner)) {
                return;
            }
            if (StateTimer >= CurrentRiseFrames) {
                State = UmbrellaState.Idle;
            }
        }

        private void UpdateHover(Player owner) {
            Projectile.Center = Vector2.Lerp(Projectile.Center, HoverAnchor(owner), 0.22f);
            visualScale = MathHelper.Lerp(visualScale, 1f, 0.2f);
            wetness = MathHelper.Lerp(wetness, 1f, 0.2f);
            flipT = MathHelper.Lerp(flipT, 0f, 0.2f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);
            UpdateEye(owner);

            if (!owner.channel) {
                //松左即走:按着右键直接变形倒撑,否则归位;全程无死帧
                if (owner.controlUseTile && !KikasaDream.DreamWorldAt(owner.Center)) {
                    State = UmbrellaState.Flip;
                    //翻成倒扣:伞面一拧
                    KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.7f, -0.45f, 2);
                }
                else {
                    BeginRecall();
                }
                return;
            }

            TickVolleyBeat(owner, -1);
        }

        /// <summary>
        /// 墨雨节拍主体:回拉蓄势 → 出手窗猛甩 → 回稳;滴数与周期随域形态和伞下鬼数走。
        /// Hover 与 AutoRain 共用:forcedTarget≥0 时全部滴指向该目标(自动交战),
        /// 否则按光标就近轮转
        /// </summary>
        private void TickVolleyBeat(Player owner, int forcedTarget) {
            SolveVolleyRhythm(owner, out int period, out int dropCount, out int stagger, out bool ghostVolley);
            int slots = KikasaOverride.GetSlotCount(owner);
            int beat = (int)(StateTimer % period);
            int fireSpan = ghostVolley ? 4 : dropCount * stagger + 2;
            float targetSpin = beat < WindupFrames
                ? (ghostVolley ? -0.34f : -0.22f)
                : beat < WindupFrames + fireSpan ? (ghostVolley ? 1.05f : 0.74f) : 0.4f;
            spinSpeed = MathHelper.Lerp(spinSpeed, targetSpin, 0.28f);
            spinPhase += spinSpeed;

            //蓄势绷紧、出手回弹;齐掷波拉得更满
            beatSquash = MathHelper.Lerp(beatSquash,
                beat < WindupFrames ? (ghostVolley ? 0.13f : 0.08f) : 0f, 0.35f);
            recoil *= 0.8f;

            //伞缘闲滴:泡透的伞一直在滴
            if (!Main.dedServ && Main.rand.NextBool(24)) {
                float xOff = Main.rand.NextFloat(-1f, 1f) * RimRadius * visualScale;
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                    Projectile.Center + new Vector2(xOff, 6f), Vector2.Zero,
                    KikasaInk.InkBody, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(24, 36));
            }

            if (beat == WindupFrames) {
                //出手拍:湿掌甩墨+向上后坐+眼睛燃一下+碎珠随甩;齐掷拍整把伞一沉
                recoil = ghostVolley ? 7f : 4.5f;
                eyeGlow = MathF.Max(eyeGlow, ghostVolley ? 1f : 0.5f);
                KikasaInk.Play(KikasaInk.InkFlick, Projectile.Center,
                    ghostVolley ? 0.9f : 0.72f, ghostVolley ? -0.12f : 0.08f, 4);
                KikasaInk.Play(SoundID.SplashWeak, Projectile.Center, 0.42f, 0.12f, 4);
                if (ghostVolley) {
                    KikasaInk.Play(KikasaInk.InkSpray, Projectile.Center, 0.55f, -0.3f, 3);
                }
                if (!Main.dedServ) {
                    int beadCount = ghostVolley ? 6 : 2;
                    for (int i = 0; i < beadCount; i++) {
                        float xOff = MathF.Cos(spinPhase + i * 2.4f) * RimRadius * visualScale;
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            Projectile.Center + new Vector2(xOff, 3f),
                            new Vector2(MathF.Sign(xOff) * Main.rand.NextFloat(2f, 3.6f), -Main.rand.NextFloat(1f, 2.4f)),
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.3f, 0.46f))?.Configure(Main.rand.Next(14, 24));
                    }
                }
                //出手拍事件(霅节拍环/雹齐掷重音等),各端同拍一次
                talismanHooks.OnVolley(Projectile, (int)(StateTimer / period), ghostVolley);
            }
            if (ghostVolley) {
                //众鬼齐掷:出手拍全鬼同帧各掷一滴,超出常规滴数的那些是鬼滴
                if (beat == WindupFrames) {
                    for (int i = 0; i < slots; i++) {
                        FireDrop(i, ghostDrop: i >= dropCount, ghostVolley: true, forcedTarget: forcedTarget);
                    }
                }
            }
            else {
                //出手窗:错拍连甩
                if (beat >= WindupFrames && beat < WindupFrames + dropCount * stagger
                    && (beat - WindupFrames) % stagger == 0) {
                    FireDrop((beat - WindupFrames) / stagger, forcedTarget: forcedTarget);
                }
                //二鬼帮衬:窗口收尾再补一颗侧掷鬼滴,轮转槽位天然掷向下一个目标;
                //节拍解 DropCount=0(霅停雨拍等)时本拍整体无滴,帮衬滴一并停手,
                //否则出手条件退化成 beat==WindupFrames 照样漏滴
                if (dropCount > 0 && slots >= KikasaOverride.TierGhostAssist
                    && beat == WindupFrames + dropCount * stagger) {
                    FireDrop(dropCount, ghostDrop: true, forcedTarget: forcedTarget);
                }
            }
        }

        /// <summary>形态差异:血湖形态少而重、鬼雨形态密而细,域外基准一滴</summary>
        private int VolleyDropCount(Player owner) {
            KikasaDomainPlayer kdp = owner.GetModPlayer<KikasaDomainPlayer>();
            if (kdp.AnyActive) {
                return kdp.IsRainForm ? DropsPerVolley + 2 : DropsPerVolley;
            }
            return DropsPerVolley;
        }

        /// <summary>归位:攻击态收场,弧线滑回随行位;不销毁,任意帧可被新输入截回攻击</summary>
        private void UpdateRecall(Player owner) {
            if (TryEnterCommandedAttack(owner)) {
                return;
            }
            float t = MathHelper.Clamp(StateTimer / (float)RecallFrames, 0f, 1f);
            //二次贝塞尔回位弧线,控制点在侧上方
            Vector2 home = IdleAnchor(owner);
            Vector2 ctrl = (recallFrom + home) * 0.5f + new Vector2(0f, -46f);
            float u = 1f - t;
            Projectile.Center = u * u * recallFrom + 2f * u * t * ctrl + t * t * home;
            visualScale = MathHelper.Lerp(visualScale, 0.8f, 0.14f);
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.06f, 0.2f);
            spinPhase += spinSpeed;
            //水膜退到闲置薄膜,眼睛合上,翻转回正
            wetness = MathHelper.Lerp(wetness, 0.5f, 0.12f);
            eyeOpen = MathHelper.Lerp(eyeOpen, 0f, 0.3f);
            flipT = MathHelper.Lerp(flipT, 0f, 0.22f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);

            if (t >= 1f) {
                State = UmbrellaState.Idle;
            }
        }

        //==================== 常态随行与自动索敌 ====================

        /// <summary>随行锚点:背肩上方,配合慢速 lerp 移动时自然拖在身后</summary>
        private Vector2 IdleAnchor(Player owner)
            => owner.MountedCenter + new Vector2(-owner.direction * IdleBackOffset,
                -IdleHeight + MathF.Sin(bobPhase) * 5f);

        /// <summary>输入抢占:右键优先倒撑,左键墨雨;各端从同步控制位同拍直入,无前摇</summary>
        private bool TryEnterCommandedAttack(Player owner) {
            if (owner.CCed || KikasaDream.DreamWorldAt(owner.Center)) {
                return false;
            }
            if (owner.controlUseTile) {
                State = UmbrellaState.Flip;
                return true;
            }
            if (owner.channel) {
                State = UmbrellaState.Hover;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 物品使用入口(所有者端):指挥常驻伞直入攻击态;左=墨雨,右=倒撑蓄墨。
        /// 墨瀑倾覆本体(猛倾+冲刷)不被打断,倒完由持键自然接续
        /// </summary>
        internal void CommandAttack(bool alt) {
            if (State == UmbrellaState.Pour
                && StateTimer <= PourTiltFrames + PourHoldFrames) {
                return;
            }
            State = alt ? UmbrellaState.Flip : UmbrellaState.Hover;
        }

        /// <summary>
        /// 一场雨的开拍:非攻击态进攻击态时各端同拍派发。
        /// 撑伞拍音效/水膜从旧上浮段挪到这里;起雨挂钩(霎首拍预置/霁清零等)同点
        /// </summary>
        private void OnAttackSessionStart() {
            wetness = MathF.Max(wetness, 0.85f);
            KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.62f, -0.22f, 2);
            KikasaInk.Play(SoundID.SplashWeak, Projectile.Center, 0.4f, -0.15f, 2);
            talismanHooks.OnRainStart(Projectile);
            if (!Main.dedServ) {
                //一圈水膜甩出去:这就是撑伞拍
                for (int i = 0; i < 10; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / 10f).ToRotationVector2();
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(
                        Projectile.Center + dir * RimRadius * 0.5f,
                        dir * Main.rand.NextFloat(2.4f, 4f) - Vector2.UnitY * 1.2f,
                        KikasaInk.InkDeep, Main.rand.NextFloat(0.32f, 0.5f))?.Configure(Main.rand.Next(16, 26));
                }
            }
        }

        /// <summary>常态随行:半收的伞浮在背肩上方,留意四周;有敌且无人指使时自行接战</summary>
        private void UpdateIdle(Player owner) {
            idleTime++;
            Vector2 home = IdleAnchor(owner);
            Projectile.Center = Vector2.Lerp(Projectile.Center, home, 0.085f);
            //拴绳:高速位移(钩爪/坐骑/传送)时慢跟会被甩出屏幕,超距硬拉回绳长内
            Vector2 offset = Projectile.Center - home;
            if (offset.Length() > 220f) {
                Projectile.Center = home + offset.SafeNormalize(Vector2.Zero) * 220f;
            }
            visualScale = MathHelper.Lerp(visualScale, 0.8f, 0.12f);
            wetness = MathHelper.Lerp(wetness, 0.5f, 0.06f);
            flipT = MathHelper.Lerp(flipT, 0f, 0.2f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.06f, 0.1f);
            spinPhase += spinSpeed;
            beatSquash = MathHelper.Lerp(beatSquash, 0f, 0.3f);
            recoil *= 0.8f;
            UpdateEye(owner);

            //闲滴:泡透的伞一直在滴,只是慢些
            if (!Main.dedServ && Main.rand.NextBool(48)) {
                float xOff = Main.rand.NextFloat(-1f, 1f) * RimRadius * visualScale;
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                    Projectile.Center + new Vector2(xOff, 6f), Vector2.Zero,
                    KikasaInk.InkBody, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(24, 36));
            }

            if (TryEnterCommandedAttack(owner)) {
                return;
            }

            //自动索敌:所有者端决断,旁观端等 State/ai[0] 补包;
            //交战延迟与脱战冷却让"松手"的意图先被尊重
            if (Main.myPlayer != Projectile.owner
                || owner.CCed || KikasaDream.DreamWorldAt(owner.Center)
                || idleTime < AutoEngageDelay || manualCooldown > 0) {
                return;
            }
            if (++autoScanTimer < AutoScanCadence) {
                return;
            }
            autoScanTimer = 0;
            int who = FindAutoTarget(owner);
            if (who >= 0) {
                AutoTargetAi = who + 1;
                State = UmbrellaState.AutoRain;
            }
        }

        /// <summary>自动索敌:召唤师目标标记优先,其余按离玩家最近;要求伞到目标视线通畅</summary>
        private int FindAutoTarget(Player owner) {
            int marked = owner.MinionAttackTargetNPC;
            if (marked >= 0 && marked < Main.maxNPCs) {
                NPC npc = Main.npc[marked];
                if (npc?.active == true && npc.CanBeChasedBy(Projectile)
                    && Vector2.Distance(npc.Center, owner.Center) < AutoSeekRange * 1.4f
                    && Collision.CanHitLine(Projectile.Center, 1, 1, npc.position, npc.width, npc.height)) {
                    return marked;
                }
            }
            int best = -1;
            float bestDist = AutoSeekRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, owner.Center);
                if (dist < bestDist
                    && Collision.CanHitLine(Projectile.Center, 1, 1, npc.position, npc.width, npc.height)) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 自动交战:往目标方向倾靠一段(始终拴在玩家身边),按放缓节拍抛洒;
        /// 玩家任何主动输入立刻接管转攻光标,攻击流不断拍
        /// </summary>
        private void UpdateAutoRain(Player owner) {
            if (TryEnterCommandedAttack(owner)) {
                return;
            }

            NPC target = AutoTargetWho >= 0 && AutoTargetWho < Main.maxNPCs
                ? Main.npc[AutoTargetWho] : null;
            bool valid = target?.active == true && target.CanBeChasedBy(Projectile)
                && Vector2.Distance(target.Center, owner.Center) < AutoSeekRange * 1.5f;
            //视线每 30 帧复核(所有者端):目标钻进地形就换锁或收场,不对着墙白抛
            if (valid && Main.myPlayer == Projectile.owner && (int)StateTimer % 30 == 29
                && !Collision.CanHitLine(Projectile.Center, 1, 1,
                    target.position, target.width, target.height)) {
                valid = false;
            }
            bool banned = owner.CCed || KikasaDream.DreamWorldAt(owner.Center);
            if (!valid || banned) {
                if (Main.myPlayer == Projectile.owner) {
                    int next = banned ? -1 : FindAutoTarget(owner);
                    if (next >= 0) {
                        //换锁:目标没了顺手找下一个,交战不散场
                        AutoTargetAi = next + 1;
                        Projectile.netUpdate = true;
                    }
                    else {
                        BeginRecall();
                    }
                }
                //旁观端等所有者的换锁/归位补包,这帧原地稳住
                return;
            }

            //倾靠锚点:从头顶悬点往目标上方靠过去一段,拴绳护住"跟着玩家"的身份
            Vector2 overhead = HoverAnchor(owner);
            Vector2 toTarget = target.Center + new Vector2(0f, -140f) - overhead;
            float leanDist = MathF.Min(toTarget.Length() * 0.45f, 170f);
            Vector2 anchor = overhead + toTarget.SafeNormalize(Vector2.Zero) * leanDist;
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.16f);

            visualScale = MathHelper.Lerp(visualScale, 1f, 0.16f);
            wetness = MathHelper.Lerp(wetness, 1f, 0.15f);
            flipT = MathHelper.Lerp(flipT, 0f, 0.2f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);
            UpdateEye(owner);

            TickVolleyBeat(owner, target.whoAmI);
        }

        //==================== 倒撑重击:蓄墨与倾覆 ====================

        /// <summary>
        /// 倒扣蓄墨:伞翻转成器皿,墨在伞里蓄积
        /// "伞下无雨":你没淋过的雨都记在伞里,现在连本带利还给别人
        /// </summary>
        private void UpdateFlip(Player owner) {
            Vector2 anchor = HoverAnchor(owner) + new Vector2(0f, -12f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.2f);
            visualScale = MathHelper.Lerp(visualScale, 1f, 0.2f);

            //翻成倒扣,自旋慢下来，蓄力不是转出来的
            flipT = MathHelper.Lerp(flipT, 1f, 0.16f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.3f);
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.06f, 0.2f);
            spinPhase += spinSpeed;
            beatSquash = MathHelper.Lerp(beatSquash, 0f, 0.3f);
            recoil *= 0.8f;

            UpdateEye(owner);
            //蓄力越满眼睁越大,蓄势本身就是 telegraph
            float fill = ChargeFill;
            eyeOpen = MathF.Max(eyeOpen, fill * 0.85f);

            //三档换挡拍:一声比一声沉的水花,碗沿荡出一圈碎珠;满帧随伞下鬼数缩短
            int chargeFull = CurrentChargeFrames;
            if ((int)StateTimer == chargeFull / 3 || (int)StateTimer == chargeFull * 2 / 3
                || (int)StateTimer == chargeFull) {
                float tier = MathHelper.Clamp(StateTimer / (float)chargeFull, 0f, 1f);
                eyeGlow = MathF.Max(eyeGlow, 0.3f + 0.4f * tier);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.48f + 0.28f * tier, -0.25f - 0.35f * tier, 3);
                KikasaInk.Play(SoundID.Item21, Projectile.Center, 0.32f + 0.22f * tier, -0.4f - 0.25f * tier, 3);
                if (!Main.dedServ) {
                    for (int i = 0; i < 6; i++) {
                        float xOff = Main.rand.NextFloat(-1f, 1f) * RimRadius * 0.8f * visualScale;
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            BowlMouthPos() + new Vector2(xOff, -2f),
                            new Vector2(xOff * 0.04f, -Main.rand.NextFloat(1.2f, 2.4f) * tier),
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(Main.rand.Next(14, 22));
                    }
                }
            }

            //蓄近满时墨自碗沿外溢
            if (!Main.dedServ && fill > 0.88f && Main.rand.NextBool(7)) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                    BowlMouthPos() + new Vector2(side * RimRadius * 0.86f * visualScale, 2f),
                    new Vector2(side * 0.3f, 0.2f), KikasaInk.InkBody,
                    Main.rand.NextFloat(0.55f, 0.85f))?.Configure(Main.rand.Next(26, 38));
            }

            if (!owner.controlUseTile) {
                if (ChargeFill < 0.2f) {
                    //一档都不到就松手:忍住不泼;按着左键直接转墨雨,否则归位
                    if (owner.channel && !owner.CCed && !KikasaDream.DreamWorldAt(owner.Center)) {
                        State = UmbrellaState.Hover;
                    }
                    else {
                        BeginRecall();
                    }
                    return;
                }
                BeginPour(owner);
            }
        }

        private void BeginPour(Player owner) {
            pourFill = ChargeFill;
            //倾覆朝向:所有者按光标,旁观端按朝向，纯表现
            pourDirSign = Main.myPlayer == Projectile.owner
                ? MathF.Sign(Main.MouseWorld.X - Projectile.Center.X + 0.01f)
                : owner.direction;
            State = UmbrellaState.Pour;
            eyeOpen = 1f;
            eyeGlow = 1f;
            KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.65f, -0.55f, 2);
            KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.9f, -0.45f, 2);
        }

        /// <summary>倾覆:猛倾→墨瀑冲刷→甩干回正;结束后按输入续蓄或收伞</summary>
        private void UpdatePour(Player owner) {
            Vector2 anchor = HoverAnchor(owner) + new Vector2(pourDirSign * 10f, -8f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.2f);

            float tiltPhase = MathHelper.Clamp(StateTimer / (float)PourTiltFrames, 0f, 1f);
            bool shaking = StateTimer > PourTiltFrames + PourHoldFrames;
            if (!shaking) {
                //猛倾跟瞄准走:出手前跟光标,出手后锁角;侧倾随偏角加大,几乎倒平
                float tiltWant = 0.62f;
                if (Main.myPlayer == Projectile.owner) {
                    if (StateTimer <= PourTiltFrames) {
                        pourAim = (Main.MouseWorld - Projectile.Center).ToRotation();
                    }
                    pourDirSign = MathF.Sign(MathF.Cos(pourAim) + 1e-4f);
                    float fromDown = MathHelper.WrapAngle(pourAim - MathHelper.PiOver2);
                    tiltWant = MathHelper.Clamp(0.32f + MathF.Abs(fromDown) * 0.55f, 0.32f, 1.12f);
                }
                pourTilt = MathHelper.Lerp(pourTilt, tiltWant * (1.1f - 0.1f * tiltPhase), 0.4f);
            }
            else {
                //甩干:回正路上抖两下
                float st = (StateTimer - PourTiltFrames - PourHoldFrames) / (float)PourShakeFrames;
                pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.35f)
                    + MathF.Sin(st * MathHelper.Pi * 3f) * 0.06f * (1f - st);
            }
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.03f, 0.25f);
            spinPhase += spinSpeed;
            eyeGlow *= 0.92f;

            //倾覆拍:墨瀑出手(所有者端),各端同帧甩出一蓬碎珠与墨雾
            if ((int)StateTimer == PourTiltFrames) {
                KikasaInk.Play(KikasaInk.InkSpray, Projectile.Center, 0.7f + 0.2f * pourFill, -0.55f, 2);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.75f + 0.2f * pourFill, -0.3f, 2);
                if (Main.myPlayer == Projectile.owner) {
                    //跟光标走,不再卡在朝下 ±31°，倒撑是碗,但瞄准必须跟手
                    pourAim = (Main.MouseWorld - Projectile.Center).ToRotation();
                    int damage = (int)(Projectile.damage * (1.2f + 0.9f * pourFill)
                        * KikasaOverride.GetSlotDamageMul(KikasaOverride.GetSlotCount(owner)));
                    //墨瀑生成挂钩(霸月瀑打标等):标签经 ai[1] 量化编码随生成包同步
                    KikasaPourSpawnContext pourCtx = new() {
                        Aim = pourAim,
                        Fill = pourFill,
                        DamageMul = 1f,
                        TagId = 0,
                    };
                    talismanHooks.ModifyPourSpawn(ref pourCtx);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), BowlMouthPos(), Vector2.Zero,
                        ModContent.ProjectileType<KikasaInkPour>(),
                        (int)(damage * pourCtx.DamageMul), Projectile.knockBack * 1.5f,
                        Projectile.owner, pourCtx.Aim,
                        KikasaInkPour.PackFillTag(pourFill, pourCtx.TagId));
                }
                if (!Main.dedServ) {
                    //旁观这帧可能还没收到墨瀑包,用倾覆朝向保底
                    Vector2 pourDir = Main.myPlayer == Projectile.owner
                        ? pourAim.ToRotationVector2()
                        : new Vector2(pourDirSign, 1f).SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 9; i++) {
                        Vector2 vel = pourDir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5.5f);
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(BowlMouthPos() + Main.rand.NextVector2Circular(10f, 5f),
                            vel, Main.rand.NextBool(3) ? KikasaInk.InkDeep : KikasaInk.InkBody,
                            Main.rand.NextFloat(0.4f, 0.7f) * (0.8f + 0.4f * pourFill))?.Configure(Main.rand.Next(18, 30));
                    }
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_KikasaInkMist>(BowlMouthPos(),
                            pourDir.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.6f, 1.4f),
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.9f, 1.3f))?.Configure(Main.rand.Next(30, 44));
                    }
                }
            }

            //甩干回正只是尾巴:墨已倒完,新输入任意帧截断直入下一动作
            bool tailDone = StateTimer >= PourTiltFrames + PourHoldFrames + PourShakeFrames;
            if (shaking || tailDone) {
                bool free = !owner.CCed && !KikasaDream.DreamWorldAt(owner.Center);
                if (free && owner.controlUseTile) {
                    //按着不放:从空碗重新蓄
                    State = UmbrellaState.Flip;
                    KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.5f, -0.35f, 2);
                    return;
                }
                if (free && owner.channel) {
                    State = UmbrellaState.Hover;
                    return;
                }
                if (tailDone) {
                    BeginRecall();
                }
            }
        }

        /// <summary>倒扣时的碗口位置(蓄墨液面与墨瀑源头)</summary>
        private Vector2 BowlMouthPos()
            => Projectile.Center + new Vector2(0f, -4f * visualScale);

        //==================== 鬼域传送:夺伞跟拍 ====================

        /// <summary>
        /// 传送跟拍:水舞台(<see cref="KikasaTeleportProj"/>)是同一根时间轴的权威,
        /// 各端看到它即入态、它谢幕即出态。去程猛扎此岸潭口,
        /// 隐没期瞬挪彼岸潭口候场,破水带过冲弹回悬点;
        /// 出态直入下一动作(与其余转移同款,无死帧),观感始终一把伞
        /// </summary>
        private void UpdateTeleport(Player owner) {
            KikasaTeleportProj stage = KikasaTeleportProj.FindFor(Projectile.owner);
            if (stage == null) {
                //舞台谢幕:透明度还清,直入下一动作
                teleportFade = 1f;
                if (!TryEnterCommandedAttack(owner)) {
                    State = UmbrellaState.Idle;
                }
                return;
            }
            if (StateTimer == 0f) {
                recallFrom = Projectile.Center;
                teleportPlunged = false;
                teleportPopped = false;
            }
            //传送期姿态回正:伞就是一支扎进水里的镖
            flipT = MathHelper.Lerp(flipT, 0f, 0.3f);
            pourTilt = MathHelper.Lerp(pourTilt, 0f, 0.35f);
            beatSquash = MathHelper.Lerp(beatSquash, 0f, 0.3f);
            recoil *= 0.8f;
            wetness = 1f;

            if (stage.UmbrellaEmerging) {
                UpdateTeleportPop(owner, stage);
                return;
            }
            if (stage.UmbrellaHidden) {
                //没入水中:扎水拍在入水沿放一次,随后隐身瞬挪彼岸潭口候场
                if (!teleportPlunged) {
                    teleportPlunged = true;
                    TeleportPlungeBeat(stage.OriginPoolPos);
                }
                teleportFade = 0f;
                Projectile.Center = stage.DestPoolPos + new Vector2(0f, 4f);
                visualScale = 0.5f;
                spinSpeed = 0.9f;
                spinPhase += spinSpeed;
                return;
            }
            //去程:加速扎向此岸潭口,伞面收拢、转速拉满,拖出俯冲水线
            float dive = stage.UmbrellaDiveT;
            float dIn = dive * dive;
            Projectile.Center = Vector2.Lerp(recallFrom,
                stage.OriginPoolPos + new Vector2(0f, 2f), dIn);
            teleportFade = 1f;
            visualScale = MathHelper.Lerp(visualScale, 0.5f, 0.3f);
            spinSpeed = MathHelper.Lerp(spinSpeed, 1.05f, 0.4f);
            spinPhase += spinSpeed;
            if (!Main.dedServ) {
                Vector2 wake = (recallFrom - stage.OriginPoolPos).SafeNormalize(Vector2.Zero);
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    wake * Main.rand.NextFloat(1f, 2.2f), KikasaInk.InkBody,
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(Main.rand.Next(10, 16), 0.12f);
            }
        }

        /// <summary>扎水拍:入水沿的一声闷水与一蓬下压碎珠</summary>
        private void TeleportPlungeBeat(Vector2 poolPos) {
            KikasaInk.Play(SoundID.SplashWeak, poolPos, 0.5f, -0.2f, 3);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    poolPos + new Vector2(Main.rand.NextFloat(-16f, 16f), -2f),
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(1f, 3.4f)),
                    KikasaInk.InkDeep, Main.rand.NextFloat(0.26f, 0.42f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        /// <summary>破水弹回:EaseOutBack 过冲回悬点,伞面回张、鬼眼出水全睁,伞缘持续甩水</summary>
        private void UpdateTeleportPop(Player owner, KikasaTeleportProj stage) {
            if (!teleportPopped) {
                teleportPopped = true;
                eyeOpen = 1f;
                eyeGlow = 1f;
                KikasaInk.Play(KikasaInk.UmbrellaWhoosh, stage.DestPoolPos, 0.55f, 0.15f, 3);
                if (!Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 dir = (MathHelper.TwoPi * i / 10f).ToRotationVector2();
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            stage.DestPoolPos + dir * 10f - Vector2.UnitY * 8f,
                            dir * Main.rand.NextFloat(2.6f, 4.6f) - Vector2.UnitY * 2f,
                            Main.rand.NextBool(3) ? KikasaInk.BloodCore : KikasaInk.InkDeep,
                            Main.rand.NextFloat(0.3f, 0.48f))?.Configure(Main.rand.Next(16, 26));
                    }
                }
            }
            float t = stage.UmbrellaPopT;
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float e = 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * (t - 1f) * (t - 1f);
            Vector2 overhead = owner.MountedCenter + new Vector2(0f, -HoverHeight * 0.86f);
            Projectile.Center = Vector2.Lerp(stage.DestPoolPos + new Vector2(0f, 2f), overhead, e);
            teleportFade = MathHelper.Clamp(t * 4f, 0f, 1f);
            visualScale = MathHelper.Lerp(0.5f, 0.86f, MathHelper.Clamp(e, 0f, 1.2f));
            spinSpeed = MathHelper.Lerp(spinSpeed, 0.16f, 0.14f);
            spinPhase += spinSpeed;
            eyeOpen = MathF.Max(eyeOpen, t * 0.85f);
            //出水甩水:上升途中伞缘持续洒珠
            if (!Main.dedServ && t < 0.7f && (int)StateTimer % 2 == 0) {
                float xOff = MathF.Cos(spinPhase) * RimRadius * visualScale;
                PRTLoader.NewParticle<PRT_KikasaInkDrip>(
                    Projectile.Center + new Vector2(xOff, 4f), Vector2.Zero,
                    KikasaInk.InkBody, Main.rand.NextFloat(0.5f, 0.75f))?.Configure(Main.rand.Next(18, 28));
            }
        }

        /// <summary>转入归位:只做状态转移,收拍挂钩与演出在会话切拍块各端统一派发</summary>
        private void BeginRecall() {
            if (State == UmbrellaState.Recall) {
                return;
            }
            recallFrom = Projectile.Center;
            AutoTargetAi = 0f;
            State = UmbrellaState.Recall;
        }

        /// <summary>
        /// 一场雨的收拍:攻击态离场时各端同拍派发。
        /// 收伞挂钩(霁光结算等)在此;持有条件破裂的直接 Kill 不经状态切换,视作非主动收伞
        /// </summary>
        private void OnAttackSessionEnd() {
            talismanHooks.OnRecall(Projectile);
            KikasaInk.Play(KikasaInk.UmbrellaWhoosh, Projectile.Center, 0.4f, -0.5f, 2);
            //收拢拍抖落最后一圈墨珠
            if (!Main.dedServ) {
                for (int i = 0; i < 8; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / 8f + 0.3f).ToRotationVector2();
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(
                        Projectile.Center + dir * RimRadius * 0.4f,
                        dir * Main.rand.NextFloat(1.6f, 3f),
                        KikasaInk.InkBody, Main.rand.NextFloat(0.28f, 0.46f))?.Configure(Main.rand.Next(14, 24));
                }
            }
        }

        private void UpdateLean(Player owner) {
            //移动倾斜各端一致;光标倾斜只有所有者本机看得到,纯表现无碍
            float target = MathHelper.Clamp(owner.velocity.X * 0.028f, -0.16f, 0.16f);
            if (Main.myPlayer == Projectile.owner) {
                target += MathHelper.Clamp((Main.MouseWorld.X - owner.Center.X) * 0.0004f, -0.1f, 0.1f);
            }
            lean = MathHelper.Lerp(lean, target, 0.1f);
        }

        //==================== 鬼眼:雨随目光 ====================

        /// <summary>
        /// 锁定 telegraph:获取目标的瞬间猛地睁眼(伴一记低哑水响),
        /// 出手窗全睁、持锁半睁、无人时眼睑几乎垂死;偶发眨眼保活性。
        /// 视线纯表现，所有者按光标就近,旁观端按离所有者最近,端间近似无碍
        /// </summary>
        private void UpdateEye(Player owner) {
            //自动交战时视线锁死交战目标,其余按光标/就近
            int newTarget = State == UmbrellaState.AutoRain && AutoTargetWho >= 0
                ? AutoTargetWho : FindGazeTarget(owner);
            if (newTarget != gazeTarget) {
                if (newTarget >= 0) {
                    //睁眼拍:锁定瞬间
                    eyeOpen = MathF.Max(eyeOpen, 0.95f);
                    eyeGlow = MathF.Max(eyeGlow, 0.45f);
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = -0.85f, MaxInstances = 2 }, Projectile.Center);
                }
                gazeTarget = newTarget;
            }

            float openTarget;
            if (gazeTarget >= 0) {
                openTarget = 0.6f;
                //只有甩雨态存在节拍;倒撑态 StateTimer 是蓄力计时,不套周期
                if (State is UmbrellaState.Hover or UmbrellaState.AutoRain) {
                    SolveVolleyRhythm(owner, out int period, out int dropCount, out int stagger, out bool ghostVolley);
                    int beat = (int)(StateTimer % period);
                    int fireSpan = ghostVolley ? 4 : dropCount * stagger;
                    if (beat >= WindupFrames && beat < WindupFrames + fireSpan) {
                        openTarget = 0.9f;
                    }
                }
            }
            else {
                openTarget = 0.12f;
            }
            if (State == UmbrellaState.Idle) {
                //闲伞半阖:留意但不紧盯
                openTarget = MathF.Min(openTarget, 0.35f);
            }
            if (--blinkTimer <= 0) {
                blinkTimer = Main.rand.Next(130, 260);
            }
            if (blinkTimer < 5) {
                openTarget = 0f;
            }
            eyeOpen = MathHelper.Lerp(eyeOpen, openTarget, 0.22f);
            eyeGlow *= 0.86f;

            Vector2 lookTarget = gazeTarget >= 0
                ? (Main.npc[gazeTarget].Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                : new Vector2(lean * 2.5f, 1f).SafeNormalize(Vector2.UnitY);
            eyeLook = Vector2.Lerp(eyeLook, lookTarget, 0.15f).SafeNormalize(Vector2.UnitY);
        }

        private int FindGazeTarget(Player owner) {
            Vector2 anchor = Main.myPlayer == Projectile.owner ? Main.MouseWorld : owner.Center;
            float bestDist = Main.myPlayer == Projectile.owner ? CursorSeekRange : FallbackSeekRange;
            int best = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, anchor);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        //==================== 甩雨 ====================

        /// <summary>
        /// 甩出一滴:弹幕只在所有者端生成,生成包带走目标/坠落列/鬼滴与墨洼标记。
        /// 鬼滴(伞下鬼的侧掷)从对侧伞缘出手并换鬼青调;湖倾档(S≥10)全部大滴且落地留墨洼;
        /// forcedTarget≥0 时(自动交战)全部滴指向该目标,不再按光标轮转;
        /// 唤雨符滴生成挂钩在基准值备齐后叠改(霏雾化/雹巨雹/霓染色/霄高坠等),符标签随 ai[2] 高位同步
        /// </summary>
        private void FireDrop(int slot, bool ghostDrop = false, bool ghostVolley = false,
            int forcedTarget = -1) {
            if (Main.myPlayer != Projectile.owner) {
                return;
            }
            int target;
            float fallbackX;
            if (forcedTarget >= 0 && forcedTarget < Main.maxNPCs
                && Main.npc[forcedTarget].active
                && Main.npc[forcedTarget].CanBeChasedBy(Projectile)) {
                target = forcedTarget;
                fallbackX = Main.npc[forcedTarget].Center.X + Main.rand.NextFloat(-34f, 34f);
            }
            else {
                target = PickTarget(slot, out fallbackX);
            }

            //伞缘切向甩出:出点随自旋相位在伞沿摆动,初速偏外偏上;鬼滴走对侧相位。
            //上抛分量给足,抛洒段才真读作"把水抛上天"而不是小跳一下
            float yaw = MathF.Cos(spinPhase + (ghostDrop ? MathHelper.Pi : 0f));
            float xOff = yaw * RimRadius * visualScale;
            Vector2 rimPos = Projectile.Center + new Vector2(xOff, 2f);
            float side = xOff >= 0f ? 1f : -1f;
            Vector2 flickVel = new(side * Main.rand.NextFloat(3.2f, 5.8f), -Main.rand.NextFloat(4.2f, 7f));

            //形态差异:血湖滴少而重,鬼雨滴密而细
            float scale = 1f;
            float dmgMul = 1f;
            KikasaDomainPlayer kdp = Owner.GetModPlayer<KikasaDomainPlayer>();
            if (kdp.AnyActive) {
                if (kdp.IsRainForm) {
                    scale = 0.85f;
                    dmgMul = 0.72f;
                }
                else {
                    scale = 1.12f;
                    dmgMul = 1.15f;
                }
            }

            //伞下鬼乘区与湖倾档:滴变大,落地留墨洼;唤雨符乘区与积潦解锁叠在其上
            int slots = KikasaOverride.GetSlotCount(Owner);
            dmgMul *= KikasaOverride.GetSlotDamageMul(slots) * talismanProfile.DropDamageMul;
            bool ghost = ghostDrop;
            bool puddle = false;
            if (slots >= KikasaOverride.TierLakeTilt) {
                scale *= 1.2f;
                puddle = true;
            }
            else if (talismanProfile.PuddleUnlock) {
                //潦符:湖倾档之下也积洼,滴的大小不变(积洼是符的事,不是档位的事)
                puddle = true;
            }

            //滴生成挂钩:基准值全部备齐后派发,挂钩只做叠改
            KikasaDropSpawnContext dropCtx = new() {
                Position = rimPos,
                Velocity = flickVel,
                Scale = scale,
                DamageMul = dmgMul,
                Penetrate = 1,
                TargetWho = target,
                FallbackX = fallbackX,
                Ghost = ghost,
                Puddle = puddle,
                GhostVolley = ghostVolley,
                FromPourScatter = false,
                DropIndex = slot,
                TagId = 0,
                TagPayload = 0,
            };
            talismanHooks.ModifyDropSpawn(ref dropCtx);

            int flags = (dropCtx.Ghost ? KikasaInkDrop.FlagGhost : 0)
                | (dropCtx.Puddle ? KikasaInkDrop.FlagPuddle : 0)
                | KikasaTalismanHooks.PackTag(dropCtx.TagId, dropCtx.TagPayload);
            int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                dropCtx.Position, dropCtx.Velocity,
                ModContent.ProjectileType<KikasaInkDrop>(),
                (int)(Projectile.damage * dropCtx.DamageMul),
                Projectile.knockBack, Projectile.owner,
                dropCtx.TargetWho, dropCtx.FallbackX, flags);
            if (p >= 0 && p < Main.maxProjectiles) {
                Projectile drop = Main.projectile[p];
                //穿透只在归属端判伤,不需同步;体积走既有的补包路径
                if (dropCtx.Penetrate != 1) {
                    drop.penetrate = dropCtx.Penetrate;
                }
                if (dropCtx.Scale != 1f) {
                    drop.scale = dropCtx.Scale;
                    drop.netUpdate = true;
                }
            }
        }

        /// <summary>光标附近的敌人按距离轮转分配,无人则退回玩家身边最近的,再无则落向光标列</summary>
        private int PickTarget(int slot, out float fallbackX) {
            fallbackX = Main.MouseWorld.X + Main.rand.NextFloat(-34f, 34f);

            List<(float dist, int who)> nearCursor = [];
            int nearestWho = -1;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float cursorDist = Vector2.Distance(npc.Center, Main.MouseWorld);
                if (cursorDist < CursorSeekRange) {
                    nearCursor.Add((cursorDist, i));
                }
                float ownerDist = Vector2.Distance(npc.Center, Owner.Center);
                if (ownerDist < FallbackSeekRange && ownerDist < nearestDist) {
                    nearestDist = ownerDist;
                    nearestWho = i;
                }
            }
            if (nearCursor.Count > 0) {
                nearCursor.Sort((a, b) => a.dist.CompareTo(b.dist));
                return nearCursor[slot % nearCursor.Count].who;
            }
            return nearestWho;
        }

        //==================== 绘制(由 KikasaRainRender 集中调用) ====================

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>伪偏航自旋的公共布局:cos 过零翻面+横向压缩,不对称剪影的翻面读作绕柄旋转</summary>
        private bool SolveDrawLayout(out Texture2D tex, out Rectangle frame, out Vector2 pos,
            out float rotation, out Vector2 scale, out SpriteEffects flip, out Color light) {
            int itemType = ModContent.ItemType<KikasaItem>();
            Main.instance.LoadItem(itemType);
            tex = TextureAssets.Item[itemType]?.Value;
            frame = default;
            pos = default;
            rotation = 0f;
            scale = default;
            flip = SpriteEffects.None;
            light = Color.White;
            if (tex == null) {
                return false;
            }
            frame = Main.itemAnimations[itemType]?.GetFrame(tex) ?? tex.Frame();
            pos = Projectile.Center - Main.screenPosition;

            float yaw = MathF.Cos(spinPhase);
            flip = yaw < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            //节拍挤压:蓄势时纵向绷紧、横向微鼓
            scale = new Vector2((0.7f + 0.3f * MathF.Abs(yaw)) * (1f + beatSquash * 0.6f),
                1f - beatSquash) * visualScale;
            //倒撑翻转+倾覆侧倾都走旋转,翻到一半的过程本身就是演出
            rotation = lean + MathF.Sin(bobPhase) * 0.035f
                + flipT * MathHelper.Pi + pourTilt * pourDirSign;
            //传送隐显:着色器全程乘顶点色,乘光即干净隐身
            light = Lighting.GetColor(Projectile.Center.ToTileCoordinates()) * teleportFade;
            return teleportFade > 0.01f;
        }

        /// <summary>着色器路径:湿光扫掠/伞骨水膜/轮廓湿线/鬼眼全在 TechCanopy 里</summary>
        internal void DrawUmbrellaShader(SpriteBatch sb, Effect fx) {
            if (!SolveDrawLayout(out Texture2D tex, out Rectangle frame, out Vector2 pos,
                out float rotation, out Vector2 scale, out SpriteEffects flip, out Color light)) {
                return;
            }

            //蓄墨液面(TechFill):倒扣稳定后才显,画在伞体之下让伞缘盖住碗沿
            float fill = ChargeFill;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (fill > 0.02f && flipT > 0.8f && canvas != null) {
                fx.Parameters["uFill"]?.SetValue(fill);
                fx.Parameters["uSlosh"]?.SetValue(State == UmbrellaState.Pour ? 1f : 0.25f + 0.75f * fill);
                fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 4f);
                fx.CurrentTechnique = fx.Techniques["TechFill"];
                fx.CurrentTechnique.Passes[0].Apply();
                Vector2 mouth = BowlMouthPos() - Main.screenPosition;
                float w = 64f * visualScale;
                float h = 36f * visualScale;
                sb.Draw(canvas, mouth, null, Color.White, 0f, canvas.Size() * 0.5f,
                    new Vector2(w / canvas.Width, h / canvas.Height), SpriteEffects.None, 0f);
            }

            //翻面帧的采样 x 镜像,瞳向补一次镜像才保持世界朝向
            Vector2 look = eyeLook;
            if (flip == SpriteEffects.FlipHorizontally) {
                look.X = -look.X;
            }
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                frame.X / (float)tex.Width, frame.Y / (float)tex.Height,
                frame.Width / (float)tex.Width, frame.Height / (float)tex.Height));
            fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
            fx.Parameters["uSpinPhase"]?.SetValue(spinPhase);
            fx.Parameters["uSpinSpeed"]?.SetValue(MathHelper.Clamp(MathF.Abs(spinSpeed) / 0.74f, 0f, 1f));
            fx.Parameters["uWet"]?.SetValue(wetness);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.173f % 4f);
            fx.Parameters["uEye"]?.SetValue(eyeOpen);
            fx.Parameters["uEyeLook"]?.SetValue(look);
            fx.Parameters["uEyeGlow"]?.SetValue(eyeGlow);
            fx.Parameters["uEyeCenter"]?.SetValue(EyeCenter);
            fx.Parameters["uEyeR"]?.SetValue(EyeRadius);
            fx.CurrentTechnique = fx.Techniques["TechCanopy"];
            fx.CurrentTechnique.Passes[0].Apply();

            DrawLayered(sb, tex, frame, pos, rotation, scale, flip, light);
        }

        /// <summary>精灵回退:同一布局的裸贴图</summary>
        internal void DrawUmbrella(SpriteBatch sb) {
            if (!SolveDrawLayout(out Texture2D tex, out Rectangle frame, out Vector2 pos,
                out float rotation, out Vector2 scale, out SpriteEffects flip, out Color light)) {
                return;
            }
            DrawLayered(sb, tex, frame, pos, rotation, scale, flip, light);
        }

        /// <summary>自旋残影+本体:高转速时两侧各一道淡影,旋转要被看见</summary>
        private void DrawLayered(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 pos,
            float rotation, Vector2 scale, SpriteEffects flip, Color light) {
            Vector2 origin = frame.Size() * 0.5f;
            float smear = MathHelper.Clamp(MathF.Abs(spinSpeed) * 1.2f, 0f, 0.55f);
            if (smear > 0.08f) {
                sb.Draw(tex, pos, frame, light * (smear * 0.3f), rotation - 0.1f * MathF.Sign(spinSpeed),
                    origin, scale * 0.98f, flip, 0f);
                sb.Draw(tex, pos, frame, light * (smear * 0.18f), rotation - 0.2f * MathF.Sign(spinSpeed),
                    origin, scale * 0.95f, flip, 0f);
            }
            sb.Draw(tex, pos, frame, light, rotation, origin, scale, flip, 0f);
        }

        /// <summary>
        /// 伞下鬼:每个召唤栏位一只,吊在伞骨下沿弧排开,档位换魂色
        /// 玩家一眼读出当前强度档。黑底贴图按 A=0 加色,暗体用真透明的 Extra_98;
        /// 收伞随湿度退场。由 <see cref="KikasaRainRender"/> 在伞体之后另开无着色器批调用
        /// </summary>
        internal void DrawCanopyGhosts(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D body = CWRAsset.Extra_98?.Value;
            if (glow == null || body == null) {
                return;
            }
            float fade = MathHelper.Clamp(visualScale * 1.6f - 0.6f, 0f, 1f)
                * MathHelper.Clamp((wetness - 0.15f) / 0.85f, 0f, 1f) * teleportFade;
            if (fade <= 0.03f) {
                return;
            }
            int slots = KikasaOverride.GetSlotCount(Owner);
            int tier = KikasaOverride.GetTier(slots);
            //档位魂色:细雨灰青 → 帮衬青白 → 齐掷鬼青 → 湖倾血芯
            Color soul = tier switch {
                3 => new Color(214, 84, 92),
                2 => new Color(148, 216, 210),
                1 => new Color(126, 176, 188),
                _ => new Color(104, 128, 140),
            };
            Vector2 anchor = Projectile.Center - Main.screenPosition;
            //倒撑时鬼群跟着翻到碗口上方
            float hangSign = flipT > 0.5f ? -1f : 1f;
            for (int i = 0; i < slots; i++) {
                //沿伞骨下沿弧排开,随自旋缓摆,逐鬼错拍浮沉
                float u = slots == 1 ? 0.5f : i / (float)(slots - 1);
                float ang = MathHelper.Lerp(-1.05f, 1.05f, u)
                    + MathF.Sin(spinPhase * 0.35f + i * 1.7f) * 0.1f;
                float hang = (11f + MathF.Sin(bobPhase * 1.3f + i * 2.3f) * 2.6f) * hangSign;
                Vector2 pos = anchor + new Vector2(
                    MathF.Sin(ang) * RimRadius * 0.86f * visualScale,
                    (MathF.Cos(ang) * 5f + hang) * visualScale);
                float pulse = 0.82f + MathF.Sin(bobPhase * 2f + i * 2.9f) * 0.18f;
                float sizePx = (13f + 2.6f * tier) * visualScale * pulse;

                //鬼身:暗滴为体,魂色为芯(A=0 加色),顶上一粒白眼点
                sb.Draw(body, pos, null, KikasaInk.InkBody * (0.85f * fade), 0f,
                    body.Size() * 0.5f,
                    new Vector2(sizePx * 0.6f / body.Width, sizePx * 1.05f / body.Height),
                    SpriteEffects.None, 0f);
                Color core = soul with { A = 0 };
                sb.Draw(glow, pos, null, core * (0.5f * fade * pulse), 0f,
                    glow.Size() * 0.5f, sizePx * 2.4f / glow.Width, SpriteEffects.None, 0f);
                sb.Draw(glow, pos - new Vector2(0f, sizePx * 0.18f), null,
                    (Color.White with { A = 0 }) * (0.28f * fade * pulse), 0f,
                    glow.Size() * 0.5f, sizePx * 0.8f / glow.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
