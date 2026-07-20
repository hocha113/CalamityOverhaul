using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 厉鬼静态定义：身份、文案、实体接线与显形参数的唯一来源，运行期不可变。
    /// 子类即注册（<see cref="WraithRegistry"/> 反射扫描），一份定义对应一个
    /// <c>WraithActor</c> 子类。创意字段全部有安全默认值，主题化 = 覆写属性 + 填 hjson，
    /// 动态进度一律走 <see cref="WraithProgressStore"/>，不得写回定义
    /// </summary>
    public abstract class WraithDefinition : ILocalizedModType
    {
        /// <summary>躁动线：驾驭度低于它即视为躁动，反噬判定与簿面躁动呈现同源</summary>
        public const float RestlessThreshold = 0.35f;

        public Mod Mod => CWRMod.Instance;
        /// <summary>内部名，本地化键第三段：Mods.CalamityOverhaul.Wraiths.{Name}.*</summary>
        public string Name => GetType().Name;
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Wraiths";

        /// <summary>稳定键，存档/进度/查询锚点，改名即断档，默认取类型名</summary>
        public virtual string Key => GetType().Name;
        /// <summary>名录排序，越小越靠前，并列时按 <see cref="Key"/> 字典序</summary>
        public virtual int SortOrder => 0;
        /// <summary>初始绑定状态：新进度记录的起点，设定为"生来封印"的鬼覆写为 Sealed</summary>
        public virtual WraithBindState InitialBindState => WraithBindState.Unknown;
        /// <summary>图鉴/名录类展示是否隐藏（调试件、剧情隐藏鬼用），不影响注册与生成</summary>
        public virtual bool HiddenFromCatalog => false;
        /// <summary>
        /// 是否调试件：调试内容不受 <c>WraithDirector.LiveContentEnabled</c> 上线闸约束（自持调试闸门）。
        /// 正典鬼一律保持 false——系统未开放期间，其全部自然出现渠道被上线闸钳在调试闹鬼闸后
        /// </summary>
        public virtual bool IsDebugContent => false;

        //====本地化（LoadLocalization 由注册表在 Mod.Load 期调用）====
        /// <summary>鬼名</summary>
        public LocalizedText DisplayName { get; private set; }
        /// <summary>来历残句</summary>
        public LocalizedText Origin { get; private set; }
        /// <summary>赋予的力</summary>
        public LocalizedText Power { get; private set; }
        /// <summary>规则死亡讯息，{0}=玩家名；鬼律第十条"可归因"，走 <see cref="Runtime.WraithLethality"/></summary>
        public LocalizedText DeathReason { get; private set; }

        internal void LoadLocalization() {
            DisplayName = this.GetLocalization("DisplayName", () => "???");
            Origin = this.GetLocalization("Origin", () => "...");
            Power = this.GetLocalization("Power", () => "...");
            DeathReason = this.GetLocalization("DeathReason", () => "{0}触犯了不可触犯之物");
            LoadExtraLocalization();
        }

        /// <summary>
        /// 装载本鬼专属的额外文案（规则专属死亡讯息等），注册期随框架文案一并调用。
        /// 鬼律第十条"可归因"：每条规则应有点名文案，<see cref="DeathReason"/> 只是兜底
        /// </summary>
        protected virtual void LoadExtraLocalization() { }

        //====实体接线====
        /// <summary>
        /// 显形态实体类型，必须是 <c>WraithActor</c> 子类且与本定义一一对应
        /// （实体经类型反查定义，多定义共用同一实体类会在注册期报错）
        /// </summary>
        public abstract Type ActorType { get; }

        /// <summary>命中箱宽（像素）</summary>
        public virtual int HitboxWidth => 60;
        /// <summary>命中箱高（像素）</summary>
        public virtual int HitboxHeight => 90;

        //====显形参数====
        /// <summary>显形过渡帧数</summary>
        public virtual int MaterializeFrames => 45;
        /// <summary>消散过渡帧数</summary>
        public virtual int DematerializeFrames => 35;
        /// <summary>无外部干预时的最大在场帧数，&lt;=0 表示不限时</summary>
        public virtual int PresentDurationLimit => 60 * 60;
        /// <summary>死机窗口帧数，窗口尽未行仪式即自然消散</summary>
        public virtual int HaltWindowTicks => 60 * 8;
        /// <summary>
        /// 是否受理外部死机请求（<c>WraithNet.HaltRequest</c>，含解除分支）——
        /// **鬼律第九条的执行点**：该通道绕过规则状态机直接逼死机，默认 false 一律拒绝；
        /// 只有调试件覆写为 true。正典鬼的死机必须与规则强相关，
        /// 唯一合法入口是各自规则状态机在权威端直呼 <c>WraithActor.BeginHalt</c>
        /// </summary>
        public virtual bool AllowExternalHaltRequest => false;

        //====仪式数值====
        /// <summary>首次铭刻落簿的初始驾驭度（新收的鬼近乎躁动）</summary>
        public virtual float FirstBindMastery => 0.15f;
        /// <summary>重续契约（认主）后的驾驭度</summary>
        public virtual float RenewedMastery => 0.85f;

        //====感知参数====
        /// <summary>凝视判定距离（像素）</summary>
        public virtual float GazeRange => 900f;
        /// <summary>接近判定半径，进入触发 OnPlayerApproach</summary>
        public virtual float ApproachRadius => 180f;
        /// <summary>脱离判定半径，接近过后离开触发 OnPlayerRetreat，应大于接近半径</summary>
        public virtual float RetreatRadius => 320f;
        /// <summary>发现判定半径，完全显形时有玩家在内即记入世界进度</summary>
        public virtual float DiscoverRadius => 1200f;

        //====占位视觉====
        /// <summary>占位绘制主色，主题化后由实体自绘覆盖</summary>
        public virtual Color BaseColor => new(150, 160, 185);
        /// <summary>占位绘制眼色</summary>
        public virtual Color EyeColor => new(120, 220, 200);

        //====行为与调度====
        /// <summary>
        /// 组装行为积木，每个实体生成时调用一次并各自持有全新实例，默认无行为（静止悬浮）
        /// </summary>
        public virtual void BuildBehaviors(List<IWraithBehavior> behaviors) { }

        /// <summary>
        /// 自动显形规则工厂，默认 null：不自动出现，只能被外部显式生成。
        /// 鬼律第五条：正典鬼一律据点制（<see cref="GetSitePlan"/>），本规则仅调试件使用。
        /// 定义运行期不可变，实例经 <see cref="SpawnRule"/> 惰性缓存，调度端勿直呼本工厂
        /// </summary>
        protected virtual WraithSpawnRule GetSpawnRule() => null;

        /// <summary>
        /// 据点计划工厂，默认 null：无据点。据点状态与存档见 <c>WraithSiteSystem</c>。
        /// 实例经 <see cref="SitePlan"/> 惰性缓存，规则谓词内的动态条件照常逐次求值
        /// </summary>
        protected virtual WraithSitePlan GetSitePlan() => null;

        private WraithSpawnRule spawnRule;
        private bool spawnRuleCreated;
        private WraithSitePlan sitePlan;
        private bool sitePlanCreated;

        /// <summary>缓存的自动显形规则，无规则为 null</summary>
        public WraithSpawnRule SpawnRule {
            get {
                if (!spawnRuleCreated) {
                    spawnRuleCreated = true;
                    spawnRule = GetSpawnRule();
                }
                return spawnRule;
            }
        }

        /// <summary>缓存的据点计划，无据点为 null</summary>
        public WraithSitePlan SitePlan {
            get {
                if (!sitePlanCreated) {
                    sitePlanCreated = true;
                    sitePlan = GetSitePlan();
                }
                return sitePlan;
            }
        }

        //====赋力====
        private WraithAbility ability;
        private bool abilityCreated;

        /// <summary>
        /// 赋力工厂，默认 null：驾驭后无主动力。实例为全局单例且必须无状态
        /// （冷却等每玩家数据在 <c>WraithPlayer</c>），经 <see cref="Ability"/> 惰性缓存
        /// </summary>
        public virtual WraithAbility CreateAbility() => null;

        /// <summary>缓存的赋力单例，无赋力为 null</summary>
        public WraithAbility Ability {
            get {
                if (!abilityCreated) {
                    abilityCreated = true;
                    ability = CreateAbility();
                    if (ability != null) {
                        ability.Definition = this;
                    }
                }
                return ability;
            }
        }
    }
}
