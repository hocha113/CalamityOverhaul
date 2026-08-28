using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills.Restarts
{
    /// <summary>
    /// 比目鱼·大范围重启权威核心（七眼起 <see cref="RestartFish.AltUse"/> 分流至此）。
    /// 潮水自屏底涌起吞没世界，作用圈内实体沿运动历史退回数秒前，
    /// 结算分两拍：先清 buff，隔两帧回满生命法力，全程无敌，随后潮水退去。
    /// 领域未开时不需要任何借还——潮水本身就是海，演出自含。
    /// 契约与鬼伞同源：服务器只验冷却/在场，不验领域层数（服务器没有领域状态，
    /// 档位门由客户端预检）；作用半径由客户端按层数推算随请求上行，服务器仅做钳制；
    /// NPC 倒放两端各按本端历史推演，服务器周期 netUpdate 与落定 SyncNPC 兜住终位；
    /// NPC 冻结走 TimeFreezes 租约；玩家生命/清 buff 一律归本机结算。
    /// 历史缓冲直接共享 <see cref="KikasaResetHistory"/>（双端无条件环形记录），
    /// 与鬼伞的大范围重启全局同刻只放一场，双向互斥预检。
    /// </summary>
    internal static class HalibutReset
    {
        //==================== 时间轴 ====================

        /// <summary>解锁大范围重启所需的领域层数</summary>
        public const int UnlockLayers = 7;

        /// <summary>起势段末帧：鱼汛与潮线在屏缘隆起</summary>
        public const int GatherEnd = 30;

        /// <summary>吞没段末帧：潮水漫顶、世界完全没入水下，倒带自此起跑</summary>
        public const int FloodEnd = 90;

        /// <summary>倒带段末帧=结算帧：恢复自此分两拍兑现</summary>
        public const int RewindEnd = 230;

        /// <summary>退潮收尾</summary>
        public const int TotalFrames = 280;

        /// <summary>倒放窗口：回到 10 秒前</summary>
        public const int RewindWindowFrames = 600;

        /// <summary>落定后的无敌缓冲</summary>
        public const int PostImmuneFrames = 60;

        /// <summary>回满相对清 buff 的延迟帧：等削上限的效果退场、statLifeMax2 恢复后再兑现满血</summary>
        private const int HealDelayFrames = 2;

        /// <summary>倒带的脉冲波数：潮汐回卷的呼吸感来源（与鬼伞同曲线）</summary>
        private const int RewindPulses = 3;

        /// <summary>作用半径钳制上界：十层领域半径再留一点余量，防伪造请求越界</summary>
        private const float MaxRangeClamp = 2200f;

        /// <summary>单帧历史深度增量的峰值：主干与脉冲波在中点同峰，导数为均值的 1.5 倍</summary>
        private const float PeakRewindSpeed
            = RewindWindowFrames / (float)(RewindEnd - FloodEnd) * 1.5f;

        //==================== 运行时 ====================

        internal sealed class ResetShow
        {
            public int OwnerWho;
            public int ResetId;
            public float Seed;
            /// <summary>作用半径：领域层数推算，随 Apply 包分发保证各端一致</summary>
            public float Range;
            public int Timer;
            public bool RestoreFired;
            public bool HealFired;
            /// <summary>受影响 NPC 身份（Apply 时刻权威圈定）</summary>
            public readonly List<NetworkNPCIdentity> Npcs = [];
            /// <summary>受影响玩家 whoAmI</summary>
            public readonly List<int> Players = [];
        }

        /// <summary>当前进行中的重启；与鬼伞的重启合计全局同刻只一场</summary>
        internal static ResetShow Active { get; private set; }

        //权威冷却（服务器/单机），客户端另有 RestartFishCooldown 与乐观锁
        private static readonly int[] cooldowns = new int[Main.maxPlayers];
        private static int nextResetId;

        //本机所有者的乐观锁：请求在途/演出进行，真限频在权威端与 RestartFishCooldown
        private static uint localLockUntil;

        //本帧已解析的受影响 NPC 槽位，GlobalNPC 的 PreAI 按此兜底拦截
        private static readonly HashSet<int> heldNpcIndices = [];
        private static readonly List<NPC> groupBuffer = [];
        private static readonly HashSet<int> seenNpcBuffer = [];
        private static readonly HashSet<int> droppedNpcBuffer = [];

        //冻结租约（本端局部状态）：走 TimeFreezes 统一 AI 入口，索引=NPC 槽位
        private static readonly TimeFreezeLease[] npcLeases
            = new TimeFreezeLease[Main.maxNPCs];

        //世界层鱼汛装饰（纯本机）：潮水里的鱼自作用圈边界洄游向施术者
        private static readonly List<RestartFishBoid> garnishFish = [];

        /// <summary>TimeFreezes 的冻结来源标记（HalibutReset 是静态类，当不了泛型实参）</summary>
        private sealed class TideRewindFreeze { }

        //==================== 状态查询 ====================

        /// <summary>演出进行中历史记录暂停：最新样本锚定在触发帧（由 KikasaResetHistory 消费）</summary>
        internal static bool HistoryPaused => Active != null;

        /// <summary>倒带段进行中（本端视角）</summary>
        internal static bool RewindActive
            => Active != null && Active.Timer > FloodEnd && Active.Timer <= RewindEnd;

        /// <summary>当帧回卷速率 0~1：AgeAt 帧间差按脉冲峰值归一，焦散涌动随这个节拍呼吸</summary>
        internal static float RewindPulseRate { get; private set; }

        /// <summary>该 NPC 是否被本场重启持有（AI 暂停、位置由倒放接管）</summary>
        internal static bool IsNpcHeld(int npcIndex) => heldNpcIndices.Contains(npcIndex);

        /// <summary>该玩家是否在本场重启的波及名单里</summary>
        internal static bool IsPlayerAffected(int who)
            => Active != null && Active.Players.Contains(who);

        /// <summary>
        /// 本机是否看这场演出：被波及玩家必看，旁观按与施术者的距离；
        /// 全屏潮汐/水下调色只给看得见的端
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
                    && Vector2.Distance(owner.Center, local.Center) <= show.Range + 1200f;
            }
        }

        //==================== 客户端入口 ====================

        /// <summary>
        /// 本机状态预检：档位门（层数 >= 7）由调用方 <see cref="RestartFish.AltUse"/> 把关；
        /// 这里只拒"全局已有重启演出"——本家与鬼伞家共用一个天下
        /// </summary>
        internal static bool CanStartLocal(Player player) {
            if (player?.active != true || player.dead) {
                return false;
            }
            return Active == null && KikasaReset.Active == null;
        }

        /// <summary>
        /// 按键受理（仅施术者本机）：作用半径按当前层数推算随请求上行；
        /// 冷却由调用方的 RestartFishCooldown 把过一道，这里再加乐观锁防连点
        /// </summary>
        internal static void TryReset(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!CanStartLocal(player)) {
                Refuse(player);
                return;
            }
            if (Main.GameUpdateCount < localLockUntil) {
                Refuse(player);
                return;
            }
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            float range = SeaDomain.MaxRadiusForLayers(halibutPlayer.SeaDomainLayers);

            //请求在途短锁防连点，真限频在权威端
            localLockUntil = Main.GameUpdateCount + 60;

            if (Main.netMode == NetmodeID.MultiplayerClient) {
                HalibutResetNet.SendRequest(range);
            }
            else {
                StartAuthoritative(player, range);
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

        /// <summary>服务器收到请求：来源以连接为准，半径只做钳制不做推算（服务器没有领域状态）</summary>
        internal static void HandleRequest(int ownerWho, float range) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            Player owner = ownerWho >= 0 && ownerWho < Main.maxPlayers
                ? Main.player[ownerWho] : null;
            if (owner?.active != true) {
                Reject(ownerWho, "owner-invalid");
                return;
            }
            StartAuthoritative(owner, range);
        }

        /// <summary>共同权威路径：单机直通与服务器请求都走这里</summary>
        internal static bool StartAuthoritative(Player owner, float range) {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            if (owner?.active != true || owner.dead) {
                Reject(owner?.whoAmI ?? -1, "owner-dead");
                return false;
            }
            int ownerWho = owner.whoAmI;
            if (Active != null || KikasaReset.Active != null) {
                Reject(ownerWho, "show-busy");
                return false;
            }
            if (cooldowns[ownerWho] > 0) {
                Reject(ownerWho, "cooldown");
                return false;
            }
            if (!float.IsFinite(range)) {
                Reject(ownerWho, "range-invalid");
                return false;
            }
            range = MathHelper.Clamp(range, 220f, MaxRangeClamp);

            ResetShow show = new() {
                OwnerWho = ownerWho,
                ResetId = ++nextResetId,
                Seed = Main.rand.NextFloat(1000f),
                Range = range,
            };
            CollectNpcs(owner, range, show.Npcs);
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true && !player.dead
                    && Vector2.Distance(player.Center, owner.Center) <= range) {
                    show.Players.Add(i);
                }
            }

            Active = show;
            //补一帧触发时刻的样本：age=0 钉在触发帧，快移实体定格不回弹
            KikasaResetHistory.ForceSample();
            //触发当帧就上冻结租约，不给 AI 留最后一帧空跑
            HoldAffectedNpcs(show);
            if (Main.netMode == NetmodeID.Server) {
                HalibutResetNet.SendApply(show);
            }
            else {
                //单机：权威与演出同机同帧
                OnShowStarted(show);
            }
            return true;
        }

        /// <summary>圈定半径内的活跃 NPC；蠕虫等整组同倒，免得半截被拖回半截照打</summary>
        private static void CollectNpcs(Player owner, float range, List<NetworkNPCIdentity> output) {
            seenNpcBuffer.Clear();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.lifeMax <= 0
                    || seenNpcBuffer.Contains(npc.whoAmI)
                    || Vector2.Distance(npc.Center, owner.Center) > range) {
                    continue;
                }
                if (IsSplitWormExcluded(npc)) {
                    //分裂型蠕虫不倒带：体节槽位随分裂/重链复用，环形历史与现节错位（鬼伞同裁定）
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
            CWRMod.Instance?.Logger?.Info($"[HalibutReset] reject owner={ownerWho} clause={clause}");
        }

        //==================== 演出入口（客户端收 Apply / 单机直通） ====================

        /// <summary>客户端按 Apply 包起演出；时间轴与权威端同构</summary>
        internal static void StartShow(int ownerWho, int resetId, float seed, float range,
            List<NetworkNPCIdentity> npcs, List<int> players) {
            ResetShow show = new() {
                OwnerWho = ownerWho,
                ResetId = resetId,
                Seed = seed,
                Range = MathHelper.Clamp(range, 220f, MaxRangeClamp),
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
                npcLeases[index] = TimeFreezeSystem.AcquireNPC<TideRewindFreeze>(npc,
                    npc.Center, index, TimeFreezeAnchorPriority.Authoritative);
            }
        }

        private static void OnShowStarted(ResetShow show) {
            if (Main.dedServ) {
                return;
            }
            Player owner = Main.player[show.OwnerWho];
            //起势拍：深水破涌+远雷，与领域开启同语系
            if (owner?.active == true) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 1f,
                    Pitch = -0.6f
                }, owner.Center);
                SoundEngine.PlaySound(SoundID.Thunder with {
                    Volume = 0.45f,
                    Pitch = -0.7f,
                    MaxInstances = 1
                }, owner.Center);
            }
            //起势鱼汛：首波自作用圈边界涌入
            if (owner?.active == true && LocallyViewed) {
                SpawnGarnishWave(owner, show.Range, 46);
            }
            //施术者本机：冷却在演出确立时就挂上（HUD 卫星立刻显示读条），运镜失败不致命
            if (show.OwnerWho == Main.myPlayer) {
                if (owner.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    halibutPlayer.RestartFishCooldown = RestartFish.RestartCooldown;
                }
                CutsceneDirector.Play<HalibutResetCutscene>(Main.LocalPlayer);
            }
        }

        /// <summary>补一波洄游鱼：出生点压在作用圈边缘，圈出机制半径</summary>
        private static void SpawnGarnishWave(Player owner, float range, int count) {
            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawn = owner.Center + angle.ToRotationVector2()
                    * (range * Main.rand.NextFloat(0.9f, 1.12f));
                garnishFish.Add(new RestartFishBoid(spawn, owner.Center));
            }
        }

        /// <summary>由 <see cref="HalibutResetRender"/> 在实体层末尾调用；自开自收批次</summary>
        internal static void DrawGarnish(SpriteBatch spriteBatch) {
            if (garnishFish.Count == 0) {
                return;
            }
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            foreach (RestartFishBoid fish in garnishFish) {
                fish.DrawTrail(1f);
            }
            foreach (RestartFishBoid fish in garnishFish) {
                fish.Draw(1f);
            }
            spriteBatch.End();
        }

        /// <summary>客户端收 Cancel（施术者掉线等）：立即收场，不做恢复</summary>
        internal static void HandleCancel(int resetId) {
            if (Active?.ResetId == resetId) {
                AbortShow();
            }
        }

        private static void AbortShow() {
            //中断放行不带历史动量：恢复各自冻结前的速度快照即可
            ReleaseAllNpcLeases();
            Active = null;
            heldNpcIndices.Clear();
            garnishFish.Clear();
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

        /// <summary>由 <see cref="HalibutResetSystem"/> 两端逐帧驱动</summary>
        internal static void Update() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < cooldowns.Length; i++) {
                    if (cooldowns[i] <= 0) {
                        continue;
                    }
                    //死亡即重启失败的硬着陆：客户端死亡会清 RestartFishCooldown，
                    //权威冷却同步跟掉，两端语义一致（重启只对活人是奢侈品）
                    if (Main.player[i]?.active == true && Main.player[i].dead) {
                        cooldowns[i] = 0;
                        continue;
                    }
                    cooldowns[i]--;
                }
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
                    HalibutResetNet.SendCancel(show.ResetId);
                    cooldowns[show.OwnerWho] = RestartFish.RestartCooldown / 2;
                }
                Reject(show.OwnerWho, "cancel:owner-lost");
                AbortShow();
                return;
            }

            show.Timer++;
            RefreshHeldNpcs(show);

            float age = AgeAt(show.Timer);
            RewindPulseRate = MathHelper.Clamp(
                (age - AgeAt(show.Timer - 1)) / PeakRewindSpeed, 0f, 1f);
            bool pushNet = Main.netMode == NetmodeID.Server && show.Timer % 10 == 0;
            foreach (int index in heldNpcIndices) {
                NPC npc = Main.npc[index];
                bool sampled = KikasaResetHistory.TrySampleNpc(index, age,
                    out Vector2 position, out float rotation,
                    out int direction, out int spriteDirection);
                //逐帧续租并把冻结锚点推向历史位置：AI 停摆与位置持有都交给
                //TimeFreezes 统一入口，租约失效（身份重置等）也会在此自愈重挂
                Vector2? anchor = sampled ? position + npc.Size * 0.5f : null;
                npcLeases[index] = TimeFreezeSystem.AcquireNPC<TideRewindFreeze>(npc,
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
            FreezeHostileProjectiles(owner, show.Range);

            if (!Main.dedServ) {
                UpdateLocalPlayer(show, age);
                UpdateShowFX(show, owner);
            }

            //结算拆两拍归本机；服务器只负责把 NPC 终位推正。
            //第一拍清 buff+落定无敌，本帧 statLifeMax2 仍带着削上限效果算出的值，
            //回满放到第二拍，等上限恢复后兑现，免得清完 buff 血却钉在低位
            if (!show.RestoreFired && show.Timer >= RewindEnd) {
                show.RestoreFired = true;
                if (!Main.dedServ) {
                    if (show.Players.Contains(Main.myPlayer)) {
                        ApplyLocalCleanse(Main.LocalPlayer, Main.myPlayer == show.OwnerWho);
                    }
                    //施术者的重启停机：交互锁定按死机等级伸缩，与重启自身同一口径
                    if (show.OwnerWho == Main.myPlayer
                        && owner.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                        halibutPlayer.IsInteractionLockedTime = (int)(60 *
                            ((10 - MathHelper.Clamp(halibutPlayer.CrashesLevel() - 5, 0, 10)) * 3));
                    }
                    if (LocallyViewed) {
                        SoundEngine.PlaySound(SoundID.Item4 with {
                            Volume = 0.85f
                        }, owner.Center);
                        SoundEngine.PlaySound(SoundID.Item29 with {
                            Volume = 0.9f,
                            Pitch = -0.2f
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

        /// <summary>
        /// 时间轴 t 上实体该处的历史深度：起势与吞没段钉在触发帧（记录已暂停，age=0 即触发帧），
        /// 倒带段沿呼吸曲线回卷推到 <see cref="RewindWindowFrames"/>
        /// </summary>
        internal static float AgeAt(int timer) {
            if (timer <= FloodEnd) {
                return 0f;
            }
            float x = MathHelper.Clamp(
                (timer - FloodEnd) / (float)(RewindEnd - FloodEnd), 0f, 1f);
            return RewindEase(x) * RewindWindowFrames;
        }

        /// <summary>
        /// 倒带进度曲线：smoothstep 主干混三重脉冲波（鬼伞同款）。
        /// 波谷仍保有均速近五成、两端缓起缓落，整体连续单调，潮汐回卷的呼吸感而非顿挫
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

        /// <summary>世界内的伴随演出：鱼汛洄游、吞没段向心水流、倒带段气泡逆飞（纯本机）</summary>
        private static void UpdateShowFX(ResetShow show, Player owner) {
            //鱼汛更新不吃视距门：出屏的鱼也要继续游完寿命，免得回看时凭空消失
            for (int i = garnishFish.Count - 1; i >= 0; i--) {
                garnishFish[i].Update(owner.Center);
                if (garnishFish[i].ShouldRemove()) {
                    garnishFish.RemoveAt(i);
                }
            }

            if (!LocallyViewed) {
                return;
            }

            //起势与吞没段持续补浪：鱼一波波从圈外赶来
            if (show.Timer <= FloodEnd && show.Timer % 14 == 0) {
                SpawnGarnishWave(owner, show.Range, 8);
            }
            //倒带段留一缕慢鱼陪着倒流的世界
            else if (RewindActive && show.Timer % 24 == 0) {
                SpawnGarnishWave(owner, show.Range * 0.8f, 3);
            }

            //吞没段：作用圈边缘的水尘向心涌入，圈出机制半径
            if (show.Timer > GatherEnd && show.Timer <= FloodEnd) {
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = owner.Center + angle.ToRotationVector2()
                        * Main.rand.NextFloat(show.Range * 0.75f, show.Range);
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Water,
                        (owner.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 11f),
                        90, new Color(100, 200, 255), 1.6f);
                    dust.noGravity = true;
                }
            }

            //吞没完成拍：深海回响
            if (show.Timer == FloodEnd) {
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.6f,
                    Pitch = -0.8f,
                    MaxInstances = 1
                }, owner.Center);
            }

            //倒带段：水下气泡随回卷脉冲逆着时间上涌
            if (RewindActive && Main.rand.NextFloat() < 0.35f + RewindPulseRate * 0.5f) {
                Vector2 pos = owner.Center + Main.rand.NextVector2Circular(show.Range * 0.8f, show.Range * 0.6f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.BreatheBubble,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(2f, 5f) * (0.4f + RewindPulseRate)),
                    120, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
            }

            //退潮起点：潮水松开世界的那一声
            if (show.Timer == RewindEnd + HealDelayFrames + 4) {
                SoundEngine.PlaySound(SoundID.Splash with {
                    Volume = 0.8f,
                    Pitch = 0.35f
                }, owner.Center);
            }
        }

        /// <summary>结算第一拍：清 buff、顶上落定无敌缓冲；回满见 <see cref="ApplyLocalHeal"/>。
        /// 施术者按重启自身的口径清全部 buff（增益一并重启），旁人只洗减益不动增益</summary>
        internal static void ApplyLocalCleanse(Player player, bool fullRestart) {
            if (player?.active != true || player.dead || Main.dedServ) {
                return;
            }
            if (fullRestart) {
                RestartFish.ClearAllBuffs(player);
            }
            else {
                for (int i = 0; i < Player.MaxBuffs; i++) {
                    int buffType = player.buffType[i];
                    if (buffType > 0 && Main.debuff[buffType]) {
                        player.DelBuff(i);
                        i--;
                    }
                }
            }
            //深渊侧状态一并重启：复苏归零、被诅咒的海妖音乐盒闭嘴
            player.SetResurrectionValue(0);
            if (player.TryGetModPlayer<Items.Tools.SirenMusicalBoxPlayer>(out var sirenPlayer)
                && sirenPlayer.IsCursed) {
                Items.Tools.SirenMusicalBoxPlayer.StopAllMusicBoxes(player);
            }
            player.immune = true;
            player.immuneTime = Math.Max(player.immuneTime, PostImmuneFrames);
        }

        /// <summary>
        /// 结算第二拍：回满生命法力并上报，仍在落定白闪峰值内。
        /// 晚清 buff 两帧，statLifeMax2 已摆脱削上限效果，回满才真是满
        /// </summary>
        internal static void ApplyLocalHeal(Player player) {
            if (player?.active != true || player.dead || Main.dedServ) {
                return;
            }
            //调整最大生命值，避免削生命上限的效果影响重启（与重启自身同一道保险）
            if (player.TryGetHalibutPlayer(out var halibutPlayer)) {
                player.statLifeMax2 = (int)MathHelper.Clamp(
                    player.statLifeMax2, halibutPlayer.PlayerLifeMax, int.MaxValue - 1);
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
            garnishFish.Clear();
            RewindPulseRate = 0f;
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                cooldowns[show.OwnerWho] = RestartFish.RestartCooldown;
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
        /// 敌方弹幕随潮水一同定格：水下的子弹不该继续飞，倒带时更不该逆着时间前进。
        /// 计时续租（每帧刷新），演出收场后自然到期解冻，以冻结前的动量续飞
        /// </summary>
        private static void FreezeHostileProjectiles(Player owner, float range) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active != true || !proj.hostile) {
                    continue;
                }
                if (CWRLoad.ProjValue.ImmuneFrozen.TryGetValue(proj.type, out bool immune)
                    && immune) {
                    continue;
                }
                if (Vector2.Distance(proj.Center, owner.Center) > range + 600f) {
                    continue;
                }
                TimeFreezeSystem.RefreshProjectile<TideRewindFreeze>(proj, 2);
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
            garnishFish.Clear();
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
