using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 厉鬼系统的每玩家状态层。<br/>
    /// 侵蚀（身层代价，鬼律第十二条）：借力即涨、静息缓退，阈值分级演出，随玩家落档；<br/>
    /// 预警拍（omen，鬼律第十条"有预警"）：倒计时与死亡判定在权威侧（本类权威分支），
    /// 受害者本端只持演出镜像（心跳渐急，视觉在 <see cref="WraithOmenRender"/>），入口见 <see cref="WraithLethality"/>；<br/>
    /// 借力键：死机仪式优先（服务器确认制事务，见 <see cref="WraithRites"/>），其次施放共鸣之力；<br/>
    /// 反噬判定：躁动之鬼按 owner 端数据掷签挣脱（owner 自治掷签，服务器复核资格后落实生成）。
    /// 所有字段实例级，绝无 static 每玩家状态
    /// </summary>
    internal class WraithPlayer : ModPlayer
    {
        //====侵蚀调参====
        /// <summary>侵蚀自然消退速率（约 4 分钟满值归零）</summary>
        private const float ErosionDecayPerTick = 1f / (60f * 240f);
        /// <summary>最后一次上涨后的消退延迟（帧）</summary>
        private const int ErosionDecayDelay = 60 * 6;
        /// <summary>阈值分级：一阶(低语)/二阶(尸斑)/三阶(临界)</summary>
        public const float TierCrawl = 0.35f, TierStain = 0.70f, TierMirror = 0.95f;

        //====反噬调参====
        /// <summary>判定间隔（帧），1Hz 掷签；同键冷却在 <see cref="WraithBacklash.KeyCooldownTicks"/></summary>
        private const int BacklashCheckInterval = 60;

        private const string SaveKey_Erosion = "CWRWraith_Erosion";

        //====状态（全部实例级）====
        private float erosion;
        private int erosionIdleTimer;
        private int lastCueTier;
        private int backlashCheckTimer;
        //赋力冷却与反噬冷却:键=定义 Key
        private readonly Dictionary<string, int> abilityCooldowns = [];
        private readonly Dictionary<string, long> backlashCooldownUntil = [];
        //上一秒仍在场的挣脱体名单,用于本地判读"散了还是被收伏了"
        private readonly HashSet<string> escapedWatch = [];
        //挣脱确认挂起:键→剩余观测次数(1Hz)。多人下请求已发但挣脱体未现身,
        //首次真实观测到才播报+落冷却;窗口内一直没来=服务器拒了,无声作废
        private readonly Dictionary<string, int> escapedPending = [];
        //键遍历临时缓冲(纯临时量,非每玩家状态;owner 端单线程串行使用)
        private static readonly List<string> keyScratch = [];

        //====预警拍:权威侧（倒计时与死亡判定,服务器或单人）====
        private int omenAuthTicksLeft;
        private WraithDefinition omenAuthDefinition;
        private LocalizedText omenAuthReason;

        //====预警拍:演出镜像（受害者本端心跳与收黑）====
        private int omenTicksLeft;
        private int omenDuration;
        private WraithDefinition omenDefinition;
        private int omenBeatTimer;

        /// <summary>侵蚀值 0~1（身层代价累计）</summary>
        public float Erosion => erosion;

        /// <summary>侵蚀阶级 0~3，视觉与反噬概率的公共读数</summary>
        public int ErosionTier => erosion >= TierMirror ? 3 : erosion >= TierStain ? 2 : erosion >= TierCrawl ? 1 : 0;

        /// <summary>预警拍演出进行中（本端镜像）</summary>
        public bool OmenActive => omenDuration > 0;

        /// <summary>预警拍进度 0~1（1=死亡判定瞬间），渲染层读它收黑；镜像宽限期计穿为负也钳在 1</summary>
        public float OmenProgress => OmenActive ? MathHelper.Clamp(1f - omenTicksLeft / (float)omenDuration, 0f, 1f) : 0f;

        //====侵蚀====

        /// <summary>上涨侵蚀（owner 端调用），越阶时给一段残句与低语</summary>
        public void AddErosion(float amount) {
            if (Player.whoAmI != Main.myPlayer || amount <= 0f) {
                return;
            }
            erosion = MathHelper.Clamp(erosion + amount, 0f, 1f);
            erosionIdleTimer = 0;
            int tier = ErosionTier;
            if (tier > lastCueTier) {
                PlayTierCue(tier);
            }
            lastCueTier = tier;
        }

        /// <summary>直接清整侵蚀（调试/特殊净化用）</summary>
        public void SetErosion(float value) {
            erosion = MathHelper.Clamp(value, 0f, 1f);
            lastCueTier = ErosionTier;
        }

        private void PlayTierCue(int tier) {
            var line = tier switch {
                1 => WraithSystemText.ErosionCrawl,
                2 => WraithSystemText.ErosionStain,
                _ => WraithSystemText.ErosionMirror,
            };
            VaultUtils.Text(line.Value, new Color(140, 120, 165));
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.7f + tier * 0.15f, Volume = 0.35f });
            Player.CWR()?.GetScreenShake(1.5f + tier);
        }

        //====预警拍（入口在 WraithLethality.StartOmen/CancelOmen，权威端专用）====

        /// <summary>权威侧起拍：倒计时落权威，本地受害者（单人）同时镜像演出；被更紧迫的现拍压住时返回 false</summary>
        internal bool BeginOmenAuthority(WraithDefinition definition, int ticks, LocalizedText reason) {
            //重复起拍取更紧迫的一段
            if (omenAuthTicksLeft > 0 && omenAuthTicksLeft <= ticks) {
                return false;
            }
            omenAuthTicksLeft = ticks;
            omenAuthDefinition = definition;
            omenAuthReason = reason;
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                BeginOmenMirror(definition, ticks);
            }
            return true;
        }

        /// <summary>权威侧撤拍（死亡/挣脱规则），本地受害者顺带撤演出</summary>
        internal void ClearOmenAuthority() {
            omenAuthTicksLeft = 0;
            omenAuthDefinition = null;
            omenAuthReason = null;
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                ClearOmenMirror();
            }
        }

        /// <summary>受害者本端演出镜像起拍（网络 op / 单人直呼）</summary>
        internal void BeginOmenMirror(WraithDefinition definition, int ticks) {
            //现拍更紧迫时保拍;已计穿(宽限期负值)的残拍不挡新拍
            if (OmenActive && omenTicksLeft > 0 && omenTicksLeft <= ticks) {
                return;
            }
            omenDuration = ticks;
            omenTicksLeft = ticks;
            omenDefinition = definition;
            omenBeatTimer = 0;
        }

        /// <summary>受害者本端演出镜像撤拍</summary>
        internal void ClearOmenMirror() {
            omenDuration = 0;
            omenTicksLeft = 0;
            omenDefinition = null;
        }

        /// <summary>
        /// 权威侧逐帧：倒计时推进与死亡判定。死于他因的撤拍不在这里——
        /// 死亡期间 PostUpdate 不执行（TML Player.Update 对 dead 提前 return），
        /// 由 <see cref="UpdateDead"/> 逐帧清拍，离场由 <see cref="PlayerDisconnect"/> 兜底
        /// </summary>
        private void UpdateOmenAuthority() {
            if (omenAuthTicksLeft <= 0) {
                return;
            }
            if (--omenAuthTicksLeft > 0) {
                return;
            }
            WraithDefinition def = omenAuthDefinition;
            LocalizedText reason = omenAuthReason;
            ClearOmenAuthority();
            WraithLethality.Kill(Player, def, reason);
        }

        /// <summary>
        /// 死亡期间的撤拍兜底（本钩子死时逐帧执行，两侧都跑）：中拍者死于他因时，
        /// 权威侧清倒计时并通知受害者端，复活后绝不带着旧拍还魂；镜像顺带清场
        /// </summary>
        public override void UpdateDead() {
            if (!VaultUtils.isClient && omenAuthTicksLeft > 0) {
                ClearOmenAuthority();
                if (VaultUtils.isServer) {
                    WraithNet.SendOmenCancel(Player.whoAmI);
                }
            }
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                ClearOmenMirror();
            }
        }

        /// <summary>中拍者离场：权威侧撤拍兜底（人已走，撤拍包不必发）</summary>
        public override void PlayerDisconnect() {
            if (!VaultUtils.isClient) {
                ClearOmenAuthority();
            }
        }

        /// <summary>受害者本端逐帧：心跳渐急；计到零只收演出（死亡判定权在权威侧，死时清场在 <see cref="UpdateDead"/>）</summary>
        private void UpdateOmenMirror() {
            if (!OmenActive) {
                return;
            }
            //无条件递减:允许计穿到宽限负值,权威包缺席时演出也能自行收场,不永久挂死收黑
            omenTicksLeft--;
            //心跳:越近越急
            int beatInterval = (int)MathHelper.Lerp(46f, 15f, OmenProgress);
            if (++omenBeatTimer >= beatInterval) {
                omenBeatTimer = 0;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.92f, Volume = 0.42f, MaxInstances = 2 });
            }
            //镜像走完仍未收到死亡/撤拍(权威包在途),再宽限半秒收场
            if (omenTicksLeft <= -30) {
                ClearOmenMirror();
            }
        }

        //====借力键====

        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (!CWRKeySystem.Wraith_Power.JustPressed || Player.dead || Player.CCed) {
                return;
            }
            if (WraithRites.PresentationBusy?.Invoke() == true) {
                return;
            }
            HandlePowerKey();
        }

        private void HandlePowerKey() {
            WraithVesselHandle vessel = WraithVessels.ResolveHeld(Player);
            if (!vessel.IsValid) {
                VaultUtils.Text(WraithSystemText.PowerDeniedNoVessel.Value, Color.DarkGray);
                return;
            }
            //死机仪式优先:窗口在前,力可以等
            if (WraithRites.TryPerform(Player, vessel)) {
                return;
            }
            TryCastAbility(vessel);
        }

        /// <summary>
        /// 共鸣之鬼：簿上 Bound 且有赋力者中驾驭度最高的一只（选择 UI 留给后续阶段）。
        /// 挣脱契约态（鬼律第十一条）：同键挣脱体在场=名讳空悬，该鬼的力借不出来
        /// </summary>
        private WraithDefinition ResolveAttuned(WraithProgressStore store, out WraithProgressRecord record, out bool escapedBlocked) {
            WraithDefinition best = null;
            record = null;
            escapedBlocked = false;
            foreach ((string key, WraithProgressRecord candidate) in store.Records) {
                if (candidate.State != WraithBindState.Bound) {
                    continue;
                }
                if (!WraithRegistry.TryGet(key, out WraithDefinition definition) || definition.Ability == null) {
                    continue;
                }
                if (WraithBacklash.AnyEscapedAlive(key, Player.whoAmI)) {
                    escapedBlocked = true;
                    continue;
                }
                if (best == null || candidate.Mastery > record.Mastery) {
                    best = definition;
                    record = candidate;
                }
            }
            return best;
        }

        private void TryCastAbility(WraithVesselHandle vessel) {
            WraithDefinition definition = ResolveAttuned(vessel.Store, out WraithProgressRecord record, out bool escapedBlocked);
            if (definition == null) {
                var line = escapedBlocked ? WraithSystemText.PowerDeniedEscaped : WraithSystemText.PowerDeniedNoBound;
                VaultUtils.Text(line.Value, Color.DarkGray);
                return;
            }
            WraithAbility ability = definition.Ability;
            if (abilityCooldowns.TryGetValue(definition.Key, out int cooldown) && cooldown > 0) {
                VaultUtils.Text(WraithSystemText.PowerDeniedCooldown.Value, Color.DarkGray);
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.5f, Volume = 0.4f });
                return;
            }

            WraithAbilityContext ctx = new() {
                Player = Player,
                VesselItem = vessel.Item,
                Store = vessel.Store,
                Record = record,
                AimWorld = Main.MouseWorld,
            };
            WraithCastResult result = ability.Cast(ctx);
            if (result == WraithCastResult.Fail) {
                return;
            }

            //双层代价:刀层磨损(犯戒加罚)+身层侵蚀;躁动与簿面读数经 Version 即时生效,
            //磨损写完显式推送持有槽同步,服务器/他端副本即时跟上(反噬复核与簿面最终一致都靠它)
            float wear = ability.MasteryWear + (result == WraithCastResult.Taboo ? ability.TabooPenalty : 0f);
            record.Mastery = MathHelper.Clamp(record.Mastery - wear, 0f, 1f);
            vessel.Store.BumpVersion();
            WraithVessels.SyncSlot(Player, vessel.Item);
            AddErosion(ability.ErosionCost);
            abilityCooldowns[definition.Key] = ability.CooldownTicks;

            if (result == WraithCastResult.Taboo) {
                VaultUtils.Text(WraithSystemText.PowerTaboo.Format(definition.DisplayName.Value), new Color(190, 60, 70));
                SoundEngine.PlaySound(SoundID.NPCDeath52 with { Pitch = -0.3f, Volume = 0.4f });
            }

            //世界改动走权威,演出本端即时、他端经广播
            if (VaultUtils.isClient) {
                WraithNet.SendAbilityCast(definition, ctx.AimWorld, record.Mastery);
            }
            else {
                ability.ExecuteWorld(Player, ctx.AimWorld, record.Mastery);
            }
            ability.PlayWorldFx(Player, ctx.AimWorld);
        }

        //====主循环====

        public override void PostUpdate() {
            //权威侧:预警拍倒计时与死亡判定(服务器为每名玩家推进,单人即本地)
            if (!VaultUtils.isClient) {
                UpdateOmenAuthority();
            }
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //冷却步进
            if (abilityCooldowns.Count > 0) {
                keyScratch.Clear();
                keyScratch.AddRange(abilityCooldowns.Keys);
                foreach (string key in keyScratch) {
                    int left = abilityCooldowns[key] - 1;
                    if (left <= 0) {
                        abilityCooldowns.Remove(key);
                    }
                    else {
                        abilityCooldowns[key] = left;
                    }
                }
            }

            //侵蚀消退与环境征兆
            if (erosionIdleTimer < ErosionDecayDelay) {
                erosionIdleTimer++;
            }
            else if (erosion > 0f) {
                erosion = Math.Max(erosion - ErosionDecayPerTick, 0f);
                lastCueTier = Math.Min(lastCueTier, ErosionTier);
            }
            UpdateErosionAmbience();

            UpdateOmenMirror();

            //反噬:1Hz 掷签 + 挂起确认观测 + 挣脱体去向判读
            if (++backlashCheckTimer >= BacklashCheckInterval) {
                backlashCheckTimer = 0;
                WraithBacklash.Judge(this);
                WatchPendingEscapes();
                WatchEscaped();
            }
        }

        /// <summary>高侵蚀的身周征兆：黑雾缕自脚边升起，阶级越高越稠</summary>
        private void UpdateErosionAmbience() {
            int tier = ErosionTier;
            if (tier <= 0 || Main.gamePaused) {
                return;
            }
            int interval = tier switch { 1 => 50, 2 => 26, _ => 12 };
            if ((int)Main.GameUpdateCount % interval != 0) {
                return;
            }
            Vector2 pos = Player.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(4f, 20f));
            PRTLoader.NewParticle<PRT_Smoke>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f)
                , new Color(96, 88, 122) * 0.5f, Main.rand.NextFloat(0.10f, 0.16f + tier * 0.03f))
                ?.Configure(Main.rand.Next(24, 40), 0.30f + tier * 0.06f);
        }

        //====反噬支撑（WraithBacklash 读写）====

        internal bool BacklashOnCooldown(string key, long now)
            => backlashCooldownUntil.TryGetValue(key, out long until) && now < until;

        internal void SetBacklashCooldown(string key, long until) => backlashCooldownUntil[key] = until;

        /// <summary>登记一只已现世的挣脱体，供去向判读</summary>
        internal void NoteEscaped(string key) => escapedWatch.Add(key);

        /// <summary>挂起一次挣脱确认观测（多人请求已发，等挣脱体真实现身），观测窗约 6 秒</summary>
        internal void NotePendingEscape(string key) => escapedPending[key] = 6;

        /// <summary>该键的挣脱确认仍在挂起观测中（掷签跳过它，防 1Hz 重复请求）</summary>
        internal bool IsEscapePending(string key) => escapedPending.ContainsKey(key);

        /// <summary>
        /// 挂起确认观测（1Hz）：挣脱体首次真实现身才播报+落 90 秒冷却并转入去向判读；
        /// 观测窗内始终没来=服务器拒了（互斥/资格/冷却），无声作废，双端冷却都不白烧
        /// </summary>
        private void WatchPendingEscapes() {
            if (escapedPending.Count == 0) {
                return;
            }
            keyScratch.Clear();
            keyScratch.AddRange(escapedPending.Keys);
            foreach (string key in keyScratch) {
                if (WraithBacklash.AnyEscapedAlive(key, Player.whoAmI)) {
                    escapedPending.Remove(key);
                    escapedWatch.Add(key);
                    SetBacklashCooldown(key, (long)Main.GameUpdateCount + WraithBacklash.KeyCooldownTicks);
                    if (WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                        WraithBacklash.AnnounceEscape(Player, definition);
                    }
                    continue;
                }
                int left = escapedPending[key] - 1;
                if (left <= 0) {
                    escapedPending.Remove(key);
                }
                else {
                    escapedPending[key] = left;
                }
            }
        }

        /// <summary>
        /// 挣脱体去向判读：上秒还在、这秒没了——驾驭度回到线上=被收伏（仪式已播报），
        /// 仍在线下=它自己散了（播"还会回来"）
        /// </summary>
        private void WatchEscaped() {
            if (escapedWatch.Count == 0) {
                return;
            }
            keyScratch.Clear();
            foreach (string key in escapedWatch) {
                if (WraithBacklash.AnyEscapedAlive(key, Player.whoAmI)) {
                    continue;
                }
                keyScratch.Add(key);
            }
            foreach (string key in keyScratch) {
                escapedWatch.Remove(key);
                if (!WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                    continue;
                }
                WraithVesselHandle vessel = WraithVessels.ResolveCarried(Player);
                bool resolved = vessel.IsValid
                    && vessel.Store.TryGet(key, out WraithProgressRecord record)
                    && record.Mastery >= WraithDefinition.RestlessThreshold;
                if (!resolved) {
                    VaultUtils.Text(WraithSystemText.BacklashFade.Format(definition.DisplayName.Value), new Color(140, 120, 165));
                }
            }
        }

        //====生命周期与存档====

        public override void OnEnterWorld() {
            abilityCooldowns.Clear();
            backlashCooldownUntil.Clear();
            escapedWatch.Clear();
            escapedPending.Clear();
            backlashCheckTimer = 0;
            ClearOmenMirror();
            lastCueTier = ErosionTier;
        }

        public override void OnRespawn() => ClearOmenMirror();

        public override void SaveData(TagCompound tag) {
            if (erosion > 0f) {
                tag[SaveKey_Erosion] = erosion;
            }
        }

        public override void LoadData(TagCompound tag) {
            erosion = tag.TryGet(SaveKey_Erosion, out float value) ? MathHelper.Clamp(value, 0f, 1f) : 0f;
            if (float.IsNaN(erosion)) {
                erosion = 0f;
            }
            lastCueTier = ErosionTier;
        }
    }
}
