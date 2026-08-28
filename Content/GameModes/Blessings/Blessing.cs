using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings
{
    /// <summary>
    /// 修罗祝福静态目录项：每尊 Boss 一条，修罗（含死神永生态）开启状态下讨伐即入世界档案。
    /// 实例是全局单例，禁止携带每玩家状态；会话态一律走
    /// <see cref="BlessingPlayer.StateOf(Blessing)"/> 的槽数组
    /// </summary>
    internal abstract class Blessing : ILocalizedModType
    {
        public Mod Mod => CWRMod.Instance;
        public string Name => GetType().Name;
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Blessings";

        /// <summary>档案键，进世界档与玩家档</summary>
        public string ID => Name;

        /// <summary>席位号（进度序下标），加载期由注册表指定；同一二进制两端恒一致，兼作网络编号</summary>
        internal int Seat { get; set; } = -1;

        /// <summary>往生轮上的进度序（小者靠前）</summary>
        public abstract int ProgressOrder { get; }

        /// <summary>
        /// 讨伐锚点 NPC 类型。死亡回调已按 realLife 归并到头节点；
        /// 体节各自独立血量的 Boss（世界吞噬者）须把全部节型列入
        /// </summary>
        public abstract int[] AnchorNPCTypes { get; }

        /// <summary>符纹线稿（SVG path d 串，0..100 画布），往生轮珠心用</summary>
        public virtual string SigilPath => "";

        /// <summary>会话态槽数（计时器/层数等），由 <see cref="BlessingPlayer"/> 按玩家惰性分配</summary>
        public virtual int StateSlots => 0;

        public LocalizedText DisplayName { get; private set; }
        public LocalizedText Description { get; private set; }

        internal void LoadLocalization() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => Name);
            Description = this.GetLocalization(nameof(Description), () => Name);
        }

        /// <summary>
        /// 该锚点之死是否意味着 Boss 整体倒下（双子/世界吞噬者这类多体 Boss 覆写检查残部）。
        /// <paramref name="npc"/> 已是 realLife 头节点
        /// </summary>
        public virtual bool IsBossFullyDown(NPC npc) => true;

        //——效果钩子：仅在祝福燃焰（模式开 + 已解锁 + 已点燃）时被 BlessingPlayer 分发——

        /// <summary>每帧杂项期，计时器/层数推进用</summary>
        public virtual void PostUpdate(BlessingPlayer bp) { }

        /// <summary>装备属性期，加数值用</summary>
        public virtual void UpdateEquips(BlessingPlayer bp) { }

        /// <summary>生命再生结算期</summary>
        public virtual void UpdateLifeRegen(BlessingPlayer bp) { }

        /// <summary>出手伤害修正（物品与自有弹幕都会路由到这里）</summary>
        public virtual void ModifyHitNPC(BlessingPlayer bp, NPC target, ref NPC.HitModifiers modifiers) { }

        /// <summary>命中敌怪后</summary>
        public virtual void OnHitNPC(BlessingPlayer bp, NPC target, in NPC.HitInfo hit, int damageDone) { }

        /// <summary>被敌怪接触所伤的修正</summary>
        public virtual void ModifyHitByNPC(BlessingPlayer bp, NPC npc, ref Player.HurtModifiers modifiers) { }

        /// <summary>任意来源受伤修正</summary>
        public virtual void ModifyHurt(BlessingPlayer bp, ref Player.HurtModifiers modifiers) { }

        /// <summary>受伤结算后</summary>
        public virtual void PostHurt(BlessingPlayer bp, in Player.HurtInfo info) { }

        /// <summary>无代价完全回避一次伤害；返回 true 即免除本次受击</summary>
        public virtual bool FreeDodge(BlessingPlayer bp, in Player.HurtInfo info) => false;

        /// <summary>是否消耗弹药；返回 false 免除本次消耗</summary>
        public virtual bool CanConsumeAmmo(BlessingPlayer bp, Item weapon, Item ammo) => true;

        /// <summary>物品使用速度乘数</summary>
        public virtual float UseSpeedMultiplier(BlessingPlayer bp, Item item) => 1f;

        /// <summary>治疗量修正（药水等恢复物品）</summary>
        public virtual void GetHealLife(BlessingPlayer bp, Item item, bool quickHeal, ref int healValue) { }
    }
}
