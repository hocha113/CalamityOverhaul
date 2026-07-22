using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.PRT;
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
    /// 每玩家状态。侵蚀/omen/借力键/反噬；字段全实例级
    /// </summary>
    internal class WraithPlayer : ModPlayer
    {
        //====侵蚀调参====
        /// <summary>侵蚀自然消退速率（约 4 分钟满值归零）</summary>
        private const float ErosionDecayPerTick = 1f / (60f * 240f);
        /// <summary>最后一次上涨后的消退延迟（帧）</summary>
        private const int ErosionDecayDelay = 60 * 6;
        /// <summary>侵蚀阈值，一/二/三阶</summary>
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
        //赋力/反噬冷却，键=定义 Key
        private readonly Dictionary<string, int> abilityCooldowns = [];
        private readonly Dictionary<string, long> backlashCooldownUntil = [];
        //上一秒在场挣脱体，去向判读用
        private readonly HashSet<string> escapedWatch = [];
        //挣脱确认挂起，键→剩余观测次数(1Hz)
        private readonly Dictionary<string, int> escapedPending = [];
        //键遍历临时缓冲
        private static readonly List<string> keyScratch = [];

        //====预警拍权威侧====
        private int omenAuthTicksLeft;
        private WraithDefinition omenAuthDefinition;
        private LocalizedText omenAuthReason;

        //====预警拍演出镜像====
        private int omenTicksLeft;
        private int omenDuration;
        private WraithDefinition omenDefinition;
        private int omenBeatTimer;

        /// <summary>侵蚀 0~1</summary>
        public float Erosion => erosion;

        /// <summary>侵蚀阶级 0~3</summary>
        public int ErosionTier => erosion >= TierMirror ? 3 : erosion >= TierStain ? 2 : erosion >= TierCrawl ? 1 : 0;

        /// <summary>预警演出中</summary>
        public bool OmenActive => omenDuration > 0;

        /// <summary>预警进度 0~1，收黑用</summary>
        public float OmenProgress => OmenActive ? MathHelper.Clamp(1f - omenTicksLeft / (float)omenDuration, 0f, 1f) : 0f;

        //====侵蚀====

        /// <summary>上涨侵蚀，越阶播残句</summary>
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

        /// <summary>清侵蚀</summary>
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

        //====预警拍====

        /// <summary>权威起拍；更紧迫现拍压住返回 false</summary>
        internal bool BeginOmenAuthority(WraithDefinition definition, int ticks, LocalizedText reason) {
            //取更紧迫一段
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

        /// <summary>权威撤拍</summary>
        internal void ClearOmenAuthority() {
            omenAuthTicksLeft = 0;
            omenAuthDefinition = null;
            omenAuthReason = null;
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer) {
                ClearOmenMirror();
            }
        }

        /// <summary>受害者镜像起拍</summary>
        internal void BeginOmenMirror(WraithDefinition definition, int ticks) {
            //更紧迫保拍；计穿残拍不挡新拍
            if (OmenActive && omenTicksLeft > 0 && omenTicksLeft <= ticks) {
                return;
            }
            omenDuration = ticks;
            omenTicksLeft = ticks;
            omenDefinition = definition;
            omenBeatTimer = 0;
        }

        /// <summary>受害者镜像撤拍</summary>
        internal void ClearOmenMirror() {
            omenDuration = 0;
            omenTicksLeft = 0;
            omenDefinition = null;
        }

        /// <summary>权威逐帧倒计时与判死；死于他因撤拍见 UpdateDead</summary>
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

        /// <summary>死亡期间撤拍兜底，两侧都跑</summary>
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

        /// <summary>中拍者离场，权威撤拍</summary>
        public override void PlayerDisconnect() {
            if (!VaultUtils.isClient) {
                ClearOmenAuthority();
            }
        }

        /// <summary>受害者镜像逐帧，心跳渐急</summary>
        private void UpdateOmenMirror() {
            if (!OmenActive) {
                return;
            }
            //允许计穿宽限，权威包缺席也能收场
            omenTicksLeft--;
            //心跳
            int beatInterval = (int)MathHelper.Lerp(46f, 15f, OmenProgress);
            if (++omenBeatTimer >= beatInterval) {
                omenBeatTimer = 0;
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.92f, Volume = 0.42f, MaxInstances = 2 });
            }
            //镜像走完再宽限半秒
            if (omenTicksLeft <= -30) {
                ClearOmenMirror();
            }
        }

        //====借力键====

        public override void ProcessTriggers(TriggersSet triggersSet) {
            //上线闸关则键位 null，输入静默
            if (CWRKeySystem.Wraith_Power?.JustPressed != true || Player.dead || Player.CCed) {
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
            //死机仪式优先
            if (WraithRites.TryPerform(Player, vessel)) {
                return;
            }
            TryCastAbility(vessel);
        }

        /// <summary>共鸣之鬼，Bound 有赋力者中驾驭最高；挣脱在场则借不出</summary>
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
                //上线闸关则正典借不出
                if (!WraithDirector.ContentActiveFor(definition)) {
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

            //双层代价，磨损后 SyncSlot
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

            //世界改动权威，演出本端即时
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
            //权威预警倒计时
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

            //反噬
            if (++backlashCheckTimer >= BacklashCheckInterval) {
                backlashCheckTimer = 0;
                WraithBacklash.Judge(this);
                WatchPendingEscapes();
                WatchEscaped();
            }
        }

        /// <summary>高侵蚀身周黑雾</summary>
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

        /// <summary>登记挣脱体</summary>
        internal void NoteEscaped(string key) => escapedWatch.Add(key);

        /// <summary>挂起挣脱确认观测，约 6 秒</summary>
        internal void NotePendingEscape(string key) => escapedPending[key] = 6;

        /// <summary>该键确认仍挂起</summary>
        internal bool IsEscapePending(string key) => escapedPending.ContainsKey(key);

        /// <summary>挂起观测 1Hz，现身才播报落冷却</summary>
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

        /// <summary>挣脱体去向，线上=收伏，线下=自散</summary>
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
