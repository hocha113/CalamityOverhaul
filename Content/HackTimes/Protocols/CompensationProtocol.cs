using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 代偿协议：预支目标一成生命立刻打掉，窗口内没杀掉就按债务反噬施术者。<br/>
    /// 全套唯一失手会伤玩家的协议。玩家生命归拥有者客户端所有，
    /// 还账因此分两条路：单机在 <see cref="OnRemove"/> 直接结算，
    /// 联机由服务端广播移除后在施术者本机的 <see cref="OnReplicatedRemove"/> 结算。<br/>
    /// 施术者死亡或下线时 <c>ResolveEffectCaster</c> 取不到人，债务勾销
    /// ：命都没了没人再去讨账，也不把账转嫁给队友
    /// </summary>
    internal class CompensationProtocol : QuickHackDef
    {
        /// <summary>预支比例；Boss 目标由 EffectMult(0.5) 折半成 5%</summary>
        private const float AdvanceRatio = 0.10f;
        /// <summary>还账附带的禁回血帧数</summary>
        internal const int RegenLockFrames = 600;

        private static readonly Color DebtRed = new(255, 70, 60);
        private static readonly Color DebtDark = new(120, 16, 20);

        //activationId → 债务额。协议实例是单例，per-effect 状态只能外挂；
        //权威端与各客户端各记各的账（各端独立算同一笔确定性数值，不跨端搬运）。
        //泄漏路径：目标死亡/消失时 OnRemove 不会被调用，账留在表里，
        //由下一次记账时的 PruneStale 与 Unload 兜底清掉
        private static readonly Dictionary<long, float> debts = [];

        public override void SetDefaults() {
            UploadTime = 110;
            RamCost = 4;
            Category = QuickHackCategory.Lethal;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 480;

        public override void Unload() {
            base.Unload();
            debts.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            //友方与镇民不作赌注；免伤目标预支不进去；同一目标不叠加
            return !npc.friendly && !npc.townNPC && !npc.dontTakeDamage
                && !HackEffectTracker.HasEffect<CompensationProtocol>(npc.whoAmI);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;

            ActiveHackEffect effect =
                HackEffectTracker.GetEffect<CompensationProtocol>(npc.whoAmI);
            float debt = ComputeDebt(npc, effect?.EffectMult ?? (npc.boss ? 0.5f : 1f));
            //预支：立刻打掉一成生命，权威端结算，伤害靠 StrikeNPC 的广播回传
            npc.SimpleStrikeNPC(Math.Max(1, (int)debt), 0, false, 0f, null, false, 0f, true);
            if (effect != null) RecordDebt(effect.ActivationId, debt);

            if (Main.netMode != NetmodeID.Server) EmitAdvance(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            //客户端记同一笔确定性债务，供本机结算用；打人本身归权威端
            ActiveHackEffect effect =
                HackEffectTracker.GetEffect<CompensationProtocol>(npc.whoAmI);
            if (effect != null) {
                RecordDebt(effect.ActivationId, ComputeDebt(npc, effect.EffectMult));
            }
            EmitAdvance(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitCountdown(npc, elapsed, GetDuration());
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) {
                EmitCountdown(npc, elapsed, GetDuration());
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            ActiveHackEffect effect =
                HackEffectTracker.GetEffect<CompensationProtocol>(npc.whoAmI);
            float debt = TakeDebt(effect, npc);

            if (Main.netMode != NetmodeID.Server) EmitRecall(npc);
            //目标死透（同帧被打死时 IsValid 仍可能为真）→ 赌赢，债务清零
            if (npc.life <= 0 || !npc.active) return;

            //联机的还账交给施术者客户端的 OnReplicatedRemove；这里只管单机
            if (Main.netMode != NetmodeID.SinglePlayer) return;
            Player caster = ResolveCaster(effect);
            if (caster != null) Settle(caster, debt);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            ActiveHackEffect effect =
                HackEffectTracker.GetEffect<CompensationProtocol>(npc.whoAmI);
            float debt = TakeDebt(effect, npc);
            EmitRecall(npc);
            if (npc.life <= 0 || !npc.active) return;

            //只有施术者本机结算；债主死了或走了这笔账就烂掉
            if (effect == null || effect.CasterIndex != Main.myPlayer) return;
            Player caster = ResolveCaster(effect);
            if (caster != null) Settle(caster, debt);
        }

        #region 账务

        private static float ComputeDebt(NPC npc, float effectMult)
            => npc.lifeMax * AdvanceRatio
                * MathHelper.Clamp(float.IsFinite(effectMult) ? effectMult : 1f, 0.1f, 1f);

        private static void RecordDebt(long activationId, float debt) {
            if (activationId <= 0) return;
            PruneStale();
            debts[activationId] = debt;
        }

        /// <summary>取出并销账；表里没有（比如中途入场）就按当前状态重算一遍</summary>
        private static float TakeDebt(ActiveHackEffect effect, NPC npc) {
            if (effect != null && debts.Remove(effect.ActivationId, out float debt)) {
                return debt;
            }
            return ComputeDebt(npc, effect?.EffectMult ?? 1f);
        }

        //效果结束时协议拿不到已消亡效果的 activationId，账只能在下次开户时对齐
        private static void PruneStale() {
            if (debts.Count == 0) return;
            List<long> stale = null;
            foreach (long id in debts.Keys) {
                if (HackEffectTracker.FindEffect(id) == null) (stale ??= []).Add(id);
            }
            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++) {
                debts.Remove(stale[i]);
            }
        }

        private static Player ResolveCaster(ActiveHackEffect effect) {
            if (effect == null || effect.CasterIndex < 0
                || effect.CasterIndex >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[effect.CasterIndex];
            return player?.active == true && !player.dead ? player : null;
        }

        /// <summary>
        /// 还账：真实伤害直写生命（不吃防御、不吃闪避、不给受击无敌），
        /// 最低留 1 HP，另附十秒禁回血。只在拥有者本机调用，差分同步会把生命带回服务端
        /// </summary>
        private static void Settle(Player caster, float debt) {
            int amount = (int)debt;
            if (amount <= 0) return;
            int payable = Math.Min(amount, Math.Max(0, caster.statLife - 1));
            if (payable > 0) {
                caster.statLife -= payable;
                CombatText.NewText(caster.Hitbox, DebtRed, $"-{payable}", true);
            }
            caster.lifeRegenTime = 0;
            caster.GetModPlayer<CompensationLedgerPlayer>().RegenLockFrames = RegenLockFrames;
            SoundEngine.PlaySound(SoundID.PlayerHit, caster.Center);
            EmitSettle(caster);
        }

        #endregion

        #region 表现

        //预支落账：一圈暗红崩流 + 核心闪
        private static void EmitAdvance(NPC npc) {
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f)
                    * Main.rand.NextFloat(0.4f, 1f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, DebtRed, 1.0f)
                    ?.Configure(false, 24);
            }
            PRTLoader.NewParticle<PRT_Spark>(npc.Center, Vector2.Zero, Color.White, 1.7f)
                ?.Configure(false, 10);
        }

        //倒计时越往后红得越急，读作"账要到期了"
        private static void EmitCountdown(NPC npc, int elapsed, int duration) {
            float t = MathHelper.Clamp(elapsed / (float)Math.Max(duration, 1), 0f, 1f);
            int interval = t > 0.75f ? 6 : t > 0.5f ? 12 : 20;
            if (elapsed % interval != 0) return;
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                npc.width * 0.45f, npc.height * 0.45f);
            PRTLoader.NewParticle<PRT_Spark>(pos, new Vector2(0f, -0.8f),
                Color.Lerp(DebtDark, DebtRed, t), 0.45f + t * 0.4f)?.Configure(false, 18);
        }

        //到期回收：目标身上的红光散场
        private static void EmitRecall(NPC npc) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.2f, 2.2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, DebtDark, 0.6f)
                    ?.Configure(false, 14);
            }
        }

        //还账落在施术者身上：向内收拢的血色
        private static void EmitSettle(Player caster) {
            for (int i = 0; i < 12; i++) {
                Vector2 edge = caster.Center + Main.rand.NextVector2CircularEdge(52f, 52f);
                Vector2 vel = (caster.Center - edge).SafeNormalize(Vector2.UnitY)
                    * Main.rand.NextFloat(3f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(edge, vel, DebtRed, 0.9f)
                    ?.Configure(false, 18);
            }
        }

        #endregion
    }

    /// <summary>代偿协议的禁回血账本；计数只会在拥有者本机被点燃，天然每端各管各的</summary>
    internal class CompensationLedgerPlayer : ModPlayer
    {
        /// <summary>剩余禁回血帧数</summary>
        public int RegenLockFrames;

        public override void UpdateBadLifeRegen() {
            if (RegenLockFrames <= 0) return;
            RegenLockFrames--;
            Player.lifeRegenTime = 0;
            if (Player.lifeRegen > 0) Player.lifeRegen = 0;
        }

        //死亡帧不跑 UpdateBadLifeRegen，账在这里继续消化，免得复活后还背着旧锁
        public override void UpdateDead() {
            if (RegenLockFrames > 0) RegenLockFrames--;
        }
    }
}
