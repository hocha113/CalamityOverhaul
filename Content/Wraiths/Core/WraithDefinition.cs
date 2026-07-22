using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 静态定义，运行期不可变；子类即注册，一份定义对应一个 <c>WraithActor</c>。<br/>
    /// 主题化=覆写属性+hjson；动态进度走 <see cref="WraithProgressStore"/>
    /// </summary>
    public abstract class WraithDefinition : ILocalizedModType
    {
        /// <summary>躁动线，驾驭度低于此即躁动</summary>
        public const float RestlessThreshold = 0.35f;

        public Mod Mod => CWRMod.Instance;
        /// <summary>内部名，本地化键第三段</summary>
        public string Name => GetType().Name;
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Wraiths";

        /// <summary>稳定键，改名即断档，默认类型名</summary>
        public virtual string Key => GetType().Name;
        /// <summary>名录排序，越小越前</summary>
        public virtual int SortOrder => 0;
        /// <summary>新记录起点，生来封印覆写 Sealed</summary>
        public virtual WraithBindState InitialBindState => WraithBindState.Unknown;
        /// <summary>图鉴隐藏，不影响注册与生成</summary>
        public virtual bool HiddenFromCatalog => false;
        /// <summary>调试件豁免上线闸；正典保持 false</summary>
        public virtual bool IsDebugContent => false;

        //====本地化====
        /// <summary>鬼名</summary>
        public LocalizedText DisplayName { get; private set; }
        /// <summary>来历残句</summary>
        public LocalizedText Origin { get; private set; }
        /// <summary>赋予的力</summary>
        public LocalizedText Power { get; private set; }
        /// <summary>规则死因，{0}=玩家名</summary>
        public LocalizedText DeathReason { get; private set; }

        internal void LoadLocalization() {
            DisplayName = this.GetLocalization("DisplayName", () => "???");
            Origin = this.GetLocalization("Origin", () => "...");
            Power = this.GetLocalization("Power", () => "...");
            DeathReason = this.GetLocalization("DeathReason", () => "{0}触犯了不可触犯之物");
            LoadExtraLocalization();
        }

        /// <summary>专属额外文案，注册期调用；DeathReason 仅兜底</summary>
        protected virtual void LoadExtraLocalization() { }

        //====实体接线====
        /// <summary>显形态类型，须为 WraithActor 子类且一对一</summary>
        public abstract Type ActorType { get; }

        /// <summary>命中箱宽 px</summary>
        public virtual int HitboxWidth => 60;
        /// <summary>命中箱高 px</summary>
        public virtual int HitboxHeight => 90;

        //====显形参数====
        /// <summary>显形过渡帧</summary>
        public virtual int MaterializeFrames => 45;
        /// <summary>消散过渡帧</summary>
        public virtual int DematerializeFrames => 35;
        /// <summary>最大在场帧，&lt;=0 不限</summary>
        public virtual int PresentDurationLimit => 60 * 60;
        /// <summary>死机窗口帧</summary>
        public virtual int HaltWindowTicks => 60 * 8;
        /// <summary>
        /// 是否受理外部死机请求（HaltRequest）。默认 false；正典死机须规则状态机直呼 BeginHalt
        /// </summary>
        public virtual bool AllowExternalHaltRequest => false;

        //====仪式数值====
        /// <summary>首次铭刻初始驾驭度</summary>
        public virtual float FirstBindMastery => 0.15f;
        /// <summary>认主后续签驾驭度</summary>
        public virtual float RenewedMastery => 0.85f;

        //====感知参数====
        /// <summary>凝视距离 px</summary>
        public virtual float GazeRange => 900f;
        /// <summary>接近半径，进则 OnPlayerApproach</summary>
        public virtual float ApproachRadius => 180f;
        /// <summary>脱离半径，应大于接近</summary>
        public virtual float RetreatRadius => 320f;
        /// <summary>发现半径，完全显形时记世界进度</summary>
        public virtual float DiscoverRadius => 1200f;

        //====占位视觉====
        /// <summary>占位主色</summary>
        public virtual Color BaseColor => new(150, 160, 185);
        /// <summary>占位眼色</summary>
        public virtual Color EyeColor => new(120, 220, 200);

        //====行为与调度====
        /// <summary>组装行为积木，每实体一次新实例；默认静止</summary>
        public virtual void BuildBehaviors(List<IWraithBehavior> behaviors) { }

        /// <summary>自动显形规则，默认 null；正典走据点，仅调试件用。经 SpawnRule 缓存</summary>
        protected virtual WraithSpawnRule GetSpawnRule() => null;

        /// <summary>据点计划，默认 null；经 SitePlan 缓存</summary>
        protected virtual WraithSitePlan GetSitePlan() => null;

        private WraithSpawnRule spawnRule;
        private bool spawnRuleCreated;
        private WraithSitePlan sitePlan;
        private bool sitePlanCreated;

        /// <summary>缓存的自动显形规则</summary>
        public WraithSpawnRule SpawnRule {
            get {
                if (!spawnRuleCreated) {
                    spawnRuleCreated = true;
                    spawnRule = GetSpawnRule();
                }
                return spawnRule;
            }
        }

        /// <summary>缓存的据点计划</summary>
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

        /// <summary>赋力工厂，默认 null；全局单例无状态，冷却在 WraithPlayer</summary>
        public virtual WraithAbility CreateAbility() => null;

        /// <summary>缓存的赋力单例</summary>
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
