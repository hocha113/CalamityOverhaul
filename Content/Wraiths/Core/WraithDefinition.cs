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

        //====本地化（LoadLocalization 由注册表在 Mod.Load 期调用）====
        /// <summary>鬼名</summary>
        public LocalizedText DisplayName { get; private set; }
        /// <summary>来历残句</summary>
        public LocalizedText Origin { get; private set; }
        /// <summary>赋予的力</summary>
        public LocalizedText Power { get; private set; }

        internal void LoadLocalization() {
            DisplayName = this.GetLocalization("DisplayName", () => "???");
            Origin = this.GetLocalization("Origin", () => "...");
            Power = this.GetLocalization("Power", () => "...");
        }

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

        /// <summary>自动显形规则，默认 null：不自动出现，只能被外部显式生成</summary>
        public virtual WraithSpawnRule GetSpawnRule() => null;
    }
}
