using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>沙丘柱生命周期（写 <see cref="BssSandPillar.PhaseRaw"/> 同步槽）</summary>
    internal enum BssPillarPhase : int
    {
        /// <summary>鼓包预告：渗沙 + 隆隆声 + 微隆起（沙丘柱版 omen）</summary>
        Omen = 0,
        /// <summary>迅猛钻出：唯一伤害窗（armed 柱才咬人）</summary>
        Erupt = 1,
        /// <summary>滞留：无害置景，边缘渗沙，是腾跃/爆震的燃料</summary>
        Linger = 2,
        /// <summary>缓沉入地消失</summary>
        Sink = 3,
        /// <summary>爆震引信：裂纹预闪到期炸沙球环（爆震态点名）</summary>
        Detonate = 4,
    }

    /// <summary>
    /// 沙丘柱：荒花沙蟒的场地实体（InnoVault Actor）。原版沙块贴图按 16px 格拼装成
    /// 锯齿柱，从地下钻出 → 滞留 → 缓沉；爆震态可点名引爆成径向沙球环。
    ///
    /// 联机契约（镜像伞鬼 KasaOniActor）：只在权威端 NewActor 生成与裁决相位转移，
    /// [SyncVar] 随生成包/增量广播到各端；各端从相位观测本地推演出计时与演出
    /// （lastSeenPhase 换相清零本地表）。客户端 OnSpawn 不覆写权威字段。
    /// Erupt 伤害窗走"被击端自结算"：各客户端检测本地玩家与钻出段重叠，
    /// 经原版 Hurt 协议同步，伤害窗 = 可见冲势（仅 Erupt 相位且 armed）。
    ///
    /// 地面探测失败/落差过大时走空中凝沙变体（airborne）：旋沙聚拢凝柱，无出土段。
    /// 公平阀：同屏柱数上限；转阶段/死亡/遁走由 <see cref="SinkAll"/> 令全部柱缓沉。
    /// </summary>
    internal class BssSandPillar : Actor
    {
        #region 同步字段
        [SyncVar]
        private int phaseRaw = (int)BssPillarPhase.Omen;
        /// <summary>宿主头 whoAmI（伤害换算与孤儿守卫）</summary>
        [SyncVar]
        private int headWhoAmI = -1;
        /// <summary>柱心 X（世界）</summary>
        [SyncVar]
        private float centerX;
        /// <summary>柱基 Y（地面线；空中变体 = 悬浮基线）</summary>
        [SyncVar]
        private float baseY;
        /// <summary>全高 px</summary>
        [SyncVar]
        private float pillarHeight = 380f;
        /// <summary>半宽 px</summary>
        [SyncVar]
        private float halfWidth = 40f;
        /// <summary>预告帧数</summary>
        [SyncVar]
        private int omenFrames = 24;
        /// <summary>滞留帧数（到期权威端转缓沉）</summary>
        [SyncVar]
        private int lingerFrames = 600;
        /// <summary>钻出段是否带伤害窗（置景柱 false）</summary>
        [SyncVar]
        private bool armed;
        /// <summary>空中凝沙变体（无出土段）</summary>
        [SyncVar]
        private bool airborne;
        /// <summary>视觉种子（各端同貌的帧选取/锯齿冠）</summary>
        [SyncVar]
        private int seed;
        /// <summary>爆震引信帧数（Detonate 相位起算；各端据此同拍演出）</summary>
        [SyncVar]
        private int fuseFrames = 30;
        #endregion

        #region 本地状态
        private BssPillarPhase lastSeenPhase = (BssPillarPhase)(-1);
        /// <summary>相位内计时（各端本地推演，换相清零）</summary>
        private int phaseTimer;
        /// <summary>总龄（状态机死角保险）</summary>
        private int age;
        /// <summary>已升起高度（各端从相位+计时确定性推演）</summary>
        private float rise;
        /// <summary>裂纹预闪强度 0..1（Detonate 期）</summary>
        private float crackGlow;
        #endregion

        #region 注册与查询
        /// <summary>活跃柱注册表（各端本地登记，查询时惰性剪枝）</summary>
        private static readonly List<BssSandPillar> registry = new();

        internal BssPillarPhase Phase => (BssPillarPhase)phaseRaw;
        internal float CenterX => centerX;
        internal float BaseY => baseY;
        internal float PillarHalfWidth => halfWidth;
        /// <summary>当前柱顶 Y（随升起量变化）</summary>
        internal float TopY => baseY - rise;
        /// <summary>可作腾跃盘柱的锚（滞留期且基本升满）</summary>
        internal bool Climbable => Phase == BssPillarPhase.Linger && rise > pillarHeight * 0.75f;
        /// <summary>可被爆震点名（柱体已可见：裂纹预闪要有载体，埋在土里的鼓包不算）</summary>
        internal bool Detonatable => Phase is BssPillarPhase.Linger or BssPillarPhase.Erupt;

        /// <summary>枚举活跃柱（惰性剪枝失效项）</summary>
        internal static IReadOnlyList<BssSandPillar> Alive {
            get {
                registry.RemoveAll(p => p == null || !p.Active
                    || p.WhoAmI < 0 || p.WhoAmI >= ActorLoader.MaxActorCount
                    || !ReferenceEquals(ActorLoader.Actors[p.WhoAmI], p));
                return registry;
            }
        }

        /// <summary>找离 pos 最近的可盘柱，无则 null</summary>
        internal static BssSandPillar FindNearestClimbable(Vector2 pos) {
            BssSandPillar best = null;
            float bestDist = float.MaxValue;
            foreach (var pillar in Alive) {
                if (!pillar.Climbable) {
                    continue;
                }
                float d = Math.Abs(pillar.centerX - pos.X);
                if (d < bestDist) {
                    bestDist = d;
                    best = pillar;
                }
            }
            return best;
        }

        /// <summary>找离 pos 最近的成形中柱（鼓包/钻出期；腾跃召柱等待时的走位锚）</summary>
        internal static BssSandPillar FindNearestForming(Vector2 pos) {
            BssSandPillar best = null;
            float bestDist = float.MaxValue;
            foreach (var pillar in Alive) {
                if (pillar.Phase is not BssPillarPhase.Omen and not BssPillarPhase.Erupt) {
                    continue;
                }
                float d = Math.Abs(pillar.centerX - pos.X);
                if (d < bestDist) {
                    bestDist = d;
                    best = pillar;
                }
            }
            return best;
        }

        /// <summary>可被爆震点名的柱数（hub 选招门槛）</summary>
        internal static int CountDetonatable() {
            int n = 0;
            foreach (var pillar in Alive) {
                if (pillar.Detonatable) {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// 柱位预留：站立柱（非沉降）到上限时令最老的滞留柱缓沉腾位；
        /// 全是引信/钻出中腾不出位才拒召。
        /// </summary>
        private static bool TryReserveSlot() {
            int standing = 0;
            BssSandPillar oldest = null;
            int oldestAge = -1;
            foreach (var pillar in Alive) {
                if (pillar.Phase == BssPillarPhase.Sink) {
                    continue;
                }
                standing++;
                if (pillar.Phase == BssPillarPhase.Linger && pillar.phaseTimer > oldestAge) {
                    oldest = pillar;
                    oldestAge = pillar.phaseTimer;
                }
            }
            if (standing < BssDirector.PillarMax) {
                return true;
            }
            if (oldest == null) {
                return false;
            }
            oldest.CommandSink();
            return standing - 1 < BssDirector.PillarMax;
        }
        #endregion

        #region 生成与指令（权威端）
        /// <summary>挂起生成参数（镜像 ArbiterManifestationActor：经 OnSpawn 消费，字段赶上生成包）</summary>
        private struct PendingSpawn
        {
            public int Head;
            public float CenterX;
            public float BaseY;
            public float Height;
            public float HalfWidth;
            public int Omen;
            public int Linger;
            public bool Armed;
            public bool Airborne;
        }

        private static bool pendingValid;
        private static PendingSpawn pending;

        /// <summary>
        /// 权威端召一根柱（客户端调用无效返回 null）。探不到地或落差过大自动转空中凝沙：
        /// 以 anchor 为悬浮基线。柱位到上限先沉最老的滞留柱腾位（沉降中不计位），
        /// 腾不出才拒召——保证点名拍不空放。
        /// </summary>
        internal static BssSandPillar Spawn(NPC head, Vector2 anchor, float height, float width2,
            int omen, int linger, bool armedPillar) {
            if (VaultUtils.isClient || !TryReserveSlot()) {
                return null;
            }

            //探地两段式：先从锚点上方 60px 向下扫（近贴锚点防打到洞顶）；
            //起扫点埋在实体里（贴崖/穿脊）就退回从锚点本体再扫一次
            float groundY = BssVfx.FindGroundY(anchor - new Vector2(0f, 60f), 1000f);
            if (groundY <= anchor.Y - 40f) {
                groundY = BssVfx.FindGroundY(anchor, 940f);
            }
            //扫空（兜底深度）或仍然埋在锚点上方 → 空中凝沙补偿
            bool air = groundY >= anchor.Y + 760f || groundY <= anchor.Y - 40f;
            float footY = air ? anchor.Y + height * 0.5f : groundY;

            pending = new PendingSpawn {
                Head = head.whoAmI,
                CenterX = anchor.X,
                BaseY = footY,
                Height = height,
                HalfWidth = width2 * 0.5f,
                Omen = Math.Max(omen, 6),
                Linger = linger,
                Armed = armedPillar,
                Airborne = air,
            };
            pendingValid = true;
            int idx;
            try {
                idx = ActorLoader.NewActor<BssSandPillar>(new Vector2(anchor.X - width2 * 0.5f, footY - height));
            }
            finally {
                pendingValid = false;
            }
            if (idx < 0 || idx >= ActorLoader.MaxActorCount
                || ActorLoader.Actors[idx] is not BssSandPillar pillar) {
                return null;
            }
            return pillar;
        }

        /// <summary>点名引爆：裂纹预闪 crack 帧后炸沙球环（延迟量做逐柱错拍）</summary>
        internal void CommandDetonate(int crackFrames, int staggerDelay) {
            if (VaultUtils.isClient || Phase == BssPillarPhase.Detonate) {
                return;
            }
            fuseFrames = Math.Max(crackFrames + staggerDelay, 8);
            phaseRaw = (int)BssPillarPhase.Detonate;
            NetUpdate = true;
        }

        /// <summary>令本柱缓沉（未在沉降/引爆时）</summary>
        internal void CommandSink() {
            if (VaultUtils.isClient || Phase is BssPillarPhase.Sink or BssPillarPhase.Detonate) {
                return;
            }
            phaseRaw = (int)BssPillarPhase.Sink;
            NetUpdate = true;
        }

        /// <summary>
        /// 公平阀：取消未成形的威胁（Omen/Erupt 柱缓沉），滞留柱留作场地与爆震燃料。
        /// 全场收尾不走这里：头消失后各柱的孤儿守卫自会缓沉。
        /// </summary>
        internal static void CancelPending() {
            if (VaultUtils.isClient) {
                return;
            }
            foreach (var pillar in Alive) {
                if (pillar.Phase is BssPillarPhase.Omen or BssPillarPhase.Erupt) {
                    pillar.CommandSink();
                }
            }
        }
        #endregion

        #region 生命周期
        public override bool IsLoadingEnabled(Mod mod) => BssGate.Enabled;

        public override void OnSpawn(params object[] args) {
            DrawExtendMode = 1200;
            DrawLayer = ActorDrawLayer.AfterTiles;
            Velocity = Vector2.Zero;
            lastSeenPhase = (BssPillarPhase)(-1);
            phaseTimer = 0;
            age = 0;
            rise = 0f;
            crackGlow = 0f;

            //权威端消费挂起参数（OnSpawn 先于生成包广播，字段随包到各端）；
            //客户端在 NetworkSpawn 已套用权威 SyncVar，这里绝不能覆写字段
            if (!VaultUtils.isClient && pendingValid) {
                headWhoAmI = pending.Head;
                centerX = pending.CenterX;
                baseY = pending.BaseY;
                pillarHeight = pending.Height;
                halfWidth = pending.HalfWidth;
                omenFrames = pending.Omen;
                lingerFrames = pending.Linger;
                armed = pending.Armed;
                airborne = pending.Airborne;
                seed = Main.rand.Next(int.MaxValue);
                phaseRaw = (int)BssPillarPhase.Omen;
                Width = (int)(pending.HalfWidth * 2f);
                Height = (int)pending.Height;
                Position = new Vector2(pending.CenterX - pending.HalfWidth, pending.BaseY - pending.Height);
            }

            //各端登记（查询时惰性剪枝失效项）
            if (!registry.Contains(this)) {
                registry.Add(this);
            }
        }

        private NPC HeadNPC {
            get {
                if (headWhoAmI < 0 || headWhoAmI >= Main.maxNPCs) {
                    return null;
                }
                NPC head = Main.npc[headWhoAmI];
                return head.active && head.ModNPC is BssHead ? head : null;
            }
        }

        public override void AI() {
            //换相观测：本地计时清零（KasaOni 模式，各端同拍推演）
            if (lastSeenPhase != Phase) {
                OnPhaseObserved(lastSeenPhase, Phase);
                lastSeenPhase = Phase;
                phaseTimer = 0;
            }
            phaseTimer++;
            age++;

            switch (Phase) {
                case BssPillarPhase.Omen:
                    UpdateOmen();
                    break;
                case BssPillarPhase.Erupt:
                    UpdateErupt();
                    break;
                case BssPillarPhase.Linger:
                    UpdateLinger();
                    break;
                case BssPillarPhase.Sink:
                    UpdateSink();
                    break;
                case BssPillarPhase.Detonate:
                    UpdateDetonate();
                    break;
            }

            //权威端孤儿守卫：宿主头没了则缓沉收场
            if (!VaultUtils.isClient && HeadNPC == null
                && Phase is not BssPillarPhase.Sink and not BssPillarPhase.Detonate) {
                CommandSink();
            }
            //状态机死角保险：任何相位都不许无限滞留（滞留上限 + 各相位余量）
            if (!VaultUtils.isClient && age > lingerFrames + 60 * 30) {
                RequestKill();
            }
        }

        /// <summary>换相帧的一次性演出（各端本地）</summary>
        private void OnPhaseObserved(BssPillarPhase from, BssPillarPhase to) {
            if (Main.dedServ || from == (BssPillarPhase)(-1) && to == BssPillarPhase.Omen) {
                return;
            }
            Vector2 foot = new(centerX, baseY);
            switch (to) {
                case BssPillarPhase.Erupt:
                    //出土帧：沙爆 + 闷吼 + 震屏（力量在出手帧）
                    BssVfx.SandBurst(foot, airborne ? 1.2f : 2.1f);
                    SoundEngine.PlaySound(SoundID.WormDigQuiet with { Volume = 0.9f, Pitch = -0.35f, MaxInstances = 5 }, foot);
                    BssVfx.Shake(foot, armed ? 6f : 4f, 1300f);
                    break;
                case BssPillarPhase.Sink:
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 5 }, foot);
                    break;
                case BssPillarPhase.Detonate:
                    //裂纹起点：脆响预告
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.8f, Pitch = -0.4f, MaxInstances = 5 }, Center);
                    break;
            }
        }

        /// <summary>鼓包预告：渗沙上量 + 隆隆节拍 + 微隆起（空中变体 = 旋沙聚拢）</summary>
        private void UpdateOmen() {
            float p = MathHelper.Clamp(phaseTimer / (float)omenFrames, 0f, 1f);
            rise = airborne ? 0f : p * p * 14f;

            if (!Main.dedServ) {
                if (airborne) {
                    //旋沙聚拢：环带向柱轴收束（无地面时"凭空凝沙"的预告）
                    for (int i = 0; i < 2; i++) {
                        if (!Main.rand.NextBool(2)) {
                            continue;
                        }
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        float r = MathHelper.Lerp(halfWidth * 4f, halfWidth * 1.2f, p) * Main.rand.NextFloat(0.8f, 1.2f);
                        Vector2 pos = new Vector2(centerX, baseY - pillarHeight * 0.5f)
                            + ang.ToRotationVector2() * r;
                        Vector2 toAxis = new Vector2(centerX - pos.X, 0f).SafeNormalize(Vector2.UnitX);
                        Dust d = Dust.NewDustPerfect(pos, DustID.Sand,
                            toAxis * Main.rand.NextFloat(2f, 4f) + new Vector2(0f, Main.rand.NextFloat(-1f, 1f)),
                            110, default, Main.rand.NextFloat(0.9f, 1.4f));
                        d.noGravity = true;
                    }
                }
                else {
                    //贴地鼓包渗沙（密度随进度上量）
                    int count = 1 + (int)(p * 3f);
                    for (int i = 0; i < count; i++) {
                        if (!Main.rand.NextBool(2)) {
                            continue;
                        }
                        Dust d = Dust.NewDustPerfect(
                            new Vector2(centerX + Main.rand.NextFloat(-halfWidth, halfWidth) * (0.5f + p * 0.7f), baseY - 2f),
                            DustID.Sand,
                            new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1f, 2.5f + 3f * p)),
                            100, default, Main.rand.NextFloat(0.9f, 1.4f));
                        d.noGravity = false;
                    }
                }
                //隆隆节拍加密
                int gap = p > 0.6f ? 9 : 15;
                if (phaseTimer % gap == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.4f + 0.35f * p, Pitch = -0.5f + 0.25f * p, MaxInstances = 4 },
                        new Vector2(centerX, baseY));
                    BssVfx.Shake(new Vector2(centerX, baseY), 1f + 2f * p, 850f);
                }
            }

            if (!VaultUtils.isClient && phaseTimer >= omenFrames) {
                phaseRaw = (int)BssPillarPhase.Erupt;
                NetUpdate = true;
            }
        }

        /// <summary>迅猛钻出：极锐缓出曲线一口气升满；armed 柱在本相位开伤害窗（被击端自结算）</summary>
        private void UpdateErupt() {
            float p = MathHelper.Clamp(phaseTimer / (float)BssDirector.PillarEruptFrames, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - p, 3f);
            rise = pillarHeight * ease;

            if (!Main.dedServ) {
                //冠部顶开的沙浪（沿上升沿连续掀）
                if (phaseTimer % 2 == 0) {
                    Vector2 crown = new(centerX, baseY - rise);
                    for (int i = 0; i < 3; i++) {
                        Dust d = Dust.NewDustPerfect(crown + new Vector2(Main.rand.NextFloat(-halfWidth, halfWidth), 4f),
                            DustID.Sand,
                            new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(3f, 8f)),
                            90, default, Main.rand.NextFloat(1.1f, 1.7f));
                        d.noGravity = false;
                    }
                }
                //本地伤害窗：armed 且升势可见时咬本地玩家（伤害窗 = 可见冲势）
                if (armed) {
                    HurtLocalPlayerInColumn();
                }
            }

            if (!VaultUtils.isClient && p >= 1f) {
                phaseRaw = (int)BssPillarPhase.Linger;
                NetUpdate = true;
            }
        }

        /// <summary>滞留：边缘渗沙的置景拍；到期权威端转缓沉</summary>
        private void UpdateLinger() {
            rise = pillarHeight;

            if (!Main.dedServ && Main.rand.NextBool(6)) {
                //边缘格崩解落沙（滞留期的持续侵蚀读数）
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = new(centerX + side * halfWidth,
                    baseY - Main.rand.NextFloat(12f, rise - 8f));
                Dust d = Dust.NewDustPerfect(pos, DustID.Sand,
                    new Vector2(side * Main.rand.NextFloat(0.3f, 1f), Main.rand.NextFloat(0.5f, 1.6f)),
                    120, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = false;
            }

            if (!VaultUtils.isClient && phaseTimer >= lingerFrames) {
                phaseRaw = (int)BssPillarPhase.Sink;
                NetUpdate = true;
            }
        }

        /// <summary>缓沉：缓入曲线落回地面，基部持续渗沙；沉完权威端销毁</summary>
        private void UpdateSink() {
            float p = MathHelper.Clamp(phaseTimer / (float)BssDirector.PillarSinkFrames, 0f, 1f);
            rise = pillarHeight * (1f - p * p);

            if (!Main.dedServ && rise > 8f && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(
                    new Vector2(centerX + Main.rand.NextFloat(-halfWidth, halfWidth), baseY - 2f),
                    DustID.Sand,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.5f)),
                    110, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }

            if (!VaultUtils.isClient && p >= 1f) {
                RequestKill();
            }
        }

        /// <summary>爆震引信：裂纹预闪脉动到期，炸径向沙球环（沙球只在权威端生成）</summary>
        private void UpdateDetonate() {
            rise = Math.Max(rise, pillarHeight * 0.3f);
            float p = MathHelper.Clamp(phaseTimer / (float)fuseFrames, 0f, 1f);
            crackGlow = 0.35f + 0.65f * MathF.Sin(phaseTimer * (0.25f + p * 0.35f));

            if (!Main.dedServ) {
                //裂缝崩渣：临爆越密
                if (Main.rand.NextBool(p > 0.6f ? 2 : 4)) {
                    Vector2 pos = new(centerX + Main.rand.NextFloat(-halfWidth, halfWidth),
                        baseY - Main.rand.NextFloat(8f, Math.Max(rise - 4f, 12f)));
                    Dust d = Dust.NewDustPerfect(pos, DustID.Dirt,
                        new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1f, 3f)),
                        80, default, Main.rand.NextFloat(0.7f, 1.1f));
                    d.noGravity = false;
                }
                if (phaseTimer % 8 == 0) {
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f + 0.4f * p, Pitch = -0.2f + 0.4f * p, MaxInstances = 6 }, Center);
                }
            }

            if (phaseTimer < fuseFrames) {
                return;
            }

            //到期：各端本地爆演出；权威端沙球环在到期帧就位，销毁多留 6 帧余量——
            //增量广播节流最小 4 帧，客户端相位观测可能滞后，销毁太早会让滞后端
            //错过本地爆炸帧（柱子无声消失）
            if (phaseTimer == fuseFrames) {
                if (!Main.dedServ) {
                    Vector2 mid = new(centerX, baseY - rise * 0.55f);
                    BssVfx.SandBurst(mid, 2.4f);
                    BssVfx.SandBurst(new Vector2(centerX, baseY - rise * 0.15f), 1.5f);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.85f, Pitch = -0.25f, MaxInstances = 5 }, mid);
                    BssVfx.Shake(mid, 7f, 1500f);
                }
                if (!VaultUtils.isClient) {
                    ExplodeIntoGlobs();
                }
            }
            //爆后残躯迅速矮塌（销毁前的余量帧不留全高幽灵柱）
            if (phaseTimer > fuseFrames) {
                rise *= 0.55f;
            }
            if (!VaultUtils.isClient && phaseTimer >= fuseFrames + 6) {
                RequestKill();
            }
        }

        /// <summary>
        /// 爆成径向沙球环（权威端）：柱中点为心均匀放射，快慢双速分层出内外两圈落点。
        /// 多柱齐爆时每柱环数自适应递减（怒放波 16 柱连爆的总弹幕压在可读区间）。
        /// </summary>
        private void ExplodeIntoGlobs() {
            NPC head = HeadNPC;
            if (head == null) {
                return;
            }
            int damage = BssDirector.ScaleProjectileDamage(head, BssDirector.SandGlobDamage);
            int type = ModContent.ProjectileType<Projectiles.BssSandGlob>();
            Vector2 mid = new(centerX, baseY - rise * 0.55f);
            int peers = 0;
            foreach (var pillar in Alive) {
                if (pillar.Phase == BssPillarPhase.Detonate) {
                    peers++;
                }
            }
            int count = peers >= 10 ? 9 : peers >= 6 ? 11 : BssDirector.BurstGlobRing;
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.06f, 0.06f);
                float speed = (i & 1) == 0
                    ? Main.rand.NextFloat(BssDirector.BurstGlobSpeedMax * 0.82f, BssDirector.BurstGlobSpeedMax)
                    : Main.rand.NextFloat(BssDirector.BurstGlobSpeedMin, BssDirector.BurstGlobSpeedMin * 1.3f);
                Projectile.NewProjectile(head.GetSource_FromAI(), mid,
                    ang.ToRotationVector2() * speed, type, damage, 0.6f, Main.myPlayer);
            }
        }

        /// <summary>Erupt 伤害窗：本地玩家与已钻出段重叠即咬一口（原版 Hurt 协议自带无敌帧与同步）</summary>
        private void HurtLocalPlayerInColumn() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead || player.immune) {
                return;
            }
            NPC head = HeadNPC;
            if (head == null) {
                return;
            }
            Rectangle column = new(
                (int)(centerX - halfWidth), (int)(baseY - rise),
                (int)(halfWidth * 2f), (int)rise);
            if (!player.Hitbox.Intersects(column)) {
                return;
            }
            int damage = (int)head.GetAttackDamage_ForProjectiles(
                BssDirector.PillarContactDamage.Normal, BssDirector.PillarContactDamage.Expert);
            int dir = player.Center.X < centerX ? -1 : 1;
            player.Hurt(PlayerDeathReason.ByNPC(head.whoAmI), damage, dir, knockback: 8f);
        }
        #endregion

        #region 绘制
        /// <summary>
        /// 鼓包预告绘制：原版沙球贴图按柱宽堆出拱形鼓包，随进度顶升（漫反射乘光照）。
        /// 与破土/沙泉的 omen 同语言：脚下有实体鼓包即警报，不靠尘雾密度赌可读性。
        /// </summary>
        private void DrawOmenMound(SpriteBatch spriteBatch) {
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D ball = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = ball.Size() * 0.5f;
            float p = MathHelper.Clamp(phaseTimer / (float)omenFrames, 0f, 1f);
            float lift = p * p * 18f;

            Span<float> slotFrac = stackalloc float[] { -0.8f, -0.4f, 0f, 0.4f, 0.8f };
            Span<float> slotH = stackalloc float[] { 0.35f, 0.72f, 1f, 0.72f, 0.35f };
            for (int i = 0; i < slotFrac.Length; i++) {
                Vector2 pos = new(centerX + slotFrac[i] * halfWidth, baseY + 3f - lift * slotH[i]);
                Color light = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f));
                float scale = (1f + 0.6f * slotH[i] * p) * (halfWidth / 40f);
                spriteBatch.Draw(ball, pos - Main.screenPosition, null,
                    light.MultiplyRGB(BssVfx.SandWarm), i * 0.7f + p * 2f, origin, scale,
                    SpriteEffects.None, 0f);
            }
        }

        /// <summary>确定性格哈希（seed + 格坐标 → 稳定伪随机，各端同貌）</summary>
        private int CellHash(int col, int row) {
            unchecked {
                int h = seed;
                h = h * 374761393 + col * 668265263;
                h = h * 1274126177 + row * 97531;
                h ^= h >> 13;
                return h & int.MaxValue;
            }
        }

        /// <summary>
        /// 原版沙块贴图拼装：16px 格网，中心帧三变体按格哈希轮换（各端同貌），
        /// 锯齿柱冠 = 每列顶部按哈希差 0~2 格；升起段沿地面线裁剪（空中变体不裁）。
        /// 漫反射材质：逐格乘本地光照，不走加色。
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            //鼓包预告期：沙丘隆起本体（镜像 BssBreachOmen 的可读预告，尘雾之外要有实体）
            if (Phase == BssPillarPhase.Omen && !airborne) {
                DrawOmenMound(spriteBatch);
                return false;
            }
            if (rise < 2f) {
                return false;
            }
            Main.instance.LoadTiles(TileID.Sand);
            Texture2D tex = TextureAssets.Tile[TileID.Sand].Value;

            int cols = Math.Max((int)(halfWidth * 2f / 16f), 1);
            float left = centerX - cols * 8f;
            int riseCells = (int)MathF.Ceiling(rise / 16f);
            int totalCells = (int)MathF.Ceiling(pillarHeight / 16f);
            riseCells = Math.Min(riseCells, totalCells);

            //裂纹预闪：暖白加色薄层随 crackGlow 泛起（Detonate 期）
            float flash = Phase == BssPillarPhase.Detonate ? MathHelper.Clamp(crackGlow, 0f, 1f) : 0f;

            for (int c = 0; c < cols; c++) {
                //锯齿柱冠：每列削去 0~2 格（空中变体底部同样削）
                int crownDrop = CellHash(c, -1) % 3;
                for (int r = crownDrop; r < riseCells; r++) {
                    //列格世界位（柱体随 rise 从地面滑升；贴图纹样锚在柱身上）
                    float cellTop = baseY - rise + r * 16f;
                    if (!airborne && cellTop > baseY - 6f) {
                        break; //地面线以下不画（沙入沙）
                    }
                    if (airborne) {
                        //空中变体底部锯齿（悬浮柱两端都参差）
                        int bottomDrop = CellHash(c, -2) % 3;
                        if (r >= riseCells - bottomDrop) {
                            break;
                        }
                    }

                    int variant = CellHash(c, r) % 3;
                    //帧表对照 Framing.Initialize：全包围 (18/36/54, 18)，顶缘 (18/36/54, 0)，
                    //左缘 (0, 0/18/36)，右缘 (72, 0/18/36)——柱缘用真实合并帧，组合感不靠硬切
                    Rectangle src;
                    if (c == 0 && cols > 1) {
                        src = new Rectangle(0, variant * 18, 16, 16);
                    }
                    else if (c == cols - 1 && cols > 1) {
                        src = new Rectangle(72, variant * 18, 16, 16);
                    }
                    else if (r == crownDrop) {
                        src = new Rectangle(18 + variant * 18, 0, 16, 16);
                    }
                    else {
                        src = new Rectangle(18 + variant * 18, 18, 16, 16);
                    }

                    Vector2 pos = new(left + c * 16f, cellTop);
                    Color light = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f));
                    //格间微色差：只向暗抖动（tone ≤ 1）——超过 1 的乘数在满亮光照下
                    //会把 byte 乘出 255 以上回绕成近黑（真机黑格棋盘的根因）
                    float tone = 0.82f + CellHash(c, r + 1000) % 100 * 0.0018f;
                    Color tint = new((byte)(light.R * tone), (byte)(light.G * tone), (byte)(light.B * tone), (byte)255);
                    spriteBatch.Draw(tex, pos - Main.screenPosition, src, tint,
                        0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

                    if (flash > 0.05f) {
                        spriteBatch.Draw(tex, pos - Main.screenPosition, src,
                            new Color(255, 190, 120, 0) * (0.35f * flash),
                            0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                    }
                }
            }
            return false;
        }
        #endregion
    }
}
