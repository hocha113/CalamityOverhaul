using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 鬼伞·役灵洗礼。持伞且血湖就绪时，湖中鬼手把持有者自己的其他召唤物
    /// （随从/哨兵；鬼伞家系与纯宠物豁免）拖入湖底浸洗：停泊一小拍后被湖水吐出，
    /// 携带限时血湖状态（浸血外观 + 伤害增益，载体见 <see cref="KikasaMinionHeldGlobal"/>），
    /// 状态激活中免再抓，到期后下一轮抓取波重新洗礼，周期性仪式。真身不杀：
    /// 杀掉会让召唤 buff 自删、模组随从无法通用重生。
    /// 浸洗途中走 <see cref="TimeFreezeSystem"/> 租约（AI/判伤/位移/寿命全停，buff 自然维持），
    /// 没入水线后隐藏绘制即"消失"，逐条记入 <see cref="OwnerState.Parked"/> 停泊待吐；
    /// 松开鬼伞或收域时未吐出的整批放还（沾过湖水的补发状态），哨兵送回原驻位。
    /// 一致性模型同鬼梦禁弹：服务器不持有领域相位，各端从已同步的快照
    /// （持有物+域形态）跑同一条确定性规则各自浸洗/吐出，无需任何包；
    /// 服务器那份副本不冻结也无判伤权（友方弹判伤在所有者本机），任其漂移
    /// </summary>
    internal static class KikasaMinionDrown
    {
        //==================== 波时间轴（60fps）====================
        //合围涟漪 0-12 → 破水错帧 12-27 → 甩到+卷指 ~39 → 绷紧 40 → 拖入 44-70（p² 加速）
        //→ 收尾化水 → 停泊 ~30 帧湖水吐出（血湖状态 900 帧，到期重新可抓）

        internal const int ConvergeEnd = 12;

        internal const int BurstStagger = 3;

        internal const int ReachFrames = 8;

        internal const int TenseBeat = 40;

        internal const int DragStart = 44;

        internal const int DragFrames = 26;

        /// <summary>拖完后留给手化水退场的帧数</summary>
        internal const int WaveEndPad = 10;

        /// <summary>单波最多几只手；超编的召唤物排下一波</summary>
        internal const int WaveCap = 6;

        /// <summary>两波之间的呼吸间隔</summary>
        private const int WaveGapFrames = 30;

        /// <summary>放还后的再收间隔，快速切换武器不至于手忙脚乱刷演出</summary>
        private const int RegrabDelayFrames = 45;

        /// <summary>入湖停泊到被吐出的帧数：湖尝一口就吐，"立刻"里留一拍屏息</summary>
        private const int SpitDwellFrames = 30;

        /// <summary>吐出错拍间隔（按停泊受理序），一串出水不糊成一声</summary>
        private const int SpitStaggerFrames = 4;

        /// <summary>血湖状态时长（900 帧 = 15 秒），到期后重新可抓洗礼</summary>
        internal const int BloodLakeFrames = 900;

        /// <summary>血湖状态的伤害乘区</summary>
        internal const float BloodDamageMul = 1.10f;

        /// <summary>持伞判定的稳定帧，滚轮扫过鬼伞不触发</summary>
        private const int HoldStableFrames = 6;

        /// <summary>湖面之上的竖直可达带，与手臂解算臂展预算对齐；更高的等它落下来</summary>
        internal const float MaxReachAboveLake = 900f;

        /// <summary>停泊深度基准（湖面之下）</summary>
        private const float ParkDepth = 64f;

        //==================== 记录 ====================

        /// <summary>被浸洗的一条召唤物记录：拖入途中在波里，入湖后转入 Parked 停泊待吐</summary>
        internal sealed class HeldEntry
        {
            public int ProjIndex;
            public int ProjType;
            public bool Sentry;
            /// <summary>抓握帧的 Center，哨兵放还的原驻位</summary>
            public Vector2 CapturePos;
            /// <summary>湖底停泊位</summary>
            public Vector2 ParkPos;
            /// <summary>当前钉位锚点，拖入曲线逐帧写入；FX 手腕跟它走</summary>
            public Vector2 Anchor;
            /// <summary>波内手位；-1 = 捕获时已在水下，直接吞没不布手</summary>
            public int HandIndex = -1;
            /// <summary>过水线闩：真身已隐藏</summary>
            public bool Splashed;
            /// <summary>停泊读拍：入 Parked 起计，到 <see cref="SpitAt"/> 被湖吐出</summary>
            public int ParkTimer;
            /// <summary>吐出到点帧（停泊计时轴），入名单时按序错拍定死，中途除名不塌拍</summary>
            public int SpitAt;
            /// <summary>真身中途消失（持有者离场等），波内除名</summary>
            public bool Dropped;
            public TimeFreezeLease Lease;
        }

        /// <summary>一波抓取：同帧受理的一批召唤物共享时间轴，各有各的手</summary>
        internal sealed class GrabWave
        {
            public int OwnerWho;
            public int Timer;
            public float Seed;
            public readonly List<HeldEntry> Entries = [];
        }

        private sealed class OwnerState
        {
            public int HoldStable;
            /// <summary>波间隔/再收间隔共用计时</summary>
            public int WaveDelay;
            public GrabWave Wave;
            /// <summary>已入湖待吐出的停泊名单</summary>
            public readonly List<HeldEntry> Parked = [];
            public bool AnyHeld => Wave != null || Parked.Count > 0;
        }

        //各端本地推导的全量表（覆盖所有玩家），非权威状态、不入档不发包
        private static readonly OwnerState[] owners = new OwnerState[Main.maxPlayers];

        /// <summary>时停租约源标记（静态类不能当泛型实参）</summary>
        internal sealed class LakeGripSource { }

        //鬼伞家系判定按类型缓存，命名空间字符串检查只跑一次
        private static readonly Dictionary<int, bool> familyCache = [];
        private static readonly List<Projectile> scanBuffer = [];
        private static int waveCounter;

        //==================== 逐帧推进（KikasaDrownSystem 驱动，仅客户端）====================

        internal static void Update() {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                OwnerState state = owners[i];
                if (player?.active != true) {
                    if (state != null) {
                        if (state.AnyHeld) {
                            ReleaseAll(state, i, silent: true);
                        }
                        state.HoldStable = 0;
                        state.WaveDelay = 0;
                    }
                    continue;
                }

                KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
                bool holding = HoldingUmbrella(player);
                state ??= owners[i] = new OwnerState();
                state.HoldStable = holding ? Math.Min(state.HoldStable + 1, 600) : 0;

                //浸洗存续：持伞 + 域活跃且未在收合 + 人活着。
                //翻转/鬼梦期间湖只是换了模样，握着的不松手；收域=湖没了，未吐出的放还
                bool holdActive = holding && !player.dead
                    && domain.AnyActive && domain.Phase != KikasaDomainPhase.Closing;
                if (!holdActive) {
                    if (state.AnyHeld) {
                        ReleaseAll(state, i, silent: false);
                    }
                    if (state.WaveDelay > 0) {
                        state.WaveDelay--;
                    }
                    continue;
                }

                AdvanceWave(state, domain);
                HoldParked(state, domain);

                if (state.WaveDelay > 0) {
                    state.WaveDelay--;
                }
                //新波门槛与湖藏 LakeReady 同口径：Open 稳态满水；全局时停里手不出水
                else if (state.Wave == null
                    && state.HoldStable >= HoldStableFrames
                    && domain.Phase == KikasaDomainPhase.Open
                    && domain.RiseT >= 0.999f
                    && !WorldFreezeSystem.IsActive) {
                    TryStartWave(state, player, domain);
                }
            }
        }

        internal static void Reset() {
            for (int i = 0; i < owners.Length; i++) {
                owners[i] = null;
            }
            scanBuffer.Clear();
            waveCounter = 0;
        }

        //==================== 资格 ====================

        /// <summary>
        /// 持伞判定。不用 VaultUtils.GetItem()：它在鼠标拿着物品时返回本机 Main.mouseItem，
        /// 对远端玩家评估会被本机状态污染；HeldItem（背包选中格）各端同步一致
        /// </summary>
        private static bool HoldingUmbrella(Player player) {
            Item item = player.HeldItem;
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        /// <summary>
        /// 判别：只收持有者本人的战斗类召唤物（随从/哨兵/占栏者）。
        /// 纯宠物三旗全无自然不中；鬼伞家系（役鬼/伞奴/恶犬全在 KikasaLegend 命名空间）
        /// 与系统级别碰名单豁免；竖直可达带外的等它自己落进臂展再抓
        /// </summary>
        private static bool IsGrabbable(Projectile proj, int ownerWho, float lakeY) {
            if (proj?.active != true || proj.owner != ownerWho || proj.hostile) {
                return false;
            }
            if (!proj.minion && !proj.sentry && proj.minionSlots <= 0f) {
                return false;
            }
            KikasaMinionHeldGlobal held = proj.GetGlobalProjectile<KikasaMinionHeldGlobal>();
            //浸洗中排重；血湖状态激活者是刚洗礼过的，到期前不回锅
            if (held.LakeHeld || held.BloodLakeTime > 0) {
                return false;
            }
            if (CWRLoad.ProjValue.ImmuneFrozen.TryGetValue(proj.type, out bool immune) && immune) {
                return false;
            }
            if (IsKikasaFamily(proj)) {
                return false;
            }
            return proj.Center.Y >= lakeY - MaxReachAboveLake
                && proj.Center.Y <= lakeY + KikasaDrown.MaxGrabDepth;
        }

        private static bool IsKikasaFamily(Projectile proj) {
            if (proj.ModProjectile == null) {
                return false;
            }
            if (!familyCache.TryGetValue(proj.type, out bool family)) {
                family = proj.ModProjectile.GetType().Namespace?
                    .StartsWith("CalamityOverhaul.Content.LegendWeapon.KikasaLegend") == true;
                familyCache[proj.type] = family;
            }
            return family;
        }

        /// <summary>条目解析：槽位复用（SetDefaults 洗掉 LakeHeld）或类型不符即失效</summary>
        private static Projectile ResolveEntry(HeldEntry entry) {
            Projectile proj = Main.projectile[entry.ProjIndex];
            if (proj?.active != true || proj.type != entry.ProjType
                || !proj.GetGlobalProjectile<KikasaMinionHeldGlobal>().LakeHeld) {
                return null;
            }
            return proj;
        }

        //==================== 抓取波 ====================

        private static void TryStartWave(OwnerState state, Player player, KikasaDomainPlayer domain) {
            float lakeY = domain.LakeWorldY;
            scanBuffer.Clear();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (IsGrabbable(proj, player.whoAmI, lakeY)) {
                    scanBuffer.Add(proj);
                }
            }
            if (scanBuffer.Count == 0) {
                return;
            }
            //按 X 排序取样，手在水面上从左到右排开；超编排下一波
            scanBuffer.Sort((a, b) => a.Center.X.CompareTo(b.Center.X));

            GrabWave wave = new() {
                OwnerWho = player.whoAmI,
                //种子只喂视觉抖动，端间发散无害（9.1 纯装饰豁免）
                Seed = (player.whoAmI * 31 + (++waveCounter & 63)) * 2.39f,
            };
            int hands = 0;
            for (int k = 0; k < scanBuffer.Count && wave.Entries.Count < WaveCap; k++) {
                Projectile proj = scanBuffer[k];
                KikasaMinionHeldGlobal held = proj.GetGlobalProjectile<KikasaMinionHeldGlobal>();
                held.LakeHeld = true;

                HeldEntry entry = new() {
                    ProjIndex = proj.whoAmI,
                    ProjType = proj.type,
                    Sentry = proj.sentry,
                    CapturePos = proj.Center,
                    Anchor = proj.Center,
                };
                float jx = Hash(wave.Seed, k * 5 + 1) - 0.5f;
                float jy = Hash(wave.Seed, k * 5 + 2);
                entry.ParkPos = new Vector2(
                    proj.Center.X + jx * 44f,
                    lakeY + ParkDepth + jy * 30f);

                if (proj.Center.Y >= lakeY) {
                    //本就在水线之下：无需手，受理帧直接吞没
                    entry.Splashed = true;
                    held.LakeHidden = true;
                }
                else {
                    entry.HandIndex = hands++;
                }
                //受理帧即钉住，手还没到人先被湖的目光定住
                entry.Lease = TimeFreezeSystem.AcquireProjectile<LakeGripSource>(
                    proj, entry.CapturePos, entry.ProjIndex,
                    TimeFreezeAnchorPriority.Authoritative);
                wave.Entries.Add(entry);
            }
            state.Wave = wave;
            KikasaMinionDrownFX.OnWaveStart(wave, lakeY);
        }

        private static void AdvanceWave(OwnerState state, KikasaDomainPlayer domain) {
            GrabWave wave = state.Wave;
            if (wave == null) {
                return;
            }
            //全局时停（骇入/域翻转专场）里波时间轴同帧停摆，手不在冻结的世界里继续拖
            if (WorldFreezeSystem.IsActive) {
                return;
            }
            wave.Timer++;
            int t = wave.Timer;
            float lakeY = domain.LakeWorldY;
            bool anyAlive = false;

            foreach (HeldEntry entry in wave.Entries) {
                if (entry.Dropped) {
                    continue;
                }
                Projectile proj = ResolveEntry(entry);
                if (proj == null) {
                    entry.Dropped = true;
                    continue;
                }
                anyAlive = true;

                //攥稳前钉在捕获位；拖入段 p² 加速拽向停泊位，禁匀速
                if (t >= DragStart) {
                    float p = MathHelper.Clamp((t - DragStart) / (float)DragFrames, 0f, 1f);
                    entry.Anchor = Vector2.Lerp(entry.CapturePos, entry.ParkPos, p * p);
                }
                else {
                    entry.Anchor = entry.CapturePos;
                }

                //过水线：真身没入，此后只剩水花墨雾；隐藏由溅水拍掩护
                if (!entry.Splashed && entry.Anchor.Y >= lakeY) {
                    entry.Splashed = true;
                    proj.GetGlobalProjectile<KikasaMinionHeldGlobal>().LakeHidden = true;
                    KikasaMinionDrownFX.OnEntrySubmerge(wave, entry, lakeY);
                }

                entry.Lease = TimeFreezeSystem.AcquireProjectile<LakeGripSource>(
                    proj, entry.Anchor, entry.ProjIndex,
                    TimeFreezeAnchorPriority.Authoritative);
            }

            if (t >= DragStart + DragFrames + WaveEndPad || !anyAlive) {
                foreach (HeldEntry entry in wave.Entries) {
                    if (!entry.Dropped) {
                        //吐出到点帧入名单时定死，靠名单当前长度错拍，中途除名不塌拍
                        entry.ParkTimer = 0;
                        entry.SpitAt = SpitDwellFrames + state.Parked.Count * SpitStaggerFrames;
                        state.Parked.Add(entry);
                    }
                }
                KikasaMinionDrownFX.OnWaveEnd(wave);
                state.Wave = null;
                state.WaveDelay = WaveGapFrames;
            }
        }

        /// <summary>
        /// 停泊名单逐帧续租钉在停泊位，读拍到点被湖吐出（发放血湖状态）；
        /// 失效条目静默除名（租约随实体世代自愈）。全局时停里停拍，与波时间轴同口径
        /// </summary>
        private static void HoldParked(OwnerState state, KikasaDomainPlayer domain) {
            bool frozen = WorldFreezeSystem.IsActive;
            for (int k = state.Parked.Count - 1; k >= 0; k--) {
                HeldEntry entry = state.Parked[k];
                Projectile proj = ResolveEntry(entry);
                if (proj == null) {
                    state.Parked.RemoveAt(k);
                    continue;
                }
                if (!frozen && ++entry.ParkTimer >= entry.SpitAt) {
                    SpitEntry(entry, proj, domain);
                    state.Parked.RemoveAt(k);
                    continue;
                }
                entry.Lease = TimeFreezeSystem.AcquireProjectile<LakeGripSource>(
                    proj, entry.ParkPos, entry.ProjIndex,
                    TimeFreezeAnchorPriority.Authoritative);
            }
        }

        //==================== 吐出 ====================

        /// <summary>
        /// 湖水吐出一条：发放血湖状态，随从从吞没处的水线口猛地抛出（自己的 AI 会归队/超距瞬移），
        /// 哨兵是驻防工事送回原驻位。确定性同放还：速度只喂 <see cref="Hash"/>，各端一致
        /// </summary>
        private static void SpitEntry(HeldEntry entry, Projectile proj, KikasaDomainPlayer domain) {
            KikasaMinionHeldGlobal held = proj.GetGlobalProjectile<KikasaMinionHeldGlobal>();
            held.LakeHeld = false;
            held.LakeHidden = false;
            held.BloodLakeTime = BloodLakeFrames;

            float lakeY = domain.LakeWorldY;
            Vector2 spitPos;
            Vector2 spitVel;
            bool surface;
            if (entry.Sentry) {
                spitPos = entry.CapturePos;
                spitVel = Vector2.Zero;
                surface = entry.CapturePos.Y >= lakeY - 30f;
            }
            else {
                //从吞它的地方吐出来，上抛比温和放还更冲一口
                spitPos = new Vector2(entry.ParkPos.X, lakeY - 12f);
                spitVel = new Vector2(
                    (Hash(entry.ParkPos.X, 3) - 0.5f) * 2.4f,
                    -5.2f - Hash(entry.ParkPos.Y, 7) * 2.6f);
                surface = true;
            }

            TimeFreezeSystem.ReleaseProjectile(proj, entry.Lease, spitVel);
            proj.Center = spitPos;
            proj.velocity = spitVel;
            //owner 端的 netUpdate 才会被消费，别端置了也无害
            proj.netUpdate = true;

            //错拍已由 SpitAt 在规则层走完，演出立即到期
            KikasaMinionDrownFX.QueueEmergence(proj.owner, spitPos, lakeY,
                MathHelper.Clamp(MathF.Sqrt(proj.width * (float)proj.height) / 30f, 0.6f, 1.6f),
                surface, 0, spit: true);
        }

        //==================== 放还 ====================

        private static void ReleaseAll(OwnerState state, int ownerWho, bool silent) {
            Player owner = Main.player[ownerWho];
            KikasaDomainPlayer domain = owner?.active == true
                ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            //湖还在场才有浮出演出；收域后段/离场走原地静默还位
            bool lakeAlive = domain != null && domain.AnyActive && domain.RiseT > 0.3f;
            float lakeY = domain?.LakeWorldY ?? 0f;

            GrabWave wave = state.Wave;
            if (wave != null) {
                //拖入中途松手：条目原地释放，鬼手空攥收回
                foreach (HeldEntry entry in wave.Entries) {
                    if (!entry.Dropped) {
                        ReleaseEntry(entry, owner, lakeAlive, lakeY, -1, 0, silent);
                    }
                }
                KikasaMinionDrownFX.OnWaveWhiff(wave);
                state.Wave = null;
            }

            int count = state.Parked.Count;
            for (int k = 0; k < count; k++) {
                ReleaseEntry(state.Parked[k], owner, lakeAlive, lakeY, k, count, silent);
            }
            state.Parked.Clear();
            state.WaveDelay = RegrabDelayFrames;
        }

        /// <summary>
        /// 放还一条：随从浮出持有者脚边的湖面（自己的 AI 会归队/超距瞬移），
        /// 哨兵是驻防工事送回原驻位；湖不在或中途松手的原地释放
        /// </summary>
        private static void ReleaseEntry(HeldEntry entry, Player owner, bool lakeAlive,
            float lakeY, int emergeIndex, int emergeCount, bool silent) {
            Projectile proj = ResolveEntry(entry);
            if (proj == null) {
                return;
            }
            KikasaMinionHeldGlobal held = proj.GetGlobalProjectile<KikasaMinionHeldGlobal>();
            held.LakeHeld = false;
            held.LakeHidden = false;
            //沾过湖水就算洗礼：沉过水线还没等到吐出的，放还时补发状态；计时与领域解耦
            if (entry.Splashed) {
                held.BloodLakeTime = BloodLakeFrames;
            }

            Vector2 releasePos;
            Vector2 releaseVel;
            bool surface = false;
            if (entry.Sentry) {
                releasePos = entry.CapturePos;
                releaseVel = Vector2.Zero;
                surface = lakeAlive && entry.CapturePos.Y >= lakeY - 30f;
            }
            else if (lakeAlive && owner?.active == true && !owner.dead && emergeIndex >= 0) {
                float spreadX = (emergeIndex - (emergeCount - 1) * 0.5f) * 30f;
                releasePos = new Vector2(owner.Center.X + spreadX, lakeY - 12f);
                releaseVel = new Vector2(
                    Hash(entry.ParkPos.X, emergeIndex) - 0.5f,
                    -3.4f - Hash(entry.ParkPos.Y, emergeIndex) * 2.4f);
                surface = true;
            }
            else {
                //中途松手/湖已不在：入过水的从水线口放出，没入水的原地松开
                releasePos = entry.Splashed && lakeAlive
                    ? new Vector2(entry.ParkPos.X, lakeY - 12f)
                    : proj.Center;
                releaseVel = new Vector2(0f, -2f);
                surface = entry.Splashed && lakeAlive;
            }

            TimeFreezeSystem.ReleaseProjectile(proj, entry.Lease, releaseVel);
            proj.Center = releasePos;
            proj.velocity = releaseVel;
            //owner 端的 netUpdate 才会被消费，别端置了也无害
            proj.netUpdate = true;

            if (!silent) {
                KikasaMinionDrownFX.QueueEmergence(proj.owner, releasePos, lakeY,
                    MathHelper.Clamp(MathF.Sqrt(proj.width * (float)proj.height) / 30f, 0.6f, 1.6f),
                    surface, Math.Max(emergeIndex, 0));
            }
        }

        internal static float Hash(float seed, int k) {
            float h = MathF.Sin(seed * 12.9898f + k * 78.233f) * 43758.547f;
            return h - MathF.Floor(h);
        }
    }

    /// <summary>
    /// 浸洗标记、血湖状态与绘制表现。SetDefaults 复位防槽位复用残留；
    /// 冻结姿态由 TimeFreezeProjectile 持有，这里管"看不见"与吐出后的浸血外观/增益。
    /// 状态由各端确定性规则同帧发放，倒计时本地自走，伤害在所有者本机结算，无需同步
    /// </summary>
    internal class KikasaMinionHeldGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>正被湖浸洗（含拖入途中），资格扫描据此排重</summary>
        public bool LakeHeld;

        /// <summary>已没入湖中，不再绘制</summary>
        public bool LakeHidden;

        /// <summary>血湖状态剩余帧：吐出/放还时发放，激活中免再抓；子弹幕沿父链继承</summary>
        public int BloodLakeTime;

        /// <summary>浸染强度 0~1：到期前最后 90 帧渐退，预告下一轮抓取</summary>
        public float BloodFade => MathHelper.Clamp(BloodLakeTime / 90f, 0f, 1f);

        //鬼雨异化时随观看域冷化，同沉溺色板
        private static Color BloodTint
            => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));

        public override void SetDefaults(Projectile projectile) {
            LakeHeld = false;
            LakeHidden = false;
            BloodLakeTime = 0;
        }

        /// <summary>子弹幕沿父链继承剩余浸血时间，远程役从的弹药同样吃洗礼、带浸染</summary>
        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (source is EntitySource_Parent parentSource
                && parentSource.Entity is Projectile parent
                && parent.TryGetGlobalProjectile(out KikasaMinionHeldGlobal parentHeld)
                && parentHeld.BloodLakeTime > 0) {
                BloodLakeTime = parentHeld.BloodLakeTime;
            }
        }

        public override void PostAI(Projectile projectile) {
            if (BloodLakeTime <= 0) {
                return;
            }
            BloodLakeTime--;
            //滴血只在召唤物/哨兵本体（子弹幕不滴防刷屏），纯装饰客户端限定
            if (Main.dedServ
                || (!projectile.minion && !projectile.sentry && projectile.minionSlots <= 0f)) {
                return;
            }
            if (Main.rand.NextBool(10)) {
                Vector2 at = projectile.Center + new Vector2(
                    Main.rand.NextFloat(-0.4f, 0.4f) * projectile.width,
                    Main.rand.NextFloat(-0.2f, 0.45f) * projectile.height);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(at,
                    new Vector2(projectile.velocity.X * 0.2f,
                        MathF.Max(projectile.velocity.Y * 0.2f, 0.4f)),
                    BloodTint * (0.55f * BloodFade), Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(Main.rand.Next(16, 26), 0f);
            }
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target,
            ref NPC.HitModifiers modifiers) {
            if (BloodLakeTime > 0) {
                modifiers.FinalDamage *= KikasaMinionDrown.BloodDamageMul;
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor) {
            if (LakeHidden) {
                return false;
            }
            if (BloodLakeTime > 0) {
                //浸血外观：向血湖色板压染，鬼雨异化随观看域冷化
                lightColor = Color.Lerp(lightColor, BloodTint, 0.32f * BloodFade);
            }
            return true;
        }
    }
}
