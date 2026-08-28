using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 恶犬（P2 计划书 S3）：鬼梦第一个真实敌人。九态状态机
    /// Emerge 凝实 → Patrol 巡行 ⇄ Alert 起疑 → Search 搜索 → Fade 化雾退场，
    /// 警觉满值任意隐蔽态转 Chase 追击 → Lunge 扑咬 → Drag 拖咬 → Stagger 硬直 → Chase。<br/>
    /// 联机合同：ai[0]=状态 ai[1]=状态计时（巡行态兼嗅地钟：正=下次嗅地倒数，负=嗅地定格）
    /// ai[2]=警觉 0..100 ai[3]=锚 X（巡逻中心/搜索焦点/追击最后目击）；
    /// 状态转移、随机滚动、警觉积分全在权威端（服务器/单人），客户端从 ai[] 重放运动与演出；
    /// 伤害窗每帧从状态重算（不吃转瞬快照毒化）；Drag 拉力在受害端本地施加
    /// （<see cref="KiyumeHoundDragPlayer"/>），松口=打它（justHit 服务器可见，零新包）。<br/>
    /// 伤害窗纪律：平时 damage=0，仅 Lunge 扑出 10t 与 Drag 期设回（Lurker/Warden 门控惯例）。
    /// 绘制全接管：狼帧 3-9 + KikasaHound.fx 实体态（参数链照抄 KiyumeHoundShade.DrawOne），
    /// 化雾走 uDissolve 不降 alpha；shader 缺编回退近黑剪影；透明度全程显式（VFX 缺陷②）。
    /// 雾后双目辉光另见 <see cref="KiyumeHoundEyeGleam"/>（PostDrawTiles 尾）
    /// </summary>
    internal class KiyumeHound : ModNPC
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>
        /// 犬类型注册表（裁决 21）：P5 犬位扫描（KiyumeSoundscape.ResolveHoundTypes）只读消费。
        /// SetStaticDefaults 填充，加载前恒空
        /// </summary>
        internal static int[] KiyumeHoundTypes = [];

        /// <summary>
        /// 哀鸣抚恤旗标（1.4C）：有犬被杀后当潮位周期不补员。
        /// W3 P2-D 导演消费：潮位过 0.5 上穿沿（读 KiyumeFogTide.Tide）清零恢复补员；
        /// 会话复位挂在 <see cref="KiyumeHoundHints"/>。世界级状态、只在权威端写，static 合法
        /// </summary>
        internal static bool RecruitHoldUntilTideRise;

        //──── ai[0] 状态位 ────
        internal const int StateEmerge = 0;
        internal const int StatePatrol = 1;
        internal const int StateAlert = 2;
        internal const int StateSearch = 3;
        internal const int StateChase = 4;
        internal const int StateLunge = 5;
        internal const int StateDrag = 6;
        internal const int StateStagger = 7;
        internal const int StateFade = 8;

        /// <summary>ai[0]：状态</summary>
        private ref float State => ref NPC.ai[0];
        /// <summary>ai[1]：状态计时（巡行态：正=嗅地倒数，负=嗅地定格计升）</summary>
        private ref float StateTimer => ref NPC.ai[1];
        /// <summary>ai[2]：警觉 0..100（EyeGleam 与教学提示的客户端读数）</summary>
        private ref float Awareness => ref NPC.ai[2];
        /// <summary>ai[3]：锚 X（巡逻中心 / 搜索焦点 / 追击最后目击，按状态复用）</summary>
        private ref float AnchorX => ref NPC.ai[3];

        //──── 绘制校准（与 KiyumeHoundShade 同源） ────
        private const float HoundScale = 1.18f;
        private static readonly Vector2 EyeAnchor = new(0.17f, 0.38f);
        private static readonly Color EdgeTint = new(112, 26, 26);

        //──── 眼光包络（纯演出：巡逻呼吸/起疑/追击/拖咬） ────
        private const float EyePatrol = 0.18f;
        private const float EyeAlert = 0.45f;
        private const float EyeChase = 0.85f;
        private const float EyeDrag = 1.0f;

        //──── 权威端字段（不入同步：转移只在权威端裁决，客户端无需重放这些） ────

        /// <summary>起疑凝视时长（进入时滚动）</summary>
        private int alertHold;
        /// <summary>追击丢失累计（视线断且听觉低）</summary>
        private int lostTicks;
        /// <summary>本轮采样：视线/听觉暴露（对玩家取 max，防 NoiseAt 场按人头重复计入）</summary>
        private float sightNow;
        private float soundNow;
        /// <summary>最佳感知者与其位置（转移时写 NPC.target / AnchorX）</summary>
        private int perceptWho = -1;
        private float perceptX;
        /// <summary>嗅迹追踪（点子 13，仅搜索态）：当前迹点=沿迹时的查询心</summary>
        private Vector2 scentFocus;
        /// <summary>迹感余量：>0=迹在（搜索不落幕），耗尽=迹断（恢复原折返与超时判定）</summary>
        private int scentLinger;

        //──── 各端本地演出字段 ────

        private int frame = 3;
        private float frameCounter;
        private float bodyPitch;
        private float eyeGlowSmooth;
        private bool prevSniffing;
        /// <summary>沿迹演出（各端本地）：搜索锚上次读数与鼻尖尘粒加密余量</summary>
        private float prevSearchAnchorX;
        private int scentDustTicks;

        private float Seed => NPC.whoAmI * 0.613f;
        private bool Authority => !VaultUtils.isClient;

        /// <summary>嗅地中（巡行态计时为负 / 搜索态折返端点驻足）：帧定格、躯干前倾、听觉折减</summary>
        private bool Sniffing =>
            ((int)State == StatePatrol && StateTimer < 0f)
            || ((int)State == StateSearch && MathF.Abs(MathF.Sin(StateTimer * 0.021f)) > 0.86f);

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            //图鉴一律 Hide（裁决 14）
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            KiyumeHoundTypes = [Type];
        }

        public override void SetDefaults() {
            NPC.width = 64;
            NPC.height = 34;
            //平时零伤害，仅 Lunge 扑出窗与 Drag 期由状态每帧重算设回
            NPC.damage = 0;
            NPC.defense = KiyumeHoundMetrics.HoundDefense;
            NPC.lifeMax = KiyumeHoundMetrics.HoundLife;
            NPC.knockBackResist = KiyumeHoundMetrics.HoundKBResist;
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.value = 0;
            NPC.npcSlots = 1f;
            NPC.HitSound = SoundID.NPCHit1 with { Volume = 0.8f, Pitch = -0.35f };
            //哀鸣即死亡音：原版死亡同步自动在各端播，不占状态沿
            NPC.DeathSound = SoundID.NPCDeath6 with { Volume = 0.9f, Pitch = -0.4f };
        }

        //导演管生死，Fade 是唯一自然离场：不参与原版远离 despawn
        public override bool CheckActive() => false;

        //==================== AI ====================

        public override void AI() {
            //鬼梦门控：绝不泄漏到主世界与其他子世界
            if (!KiyumeWorld.Active) {
                NPC.active = false;
                return;
            }
            //出生透明度显式清零（VFX 缺陷②）：绘制全接管不读 NPC.alpha，这里兜底防全局层误读
            NPC.alpha = 0;
            //伤害窗每帧从状态重算（netcode 2.10：转瞬快照不会把零毒化成常态，各端同式）
            NPC.damage = 0;

            //出生初始化：导演写 ai[3] 锚点，异常生成用当前位自锚
            if (NPC.localAI[0] == 0f) {
                NPC.localAI[0] = 1f;
                if (AnchorX <= 0f) {
                    AnchorX = NPC.Center.X;
                }
                if (Authority) {
                    FaceNearestPlayer();
                }
            }

            bool authority = Authority;
            if (authority) {
                if (NPC.justHit) {
                    HandleHit();
                }
                SampleSenses();
            }

            switch ((int)State) {
                case StatePatrol:
                    UpdatePatrol(authority);
                    break;
                case StateAlert:
                    UpdateAlert(authority);
                    break;
                case StateSearch:
                    UpdateSearch(authority);
                    break;
                case StateChase:
                    UpdateChase(authority);
                    break;
                case StateLunge:
                    UpdateLunge(authority);
                    break;
                case StateDrag:
                    UpdateDrag(authority);
                    break;
                case StateStagger:
                    UpdateStagger(authority);
                    break;
                case StateFade:
                    UpdateFade(authority);
                    break;
                default:
                    UpdateEmerge(authority);
                    break;
            }

            //自写步态无原版双端等同模拟兜底：低频重发 SyncNPC 钳住位置/警觉漂移（netcode 3.2）
            ServerSyncPacer();
            if (!Main.dedServ) {
                UpdatePresentation();
            }
        }

        //==================== 侦测消费（权威端，6t 节流） ====================

        private void SampleSenses() {
            if ((Main.GameUpdateCount + (uint)NPC.whoAmI) % KiyumeHoundMetrics.HoundSenseTicks != 0) {
                return;
            }
            sightNow = 0f;
            soundNow = 0f;
            int best = -1;
            float bestScore = 0f;
            float bestX = AnchorX;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float sight = KiyumeStealthSense.SightExposure(NPC, player, KiyumeHoundMetrics.HoundSight);
                float sound = KiyumeStealthSense.SoundExposure(NPC, player, KiyumeHoundMetrics.HoundHearing);
                //SoundExposure 已并入 NoiseAt 场采样：对玩家取 max 而非求和，防环境噪声按人头重复计入
                sightNow = MathF.Max(sightNow, sight);
                soundNow = MathF.Max(soundNow, sound);
                float score = sight * KiyumeHoundMetrics.GainSight + sound * KiyumeHoundMetrics.GainHear;
                if (score > bestScore) {
                    bestScore = score;
                    best = player.whoAmI;
                    bestX = player.Center.X;
                }
            }
            //嗅地窗它是聋的：玩家的移动窗口
            if (Sniffing) {
                soundNow *= KiyumeHoundMetrics.SniffHearingMul;
            }
            if (best >= 0 && bestScore > 0.01f) {
                perceptWho = best;
                perceptX = bestX;
            }

            int state = (int)State;
            if (state is StateChase or StateLunge or StateDrag or StateStagger) {
                //战斗态警觉锁满：退火只走追击丢失判定
                Awareness = KiyumeHoundMetrics.ChaseThreshold;
                return;
            }
            float gain = sightNow * KiyumeHoundMetrics.GainSight + soundNow * KiyumeHoundMetrics.GainHear;
            float decay = state switch {
                StateAlert => KiyumeHoundMetrics.DecayAlert,
                StateSearch => KiyumeHoundMetrics.DecaySearch,
                _ => KiyumeHoundMetrics.DecayPatrol,
            };
            Awareness = MathHelper.Clamp(
                Awareness + (gain - decay) * KiyumeHoundMetrics.HoundSenseTicks,
                0f, KiyumeHoundMetrics.ChaseThreshold);

            //三档阈值转移（Fade 已承诺离场、Emerge 未成形，都不被警觉打断，只被 justHit 打断）
            if (state is StateEmerge or StateFade) {
                return;
            }
            if (Awareness >= KiyumeHoundMetrics.ChaseThreshold) {
                EnterChase(fresh: true);
            }
            else if (state == StatePatrol
                && Awareness >= KiyumeHoundMetrics.AlertThreshold && gain > 0f) {
                EnterAlert();
            }
            else if (state == StateAlert && Awareness >= KiyumeHoundMetrics.SearchThreshold) {
                EnterSearch(perceptX);
            }
        }

        //受击宣战（镜像 Lurker 受击现身）：隐蔽/巡逻态直满进追击；拖咬立即松口
        private void HandleHit() {
            int state = (int)State;
            if (state == StateDrag) {
                EnterStagger();
                return;
            }
            if (state is StateEmerge or StatePatrol or StateAlert or StateSearch or StateFade) {
                int hitter = NPC.lastInteraction;
                if (hitter >= 0 && hitter < Main.maxPlayers && Main.player[hitter].active
                    && !Main.player[hitter].dead) {
                    perceptWho = hitter;
                    perceptX = Main.player[hitter].Center.X;
                }
                Awareness = KiyumeHoundMetrics.ChaseThreshold;
                EnterChase(fresh: true);
            }
        }

        //==================== 状态转移（只在权威端调） ====================

        private void EnterPatrol() {
            State = StatePatrol;
            AnchorX = NPC.Center.X;
            StateTimer = RollSniffCountdown();
            NPC.netUpdate = true;
        }

        private void EnterAlert() {
            State = StateAlert;
            StateTimer = 0f;
            alertHold = Main.rand.Next(
                KiyumeHoundMetrics.AlertHoldMinTicks, KiyumeHoundMetrics.AlertHoldMaxTicks + 1);
            if (perceptWho >= 0) {
                NPC.target = perceptWho;
            }
            Face(MathF.Sign(perceptX - NPC.Center.X));
            NPC.netUpdate = true;
        }

        private void EnterSearch(float focusX) {
            State = StateSearch;
            StateTimer = 0f;
            AnchorX = focusX;
            //嗅迹从头拾取：上一轮的迹感不带进新搜索
            scentFocus = default;
            scentLinger = 0;
            Awareness = MathF.Max(Awareness, KiyumeHoundMetrics.SearchThreshold);
            NPC.netUpdate = true;
        }

        /// <summary>fresh=正规起追（蓄力+长嚎+连锁）；否则从扑咬/硬直返场，跳过前摇不重嚎
        /// （预置 +1 越过长嚎的 ==精确拍：入场帧演出就能看见计时，恰等于拍值会重嚎）</summary>
        private void EnterChase(bool fresh) {
            State = StateChase;
            StateTimer = fresh ? 0f : KiyumeHoundMetrics.ChaseWindupTicks + 1;
            Awareness = KiyumeHoundMetrics.ChaseThreshold;
            lostTicks = 0;
            if (perceptWho >= 0) {
                NPC.target = perceptWho;
            }
            if (fresh) {
                //嚎叫连锁（点子 7）：一家犬吠百家应，长嚎给全图同类抬警觉
                ShareAwareness(NPC.Center.X, forceSearch: false);
            }
            NPC.netUpdate = true;
        }

        private void EnterLunge() {
            State = StateLunge;
            StateTimer = 0f;
            NPC.netUpdate = true;
        }

        private void EnterDrag(int victim) {
            State = StateDrag;
            StateTimer = 0f;
            NPC.target = victim;
            //向雾浓处倒拖：采样两侧解析浓度定方向（速度经转移同步，客户端顺着惯性重放）
            float left = KiyumeStealthSense.FogConcealmentAt(NPC.Center - new Vector2(320f, 0f));
            float right = KiyumeStealthSense.FogConcealmentAt(NPC.Center + new Vector2(320f, 0f));
            int dragDir = left >= right ? -1 : 1;
            NPC.velocity.X = dragDir * KiyumeHoundMetrics.DragCarrySpeed;
            NPC.netUpdate = true;
        }

        private void EnterStagger() {
            State = StateStagger;
            StateTimer = 0f;
            //松口后撤半步
            NPC.velocity.X = -NPC.direction * 2.5f;
            NPC.netUpdate = true;
        }

        private void EnterFade() {
            State = StateFade;
            StateTimer = 0f;
            NPC.netUpdate = true;
        }

        //==================== 各态推进 ====================

        //凝实入场：影子从雾里走出来，身体先成、双目后亮（演出见 Dissolve01/EyeGlow01）
        private void UpdateEmerge(bool authority) {
            StateTimer++;
            NPC.velocity.X = NPC.direction * 0.4f;
            if (authority && StateTimer >= KiyumeHoundMetrics.EmergeTicks) {
                EnterPatrol();
            }
        }

        //巡行：锚点±范围低速小跑，间歇停下嗅地（嗅地钟走 ai[1] 符号位，滚动只在权威端）
        private void UpdatePatrol(bool authority) {
            TickSniffClock(authority);
            if (StateTimer < 0f) {
                //嗅地定格
                NPC.velocity.X *= 0.8f;
                return;
            }
            float range = KiyumeHoundMetrics.PatrolRangeCols * 16f;
            if (NPC.Center.X > AnchorX + range) {
                Face(-1);
            }
            else if (NPC.Center.X < AnchorX - range) {
                Face(1);
            }
            else if (NPC.collideX && NPC.velocity.Y == 0f) {
                //巡逻撞墙不跳，转身（跳跃是追击的专利，巡行要慢要闷）
                Face(-NPC.direction);
            }
            WalkTowards(NPC.direction, KiyumeHoundMetrics.PatrolSpeed);
        }

        private void TickSniffClock(bool authority) {
            if (StateTimer > 0f) {
                if (authority) {
                    if (--StateTimer <= 0f) {
                        StateTimer = -KiyumeHoundMetrics.SniffHoldTicks;
                        NPC.netUpdate = true;
                    }
                }
                else if (StateTimer > 1f) {
                    //客户端钳在 1 等权威翻沿，不自滚随机
                    StateTimer--;
                }
            }
            else if (StateTimer < 0f) {
                if (authority) {
                    if (++StateTimer >= 0f) {
                        StateTimer = RollSniffCountdown();
                        NPC.netUpdate = true;
                    }
                }
                else if (StateTimer < -1f) {
                    StateTimer++;
                }
            }
        }

        private float RollSniffCountdown() => Main.rand.Next(
            KiyumeHoundMetrics.SniffIntervalMinTicks, KiyumeHoundMetrics.SniffIntervalMaxTicks + 1);

        //起疑：急停凝视，头抬起耳朵竖起读成眼光突亮；平息回巡逻、坐实进搜索（转移在采样器）
        private void UpdateAlert(bool authority) {
            StateTimer++;
            NPC.velocity.X *= 0.8f;
            if (authority) {
                Face(MathF.Sign(perceptX - NPC.Center.X));
                if (Awareness < KiyumeHoundMetrics.AlertThreshold || StateTimer >= alertHold) {
                    //平息或凝视到时仍未坐实：回巡逻（警觉保留，自然衰减）
                    EnterPatrol();
                }
            }
        }

        //搜索：小跑至最后感知点±6 tile 折返，端点驻足嗅地；一轮无果化雾退场。
        //嗅迹消费（点子 13）与折返并行：TrackScent 沿迹链推进锚点，折返/嗅地围着新锚照旧演
        private void UpdateSearch(bool authority) {
            StateTimer++;
            if (authority) {
                TrackScent();
            }
            if (Sniffing) {
                NPC.velocity.X *= 0.8f;
            }
            else {
                float targetX = AnchorX + MathF.Sin(StateTimer * 0.021f) * 6f * 16f;
                int dir = MathF.Abs(targetX - NPC.Center.X) < 12f ? 0 : MathF.Sign(targetX - NPC.Center.X);
                if (dir != 0) {
                    Face(dir);
                    WalkTowards(dir, KiyumeHoundMetrics.SearchSpeed);
                    TryJumpAhead();
                }
                else {
                    NPC.velocity.X *= 0.85f;
                }
            }
            //迹在（scentLinger>0）搜索不落幕；迹断后超时 Fade 的原规则原样恢复
            if (authority && StateTimer >= KiyumeHoundMetrics.SearchTicks && scentLinger <= 0) {
                EnterFade();
            }
        }

        //嗅迹追踪（权威端，仅搜索态调）：30t 一查半径内最新鲜活点，走近当前迹点才认领下一点
        //（真沿地走迹，不隔空跳锚；点寿命同长 → 活点非当前迹点即必更新，链推进单调不回头）。
        //浓雾不影响气味场——嗅觉不是视觉，这里刻意不掺任何雾采样。
        //只改搜索路径不喂警觉：侦测强度与警觉/追击转换规则一律不动（平衡纪律），
        //玩家甩掉它的动词照旧成立——停跑 8s 迹自断，犬围着迹尽处折返、超时化雾
        private void TrackScent() {
            if (scentLinger > 0) {
                scentLinger--;
            }
            if ((int)StateTimer % KiyumeHoundMetrics.ScentQueryIntervalTicks != 0) {
                return;
            }
            bool onTrail = scentLinger > 0;
            Vector2 center = onTrail ? scentFocus : new Vector2(AnchorX, NPC.Center.Y);
            if (!KiyumeScentTrail.TryGetFreshScent(center, KiyumeHoundMetrics.ScentSniffRadiusPx,
                out Vector2 pos, out _)) {
                //半径内无活迹：迹感自然耗尽=迹断，回原折返与超时判定
                return;
            }
            scentLinger = KiyumeHoundMetrics.ScentHoldTicks;
            //首次拾迹直接认领；沿迹中要走近当前迹点、且半径内确有更新点才推进
            if (onTrail
                && (pos == scentFocus
                    || Vector2.DistanceSquared(NPC.Center, scentFocus)
                        > KiyumeHoundMetrics.ScentAdvanceGatePx * KiyumeHoundMetrics.ScentAdvanceGatePx)) {
                return;
            }
            scentFocus = pos;
            if (MathF.Abs(pos.X - AnchorX) > 1f) {
                //搜索锚更新到迹点：客户端从 ai[3] 重放同一沿迹路径（零新包）
                AnchorX = pos.X;
                NPC.netUpdate = true;
            }
        }

        //追击：后蹲蓄力 24t → 长嚎 → 全速奔袭可跳跃；视线断且听觉低 180t 丢失回搜索
        private void UpdateChase(bool authority) {
            StateTimer++;
            Player target = TargetPlayer();
            if (target == null) {
                if (authority) {
                    EnterSearch(AnchorX);
                }
                return;
            }

            if (StateTimer <= KiyumeHoundMetrics.ChaseWindupTicks) {
                //后蹲蓄力：力量在蓄势里
                NPC.velocity.X *= 0.85f;
                Face(MathF.Sign(target.Center.X - NPC.Center.X));
                return;
            }

            int dir = MathF.Sign(target.Center.X - NPC.Center.X);
            if (dir != 0) {
                Face(dir);
            }
            WalkTowards(NPC.direction, KiyumeHoundMetrics.ChaseSpeed);
            TryJumpAhead();

            if (!authority) {
                return;
            }
            //最后目击持续刷进锚（丢失转搜索的交接点）
            AnchorX = target.Center.X;
            //丢失判定：视线断且听觉 <0.2；目标躲进有墙有顶（犬不入宅）加速失的（×3）。
            //本块每帧跑（sightNow 是 6t 采样的驻留值），累计必须按帧 +1，不能再乘采样间隔
            if (sightNow <= 0f && soundNow < 0.2f) {
                lostTicks++;
                if (KiyumeStealthSense.ShelterFactor(target) < 1f) {
                    lostTicks += 2;
                }
            }
            else {
                lostTicks = 0;
            }
            if (lostTicks >= KiyumeHoundMetrics.LostGraceTicks) {
                EnterSearch(AnchorX);
                return;
            }
            //贴身起扑
            if (Vector2.Distance(target.Center, NPC.Center) < KiyumeHoundMetrics.LungeTriggerPx) {
                EnterLunge();
            }
        }

        //扑咬：蹲伏读帧（伤害 0）→ 定向扑出（唯一接触伤害窗）→ 落地硬直（反击窗）
        private void UpdateLunge(bool authority) {
            StateTimer++;
            int crouchEnd = KiyumeHoundMetrics.LungeCrouchTicks;
            int flightEnd = crouchEnd + KiyumeHoundMetrics.LungeFlightTicks;
            int recoverEnd = flightEnd + KiyumeHoundMetrics.LungeRecoverTicks;
            int t = (int)StateTimer;

            if (t <= crouchEnd) {
                NPC.velocity.X *= 0.7f;
                Player aim = TargetPlayer();
                if (aim != null) {
                    Face(MathF.Sign(aim.Center.X - NPC.Center.X));
                }
                if (authority && t == crouchEnd) {
                    //扑出矢量在蹲伏末帧锁定（committed），带提前量；velocity 随转移包过线
                    Vector2 lead = aim != null
                        ? aim.Center + aim.velocity * 6f
                        : NPC.Center + new Vector2(NPC.direction * 90f, -24f);
                    NPC.velocity = (lead - NPC.Center).SafeNormalize(Vector2.UnitX * NPC.direction)
                        * KiyumeHoundMetrics.LungeSpeed;
                    NPC.netUpdate = true;
                }
                return;
            }
            if (t <= flightEnd) {
                //唯一接触伤害窗（每帧从状态重算，见 AI 顶部纪律注释）
                NPC.damage = KiyumeHoundMetrics.LungeDamage;
                //撞地撞墙提前收扑：各端按本地物理同式收窗（受害端判伤窗与其画面一致）
                if (NPC.collideX || NPC.collideY) {
                    StateTimer = flightEnd;
                }
                if (authority) {
                    //拖咬闸：扑中低血目标即咬住（命中判定本身在受害端走原版接触，
                    //这里用服务器视角的盒相交独立裁决转态，零新包）
                    foreach (Player player in Main.ActivePlayers) {
                        if (player.dead || player.ghost
                            || !NPC.Hitbox.Intersects(player.Hitbox)) {
                            continue;
                        }
                        if (player.statLife <= player.statLifeMax2 * KiyumeHoundMetrics.DragHpGate) {
                            EnterDrag(player.whoAmI);
                            return;
                        }
                    }
                }
                return;
            }
            //落地硬直：扑空是反击窗口
            NPC.velocity.X *= 0.72f;
            if (authority && t >= recoverEnd) {
                EnterChase(fresh: false);
            }
        }

        //拖咬：咬住向雾浓处倒拖，受害端本地被拉（KiyumeHoundDragPlayer）；
        //挣脱方式=打它（justHit 在 HandleHit 里松口），每 24t 一跳撕咬节拍
        private void UpdateDrag(bool authority) {
            StateTimer++;
            //拖咬期接触伤害设回：受害者被拉贴身，原版接触即撕咬（节拍受无敌帧钳制）
            NPC.damage = KiyumeHoundMetrics.DragBiteDamage;
            //面朝猎物、倒退拖行：velocity.X 由入态裁决并同步，这里不覆写（客户端顺惯性重放）
            Player victim = TargetPlayer();
            if (victim != null) {
                Face(MathF.Sign(victim.Center.X - NPC.Center.X));
            }
            if (!authority) {
                return;
            }
            bool broke = victim == null
                || Vector2.Distance(victim.Center, NPC.Center) > KiyumeHoundMetrics.DragBreakDistPx;
            if (broke || StateTimer >= KiyumeHoundMetrics.DragTicks) {
                EnterStagger();
            }
        }

        //硬直：松口后撤甩头，反击奖励窗
        private void UpdateStagger(bool authority) {
            StateTimer++;
            NPC.velocity.X *= 0.9f;
            if (authority && StateTimer >= KiyumeHoundMetrics.StaggerTicks) {
                EnterChase(fresh: false);
            }
        }

        //化雾退场：原地转一圈、小跑走远、化进雾里；不留尸不留痕，来去都是雾
        private void UpdateFade(bool authority) {
            StateTimer++;
            int t = (int)StateTimer;
            if (t == 12 || t == 24) {
                //原地转一圈（两次转向读成回头张望）
                Face(-NPC.direction);
            }
            if (t <= 30) {
                NPC.velocity.X *= 0.8f;
            }
            else {
                if (t == 31) {
                    //背身走远：远离最近玩家（timer 同拍，各端一致）
                    Player near = NearestPlayer();
                    Face(near != null ? -MathF.Sign(near.Center.X - NPC.Center.X + 0.01f) : NPC.direction);
                }
                WalkTowards(NPC.direction, KiyumeHoundMetrics.PatrolSpeed);
            }
            if (authority && StateTimer >= KiyumeHoundMetrics.FadeTicks) {
                NPC.active = false;
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
                }
            }
        }

        //==================== 运动与目标小件 ====================

        private void WalkTowards(int dir, float speed) {
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, dir * speed, 0.18f);
        }

        private void Face(int dir) {
            if (dir != 0) {
                NPC.direction = dir;
                NPC.spriteDirection = dir;
            }
        }

        private void FaceNearestPlayer() {
            Player near = NearestPlayer();
            if (near != null) {
                Face(MathF.Sign(near.Center.X - NPC.Center.X));
            }
        }

        private Player NearestPlayer() {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float dist = Vector2.DistanceSquared(player.Center, NPC.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = player;
                }
            }
            return best;
        }

        private Player TargetPlayer() {
            if (NPC.target < 0 || NPC.target >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[NPC.target];
            return player != null && player.active && !player.dead && !player.ghost ? player : null;
        }

        //追击/搜索可跳跃：前方 1-3 tile 障碍给向上初速（狼是活物，不悬浮不穿墙）
        private void TryJumpAhead() {
            if (NPC.velocity.Y != 0f) {
                return;
            }
            int dir = NPC.direction;
            int probeX = (int)((NPC.Center.X + dir * (NPC.width * 0.5f + 12f)) / 16f);
            int feetY = (int)((NPC.position.Y + NPC.height - 4f) / 16f);
            if (!WorldGen.InWorld(probeX, feetY, 20)) {
                return;
            }
            int height = 0;
            for (int i = 0; i < 3; i++) {
                Tile tile = Framing.GetTileSafely(probeX, feetY - i);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    height = i + 1;
                }
                else if (height > 0) {
                    break;
                }
            }
            if (height > 0) {
                NPC.velocity.Y = -(4.6f + 1.1f * height);
            }
        }

        //==================== 连锁（权威端） ====================

        /// <summary>长嚎/哀鸣的全图同类警觉分享；forceSearch=哀鸣（余犬立即去死处搜索）</summary>
        private void ShareAwareness(float sourceX, bool forceSearch) {
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.type != Type || other.whoAmI == NPC.whoAmI
                    || other.ModNPC is not KiyumeHound hound) {
                    continue;
                }
                other.ai[2] = MathHelper.Clamp(
                    other.ai[2] + KiyumeHoundMetrics.HowlAwarenessShare,
                    0f, KiyumeHoundMetrics.ChaseThreshold);
                int state = (int)other.ai[0];
                if (forceSearch && state is StatePatrol or StateAlert or StateEmerge) {
                    hound.EnterSearch(sourceX);
                }
                other.netUpdate = true;
            }
        }

        public override void OnKill() {
            //哀鸣连锁（1.4C）：全图犬同悲同怒；死亡音走 DeathSound 原版同步
            ShareAwareness(NPC.Center.X, forceSearch: true);
            //当潮位周期不补员（W3 导演消费接口）
            RecruitHoldUntilTideRise = true;
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            //湿墨黑液溅出；死亡整只化散成雾（不留尸不留痕）
            int count = NPC.life <= 0 ? 26 : 4;
            for (int i = 0; i < count; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke,
                    Main.rand.NextFloat(-2.2f, 2.2f), Main.rand.NextFloat(-2.6f, 0.6f),
                    120, new Color(26, 10, 14), Main.rand.NextFloat(0.9f, 1.7f));
                dust.noGravity = true;
            }
        }

        //==================== 同步小件（镜像 KiyumeYokaiNPC 惯例） ====================

        private void ServerSyncPacer(int interval = 24) {
            if (!VaultUtils.isServer) {
                return;
            }
            if (++NPC.localAI[1] >= interval) {
                NPC.localAI[1] = 0f;
                NPC.netUpdate = true;
            }
        }

        /// <summary>状态切换沿（客户端音频 cue 用）：localAI[2] 缓存上帧状态+1；
        /// first=首次观察（新生/迟入端第一帧），只记不播，resync 与迟到包不重播</summary>
        private bool StateEdge(out bool first) {
            first = (int)NPC.localAI[2] == 0;
            if ((int)NPC.localAI[2] == (int)State + 1) {
                return false;
            }
            NPC.localAI[2] = (int)State + 1;
            NPC.localAI[3] = 0f;
            return true;
        }

        /// <summary>状态内节拍严格前进沿（netcode 7.5）：回卷快照不重播已放过的拍</summary>
        private bool BeatForward(int beat) {
            if (beat <= (int)NPC.localAI[3]) {
                return false;
            }
            NPC.localAI[3] = beat;
            return true;
        }

        //==================== 演出（各端本地：帧、姿态、音频 cue、教学） ====================

        private void UpdatePresentation() {
            int state = (int)State;
            int prevState = (int)NPC.localAI[2] - 1;
            if (StateEdge(out bool first) && !first) {
                PlayStateCue(prevState, state);
            }

            //帧速随速度，追击帧速 ×1.6；停驻/嗅地/蹲伏定格帧 3
            bool frozen = Sniffing || state is StateAlert or StateStagger
                || (state == StateLunge && StateTimer <= KiyumeHoundMetrics.LungeCrouchTicks)
                || (state == StateChase && StateTimer <= KiyumeHoundMetrics.ChaseWindupTicks);
            if (frozen || MathF.Abs(NPC.velocity.X) < 0.25f) {
                frame = 3;
                frameCounter = 0f;
            }
            else {
                //帧速随速度、追击 ×1.6，封顶防高速糊帧（奔袭要读得清腿）
                float rate = state == StateChase ? 1.6f : 1f;
                frameCounter += MathF.Min(MathF.Abs(NPC.velocity.X) * 0.5f * rate, 2.2f);
                if (frameCounter > 8f) {
                    frameCounter -= 8f;
                    frame++;
                }
                if (frame > 9 || frame < 3) {
                    frame = 3;
                }
            }

            //躯干姿态：嗅地前倾 / 凝视后仰 / 蹲伏压低 / 扑出前扑 / 硬直甩头
            float pitchTarget = 0f;
            if (Sniffing) {
                pitchTarget = 0.09f;
            }
            else {
                pitchTarget = state switch {
                    StateAlert => -0.08f,
                    StateChase when StateTimer <= KiyumeHoundMetrics.ChaseWindupTicks => 0.07f,
                    StateLunge when StateTimer <= KiyumeHoundMetrics.LungeCrouchTicks => 0.10f,
                    StateLunge => -0.14f,
                    StateDrag => 0.06f,
                    StateStagger => MathF.Sin(StateTimer * 0.9f) * 0.12f,
                    StateChase => MathHelper.Clamp(-NPC.velocity.Y * 0.015f, -0.16f, 0.16f),
                    _ => 0f,
                };
            }
            bodyPitch = MathHelper.Lerp(bodyPitch, pitchTarget, 0.16f);

            //眼光包络平滑（身体 uEyeGlow；穿雾光点另走 KiyumeHoundEyeGleam）
            eyeGlowSmooth = MathHelper.Lerp(eyeGlowSmooth, EyeGlow01(), 0.1f);

            //嗅地起沿：轻响 + 鼻尖尘粒
            bool sniffing = Sniffing;
            if (sniffing && !prevSniffing) {
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.22f, Pitch = -0.9f }, NPC.Center);
            }
            prevSniffing = sniffing;
            if (sniffing && Main.rand.NextBool(9)) {
                Vector2 nose = NPC.Center + new Vector2(NPC.direction * NPC.width * 0.46f, 6f);
                Dust dust = Dust.NewDustPerfect(nose, DustID.Smoke,
                    new Vector2(NPC.direction * 0.3f, -0.2f), 160, new Color(60, 44, 40), 0.7f);
                dust.noGravity = true;
            }

            //嗅迹沿途表现（点子 13）：搜索锚被迹点推进（ai[3] 变沿，从同步数据读出、各端一致）
            //时鼻尖尘粒稍密（嗅地粒子复用）；玩家足迹本身无任何可见物（不可见是点子原文）
            if (state == StateSearch) {
                if (prevState == StateSearch && NPC.ai[3] != prevSearchAnchorX) {
                    scentDustTicks = KiyumeHoundMetrics.ScentDustHoldTicks;
                }
                prevSearchAnchorX = NPC.ai[3];
                if (scentDustTicks > 0) {
                    scentDustTicks--;
                    if (Main.rand.NextBool(4)) {
                        Vector2 nose = NPC.Center + new Vector2(NPC.direction * NPC.width * 0.46f, 6f);
                        Dust trailDust = Dust.NewDustPerfect(nose, DustID.Smoke,
                            new Vector2(NPC.direction * 0.3f, -0.2f), 160, new Color(60, 44, 40), 0.7f);
                        trailDust.noGravity = true;
                    }
                }
            }
            else {
                scentDustTicks = 0;
            }

            //凝实/化雾的雾息
            if ((state == StateEmerge || state == StateFade) && Main.rand.NextBool(4)) {
                Dust wisp = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke,
                    Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.7f, -0.1f),
                    170, new Color(46, 13, 14), Main.rand.NextFloat(1f, 1.6f));
                wisp.noGravity = true;
            }

            //长嚎：蓄力末拍（状态内节拍沿，回卷不重播；==精确拍让扑咬/硬直返场的
            //timer 预置入场（fresh=false）天然错过此拍，不重嚎）
            if (state == StateChase && (int)StateTimer == KiyumeHoundMetrics.ChaseWindupTicks
                && BeatForward(1)) {
                SoundEngine.PlaySound(SoundID.Zombie103 with { Volume = 1f, Pitch = -0.3f }, NPC.Center);
            }
            //拖咬撕咬节拍
            if (state == StateDrag
                && BeatForward((int)StateTimer / KiyumeHoundMetrics.DragBiteIntervalTicks + 1)) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.55f }, NPC.Center);
            }
        }

        //状态变迁沿音频 cue（低吼/长嚎/哀鸣三件套里的低吼；长嚎走节拍沿，哀鸣走 DeathSound）
        private void PlayStateCue(int prevState, int state) {
            switch (state) {
                case StateAlert:
                    //低吼：位置声源自带衰减与声像=方位提示
                    SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.9f, Pitch = -0.55f }, NPC.Center);
                    KiyumeHoundHints.TryShowAlert(NPC);
                    break;
                case StateChase:
                    if (prevState is not (StateLunge or StateDrag or StateStagger)) {
                        KiyumeHoundHints.TryShowChase(NPC);
                    }
                    break;
                case StateDrag:
                    SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.75f, Pitch = -0.75f }, NPC.Center);
                    break;
                case StateStagger:
                    //松口哀叫（哀鸣 NPCDeath6 留给真死，避免混淆生死信号）
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.6f, Pitch = 0.4f }, NPC.Center);
                    break;
            }
        }

        //==================== 演出参数（绘制与 EyeGleam 共用） ====================

        /// <summary>化雾度：凝实 1→0（身体先成），化雾 0→1；在场恒 0（真犬不近身消隐，狼来了机制）</summary>
        internal float Dissolve01() {
            int state = (int)State;
            if (state == StateEmerge) {
                return 1f - MathHelper.Clamp(StateTimer / KiyumeHoundMetrics.EmergeTicks * 1.15f, 0f, 1f);
            }
            if (state == StateFade) {
                return MathHelper.Clamp(StateTimer / KiyumeHoundMetrics.FadeTicks, 0f, 1f);
            }
            return 0f;
        }

        /// <summary>身体眼光包络：凝实后段才亮（双目后亮）、分态定值、化雾渐熄；shader 内自带呼吸</summary>
        private float EyeGlow01() {
            int state = (int)State;
            float t01;
            switch (state) {
                case StateEmerge:
                    t01 = MathHelper.Clamp(StateTimer / KiyumeHoundMetrics.EmergeTicks, 0f, 1f);
                    return t01 <= 0.6f ? 0f : EyePatrol * (t01 - 0.6f) / 0.4f;
                case StateFade:
                    t01 = MathHelper.Clamp(StateTimer / KiyumeHoundMetrics.FadeTicks, 0f, 1f);
                    return EyeAlert * (1f - t01);
                case StateAlert:
                case StateSearch:
                    return EyeAlert;
                case StateChase:
                case StateLunge:
                case StateStagger:
                    return EyeChase;
                case StateDrag:
                    return EyeDrag;
                default:
                    return EyePatrol;
            }
        }

        /// <summary>眼点世界坐标（EyeGleam 消费）：EyeAnchor 帧内 uv 展到世界；second=后眼
        /// （偏移与 KikasaHound.fx 的 eye2x=0.055 同源）</summary>
        internal static Vector2 EyeWorldPos(NPC npc, bool second) {
            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf].Value;
            if (tex == null) {
                return npc.Center;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.Wolf] - 2;
            float width = tex.Width * HoundScale;
            float height = frameH * HoundScale;
            bool faceRight = npc.spriteDirection > 0;
            float eyeU = faceRight ? 1f - EyeAnchor.X : EyeAnchor.X;
            float x = npc.Center.X - width * 0.5f + eyeU * width;
            float y = npc.Bottom.Y + 2f + npc.gfxOffY - height + EyeAnchor.Y * height;
            if (second) {
                x -= npc.spriteDirection * 0.055f * width;
                y += 0.012f * height;
            }
            return new Vector2(x, y);
        }

        //==================== 绘制（全接管：狼帧 + KikasaHound.fx 实体态） ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态自愈（netcode 7.2）
            BeginDefault(spriteBatch);
            Main.instance.LoadNPC(NPCID.Wolf);
            Texture2D tex = TextureAssets.Npc[NPCID.Wolf].Value;
            if (tex == null) {
                return false;
            }
            int frameCount = Main.npcFrameCount[NPCID.Wolf];
            int frameH = tex.Height / frameCount;
            int safeFrame = Math.Clamp(frame, 0, frameCount - 1);
            //源矩形上下各内缩 1px + shader 帧界钳制，双通道防帧表渗色（HoundShade 同款纪律）
            var source = new Rectangle(0, safeFrame * frameH + 1, tex.Width, frameH - 2);
            float height = source.Height * HoundScale;
            //贴地：精灵底缘对齐碰撞盒底缘，绕体心旋转出躯干姿态
            var center = new Vector2(NPC.Center.X,
                NPC.Bottom.Y + 2f + NPC.gfxOffY - height * 0.5f);
            var origin = new Vector2(source.Width * 0.5f, source.Height * 0.5f);
            float rotation = bodyPitch * NPC.spriteDirection;
            bool faceRight = NPC.spriteDirection > 0;
            float dissolve = Dissolve01();

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (hound == null || noise == null) {
                //着色器缺编：近黑剪影回退（透明度显式给值，化雾降级为降透明）
                SpriteEffects flip = faceRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                spriteBatch.Draw(tex, center - screenPos, source,
                    new Color(10, 5, 8) * (0.9f * (1f - dissolve)), rotation,
                    origin, HoundScale, flip, 0f);
                return false;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            hound.Parameters["uFlipH"]?.SetValue(faceRight ? 1f : 0f);
            hound.Parameters["uFlipV"]?.SetValue(0f);
            hound.Parameters["uMode"]?.SetValue(1f);
            hound.Parameters["uSeamGate"]?.SetValue(0f);
            hound.Parameters["uWobble"]?.SetValue(0.010f);
            hound.Parameters["uEyeGlow"]?.SetValue(eyeGlowSmooth);
            hound.Parameters["uEyeAnchor"]?.SetValue(EyeAnchor);
            hound.Parameters["uDissolve"]?.SetValue(dissolve);
            hound.Parameters["uEdgeTint"]?.SetValue(EdgeTint.ToVector3());
            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.CurrentTechnique.Passes[0].Apply();

            //vc.a 恒 1：来去全走 uDissolve，不降 alpha（化雾不是调透明度）
            spriteBatch.Draw(tex, center - screenPos, source, Color.White,
                rotation, origin, HoundScale, SpriteEffects.None, 0f);

            BeginDefault(spriteBatch);
            gd.Textures[1] = null;

#if DEBUG
            //迹=嗅迹迹感余量（权威端字段，单人可读；联机客户端恒 0 属预期）
            Utils.DrawBorderString(spriteBatch,
                $"状态 {(int)State}  警觉 {(int)Awareness}  迹 {scentLinger}",
                NPC.Top - screenPos + new Vector2(-28f, -32f),
                Color.LightGoldenrodYellow, 0.7f);
#endif
            return false;
        }

        private static void BeginDefault(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    /// <summary>
    /// 拖咬受害端施力（联机纪律：玩家位置 client-authoritative，服务器写不动）。
    /// 从 ai[0]=Drag + NPC.target=自己 读出被咬，本地朝犬口加速；可反向输入抵抗
    /// </summary>
    internal class KiyumeHoundDragPlayer : ModPlayer
    {
        public override void PreUpdateMovement() {
            if (!KiyumeWorld.Active || Player.whoAmI != Main.myPlayer
                || Player.dead || Player.ghost) {
                return;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != ModContent.NPCType<KiyumeHound>()
                    || (int)npc.ai[0] != KiyumeHound.StateDrag
                    || npc.target != Player.whoAmI) {
                    continue;
                }
                Vector2 mouth = npc.Center + new Vector2(npc.spriteDirection * 14f, -2f);
                Vector2 to = mouth - Player.Center;
                if (to.Length() > 6f) {
                    Player.velocity += to.SafeNormalize(Vector2.Zero)
                        * KiyumeHoundMetrics.DragPullAccel;
                }
            }
        }
    }

    /// <summary>
    /// 恶犬教学两条（一次性）+ 会话旗标复位。zh-Hans 正典、Category="UI"。
    /// 去重旗标先住本类静态（本地演出进度，非 per-player 游戏状态）；
    /// W3 P2-D 导演接管教学调度时整体迁移
    /// </summary>
    internal class KiyumeHoundHints : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        internal static LocalizedText HintAlert { get; private set; }
        internal static LocalizedText HintChase { get; private set; }

        private static bool alertShown;
        private static bool chaseShown;

        public override void SetStaticDefaults() {
            HintAlert = this.GetLocalization(nameof(HintAlert), () => "站住别动，雾会替你藏。");
            HintChase = this.GetLocalization(nameof(HintChase), () => "进屋，或者进雾最深的地方。");
        }

        public override void OnWorldLoad() => ResetSession();
        public override void OnWorldUnload() => ResetSession();

        private static void ResetSession() {
            alertShown = false;
            chaseShown = false;
            KiyumeHound.RecruitHoldUntilTideRise = false;
        }

        /// <summary>首次被起疑（本机是它盯上的人才提示）</summary>
        internal static void TryShowAlert(NPC hound) {
            if (alertShown || hound.target != Main.myPlayer) {
                return;
            }
            alertShown = true;
            Show(HintAlert);
        }

        /// <summary>首次被追</summary>
        internal static void TryShowChase(NPC hound) {
            if (chaseShown || hound.target != Main.myPlayer) {
                return;
            }
            chaseShown = true;
            Show(HintChase);
        }

        //骨灰色小字进聊天栏：留得住、不占 HUD（怪谈不放教学弹窗）
        private static void Show(LocalizedText text) {
            if (text != null) {
                Main.NewText(text.Value, 168, 132, 128);
            }
        }
    }
}
