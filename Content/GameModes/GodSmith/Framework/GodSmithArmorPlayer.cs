using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神赋路由 ModPlayer：装备结算期比对三件套决定主方案，再沿头盔镶嵌链收集被继承的接管方案，
    /// 组成本帧「生效奖励清单」；套装奖励数值层在 <see cref="UpdateEquips"/> 下发（与原版套装奖励同期），
    /// 文本与驻留效果在 <see cref="PostUpdateEquips"/>，命中/受击/击杀/魔耗/渔获事件逐方案分发。<br/>
    /// 三个暂存寄存器归主方案所有（方案切换即清）；接管方案的每玩家轻量状态用按方案键的冷却表，
    /// 复杂状态请族内自建 ModPlayer
    /// </summary>
    internal class GodSmithArmorPlayer : ModPlayer
    {
        /// <summary>当前命中的主方案（三件命中）；null = 未命中或模式关闭</summary>
        internal GodSmithArmorScheme ActiveScheme { get; private set; }

        //本帧生效的奖励方案：首项恒为主方案，其余为镶嵌链继承；与 bonusSourceHeads 一一对应
        private readonly List<GodSmithArmorScheme> activeBonuses = [];
        //各生效方案的来源头盔 ID（主方案为 0；继承项 = 链上那顶头盔），套装奖励文本用
        private readonly List<int> bonusSourceHeads = [];

        /// <summary>本帧生效的奖励方案（主方案 + 镶嵌继承）</summary>
        internal IReadOnlyList<GodSmithArmorScheme> ActiveBonuses => activeBonuses;

        //——每玩家暂存寄存器：归主方案所有，方案切换即清——

        /// <summary>整型暂存（层数/计数）</summary>
        internal int EndowCharge;

        /// <summary>计时暂存（对比 Main.GameUpdateCount）</summary>
        internal uint EndowTimer;

        /// <summary>布尔暂存（姿态就绪等）</summary>
        internal bool EndowFlag;

        internal void ClearScratch() {
            EndowCharge = 0;
            EndowTimer = 0;
            EndowFlag = false;
        }

        //——按方案键的冷却表：接管方案共存时各用各的键，互不干扰——
        private readonly Dictionary<GodSmithArmorScheme, uint> cooldownExpiry = [];

        /// <summary>冷却就绪则立刻进入冷却并返回 true，否则返回 false</summary>
        public bool TryUseCooldown(GodSmithArmorScheme key, int cooldownFrames) {
            if (IsOnCooldown(key)) {
                return false;
            }
            cooldownExpiry[key] = Main.GameUpdateCount + (uint)Math.Max(0, cooldownFrames);
            return true;
        }

        /// <summary>是否仍在冷却</summary>
        public bool IsOnCooldown(GodSmithArmorScheme key)
            => cooldownExpiry.TryGetValue(key, out uint expiry) && Main.GameUpdateCount < expiry;

        /// <summary>该方案本帧是否生效（主方案或被继承）</summary>
        public bool HasBonus(GodSmithArmorScheme scheme) => activeBonuses.Contains(scheme);

        /// <summary>某类方案（含其子类）本帧是否生效</summary>
        public bool HasBonus<T>() where T : GodSmithArmorScheme {
            for (int i = 0; i < activeBonuses.Count; i++) {
                if (activeBonuses[i] is T) {
                    return true;
                }
            }
            return false;
        }

        //==================== 装备结算 ====================

        public override void UpdateEquips() {
            GodSmithArmorScheme next = ResolveScheme();
            if (next != ActiveScheme) {
                //切走先给旧方案清理机会（默认清空寄存器）
                ActiveScheme?.OnEndowLost(Player, this);
                ActiveScheme = next;
            }
            activeBonuses.Clear();
            bonusSourceHeads.Clear();
            if (ActiveScheme == null) {
                return;
            }
            activeBonuses.Add(ActiveScheme);
            bonusSourceHeads.Add(0);

            //镶嵌链继承：只有接管方案之间才互相继承
            if (ActiveScheme.OverridesVanilla && GodSmithHelmetNestItem.TryGetChain(Player.armor[0], out int[] chain)) {
                for (int i = 0; i < chain.Length; i++) {
                    if (!GodSmithArmorScheme.SchemeByHead.TryGetValue(chain[i], out GodSmithArmorScheme inherited)
                        || !inherited.OverridesVanilla || activeBonuses.Contains(inherited)) {
                        continue;
                    }
                    activeBonuses.Add(inherited);
                    bonusSourceHeads.Add(chain[i]);
                }
            }

            //套装奖励数值层与原版套装奖励同期下发
            for (int i = 0; i < activeBonuses.Count; i++) {
                if (activeBonuses[i].OverridesVanilla) {
                    activeBonuses[i].UpdateSetBonus(Player, this);
                }
            }
        }

        public override void PostUpdateEquips() {
            if (ActiveScheme == null) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                activeBonuses[i].UpdateEndowment(Player, this);
            }

            if (ActiveScheme.OverridesVanilla) {
                //接管形态：套装奖励文本由方案定义，继承项逐行追加
                StringBuilder text = new(ActiveScheme.SetBonusLine.Value);
                for (int i = 1; i < activeBonuses.Count; i++) {
                    text.Append('\n').Append(GameModeText.GodSmithInheritLine.Format(
                        Lang.GetItemNameValue(bonusSourceHeads[i]), activeBonuses[i].SetBonusLine.Value));
                }
                Player.setBonus = text.ToString();
                return;
            }

            //叠加形态：神赋行叠加在原版套装奖励之后
            string endow = GameModeText.GodSmithEndowPrefix.Value + ActiveScheme.EndowLine.Value;
            Player.setBonus = string.IsNullOrEmpty(Player.setBonus)
                ? endow
                : Player.setBonus + "\n" + endow;
        }

        private GodSmithArmorScheme ResolveScheme() {
            if (!GameModeSystem.GodSmithActive) {
                return null;
            }
            if (!GodSmithArmorScheme.SchemesByBody.TryGetValue(Player.armor[1].type, out var candidates)) {
                return null;
            }
            for (int i = 0; i < candidates.Count; i++) {
                if (candidates[i].Matches(Player)) {
                    return candidates[i];
                }
            }
            return null;
        }

        private bool Dispatching => ActiveScheme != null && GameModeSystem.GodSmithActive;

        //==================== 命中 ====================

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => DispatchHit(target, hit, damageDone, sourceProj: null);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => DispatchHit(target, hit, damageDone, sourceProj: proj);

        private void DispatchHit(NPC target, in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (!Dispatching) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                GodSmithArmorScheme scheme = activeBonuses[i];
                if (sourceProj != null && scheme.IsOwnEndowProj(sourceProj)) {
                    continue;
                }
                scheme.OnEndowHitNPC(Player, this, target, hit, damageDone, sourceProj);
                //击杀判定：命中后目标生命归零即视为击杀
                if (target.life <= 0) {
                    scheme.OnEndowKillNPC(Player, this, target);
                }
            }
        }

        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
            => DispatchModifyHit(target, ref modifiers, sourceProj: null);

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
            => DispatchModifyHit(target, ref modifiers, sourceProj: proj);

        private void DispatchModifyHit(NPC target, ref NPC.HitModifiers modifiers, Projectile sourceProj) {
            if (!Dispatching) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                GodSmithArmorScheme scheme = activeBonuses[i];
                if (sourceProj != null && scheme.IsOwnEndowProj(sourceProj)) {
                    continue;
                }
                scheme.ModifyEndowHitNPC(Player, this, target, ref modifiers, sourceProj);
            }
        }

        //==================== 受击 ====================

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!Dispatching) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                activeBonuses[i].ModifyEndowHurt(Player, this, ref modifiers);
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!Dispatching) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                activeBonuses[i].OnEndowHurt(Player, this, info);
            }
        }

        public override bool ConsumableDodge(Player.HurtInfo info) {
            if (!Dispatching) {
                return false;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                if (activeBonuses[i].EndowConsumableDodge(Player, this, info)) {
                    return true;
                }
            }
            return false;
        }

        //==================== 魔力与渔获 ====================

        public override void OnConsumeMana(Item item, int manaConsumed) {
            if (!Dispatching) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                activeBonuses[i].OnEndowConsumeMana(Player, this, item, manaConsumed);
            }
        }

        public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn,
            ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) {
            if (!Dispatching) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                activeBonuses[i].EndowCatchFish(Player, this, attempt, ref itemDrop, ref npcSpawn);
            }
        }

        public override void ModifyCaughtFish(Item fish) {
            if (!Dispatching) {
                return;
            }
            for (int i = 0; i < activeBonuses.Count; i++) {
                activeBonuses[i].EndowModifyCaughtFish(Player, this, fish);
            }
        }
    }
}
