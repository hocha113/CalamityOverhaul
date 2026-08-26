using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using CalamityOverhaul.Content.GameModes.UI;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠武器方案基类。继承 InnoVault <see cref="ItemOverride"/>，
    /// 把「神匠模式开启才生效」的闸门封死在这里：
    /// <see cref="CanOverride"/> 直读 <see cref="GameModeSystem.GodSmithActive"/>，
    /// 模式关闭时 InnoVault 的分发根本取不到本方案，行为即时退回原版；
    /// 因此子类禁写 SetDefaults（已密封），一切改动都必须是每帧动态钩子。<br/>
    /// 子类只允许重写 Gs* 虚方法；本地化载体即本类
    /// （键 = Mods.CalamityOverhaul.GodSmith{GsFamily}.{类名}.{后缀}）。<br/>
    /// 联机纪律：方案是单例，跨玩家共享；连段计数等瞬时字段只允许在
    /// 本地玩家路径（GsShoot/GsCanUseItem 的 myPlayer 守门内）消费
    /// </summary>
    internal abstract class GodSmithScheme : ItemOverride
    {
        //==================== 注册表 ====================

        /// <summary>物品 ID → 方案，加载期填充，供路由/桥接快速查询</summary>
        public static Dictionary<int, GodSmithScheme> SchemeByItemID { get; private set; } = [];

        /// <summary>按物品 ID 查方案（不含模式闸门，调用方自查 <see cref="GameModeSystem.GodSmithActive"/>）</summary>
        public static bool TryGetScheme(int itemType, out GodSmithScheme scheme)
            => SchemeByItemID.TryGetValue(itemType, out scheme);

        internal static void ClearRegistry() => SchemeByItemID = [];

        //==================== 子类必填 ====================

        /// <summary>目标原版物品 ID</summary>
        public abstract int TargetItemID { get; }

        /// <summary>族名（如 Broadswords/Bows/Exemplars），决定本地化类目 GodSmith{族名} 与 loc 文件名</summary>
        public abstract string GsFamily { get; }

        //==================== 身份与闸门（密封） ====================

        public sealed override int TargetID => TargetItemID;

        /// <summary>总闸：模式关闭时 InnoVault 取不到本方案，所有钩子对原版零footprint</summary>
        public sealed override bool CanOverride() => GameModeSystem.GodSmithActive;

        public sealed override string LocalizationCategory => "GodSmith" + GsFamily;

        /// <summary>不注册 DisplayName/Tooltip 键，方案不改名不换介绍，只追加</summary>
        public sealed override bool CanLoadLocalization => false;

        public sealed override LocalizedText DisplayName =>
            TargetItemID < ItemID.Count
                ? Language.GetText("ItemName." + ItemID.Search.GetName(TargetItemID))
                : base.DisplayName;

        public sealed override LocalizedText Tooltip =>
            TargetItemID < ItemID.Count
                ? Language.GetText("ItemTooltip." + ItemID.Search.GetName(TargetItemID))
                : base.Tooltip;

        //==================== 生命周期 ====================

        /// <summary>神匠重铸简述（tooltip 注入正文），键后缀取 <see cref="GsDescKeySuffix"/></summary>
        public LocalizedText GsDesc { get; private set; }

        /// <summary>重铸简述的键后缀，默认 Desc</summary>
        public virtual string GsDescKeySuffix => "Desc";

        /// <summary>重铸简述的代码默认值（en 文案；正典 zh 写进族 loc 文件）</summary>
        protected virtual string GsDescFallback => "";

        /// <summary>禁用：加载期不得留任何足迹（词缀表等一律不碰）；静态初始化写 <see cref="GsSetStaticDefaults"/></summary>
        public sealed override void SetStaticDefaults() { }

        public sealed override void PostSetStaticDefaults() {
            GsDesc = this.GetLocalization(GsDescKeySuffix, () => GsDescFallback);
            if (!SchemeByItemID.TryAdd(TargetItemID, this)) {
                CWRMod.Instance.Logger.Error(
                    $"[GodSmith] 物品 {TargetItemID} 被重复认领：{SchemeByItemID[TargetItemID].FullName} 与 {FullName}，后者生效");
                SchemeByItemID[TargetItemID] = this;
            }
            GsSetStaticDefaults();
        }

        /// <summary>加载期静态初始化：注册弹幕增强通道、缓存本地化等（禁止改全局表）</summary>
        public virtual void GsSetStaticDefaults() { }

        //==================== 通用小工具 ====================

        /// <summary>本地玩家的鼠标瞄准单位向量；只在 myPlayer 路径调用</summary>
        protected static Vector2 GsAimUnit(Player player)
            => (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);

        /// <summary>该玩家名下是否已有指定手持弹幕（近战接管防重复生成）</summary>
        protected static bool HeldAlive<T>(Player player) where T : ModProjectile
            => player.ownedProjectileCounts[ModContent.ProjectileType<T>()] > 0;

        //==================== Gs 钩子面：使用流 ====================

        /// <summary>是否可用物品。近战接管在这里 owner 侧生成手持弹幕并返回 false 压掉原版挥舞</summary>
        public virtual bool? GsCanUseItem(Item item, Player player) => null;

        /// <summary>物品使用时（UseItem 时机）</summary>
        public virtual bool? GsUseItem(Item item, Player player) => null;

        /// <summary>使用动画开始时</summary>
        public virtual void GsUseAnimation(Item item, Player player) { }

        /// <summary>返回 true 允许右键使用</summary>
        public virtual bool? GsAltFunctionUse(Item item, Player player) => null;

        /// <summary>手持时每帧（连段衰减计时等；跨玩家共享单例，写字段先守 myPlayer）</summary>
        public virtual void GsHoldItem(Item item, Player player) { }

        /// <summary>手持姿态帧</summary>
        public virtual void GsHoldItemFrame(Item item, Player player) { }

        /// <summary>使用中的手臂/身体帧</summary>
        public virtual void GsUseItemFrame(Item item, Player player) { }

        /// <summary>使用中的持握位置与旋转</summary>
        public virtual void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) { }

        /// <summary>原版挥舞的碰撞箱修改</summary>
        public virtual void GsUseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox) { }

        /// <summary>原版挥舞的近战粒子时机</summary>
        public virtual void GsMeleeEffects(Item item, Player player, Rectangle hitbox) { }

        /// <summary>用速倍率（经 GlobalItem 桥接，同时缩放 useTime 与 useAnimation）</summary>
        public virtual float GsUseSpeedMultiplier(Item item, Player player) => 1f;

        //==================== Gs 钩子面：射击流 ====================

        /// <summary>是否允许进入 Shoot（返回 false 直接砍掉本次射击）</summary>
        public virtual bool? GsCanShoot(Item item, Player player) => null;

        /// <summary>
        /// 射击。返回 null 走原版；生成自定义弹幕后返回 false 压掉原版弹幕；
        /// 返回 true 放行原版默认弹幕但跳过其余模组的 Shoot 链。只在 owner 端执行
        /// </summary>
        public virtual bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) => null;

        /// <summary>射击参数修改（弹速/伤害/弹种置换）。只在 owner 端执行，先于 GsShoot</summary>
        public virtual void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) { }

        /// <summary>本次射击是否消耗弹药（true/false 均阻断原版判定）</summary>
        public virtual bool? GsCanConsumeAmmo(Item weapon, Item ammo, Player player) => null;

        /// <summary>本次使用是否消耗该物品（消耗投掷族的回收经济入口；返回 false 阻止消耗）</summary>
        public virtual bool? GsConsumeItem(Item item, Player player) => null;

        /// <summary>该物品被消耗时</summary>
        public virtual void GsOnConsumeItem(Item item, Player player) { }

        /// <summary>按弹药修改发射参数</summary>
        public virtual void GsPickAmmo(Item weapon, Item ammo, Player player,
            ref int type, ref float speed, ref StatModifier damage, ref float knockback) { }

        /// <summary>弹药被消耗时</summary>
        public virtual void GsOnConsumeAmmo(Item weapon, Item ammo, Player player) { }

        //==================== Gs 钩子面：数值 ====================

        /// <summary>武器伤害修饰（残酷下敌人 +50%，重铸后有效 DPS 允许原版 100%~120%，弱势武器至 135%）</summary>
        public virtual void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }

        /// <summary>暴击修饰</summary>
        public virtual void GsModifyWeaponCrit(Item item, Player player, ref float crit) { }

        /// <summary>击退修饰</summary>
        public virtual void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) { }

        /// <summary>物品体积（近战范围）动态缩放</summary>
        public virtual void GsModifyItemScale(Item item, Player player, ref float scale) { }

        /// <summary>魔耗修改</summary>
        public virtual void GsModifyManaCost(Item item, Player player, ref float reduce, ref float mult) { }

        /// <summary>魔力被实际扣除时（咏唱增幅/连击共鸣计数放这）</summary>
        public virtual void GsOnConsumeMana(Item item, Player player, int manaConsumed) { }

        /// <summary>魔力不足仍试图使用时</summary>
        public virtual void GsOnMissingMana(Item item, Player player, int neededMana) { }

        //==================== Gs 钩子面：命中 ====================

        /// <summary>物品直击是否可命中该 NPC</summary>
        public virtual bool? GsCanHitNPC(Item item, Player player, NPC target) => null;

        /// <summary>原版挥舞的贪婪判定入口（自定义碰撞区）</summary>
        public virtual bool? GsCanMeleeCollide(Item item, Rectangle meleeAttackHitbox, Player player, NPC target) => null;

        /// <summary>物品直击伤害修饰</summary>
        public virtual void GsModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers) { }

        /// <summary>物品直击命中（只在攻击方端执行）</summary>
        public virtual void GsOnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) { }

        //==================== Gs 钩子面：tooltip ====================

        /// <summary>金色标题行与简述注入之后追加自定义行（模式开启时才会被调用）</summary>
        public virtual void GsModifyTooltips(Item item, List<TooltipLine> tooltips) { }

        //==================== Gs 钩子面：弹幕增强（由 GodSmithProjRouter 分发） ====================

        /// <summary>被打标弹幕出生瞬间（owner 端，先于生成包发出；写 router.MarkData/MarkData2 可随包过线）</summary>
        public virtual void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) { }

        /// <summary>
        /// 子弹幕承签：出生源的父弹幕已打标时，标记与 MarkData/MarkData2 已自动复制到子弹幕，
        /// 随后调用本钩子（集束子雷/弹片/分裂类的二级增强入口；生成端执行，标记照常过线）
        /// </summary>
        public virtual void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router, Projectile parent, GodSmithProjRouter parentRouter) { }

        /// <summary>弹幕 AI 前置；返回 false 压掉原版 AI（各端都会执行，权威改动守 IsOwnedByLocalPlayer）</summary>
        public virtual bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) => true;

        /// <summary>弹幕 AI 后置（速度塑形/拖尾粒子；粒子守 !VaultUtils.isServer）</summary>
        public virtual void GsProjPostAI(Projectile proj, GodSmithProjRouter router) { }

        /// <summary>弹幕命中伤害修饰</summary>
        public virtual void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) { }

        /// <summary>弹幕命中（只在 owner 端执行）</summary>
        public virtual void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) { }

        /// <summary>弹幕绘制前置；返回非 null 阻断后续绘制（绘制禁 Main.rand，用 identity 种子）</summary>
        public virtual bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) => null;

        /// <summary>弹幕绘制后置</summary>
        public virtual void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) { }

        /// <summary>弹幕消亡</summary>
        public virtual void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) { }

        /// <summary>
        /// 注册按弹幕类型的增强通道（仆从/哨兵/驻场弹幕）。
        /// 与打标通道不同：不需要出生源，模式关闭时在场弹幕即刻退回原版行为。
        /// 在 <see cref="GsSetStaticDefaults"/> 里调用
        /// </summary>
        protected void GsRegisterProjChannel(params int[] projTypes)
            => GodSmithProjRouter.RegisterChannel(this, projTypes);

        //==================== 密封转发：InnoVault 钩子 → Gs* ====================

        public sealed override bool? CanUseItem(Item item, Player player) => GsCanUseItem(item, player);

        public sealed override bool? UseItem(Item item, Player player) => GsUseItem(item, player);

        public sealed override void UseAnimation(Item item, Player player) => GsUseAnimation(item, player);

        public sealed override bool? AltFunctionUse(Item item, Player player) => GsAltFunctionUse(item, player);

        public sealed override void HoldItem(Item item, Player player) => GsHoldItem(item, player);

        public sealed override void HoldItemFrame(Item item, Player player) => GsHoldItemFrame(item, player);

        public sealed override void UseItemFrame(Item item, Player player) => GsUseItemFrame(item, player);

        public sealed override void UseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsUseStyle(item, player, heldItemFrame);

        public sealed override void UseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
            => GsUseItemHitbox(item, player, ref hitbox, ref noHitbox);

        public sealed override void MeleeEffects(Item item, Player player, Rectangle hitbox)
            => GsMeleeEffects(item, player, hitbox);

        public sealed override bool? CanShoot(Item item, Player player) => GsCanShoot(item, player);

        public sealed override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => GsShoot(item, player, source, position, velocity, type, damage, knockback);

        public sealed override void ModifyShootStats(Item item, Player player, ref ItemShootState shootStats)
            => base.ModifyShootStats(item, player, ref shootStats);

        public sealed override void ModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => GsModifyShootStats(item, player, ref position, ref velocity, ref type, ref damage, ref knockback);

        public sealed override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player)
            => GsCanConsumeAmmo(weapon, ammo, player);

        public sealed override void PickAmmo(Item weapon, Item ammo, Player player,
            ref int type, ref float speed, ref StatModifier damage, ref float knockback)
            => GsPickAmmo(weapon, ammo, player, ref type, ref speed, ref damage, ref knockback);

        public sealed override void OnConsumeAmmo(Item weapon, Item ammo, Player player)
            => GsOnConsumeAmmo(weapon, ammo, player);

        public sealed override bool? ConsumeItem(Item item, Player player) => GsConsumeItem(item, player);

        public sealed override void OnConsumeItem(Item item, Player player) => GsOnConsumeItem(item, player);

        public sealed override void OnConsumeMana(Item item, Player player, int manaConsumed)
            => GsOnConsumeMana(item, player, manaConsumed);

        public sealed override void OnMissingMana(Item item, Player player, int neededMana)
            => GsOnMissingMana(item, player, neededMana);

        public sealed override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => GsModifyWeaponDamage(item, player, ref damage);

        public sealed override void ModifyWeaponCrit(Item item, Player player, ref float crit)
            => GsModifyWeaponCrit(item, player, ref crit);

        public sealed override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => GsModifyWeaponKnockback(item, player, ref knockback);

        public sealed override void ModifyItemScale(Item item, Player player, ref float scale)
            => GsModifyItemScale(item, player, ref scale);

        public sealed override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult)
            => GsModifyManaCost(item, player, ref reduce, ref mult);

        public sealed override bool? CanHitNPC(Item item, Player player, NPC target)
            => GsCanHitNPC(item, player, target);

        public sealed override bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle meleeAttackHitbox, Player player, NPC target)
            => GsCanMeleeCollide(item, meleeAttackHitbox, player, target);

        public sealed override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
            => GsModifyHitNPC(item, player, target, ref modifiers);

        public sealed override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
            => GsOnHitNPC(item, player, target, hit, damageDone);

        /// <summary>金色「神匠重铸」标题行 + 方案简述注入；追加行走 <see cref="GsModifyTooltips"/></summary>
        public sealed override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            Color gold = Color.Lerp(GameModeTheme.GodSmithAccent, GameModeTheme.GodSmithEmber, 0.55f);
            tooltips.Add(new TooltipLine(CWRMod.Instance, "CWR_GodSmithTitle",
                GameModeText.GodSmithRecastTitle.Value) { OverrideColor = gold });
            string desc = GsDesc?.Value;
            if (!string.IsNullOrWhiteSpace(desc)) {
                string[] lines = desc.Split('\n');
                for (int i = 0; i < lines.Length; i++) {
                    if (string.IsNullOrWhiteSpace(lines[i])) {
                        continue;
                    }
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWR_GodSmithDesc" + i, lines[i]) {
                        OverrideColor = Color.Lerp(gold, new Color(200, 190, 172), 0.62f)
                    });
                }
            }
            GsModifyTooltips(item, tooltips);
        }

        //==================== 密封空挡：其余 InnoVault 钩子一律不许子类触碰 ====================

        /// <summary>禁用：改 SetDefaults 会留下切模式不消退的footprint，一切改动走动态钩子</summary>
        public sealed override void SetDefaults(Item item) { }

        public sealed override void SaveData(Item item, TagCompound tag) { }
        public sealed override void LoadData(Item item, TagCompound tag) { }
        public sealed override void ModifyName(Item item, ref string reset) { }
        public sealed override void ModifyAffixName(Item item, ref string reset) { }
        public sealed override bool PreShimmering(Item item) => true;
        public sealed override bool PreGetShimmered(Item item) => true;
        public sealed override void PostShimmering(Item item) { }
        public sealed override void PostGetShimmered(int originalType, Item item, bool shimmerOccurred) { }
        public sealed override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) { }
        public sealed override void On_PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) { }
        public sealed override bool? PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) => null;
        public sealed override bool? On_PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) => null;
        public sealed override bool? AllowPrefix(Item item, int pre) => null;
        public sealed override bool? On_AltFunctionUse(Item item, Player player) => null;
        public sealed override bool? CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) => null;
        public sealed override bool? CanBeChosenAsAmmo(Item ammo, Item weapon, Player player) => null;
        public sealed override bool? CanBeConsumedAsAmmo(Item ammo, Item weapon, Player player) => null;
        public sealed override bool? CanCatchNPC(Item item, NPC target, Player player) => null;
        public sealed override bool? CanEquipAccessory(Item item, Player player, int slot, bool modded) => null;
        public sealed override bool? CanHitPvp(Item item, Player player, Player target) => null;
        public sealed override bool? CanPickup(Item item, Player player) => null;
        public sealed override bool? CanReforge(Item item) => null;
        public sealed override bool? CanResearch(Item item) => null;
        public sealed override bool? CanRightClick(Item item) => null;
        public sealed override bool? CanStack(Item destination, Item source) => null;
        public sealed override bool? CanStackInWorld(Item destination, Item source) => null;
        public sealed override bool? On_CanUseItem(Item item, Player player) => null;
        public sealed override bool? On_ConsumeItem(Item item, Player player) => null;
        public sealed override bool On_PreEmitUseVisuals(Item item, Player player, ref Rectangle itemRectangle) => true;
        public sealed override void On_PostEmitUseVisuals(Item item, Player player, ref Rectangle itemRectangle) { }
        public sealed override bool? On_ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers) => null;
        public sealed override void ModifyHitPvp(Item item, Player player, Player target, ref Player.HurtModifiers modifiers) { }
        public sealed override void ModifyItemLoot(Item item, ItemLoot itemLoot) { }
        public sealed override bool? On_ModifyItemLoot(Item item, ItemLoot itemLoot) => null;
        public sealed override bool? On_ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) => null;
        public sealed override bool? On_ModifyTooltips(Item item, List<TooltipLine> tooltips) => null;
        public sealed override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) => null;
        public sealed override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => true;
        public sealed override void OnConsumedAsAmmo(Item ammo, Item weapon, Player player) { }
        public sealed override bool On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) => true;
        public sealed override void OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) { }
        public sealed override bool On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) => true;
        public sealed override bool? OnPickup(Item item, Player player) => null;
        public sealed override void OnSpawn(Item item, IEntitySource source) { }
        public sealed override void OnStack(Item destination, Item source, int numToTransfer) { }
        public sealed override void RightClick(Item item, Player player) { }
        public sealed override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) => null;
        public sealed override void On_PostShoot(Item item, int whoAmI, int weaponDamage) { }
        public sealed override void SplitStack(Item destination, Item source, int numToTransfer) { }
        public sealed override void Update(Item item, ref float gravity, ref float maxFallSpeed) { }
        public sealed override void UpdateAccessory(Item item, Player player, bool hideVisual) { }
        public sealed override void UpdateArmorByHead(Player player, Item body, Item legs) { }
        public sealed override void UpdateEquip(Item item, Player player) { }
        public sealed override bool On_UpdateAccessory(Item item, Player player, bool hideVisual) => true;
        public sealed override void UpdateInventory(Item item, Player player) { }
        public sealed override bool? On_UseAnimation(Item item, Player player) => null;
        public sealed override bool? On_UseItem(Item item, Player player) => null;
        public sealed override bool? On_UseItemFrame(Item item, Player player) => null;
        public sealed override bool? On_UseStyle(Item item, Player player, Rectangle heldItemFrame) => null;
        public sealed override void VerticalWingSpeeds(Item item, Player player, ref float ascentWhenFalling, ref float ascentWhenRising, ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend) { }
        public sealed override bool? WingUpdate(int wings, Player player, bool inUse) => null;
        public sealed override bool? CanSwitchWeapon(Item item, Player player) => null;
        public sealed override void ModifyRecipe(Recipe recipe) { }
        public sealed override void AddRecipe() { }
    }
}
