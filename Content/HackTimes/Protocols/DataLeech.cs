using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 数据榨取：在目标身上开一道回流口，打它就回 RAM。<br/>
    /// 回流由 <see cref="HackEffectNPCCombat"/> 在受击时结算
    /// </summary>
    internal class DataLeech : QuickHackDef
    {
        /// <summary>每点伤害折算的 RAM</summary>
        internal const float LeechPerDamage = 0.004f;
        /// <summary>单次受击回流上限，免得一发大招把 RAM 直接灌满</summary>
        internal const float LeechCap = 1.2f;
        /// <summary>单次激活的回流总额，约为施放成本的两倍半</summary>
        internal const float LeechBudget = 10f;
        //两次回流之间的最小间隔：鞭子、连枷与穿透弹一帧里就能进十几次受击
        private const int LeechCooldown = 10;

        private static readonly Color Siphon = new(120, 255, 180);

        //activationId → 这次激活的回流账。协议实例是单例，per-effect 状态只能外挂；
        //按 activationId 记账不会像按 NPC 槽位那样在槽位复用后认错目标
        private static readonly Dictionary<long, LeechAccount> accounts = [];

        private struct LeechAccount
        {
            public float Remaining;
            public ulong NextFrame;
        }

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.Contagion;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 6;

        public override void Unload() {
            base.Unload();
            accounts.Clear();
        }

        /// <summary>切世界时把回流账清空</summary>
        internal static void ClearAccounts() => accounts.Clear();

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitTap(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitTap(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitIdle(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitIdle(npc, elapsed);
        }

        /// <summary>
        /// 把这次伤害折算成 RAM 还给施法者。<br/>
        /// 单次上限管不住多段武器：六秒窗口里鞭子与穿透弹能打进几十次，
        /// 四下就把施放成本赚回来，之后一路把 RAM 条钉满。
        /// 所以按激活给总额，并给回流本身上一层间隔
        /// 每次回流都会立刻提交一次 RAM 状态，频率本身也是成本
        /// </summary>
        internal static void ApplyLeech(Player caster, NPC npc, int damage,
            long activationId) {
            if (caster == null || damage <= 0 || activationId <= 0) return;
            if (!accounts.TryGetValue(activationId, out LeechAccount account)) {
                PruneAccounts();
                account = new LeechAccount { Remaining = LeechBudget };
            }

            float amount = MathHelper.Min(damage * LeechPerDamage, LeechCap);
            amount = MathHelper.Min(amount, account.Remaining);
            if (amount <= 0f || Main.GameUpdateCount < account.NextFrame) {
                accounts[activationId] = account;
                return;
            }

            account.Remaining -= amount;
            account.NextFrame = Main.GameUpdateCount + LeechCooldown;
            accounts[activationId] = account;
            RamSystem.Restore(caster, amount, out _);
            if (Main.netMode != NetmodeID.Server) EmitDrain(npc, caster);
        }

        //效果结束时协议拿不到 activationId，账只能在下次开户时对齐一遍
        private static void PruneAccounts() {
            if (accounts.Count == 0) return;
            List<long> stale = null;
            foreach (long id in accounts.Keys) {
                if (HackEffectTracker.FindEffect(id) == null) (stale ??= []).Add(id);
            }
            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++) {
                accounts.Remove(stale[i]);
            }
        }

        private static void EmitTap(NPC npc) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.6f, 2.6f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Siphon, 0.9f)
                    ?.Configure(false, 20);
            }
        }

        private static void EmitIdle(NPC npc, int elapsed) {
            if (elapsed % 20 != 0) return;
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(
                npc.width * 0.45f, npc.height * 0.45f);
            PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Siphon, 0.4f)
                ?.Configure(false, 18);
        }

        //回流朝施法者飞，看得见钱从哪儿来
        private static void EmitDrain(NPC npc, Player caster) {
            Vector2 toCaster = (caster.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = toCaster.RotatedByRandom(0.35f) * Main.rand.NextFloat(3f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Siphon, 0.8f)
                    ?.Configure(false, 16);
            }
        }
    }
}
