using CalamityOverhaul.Content.TimeFreezes;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips
{
    /// <summary>
    /// 单个被鞭目标的标记状态。owner 本地量（命中判定与收益全在攻击方端），
    /// 键控 <see cref="NetworkNPCIdentity"/> 防槽位复用继承脏层数
    /// </summary>
    internal sealed class WhipMarkState
    {
        /// <summary>鞭痕层衰减窗（帧）：超时未续鞭即清层</summary>
        internal const int MarkDecayFrames = 300;
        /// <summary>处决印宽限窗（帧）：转印后给玩家十秒组织踩拍</summary>
        internal const int SealDecayFrames = 600;

        /// <summary>当前鞭痕层数</summary>
        internal int Stacks;
        /// <summary>最近一次鞭中该目标的世界帧</summary>
        internal uint LastHitTick;
        /// <summary>处决印已点亮（鞭痕满层转化）</summary>
        internal bool ExecuteReady;
        /// <summary>处决印点亮时刻</summary>
        internal uint ExecuteReadyTick;
        /// <summary>登记的鞭面板伤（处决/连环爆的折算基数）</summary>
        internal int MarkDamage;
        /// <summary>鞭刑余韵截止帧：引爆后 120f 内自家召唤物 +10%</summary>
        internal uint AfterglowUntil;
        /// <summary>皮鞭处决追加：截止前自家仆从 +15%</summary>
        internal uint LeatherBoostUntil;
        /// <summary>万花筒棱彩暴露截止帧：期间自家召唤物 10% 概率强制暴击</summary>
        internal uint PrismExposeUntil;

        /// <summary>惰性衰减：读取前调用，超时的层与印就地清空</summary>
        internal void Refresh(uint now) {
            if (Stacks > 0 && now - LastHitTick > MarkDecayFrames && !ExecuteReady) {
                Stacks = 0;
            }
            if (ExecuteReady && now - ExecuteReadyTick > SealDecayFrames) {
                ExecuteReady = false;
                Stacks = 0;
            }
        }

        /// <summary>还持有任何活性状态（层/印/余韵/专属时限）</summary>
        internal bool IsAlive(uint now)
            => Stacks > 0 || ExecuteReady
            || now < AfterglowUntil || now < LeatherBoostUntil || now < PrismExposeUntil;
    }

    /// <summary>
    /// 鞭刑节拍的每玩家状态载体。节拍连击/挥击窗口/鞭痕字典全部是
    /// owner 本地玩法量（命中链只在攻击方端执行），不同步、不发包；
    /// 远端玩家实例上这些字段恒为初值，读到即无效果，天然联机安全
    /// </summary>
    internal class GsWhipPlayer : ModPlayer
    {
        //==================== 节拍状态（仅 myPlayer 路径消费） ====================

        /// <summary>当前节拍归属的鞭物品 ID，换鞭即重置</summary>
        internal int WhipItemType;
        /// <summary>本次挥击动画结束帧，即 on-beat 窗口开启帧；0 = 无在途挥击</summary>
        internal uint SwingEndTick;
        /// <summary>本次窗口宽度（帧），按实际动画帧数动态换算</summary>
        internal int WindowFrames;
        /// <summary>本次挥击是否已命中过任何敌人（空挥判定）</summary>
        internal bool SwingHasHit;
        /// <summary>本次挥击的收尾（空挥惩罚）是否已结算</summary>
        internal bool SwingSettled = true;
        /// <summary>本次挥击是否踩拍</summary>
        internal bool SwingOnBeat;
        /// <summary>本次挥击命中的敌人数</summary>
        internal int SwingHitCount;
        /// <summary>本次挥击命中过的 npc.whoAmI（波尼鞭连骨振排除已中目标用）</summary>
        internal readonly HashSet<int> SwingHitNPCs = [];

        //==================== 鞭痕字典（owner 本地） ====================

        /// <summary>鞭痕/处决印登记表，键 = 网络身份防槽位复用</summary>
        internal readonly Dictionary<NetworkNPCIdentity, WhipMarkState> Marks = [];

        private uint nextSweepTick;
        private static readonly List<NetworkNPCIdentity> sweepBuffer = [];

        /// <summary>节拍全清（换鞭/超窗归零/空挥 Reset 政策）</summary>
        internal void ResetTempo() {
            SwingEndTick = 0;
            WindowFrames = 0;
            SwingHasHit = false;
            SwingSettled = true;
            SwingOnBeat = false;
            SwingHitCount = 0;
            SwingHitNPCs.Clear();
            BeatCombo = 0;
        }

        /// <summary>节拍连击层数（0~5）</summary>
        internal int BeatCombo;

        /// <summary>取或建该目标的标记条目；身份无效（假人边界等）返回 null</summary>
        internal WhipMarkState GetOrCreateMark(NPC npc) {
            if (!NetworkNPCIdentity.TryCapture(npc, out NetworkNPCIdentity id)) {
                return null;
            }
            if (!Marks.TryGetValue(id, out WhipMarkState st)) {
                st = new WhipMarkState();
                Marks[id] = st;
            }
            st.Refresh(Main.GameUpdateCount);
            return st;
        }

        /// <summary>查询该目标仍有活性的标记；顺带做惰性衰减</summary>
        internal bool TryGetMark(NPC npc, out WhipMarkState st) {
            st = null;
            if (Marks.Count == 0) {
                return false;
            }
            if (!NetworkNPCIdentity.TryCapture(npc, out NetworkNPCIdentity id)
                || !Marks.TryGetValue(id, out st)) {
                return false;
            }
            st.Refresh(Main.GameUpdateCount);
            return st.IsAlive(Main.GameUpdateCount);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer || !GameModeSystem.GodSmithActive) {
                return;
            }
            uint now = Main.GameUpdateCount;
            //低频清扫：失活条目与身份失效条目出表，防字典无界增长
            if (now >= nextSweepTick) {
                nextSweepTick = now + 120;
                SweepDead(now);
            }
        }

        private void SweepDead(uint now) {
            sweepBuffer.Clear();
            foreach (KeyValuePair<NetworkNPCIdentity, WhipMarkState> kv in Marks) {
                kv.Value.Refresh(now);
                if (!kv.Value.IsAlive(now) || !kv.Key.TryResolve(out _)) {
                    sweepBuffer.Add(kv.Key);
                }
            }
            foreach (NetworkNPCIdentity id in sweepBuffer) {
                Marks.Remove(id);
            }
            sweepBuffer.Clear();
        }
    }

    /// <summary>
    /// 鞭痕的收益出口。加成结算发生在弹幕命中结算端
    /// （= 召唤物 owner 端），读的是该端本地玩家自己的鞭痕字典，
    /// 天然只有「自家召唤物」吃到加成，各端结论确定
    /// </summary>
    internal class GsWhipMarkNPC : GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            //自建钩子：模式门自查
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //口径对齐 1.4.4 鞭 tag：仆从/哨兵本体与其派生弹一并吃鞭痕收益
            bool summonHit = projectile.minion || projectile.sentry
                || ProjectileID.Sets.MinionShot[projectile.type]
                || ProjectileID.Sets.SentryShot[projectile.type];
            if (!summonHit || projectile.owner < 0 || projectile.owner >= Main.maxPlayers) {
                return;
            }
            Player owner = Main.player[projectile.owner];
            if (owner?.active != true
                || !owner.GetModPlayer<GsWhipPlayer>().TryGetMark(npc, out WhipMarkState st)) {
                return;
            }
            uint now = Main.GameUpdateCount;
            float bonus = 0.02f * st.Stacks;
            if (now < st.AfterglowUntil) {
                bonus += 0.10f;   //鞭刑余韵
            }
            if (now < st.LeatherBoostUntil) {
                bonus += 0.15f;   //皮鞭处决追加
            }
            if (bonus > 0f) {
                modifiers.FinalDamage *= 1f + bonus;
            }
            //棱彩暴露：+10% 暴击的真实实现（结算端只跑一次，掷点安全）
            if (now < st.PrismExposeUntil && Main.rand.NextBool(10)) {
                modifiers.SetCrit();
            }
        }
    }
}
