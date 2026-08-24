using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 鬼伞·役灵收湖。持伞且血湖就绪时，湖中鬼手把持有者自己的其他召唤物
    /// （随从/哨兵；鬼伞家系与纯宠物豁免）拖入湖底扣押。真身不杀：
    /// 杀掉会让召唤 buff 自删、模组随从无法通用重生，"切回来自动放还"就断了。
    /// 扣押走 <see cref="TimeFreezeSystem"/> 租约（AI/判伤/位移/寿命全停，buff 自然维持），
    /// 没入水线后隐藏绘制即"消失"，逐条记入 <see cref="OwnerState.Parked"/>；
    /// 松开鬼伞或收域时整批放还，随从浮出湖面自行归队，哨兵送回原驻位。
    /// 一致性模型同鬼梦禁弹：服务器不持有领域相位，各端从已同步的快照
    /// （持有物+域形态）跑同一条确定性规则各自扣押/放还，无需任何包；
    /// 服务器那份副本不冻结也无判伤权（友方弹判伤在所有者本机），任其漂移
    /// </summary>
    internal static class KikasaMinionDrown
    {
        //==================== 波时间轴（60fps）====================
        //合围涟漪 0-12 → 破水错帧 12-27 → 甩到+卷指 ~39 → 绷紧 40 → 拖入 44-70（p² 加速）→ 收尾化水

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

        /// <summary>持伞判定的稳定帧，滚轮扫过鬼伞不触发</summary>
        private const int HoldStableFrames = 6;

        /// <summary>湖面之上的竖直可达带，与手臂解算臂展预算对齐；更高的等它落下来</summary>
        internal const float MaxReachAboveLake = 900f;

        /// <summary>停泊深度基准（湖面之下）</summary>
        private const float ParkDepth = 64f;

        //==================== 记录 ====================

        /// <summary>被扣押的一条召唤物记录：拖入途中在波里，入湖后转入 Parked 名单</summary>
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
            /// <summary>已入湖的扣押名单</summary>
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

                //扣押存续：持伞 + 域活跃且未在收合 + 人活着。
                //翻转/鬼梦期间湖只是换了模样，握着的不松手；收域=湖没了，放还
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
                HoldParked(state);

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
            if (proj.GetGlobalProjectile<KikasaMinionHeldGlobal>().LakeHeld) {
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
                        state.Parked.Add(entry);
                    }
                }
                KikasaMinionDrownFX.OnWaveEnd(wave);
                state.Wave = null;
                state.WaveDelay = WaveGapFrames;
            }
        }

        /// <summary>入湖名单逐帧续租钉在停泊位；失效条目静默除名（租约随实体世代自愈）</summary>
        private static void HoldParked(OwnerState state) {
            for (int k = state.Parked.Count - 1; k >= 0; k--) {
                HeldEntry entry = state.Parked[k];
                Projectile proj = ResolveEntry(entry);
                if (proj == null) {
                    state.Parked.RemoveAt(k);
                    continue;
                }
                entry.Lease = TimeFreezeSystem.AcquireProjectile<LakeGripSource>(
                    proj, entry.ParkPos, entry.ProjIndex,
                    TimeFreezeAnchorPriority.Authoritative);
            }
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
    /// 扣押标记与绘制隐藏。SetDefaults 复位防槽位复用残留；
    /// 冻结姿态由 TimeFreezeProjectile 持有，这里只管"看不见"
    /// </summary>
    internal class KikasaMinionHeldGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>正被湖扣押（含拖入途中），资格扫描据此排重</summary>
        public bool LakeHeld;

        /// <summary>已没入湖中，不再绘制</summary>
        public bool LakeHidden;

        public override void SetDefaults(Projectile projectile) {
            LakeHeld = false;
            LakeHidden = false;
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor) => !LakeHidden;
    }
}
