using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 鬼伞·大范围重启权威核心。鬼雨形态下把最近数秒"冲洗"回去：
    /// 屏幕定格成黑白照片、被雨痕刷掉，场内 NPC 与玩家沿位置历史倒退，
    /// 结算分两拍：先清 debuff，隔两帧回满生命法力，全程无敌。
    /// 契约：服务器只验冷却/在场，不验领域——服务器没有领域状态，形态由客户端预检；
    /// NPC 倒放两端各按本端历史推演，服务器周期 netUpdate 与落定 SyncNPC 兜住终位；
    /// 玩家生命/清 buff 一律归本机结算，服务器不碰（CyberRestart 同款）。
    /// 全屏演出全局同刻只放一场。
    /// </summary>
    internal static class KikasaReset
    {
        //==================== 时间轴 ====================

        /// <summary>定格段末帧：快门白闪，世界钉住成照片</summary>
        public const int SnapshotEnd = 24;

        /// <summary>冲刷段末帧：雨痕自上而下把照片刷掉</summary>
        public const int WashEnd = 70;

        /// <summary>倒带段末帧=结算帧：恢复自此分两拍兑现</summary>
        public const int RewindEnd = 160;

        /// <summary>落定收尾</summary>
        public const int TotalFrames = 176;

        /// <summary>倒放窗口：回到 5 秒前</summary>
        public const int RewindWindowFrames = 300;

        /// <summary>作用半径，约一屏半</summary>
        public const float ResetRange = 2400f;

        /// <summary>完成后冷却</summary>
        public const int CooldownFrames = 60 * 60;

        /// <summary>落定后的无敌缓冲</summary>
        public const int PostImmuneFrames = 60;

        /// <summary>回满相对清 debuff 的延迟帧：等削上限的效果退场、statLifeMax2 恢复后再兑现满血</summary>
        private const int HealDelayFrames = 2;

        /// <summary>倒带的脉冲拍数：胶片回卷的顿挫感</summary>
        private const int RewindPulses = 3;

        /// <summary>单帧历史深度增量的峰值：段内 smoothstep 峰值导数为均值的 1.5 倍</summary>
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

        //本帧已解析的受影响 NPC 槽位，GlobalNPC 的 PreAI 按此拦截
        private static readonly HashSet<int> heldNpcIndices = [];
        private static readonly List<NPC> groupBuffer = [];
        private static readonly HashSet<int> seenNpcBuffer = [];

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

        //==================== 客户端入口 ====================

        /// <summary>
        /// 按键受理：鬼雨形态稳态预检（服务器没有领域状态，这里是唯一的形态门）、
        /// 本机乐观锁，然后单机直通/联机上行请求
        /// </summary>
        internal static void TryReset(Player player) {
            if (player == null || player.whoAmI != Main.myPlayer) {
                return;
            }
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            if (domain.Phase != KikasaDomainPhase.Open || !domain.IsRainForm
                || domain.RiseT < 0.999f) {
                Refuse(player, KikasaResetSystem.NeedRainForm);
                return;
            }
            if (Active != null) {
                Refuse(player, KikasaResetSystem.ResetBusy);
                return;
            }
            if (Main.GameUpdateCount < localLockUntil) {
                Refuse(player, KikasaResetSystem.ResetCooling);
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

        private static void Refuse(Player player, LocalizedText text) {
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2
            }, player.Center);
            if (Main.netMode != NetmodeID.Server && text != null) {
                CombatText.NewText(player.Hitbox, new Color(160, 178, 184), text.Value);
            }
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
            if (Active != null) {
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
            OnShowStarted(show);
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
                    Volume = 0.9f, Pitch = -0.1f
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
            Active = null;
            heldNpcIndices.Clear();
            RewindPulseRate = 0f;
            //实体已被部分倒放，旧轨迹同样作废
            KikasaResetHistory.Clear();
        }

        //==================== 每帧推进 ====================

        /// <summary>由 <see cref="KikasaResetSystem"/> 两端逐帧驱动</summary>
        internal static void Update() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < cooldowns.Length; i++) {
                    if (cooldowns[i] > 0) {
                        cooldowns[i]--;
                    }
                }
            }
            UpdateShow();
        }

        private static void UpdateShow() {
            ResetShow show = Active;
            if (show == null) {
                return;
            }

            //施术者掉线整场收场；死亡不收——无敌顶着，且其余人还等着被倒放回血
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
                if (KikasaResetHistory.TrySampleNpc(index, age, out Vector2 position)) {
                    npc.position = position;
                }
                npc.velocity = Vector2.Zero;
                if (pushNet) {
                    npc.netUpdate = true;
                }
            }

            if (!Main.dedServ) {
                UpdateLocalPlayer(show, age);
            }

            //结算拆两拍归本机；服务器只负责把 NPC 终位推正。
            //第一拍清 debuff+落定无敌——本帧 statLifeMax2 仍带着削上限效果算出的值，
            //回满放到第二拍，等上限恢复后兑现，免得清完 debuff 血却钉在低位
            if (!show.RestoreFired && show.Timer >= RewindEnd) {
                show.RestoreFired = true;
                if (!Main.dedServ) {
                    if (show.Players.Contains(Main.myPlayer)) {
                        ApplyLocalCleanse(Main.LocalPlayer);
                    }
                    if (LocallyViewed) {
                        SoundEngine.PlaySound(SoundID.Splash with {
                            Volume = 0.8f, Pitch = 0.35f
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
        /// 时间轴 t 上实体该处的历史深度：定格与冲刷段钉在触发帧（记录已暂停，age=0 即触发帧），
        /// 倒带段按三段脉冲回卷推到 <see cref="RewindWindowFrames"/>
        /// </summary>
        internal static float AgeAt(int timer) {
            if (timer <= WashEnd) {
                return 0f;
            }
            float x = MathHelper.Clamp(
                (timer - WashEnd) / (float)(RewindEnd - WashEnd), 0f, 1f);
            return RewindEase(x) * RewindWindowFrames;
        }

        /// <summary>三段回卷：每段内 smoothstep、段界一阶导归零，整体连续单调的顿挫感</summary>
        private static float RewindEase(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            float seg = x * RewindPulses;
            int index = Math.Min((int)seg, RewindPulses - 1);
            float f = seg - index;
            float eased = f * f * (3f - 2f * f);
            return (index + eased) / RewindPulses;
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
        /// 两者都失败视为已死亡/消失，移出集合
        /// </summary>
        private static void RefreshHeldNpcs(ResetShow show) {
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
        }

        internal static void Reset() {
            Active = null;
            heldNpcIndices.Clear();
            groupBuffer.Clear();
            seenNpcBuffer.Clear();
            RewindPulseRate = 0f;
            for (int i = 0; i < cooldowns.Length; i++) {
                cooldowns[i] = 0;
            }
            localLockUntil = 0;
        }
    }
}
