using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 接管重置的原版压制层：钩住 Player.GrantArmorBenefits（原版单件属性）与
    /// Player.UpdateArmorSets（原版套装奖励），神匠模式开启且命中接管方案时压掉原版结算，
    /// 让 <see cref="GodSmithArmorScheme.UpdateHead"/> 等钩子成为唯一定义。
    /// 压制的是原版逐物品特判，与物品 ID 无关的通用结算照常补发（见
    /// <see cref="GrantPieceBenefitsWithoutVanillaStats"/>）。
    /// 模式关闭时两处钩子原样放行，零 footprint
    /// </summary>
    internal class GodSmithArmorReset : ModSystem
    {
        public override void Load() {
            On_Player.GrantArmorBenefits += SkipVanillaPieceStats;
            On_Player.UpdateArmorSets += SkipVanillaSetBonus;
        }

        //On_ 钩子由 tML 随模组卸载自动摘除

        private static void SkipVanillaPieceStats(On_Player.orig_GrantArmorBenefits orig, Player self, Item armorPiece) {
            if (GameModeSystem.GodSmithActive && armorPiece != null && GodSmithArmorScheme.OverridesPiece(armorPiece.type)) {
                GrantPieceBenefitsWithoutVanillaStats(self, armorPiece);
                return;
            }
            orig(self, armorPiece);
        }

        /// <summary>
        /// 压掉原版逐物品特判后仍要补发的通用部分，逐条对应原版 GrantArmorBenefits 里与物品 ID 无关的几行：
        /// 护甲值（前缀加成也记在 item.defense 上，一并回来）、生命回复、举盾位、信息饰品刷新。<br/>
        /// 末行的 ItemLoader.UpdateEquip 尤其不能漏：模组 UpdateEquip 钩子全靠那一个派发点，
        /// 整段 return 会连方案自己的 UpdateHead/Body/Legs 一起吞掉，
        /// 结果是穿上后既没有原版防御也没有重铸属性
        /// </summary>
        private static void GrantPieceBenefitsWithoutVanillaStats(Player player, Item armorPiece) {
            player.RefreshInfoAccsFromItemType(armorPiece);
            player.RefreshMechanicalAccsFromItemType(armorPiece.type);
            player.statDefense += armorPiece.defense;
            player.lifeRegen += armorPiece.lifeRegen;
            if (armorPiece.shieldSlot > 0) {
                player.hasRaisableShield = true;
            }
            ItemLoader.UpdateEquip(armorPiece, player);
        }

        private static void SkipVanillaSetBonus(On_Player.orig_UpdateArmorSets orig, Player self, int i) {
            if (GameModeSystem.GodSmithActive && IsResetSetWorn(self)) {
                return;
            }
            orig(self, i);
        }

        /// <summary>
        /// 三件是否命中某个要压掉原版套装奖励的接管方案（直接查表，不依赖 ModPlayer 的帧序）；
        /// 放行原版机制的方案（KeepsVanillaSetBonus）不算
        /// </summary>
        internal static bool IsResetSetWorn(Player player) {
            if (!GodSmithArmorScheme.SchemesByBody.TryGetValue(player.armor[1].type, out var candidates)) {
                return false;
            }
            for (int k = 0; k < candidates.Count; k++) {
                GodSmithArmorScheme scheme = candidates[k];
                if (scheme.OverridesVanilla && !scheme.KeepsVanillaSetBonus && scheme.Matches(player)) {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 接管方案单件的 GlobalItem 分发面（无实例数据）：UpdateEquip 按部件派发 UpdateHead/Body/Legs，
    /// ModifyTooltips 隐掉原版属性行、换上方案的属性行，套装奖励一律落在「神匠重铸」段内
    /// （穿着中的件把原版套装奖励行搬进段内，其余件预告）；全部以 GodSmithActive 为闸
    /// </summary>
    internal class GodSmithArmorPiece : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && GodSmithArmorScheme.SchemeByPiece.ContainsKey(entity.type);

        public override void UpdateEquip(Item item, Player player) {
            if (!GameModeSystem.GodSmithActive
                || !GodSmithArmorScheme.SchemeByPiece.TryGetValue(item.type, out GodSmithArmorScheme scheme)
                || !scheme.OverridesPieceStats) {
                return;
            }
            if (scheme.IsHead(item.type)) {
                scheme.UpdateHead(player, item);
            }
            else if (scheme.IsBody(item.type)) {
                scheme.UpdateBody(player, item);
            }
            else if (scheme.IsLegs(item.type)) {
                scheme.UpdateLegs(player, item);
            }
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (!GameModeSystem.GodSmithActive
                || !GodSmithArmorScheme.SchemeByPiece.TryGetValue(item.type, out GodSmithArmorScheme scheme)) {
                return;
            }
            //原版套装奖励行（只有穿在身上且整套命中的那件才带，内容已是方案写进 setBonus 的文本）
            //固定排在原版区，先摘下来，随后搬进「神匠重铸」段内
            TooltipLine setBonusLine = null;
            foreach (TooltipLine line in tooltips) {
                if (line.Mod != "Terraria") {
                    continue;
                }
                if (line.Name == "SetBonus") {
                    setBonusLine = line;
                }
                else if (scheme.OverridesPieceStats && line.Name.StartsWith("Tooltip")) {
                    //原版属性行全部隐掉，换成方案定义的属性行
                    line.Hide();
                }
            }
            if (setBonusLine != null) {
                tooltips.Remove(setBonusLine);
            }

            GodSmithTooltip.EnsureTitle(tooltips);
            if (scheme.OverridesPieceStats) {
                LocalizedText pieceLine = scheme.IsHead(item.type) ? scheme.HeadLine
                    : scheme.IsBody(item.type) ? scheme.BodyLine : scheme.LegsLine;
                if (pieceLine != null) {
                    GodSmithTooltip.AddBodyLines(tooltips, "CWR_GodSmithPieceLine", pieceLine.Value, GodSmithTooltip.BodyGold);
                }
            }
            if (setBonusLine != null) {
                //穿着中：原版套装奖励行原样落在段内（含镶嵌链继承行），只换成段内正文色
                setBonusLine.OverrideColor = GodSmithTooltip.BodyGold;
                tooltips.Add(setBonusLine);
                return;
            }
            //没穿在身上（未穿满，或背包里的同款）：预告套装奖励
            GodSmithTooltip.AddBodyLines(tooltips, "CWR_GodSmithSetPreview",
                GameModeText.GodSmithSetPreview.Format(scheme.SetBonusLine.Value), GodSmithTooltip.BodyGold);
        }
    }

    /// <summary>
    /// 盔甲奖励作用于武器与鱼竿的 GlobalItem 桥：把穿戴者本帧生效的奖励方案清单分发到
    /// 武器伤害/用速/物品消耗/弹药消耗/射击钩子（投掷不消耗、加射速、多甩鱼线等套装奖励靠它落地）
    /// </summary>
    internal class GodSmithArmorItemBridge : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => lateInstantiation && (entity.damage > 0 || entity.fishingPole > 0);

        private static bool TryBonuses(Player player, out IReadOnlyList<GodSmithArmorScheme> bonuses) {
            bonuses = null;
            if (!GameModeSystem.GodSmithActive || player == null) {
                return false;
            }
            GodSmithArmorPlayer state = player.GetModPlayer<GodSmithArmorPlayer>();
            if (state.ActiveScheme == null) {
                return false;
            }
            bonuses = state.ActiveBonuses;
            return bonuses.Count > 0;
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            if (!TryBonuses(player, out var bonuses)) {
                return;
            }
            for (int i = 0; i < bonuses.Count; i++) {
                bonuses[i].ModifyEndowWeaponDamage(player, item, ref damage);
            }
        }

        public override float UseSpeedMultiplier(Item item, Player player) {
            if (!TryBonuses(player, out var bonuses)) {
                return 1f;
            }
            float mult = 1f;
            for (int i = 0; i < bonuses.Count; i++) {
                mult *= bonuses[i].EndowUseSpeedMultiplier(player, item);
            }
            return mult;
        }

        public override bool ConsumeItem(Item item, Player player) {
            if (!TryBonuses(player, out var bonuses)) {
                return true;
            }
            for (int i = 0; i < bonuses.Count; i++) {
                if (bonuses[i].EndowConsumeItem(player, item) == false) {
                    return false;
                }
            }
            return true;
        }

        public override bool CanConsumeAmmo(Item weapon, Item ammo, Player player) {
            if (!TryBonuses(player, out var bonuses)) {
                return true;
            }
            for (int i = 0; i < bonuses.Count; i++) {
                if (bonuses[i].EndowCanConsumeAmmo(player, weapon, ammo) == false) {
                    return false;
                }
            }
            return true;
        }

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!TryBonuses(player, out var bonuses)) {
                return true;
            }
            bool allow = true;
            for (int i = 0; i < bonuses.Count; i++) {
                allow &= bonuses[i].EndowShoot(player, item, source, position, velocity, type, damage, knockback);
            }
            return allow;
        }
    }

    /// <summary>
    /// 盔甲奖励用的弹幕来源打标（逐实例 GlobalProjectile）：OnSpawn 记下出生源武器与弹药的物品 ID，
    /// 父弹幕已打标时子弹幕承签；只存在于生成端且不上网，唯一消费点是 owner 端的命中钩子
    /// （投掷/吹箭命中挂减益等）。神赋 proc 弹用 GetSource_Misc 出生，不打标不承签
    /// </summary>
    internal class GodSmithArmorProjMark : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>出生源武器物品 ID；0 = 无</summary>
        internal int SourceItemType;

        /// <summary>出生时消耗的弹药物品 ID；0 = 无</summary>
        internal int SourceAmmoType;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //EntitySource_ItemUse_WithAmmo 派生自 EntitySource_ItemUse，一并覆盖
            if (source is EntitySource_ItemUse itemUse && itemUse.Item != null) {
                SourceItemType = itemUse.Item.type;
                if (source is EntitySource_ItemUse_WithAmmo withAmmo && withAmmo.AmmoItemIdUsed > 0) {
                    SourceAmmoType = withAmmo.AmmoItemIdUsed;
                }
                return;
            }
            if (source is EntitySource_Parent parentSource && parentSource.Entity is Projectile parentProj
                && parentProj.TryGetGlobalProjectile(out GodSmithArmorProjMark parentMark)) {
                SourceItemType = parentMark.SourceItemType;
                SourceAmmoType = parentMark.SourceAmmoType;
            }
        }

        /// <summary>读取弹幕的来源武器与弹药 ID（无标时均为 0）</summary>
        public static void SourceOf(Projectile proj, out int itemType, out int ammoType) {
            itemType = 0;
            ammoType = 0;
            if (proj != null && proj.TryGetGlobalProjectile(out GodSmithArmorProjMark mark)) {
                itemType = mark.SourceItemType;
                ammoType = mark.SourceAmmoType;
            }
        }
    }
}
