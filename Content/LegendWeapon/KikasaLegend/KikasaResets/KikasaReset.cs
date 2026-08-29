using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills.Restarts;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.Scenarios.Kiame.Overlay;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 鬼伞·大范围重启权威核心。持伞随时可按，把最近数秒"冲洗"回去：
    /// 屏幕定格成黑白照片、被雨痕刷掉，场内 NPC 与玩家沿运动历史倒退，
    /// 结算分两拍：先清 debuff，隔两帧回满生命法力，全程无敌。
    /// 非鬼雨状态下发动时演出借雨：照片掩护下把施术者领域强制成鬼雨满水稳态，
    /// 落定白闪峰值弹回施术前状态，鬼雨只借给演出用（见 <see cref="UpdateBorrow"/>）。
    /// 契约：服务器只验冷却/在场，不验领域，服务器没有领域状态，冲突门由客户端预检；
    /// NPC 倒放两端各按本端历史推演，服务器周期 netUpdate 与落定 SyncNPC 兜住终位；
    /// NPC 冻结走 TimeFreezes 租约（统一 AI 入口把 Override/原版 AI 一并停摆，
    /// 位置由冻结锚点沿历史驱动），敌方弹幕在范围内计时冻结、演出收场自然解冻；
    /// 玩家生命/清 buff 一律归本机结算，服务器不碰（CyberRestart 同款）。
    /// 全屏演出全局同刻只放一场。
    /// </summary>
    internal static class KikasaReset
    {
        //==================== 时间轴 ====================

        /// <summary>定格段末帧：快门白闪，世界钉住成照片</summary>
        public const int SnapshotEnd = 24;

        /// <summary>冲刷段末帧：雨痕自上而下把照片刷掉，沙漏同步汇聚成形</summary>
        public const int WashEnd = 100;

        /// <summary>倒带段末帧=结算帧：恢复自此分两拍兑现</summary>
        public const int RewindEnd = 250;

        /// <summary>落定收尾</summary>
        public const int TotalFrames = 274;

        /// <summary>倒放窗口：回到 10 秒前</summary>
        public const int RewindWindowFrames = 600;

        /// <summary>作用半径，约一屏半</summary>
        public const float ResetRange = 2400f;

        /// <summary>完成后冷却</summary>
        public const int CooldownFrames = 60 * 60;

        /// <summary>落定后的无敌缓冲</summary>
        public const int PostImmuneFrames = 60;

        /// <summary>回满相对清 debuff 的延迟帧：等削上限的效果退场、statLifeMax2 恢复后再兑现满血</summary>
        private const int HealDelayFrames = 2;

        /// <summary>借雨帧：照片已在演出首帧的绘制里捕获，留一帧余量，照片永远定格旧世界</summary>
        private const int BorrowFrame = 2;

        /// <summary>借雨弹回帧：落定白闪峰值，与结算两拍（<see cref="RewindEnd"/> / +<see cref="HealDelayFrames"/>）错开</summary>
        private const int BorrowRevertFrame = RewindEnd + 3;

        /// <summary>倒带的脉冲波数：胶片回卷的呼吸感来源（波谷不停摆，见 <see cref="RewindEase"/>）</summary>
        private const int RewindPulses = 3;

        /// <summary>单帧历史深度增量的峰值：主干与脉冲波在中点同峰，导数为均值的 1.5 倍</summary>
        private const float PeakRewindSpeed
            = RewindWindowFrames / (float)(RewindEnd - WashEnd) * 1.5f;

        //==================== 运行时 ====================

        internal sealed class ResetShow
        {
            public int OwnerWho;
            public int ResetId;
            public float Seed;
            public int Timer;
            public bool RestoreFired;
            public bool HealFired;
            /// <summary>借雨生效中：演出把施术者领域临时强制成鬼雨满水稳态</summary>
            public bool BorrowActive;
            /// <summary>借雨前的领域底片，弹回用</summary>
            public KikasaDomainPlayer.ResetBorrowState BorrowSnapshot;
            /// <summary>受影响 NPC 身份（Apply 时刻权威圈定）</summary>
            public readonly List<NetworkNPCIdentity> Npcs = [];
            /// <summary>受影响玩家 whoAmI</summary>
            public readonly List<int> Players = [];
        }

        /// <summary>当前进行中的重启；全局同刻只一场</summary>
        internal static ResetShow Active { get; private set; }

        //权威冷却（服务器/单机），客户端另有乐观锁
        private static readonly int[] cooldowns = new int[Main.maxPlayers];
        private static int nextResetId;

        //本机所有者的乐观锁：请求在途/演出进行/完成冷却，服务器另有真限频
        private static uint localLockUntil;

        //本帧已解析的受影响 NPC 槽位，GlobalNPC 的 PreAI 按此兜底拦截
        private static readonly HashSet<int> heldNpcIndices = [];
        private static readonly List<NPC> groupBuffer = [];
        private static readonly HashSet<int> seenNpcBuffer = [];
        private static readonly HashSet<int> droppedNpcBuffer = [];

        //冻结租约（本端局部状态）：走 TimeFreezes 统一 AI 入口，
        //Override/原版 AI 一并停摆，位置由锚点持住；索引=NPC 槽位
        private static readonly TimeFreezeLease[] npcLeases
            = new TimeFreezeLease[Main.maxNPCs];

        /// <summary>TimeFreezes 的冻结来源标记（KikasaReset 是静态类，当不了泛型实参）</summary>
        private sealed class RewindFreeze { }

        //==================== 状态查询 ====================

        /// <summary>演出进行中历史记录暂停：最新样本锚定在触发帧</summary>
        internal static bool HistoryPaused => Active != null;

        /// <summary>倒带段进行中（本端视角），雨滴据此倒飞</summary>
        internal static bool RainRewindActive
            => Active != null && Active.Timer > WashEnd && Active.Timer <= RewindEnd;

        /// <summary>当帧回卷速率 0~1：AgeAt 帧间差按脉冲峰值归一，雨滴上飞随这个节拍呼吸</summary>
        internal static float RewindPulseRate { get; private set; }

        /// <summary>该 NPC 是否被本场重启持有（AI 暂停、位置由倒放接管）</summary>
        internal static bool IsNpcHeld(int npcIndex) => heldNpcIndices.Contains(npcIndex);

        /// <summary>该玩家是否在本场重启的波及名单里</summary>
        internal static bool IsPlayerAffected(int who)
            => Active != null && Active.Players.Contains(who);

        /// <summary>
        /// 本机是否看这场演出：被波及玩家必看，旁观按与施术者的距离；
        /// 全屏照片/冲刷/冷调只给看得见的端
        /// </summary>
        internal static bool LocallyViewed {
            get {
                ResetShow show = Active;
                if (show == null || Main.dedServ || Main.gameMenu) {
                    return false;
                }
                Player local = Main.LocalPlayer;
                if (local?.active != true) {
                    return false;
                }
                if (show.Players.Contains(local.whoAmI)) {
                    return true;
                }
                Player owner = Main.player[show.OwnerWho];
                return owner?.active == true
                    && Vector2.Distance(owner.Center, local.Center) <= ResetRange + 1200f;
            }
        }

        /// <summary>本机重启锁剩余 0~1（1=刚上锁），HUD 冷却墨线消费；无锁=0。
        /// 分母守一道零：冷却常量被调试归零时读数直接归零，不除爆</summary>
        internal static float LocalCooldown01 {
            get {
                if (CooldownFrames <= 0 || Main.GameUpdateCount >= localLockUntil) {
                    return 0f;
                }
                return MathHelper.Clamp(
                    (localLockUntil - Main.GameUpdateCount) / (float)CooldownFrames, 0f, 1f);
            }
        }

        //==================== 客户端入口 ====================

        /// <summary>
        /// 本机状态预检（不含冷却与在演场次）：重启不再看领域形态，随时可按；
        /// 只拒全屏世界改写正忙的场合——翻转/鬼梦过场、入雨/深潜、本人鬼切领域，
        /// 借雨不与它们叠加。HUD 教程与按键受理同口径；服务器照旧不验领域
        /// </summary>
        internal static bool CanStartLocal(Player player) {
            if (player?.active != true || player.dead) {
                return false;
            }
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            if (domain.Phase == KikasaDomainPhase.Flipping || domain.InDreamPhase) {
                return false;
            }
            if (OniRainWorldTransition.Active || OniRainDescentTransition.Active) {
                return false;
            }
            return !player.GetModPlayer<OniDomainPlayer>().AnyActive;
        }

        /// <summary>
        /// 按键受理：随时可按，非鬼雨状态由演出借雨补齐（见 <see cref="UpdateBorrow"/>）；
        /// 冲突预检、本机乐观锁，然后单机直通/联机上行请求
        /// </summary>
        internal static void TryReset(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!CanStartLocal(player)) {
                Refuse(player);
                return;
            }
            //倒带演出全局同刻只一场：比目鱼的大范围重启共用同一个天下
            if (Active != null || HalibutReset.Active != null) {
                Refuse(player);
                return;
            }
            if (Main.GameUpdateCount < localLockUntil) {
                Refuse(player);
                return;
            }

            //请求在途短锁防连点，真限频在权威端
            localLockUntil = Main.GameUpdateCount + 60;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                KikasaResetNet.SendRequest();
            }
            else {
                StartAuthoritative(player);
            }
        }

        private static void Refuse(Player player) {
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Volume = 0.55f,
                Pitch = -0.7f,
                MaxInstances = 2
            }, player.Center);
        }

        //==================== 权威路径 ====================

        /// <summary>服务器收到请求：来源以连接为准</summary>
        internal static void HandleRequest(int ownerWho) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            Player owner = ownerWho >= 0 && ownerWho < Main.maxPlayers
                ? Main.player[ownerWho] : null;
            if (owner?.active != true) {
                Reject(ownerWho, "owner-invalid");
                return;
            }
            StartAuthoritative(owner);
        }

        /// <summary>共同权威路径：单机直通与服务器请求都走这里</summary>
        internal static bool StartAuthoritative(Player owner) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            if (owner?.active != true || owner.dead) {
                Reject(owner?.whoAmI ?? -1, "owner-dead");
                return false;
            }
            int ownerWho = owner.whoAmI;
            if (Active != null || HalibutReset.Active != null) {
                Reject(ownerWho, "show-busy");
                return false;
            }
            if (cooldowns[ownerWho] > 0) {
                Reject(ownerWho, "cooldown");
                return false;
            }

            ResetShow show = new() {
                OwnerWho = ownerWho,
                ResetId = ++nextResetId,
                Seed = Main.rand.NextFloat(1000f),
            };
            CollectNpcs(owner, show.Npcs);
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true && !player.dead
                    && Vector2.Distance(player.Center, owner.Center) <= ResetRange) {
                    show.Players.Add(i);
                }
            }

            Active = show;
            //补一帧触发时刻的样本：age=0 钉在触发帧，快移实体定格不回弹
            KikasaResetHistory.ForceSample();
            //触发当帧就上冻结租约，不给 AI 留最后一帧空跑
            HoldAffectedNpcs(show);
            if (Main.netMode == NetmodeID.Server) {
                KikasaResetNet.SendApply(show);
            }
            else {
                //单机：权威与演出同机同帧
                OnShowStarted(show);
            }
            return true;
        }

        /// <summary>圈定半径内的活跃 NPC；蠕虫等整组同倒，免得半截被拖回半截照打</summary>
        private static void CollectNpcs(Player owner, List<NetworkNPCIdentity> output) {
            seenNpcBuffer.Clear();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.lifeMax <= 0
                    || seenNpcBuffer.Contains(npc.whoAmI)
                    || Vector2.Distance(npc.Center, owner.Center) > ResetRange) {
                    continue;
                }
                if (IsSplitWormExcluded(npc)) {
                    //分裂型蠕虫不倒带：体节槽位随分裂/重链复用，环形历史与现节错位
                    //会拖出鬼节与断头（反馈六·#61 拍板：点名单排除，其余照常）
                    continue;
                }
                NpcGroupHelper.CollectGroup(npc, groupBuffer);
                if (groupBuffer.Count == 0) {
                    groupBuffer.Add(npc);
                }
                foreach (NPC member in groupBuffer) {
                    if (member?.active != true || member.lifeMax <= 0
                        || !seenNpcBuffer.Add(member.whoAmI)
                        || CyberBanish.IsBanishing(member.whoAmI)) {
                        continue;
                    }
                    if (NetworkNPCIdentity.TryCapture(member, out NetworkNPCIdentity id)) {
                        output.Add(id);
                    }
                }
                groupBuffer.Clear();
            }
        }

        //被拒的请求写日志：静默拒绝没法诊断
        private static void Reject(int ownerWho, string clause) {
            CWRMod.Instance?.Logger?.Info($"[KikasaReset] reject owner={ownerWho} clause={clause}");
        }

        //==================== 演出入口（客户端收 Apply / 单机直通） ====================

        /// <summary>客户端按 Apply 包起演出；时间轴与权威端同构</summary>
        internal static void StartShow(int ownerWho, int resetId, float seed,
            List<NetworkNPCIdentity> npcs, List<int> players) {
            ResetShow show = new() {
                OwnerWho = ownerWho,
                ResetId = resetId,
                Seed = seed,
            };
            show.Npcs.AddRange(npcs);
            show.Players.AddRange(players);
            Active = show;
            //客户端同样把本端历史的最新样本钉在收包帧
            KikasaResetHistory.ForceSample();
            //收包当帧就冻住，收包与首次逐帧推进之间不留 AI 空窗
            HoldAffectedNpcs(show);
            OnShowStarted(show);
        }

        /// <summary>起演立即持住全体受影响 NPC：先解一次身份，再逐个上租约锚定当前位置</summary>
        private static void HoldAffectedNpcs(ResetShow show) {
            RefreshHeldNpcs(show);
            foreach (int index in heldNpcIndices) {
                NPC npc = Main.npc[index];
                npcLeases[index] = TimeFreezeSystem.AcquireNPC<RewindFreeze>(npc,
                    npc.Center, index, TimeFreezeAnchorPriority.Authoritative);
            }
        }

        private static void OnShowStarted(ResetShow show) {
            if (Main.dedServ) {
                return;
            }
            //定格帧捕获请求：下一次全屏合成把主屏存作照片
            KikasaResetRender.RequestSnapshot();
            //快门按距离衰减给每个端
            Player owner = Main.player[show.OwnerWho];
            if (owner?.active == true) {
                SoundEngine.PlaySound(SoundID.Camera with {
                    Volume = 0.9f,
                    Pitch = -0.1f
                }, owner.Center);
            }
            //施术者本机才有运镜；运镜失败不致命，演出照走
            if (show.OwnerWho == Main.myPlayer) {
                CutsceneDirector.Play<KikasaResetCutscene>(Main.LocalPlayer);
            }
        }

        /// <summary>客户端收 Cancel（施术者掉线等）：立即收场，不做恢复</summary>
        internal static void HandleCancel(int resetId) {
            if (Active?.ResetId == resetId) {
                AbortShow();
            }
        }

        private static void AbortShow() {
            //中断也要把借来的雨还回去，不留借用残留
            RevertBorrowOnEnd(Active);
            //中断放行不带历史动量：恢复各自冻结前的速度快照即可
            ReleaseAllNpcLeases();
            Active = null;
            heldNpcIndices.Clear();
            RewindPulseRate = 0f;
            //实体已被部分倒放，旧轨迹同样作废
            KikasaResetHistory.Clear();
        }

        //==================== 每帧推进 ====================

        /// <summary>分裂型蠕虫黑名单：世吞与神吞战中会分裂重链，槽位复用让回溯历史
        /// 与现节对不上号；灾厄未装载时 CWRID 取 0，对活跃 NPC 永不误匹配</summary>
        private static bool IsSplitWormExcluded(NPC npc) {
            int t = npc.type;
            return t == NPCID.EaterofWorldsHead || t == NPCID.EaterofWorldsBody
                || t == NPCID.EaterofWorldsTail
                || t == CWRID.NPC_DevourerofGodsHead
                || t == CWRID.NPC_DevourerofGodsBody
                || t == CWRID.NPC_DevourerofGodsTail;
        }

        /// <summary>由 <see cref="KikasaResetSystem"/> 两端逐帧驱动</summary>
        internal static void Update() {
            //时停期间冷却同步暂停：装备冷却被冻结快照冻住，重启冷却不该独走白拖（反馈四·#119）；
            //冻结旗在服务器恒假，联机权威节拍不受影响
            bool frozen = WorldFreezeSystem.IsActive
                || HackTimes.HackTime.Active || TimeGear.TimeScale <= 0f;
            if (!frozen && Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < cooldowns.Length; i++) {
                    if (cooldowns[i] > 0) {
                        cooldowns[i]--;
                    }
                }
            }
            if (frozen && localLockUntil > Main.GameUpdateCount) {
                //本机锁是绝对帧戳：冻结帧把到期时刻同步后移，等效暂停
                localLockUntil++;
            }
            UpdateShow();
        }

        private static void UpdateShow() {
            ResetShow show = Active;
            if (show == null) {
                return;
            }

            //施术者掉线整场收场；死亡不收，无敌顶着，且其余人还等着被倒放回血
            Player owner = Main.player[show.OwnerWho];
            if (owner?.active != true) {
                if (Main.netMode == NetmodeID.Server) {
                    KikasaResetNet.SendCancel(show.ResetId);
                    cooldowns[show.OwnerWho] = CooldownFrames / 2;
                }
                Reject(show.OwnerWho, "cancel:owner-lost");
                AbortShow();
                return;
            }

            show.Timer++;
            UpdateBorrow(show, owner);
            RefreshHeldNpcs(show);

            float age = AgeAt(show.Timer);
            RewindPulseRate = MathHelper.Clamp(
                (age - AgeAt(show.Timer - 1)) / PeakRewindSpeed, 0f, 1f);
            //倒带段原版天气雨与倒飞的鬼雨方向相斥，本机逐帧熄掉；窗口过后自然重落
            if (RainRewindActive && LocallyViewed) {
                Rain.ClearRain();
            }
            bool pushNet = Main.netMode == NetmodeID.Server && show.Timer % 10 == 0;
            foreach (int index in heldNpcIndices) {
                NPC npc = Main.npc[index];
                bool sampled = KikasaResetHistory.TrySampleNpc(index, age,
                    out Vector2 position, out float rotation,
                    out int direction, out int spriteDirection);
                //逐帧续租并把冻结锚点推向历史位置：AI 停摆与位置持有都交给
                //TimeFreezes 统一入口，租约失效（身份重置等）也会在此自愈重挂
                Vector2? anchor = sampled ? position + npc.Size * 0.5f : null;
                npcLeases[index] = TimeFreezeSystem.AcquireNPC<RewindFreeze>(npc,
                    anchor, index, TimeFreezeAnchorPriority.Authoritative);
                if (sampled) {
                    //姿态随位置一同回放：朝向与旋转沿历史倒退，读作真倒带而非拖拽
                    npc.rotation = rotation;
                    if (direction != 0) {
                        npc.direction = direction;
                    }
                    if (spriteDirection != 0) {
                        npc.spriteDirection = spriteDirection;
                    }
                }
                if (pushNet) {
                    npc.netUpdate = true;
                }
            }
            FreezeHostileProjectiles(owner);

            if (!Main.dedServ) {
                UpdateLocalPlayer(show, age);
            }

            //结算拆两拍归本机；服务器只负责把 NPC 终位推正。
            //第一拍清 debuff+落定无敌，本帧 statLifeMax2 仍带着削上限效果算出的值，
            //回满放到第二拍，等上限恢复后兑现，免得清完 debuff 血却钉在低位
            if (!show.RestoreFired && show.Timer >= RewindEnd) {
                show.RestoreFired = true;
                if (!Main.dedServ) {
                    if (show.Players.Contains(Main.myPlayer)) {
                        ApplyLocalCleanse(Main.LocalPlayer);
                    }
                    if (LocallyViewed) {
                        SoundEngine.PlaySound(SoundID.Splash with {
                            Volume = 0.8f,
                            Pitch = 0.35f
                        }, owner.Center);
                    }
                }
            }
            if (!show.HealFired && show.Timer >= RewindEnd + HealDelayFrames) {
                show.HealFired = true;
                if (!Main.dedServ && show.Players.Contains(Main.myPlayer)) {
                    ApplyLocalHeal(Main.LocalPlayer);
                }
            }

            if (show.Timer >= TotalFrames) {
                FinishShow(show);
            }
        }

        //==================== 借雨 ====================

        /// <summary>
        /// 借雨推进：非鬼雨状态下发动时，照片掩护下把施术者领域强制成鬼雨满水稳态，
        /// 冲刷揭出的就是一场下了很久的鬼雨；落定白闪峰值弹回施术前状态。
        /// 借用/弹回是 show 时间轴的确定性函数，各端本地自算同一答案，
        /// 每帧幂等重申压过途中迟到的旧领域快照包；owner 端在借用与弹回帧各转播一份兜底。
        /// 服务器没有领域状态，全程跳过
        /// </summary>
        private static void UpdateBorrow(ResetShow show, Player owner) {
            if (Main.dedServ) {
                return;
            }
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            if (show.Timer == BorrowFrame) {
                //已是稳定鬼雨不借；翻转/鬼梦正忙（服务器不验领域留下的竞态缝隙）整场跳过借雨
                if ((domain.IsRainForm && domain.LakeAbilityReady)
                    || domain.Phase == KikasaDomainPhase.Flipping || domain.InDreamPhase) {
                    return;
                }
                show.BorrowSnapshot = domain.CaptureResetBorrow();
                show.BorrowActive = true;
                domain.ApplyResetBorrow(entryBeat: true);
                if (LocallyViewed) {
                    //冲刷揭幕前空中先布满雨：照片背后世界已在下雨
                    KikasaDomainDeco.PrefillRainCurtain();
                    //入雨先兆：天幕先闪在照片后头，雷声延迟到冲刷揭幕时分（光先于声）
                    KikasaDomainSky.NotifyThunder();
                }
                return;
            }
            if (!show.BorrowActive || show.Timer < BorrowFrame) {
                return;
            }
            if (show.Timer < BorrowRevertFrame) {
                domain.ApplyResetBorrow(entryBeat: false);
            }
            else if (show.Timer == BorrowRevertFrame) {
                RevertBorrow(show, domain);
            }
        }

        /// <summary>弹回借雨；未借用时为空操作</summary>
        private static void RevertBorrow(ResetShow show, KikasaDomainPlayer domain) {
            if (!show.BorrowActive) {
                return;
            }
            show.BorrowActive = false;
            domain.RevertResetBorrow(show.BorrowSnapshot);
        }

        /// <summary>演出中断/收尾的借雨兜底弹回：正常路径在弹回帧已还原，这里防中断残留</summary>
        private static void RevertBorrowOnEnd(ResetShow show) {
            if (show == null || !show.BorrowActive || Main.dedServ) {
                return;
            }
            Player owner = show.OwnerWho >= 0 && show.OwnerWho < Main.maxPlayers
                ? Main.player[show.OwnerWho] : null;
            if (owner?.active == true) {
                RevertBorrow(show, owner.GetModPlayer<KikasaDomainPlayer>());
            }
            else {
                //施术者已离场：其域由 PlayerDisconnect→ResetDomain 清场，借用旗就地作废
                show.BorrowActive = false;
            }
        }

        /// <summary>
        /// 时间轴 t 上实体该处的历史深度：定格与冲刷段钉在触发帧（记录已暂停，age=0 即触发帧），
        /// 倒带段沿呼吸曲线回卷推到 <see cref="RewindWindowFrames"/>
        /// </summary>
        internal static float AgeAt(int timer) {
            if (timer <= WashEnd) {
                return 0f;
            }
            float x = MathHelper.Clamp(
                (timer - WashEnd) / (float)(RewindEnd - WashEnd), 0f, 1f);
            return RewindEase(x) * RewindWindowFrames;
        }

        /// <summary>
        /// 倒带进度曲线：smoothstep 主干混三重脉冲波。旧版纯脉冲在段界导数归零，
        /// 实体"停一下再猛冲"读作跳切；混入主干后波谷仍保有均速近五成、
        /// 两端缓起缓落，整体连续单调，胶片回卷的呼吸感而非顿挫
        /// </summary>
        private static float RewindEase(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            float spine = x * x * (3f - 2f * x);
            float seg = x * RewindPulses;
            int index = Math.Min((int)seg, RewindPulses - 1);
            float f = seg - index;
            float pulses = (index + f * f * (3f - 2f * f)) / RewindPulses;
            return 0.35f * spine + 0.65f * pulses;
        }

        private static void UpdateLocalPlayer(ResetShow show, float age) {
            Player player = Main.LocalPlayer;
            if (player?.active != true || player.dead
                || !show.Players.Contains(player.whoAmI)) {
                return;
            }

            //全程无敌逐帧顶住，落定后由结算的缓冲接手
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, 2);

            if (KikasaResetHistory.TrySamplePlayer(player.whoAmI, age, out Vector2 position)) {
                player.position = position;
                player.velocity = Vector2.Zero;
            }
            //倒放的竖向大位移不算坠落
            player.fallStart = (int)(player.position.Y / 16f);
        }

        /// <summary>结算第一拍：清全部 debuff、顶上落定无敌缓冲；回满见 <see cref="ApplyLocalHeal"/></summary>
        internal static void ApplyLocalCleanse(Player player) {
            if (player?.active != true || player.dead || Main.dedServ) {
                return;
            }
            for (int i = 0; i < Player.MaxBuffs; i++) {
                int buffType = player.buffType[i];
                if (buffType > 0 && Main.debuff[buffType]) {
                    player.DelBuff(i);
                    i--;
                }
            }
            //海妖八音盒的必死是玩家字段不是 debuff，清 buff 洗不掉——与比目鱼重启同款收口（反馈 #7）
            if (player.TryGetModPlayer<Items.Tools.SirenMusicalBoxPlayer>(out var sirenPlayer)
                && sirenPlayer.IsCursed) {
                Items.Tools.SirenMusicalBoxPlayer.StopAllMusicBoxes(player);
            }
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, PostImmuneFrames);
        }

        /// <summary>
        /// 结算第二拍：回满生命法力并上报，仍在落定白闪峰值内。
        /// 晚清 debuff 两帧，statLifeMax2 已摆脱削上限效果，回满才真是满
        /// </summary>
        internal static void ApplyLocalHeal(Player player) {
            if (player?.active != true || player.dead || Main.dedServ) {
                return;
            }
            int healed = player.statLifeMax2 - player.statLife;
            player.statLife = player.statLifeMax2;
            player.statMana = player.statManaMax2;
            if (healed > 0) {
                player.HealEffect(healed, true);
            }
            //非 SSC 下服务器写不动客户端血量，本机写完自己上报
            if (Main.netMode == NetmodeID.MultiplayerClient
                && player.whoAmI == Main.myPlayer) {
                NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, player.whoAmI);
                NetMessage.SendData(MessageID.PlayerMana, -1, -1, null, player.whoAmI);
            }
        }

        private static void FinishShow(ResetShow show) {
            //借雨兜底：正常路径已在弹回帧还原
            RevertBorrowOnEnd(show);
            //放行：以回溯终点当刻的历史速度续走，时间接回过去，动量也接回过去
            foreach (int index in heldNpcIndices) {
                Vector2? resume = KikasaResetHistory.TryNpcVelocityAt(index,
                    RewindWindowFrames, out Vector2 velocity) ? velocity : null;
                ReleaseNpcLease(index, resume);
            }
            //兜底扫尾：正常路径上面已清空，防身份漂移留下滞留租约
            ReleaseAllNpcLeases();
            //落定：服务器把每个受影响 NPC 的终位立即推正，别等下一次自然同步
            if (Main.netMode == NetmodeID.Server) {
                foreach (int index in heldNpcIndices) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
                }
            }
            Active = null;
            heldNpcIndices.Clear();
            RewindPulseRate = 0f;
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                cooldowns[show.OwnerWho] = CooldownFrames;
            }
            if (show.OwnerWho == Main.myPlayer) {
                localLockUntil = Main.GameUpdateCount + CooldownFrames;
            }
            //实体已跳回过去，旧轨迹作废、重新积累
            KikasaResetHistory.Clear();
        }

        /// <summary>
        /// 逐帧重解受影响 NPC：generation 未同步到本端时按 index+type 松解析兜底（演出层，错抓代价小），
        /// 两者都失败视为已死亡/消失，移出集合并退掉冻结租约
        /// </summary>
        private static void RefreshHeldNpcs(ResetShow show) {
            droppedNpcBuffer.Clear();
            droppedNpcBuffer.UnionWith(heldNpcIndices);
            heldNpcIndices.Clear();
            for (int i = show.Npcs.Count - 1; i >= 0; i--) {
                NetworkNPCIdentity id = show.Npcs[i];
                if (id.TryResolve(out NPC npc)) {
                    heldNpcIndices.Add(npc.whoAmI);
                    continue;
                }
                if (id.Index >= 0 && id.Index < Main.maxNPCs) {
                    NPC fallback = Main.npc[id.Index];
                    if (fallback?.active == true && fallback.type == id.Type) {
                        heldNpcIndices.Add(id.Index);
                        continue;
                    }
                }
                show.Npcs.RemoveAt(i);
            }
            //掉出名单（死亡/消失/身份失效）的槽位立即退租，冻结不得外溢滞留
            droppedNpcBuffer.ExceptWith(heldNpcIndices);
            foreach (int index in droppedNpcBuffer) {
                ReleaseNpcLease(index, null);
            }
        }

        /// <summary>
        /// 敌方弹幕随世界一同定格：照片里的子弹不该继续飞，倒带时更不该逆着时间前进。
        /// 计时续租（每帧刷新），演出收场后自然到期解冻，以冻结前的动量续飞
        /// </summary>
        private static void FreezeHostileProjectiles(Player owner) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active != true || !proj.hostile) {
                    continue;
                }
                if (CWRLoad.ProjValue.ImmuneFrozen.TryGetValue(proj.type, out bool immune)
                    && immune) {
                    continue;
                }
                if (Vector2.Distance(proj.Center, owner.Center) > ResetRange + 600f) {
                    continue;
                }
                TimeFreezeSystem.RefreshProjectile<RewindFreeze>(proj, 2);
            }
        }

        /// <summary>退掉单个槽位的冻结租约；releaseVelocity=null 时恢复冻结前的速度快照</summary>
        private static void ReleaseNpcLease(int index, Vector2? releaseVelocity) {
            if (index < 0 || index >= npcLeases.Length || !npcLeases[index].IsValid) {
                return;
            }
            TimeFreezeSystem.ReleaseNPC(Main.npc[index], npcLeases[index], releaseVelocity);
            npcLeases[index] = default;
        }

        private static void ReleaseAllNpcLeases() {
            for (int i = 0; i < npcLeases.Length; i++) {
                ReleaseNpcLease(i, null);
            }
        }

        internal static void Reset() {
            Active = null;
            heldNpcIndices.Clear();
            droppedNpcBuffer.Clear();
            groupBuffer.Clear();
            seenNpcBuffer.Clear();
            //世界卸载路径不调 Release：实体表在拆，TimeFreezes 的 ResetSession 自会清场
            Array.Clear(npcLeases);
            RewindPulseRate = 0f;
            for (int i = 0; i < cooldowns.Length; i++) {
                cooldowns[i] = 0;
            }
            localLockUntil = 0;
        }
    }
}
