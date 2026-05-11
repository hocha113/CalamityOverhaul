using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇入目标种类工厂基类
    /// <br/>每加入一个新的可骇入目标种类（如电梯、传送门等），只需新建一个继承自此类的子类，无需改动任何分发逻辑
    /// <br/>通过 VaultType 自动注册，加载时按 <see cref="VaultType{T}.Instances"/> 顺序分配 <see cref="SlotIndex"/>
    /// <br/>面板过滤所用的 <see cref="HackTargetKind"/> 由子类的 <see cref="Kind"/> 属性给出，二者一一对应
    /// </summary>
    internal abstract class HackTargetType : VaultType<HackTargetType>, ILocalizedModType
    {
        #region 静态注册表
        /// <summary>
        /// 稳定字符串 ID（FullName） → 实例
        /// <br/>FullName 由 ModName/Name 组成，用于持久化场景下保持稳定
        /// </summary>
        public static readonly Dictionary<string, HackTargetType> FullNameToInstance = [];
        /// <summary>
        /// HackTargetKind → 实例
        /// </summary>
        public static readonly Dictionary<HackTargetKind, HackTargetType> KindToInstance = [];

        /// <summary>
        /// 已注册的目标种类总数
        /// </summary>
        public static int Count => Instances.Count;

        public static T Get<T>() where T : HackTargetType {
            if (TypeToInstance.TryGetValue(typeof(T), out var inst) && inst is T t) return t;
            return null;
        }

        public static HackTargetType GetByKind(HackTargetKind kind) {
            return KindToInstance.TryGetValue(kind, out var inst) ? inst : null;
        }

        public static HackTargetType GetByFullName(string fullName) {
            return FullNameToInstance.TryGetValue(fullName, out var inst) ? inst : null;
        }

        #endregion

        #region 本地化

        public string LocalizationCategory => "HackTargetType";
        public LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

        #endregion

        #region 实例属性

        /// <summary>
        /// 注册序号（按加载顺序自增）
        /// </summary>
        public int SlotIndex { get; private set; } = -1;

        /// <summary>
        /// 该目标种类对应的过滤位标志
        /// <br/>用于 <see cref="QuickHackDef.SupportedTargets"/> 的位运算过滤
        /// </summary>
        public abstract HackTargetKind Kind { get; }

        /// <summary>
        /// 悬停优先级，数值越大越优先被选中
        /// <br/>例如 NPC 应高于物块，因为 NPC 体积通常压在物块格子上
        /// </summary>
        public virtual int HoverPriority => 0;

        #endregion

        #region VaultType 生命周期

        protected sealed override void VaultRegister() {
            Instances.Add(this);
            SlotIndex = Instances.Count - 1;
            TypeToInstance[GetType()] = this;
            FullNameToInstance[FullName] = this;
            KindToInstance[Kind] = this;
        }

        public override void VaultSetup() {
            _ = DisplayName;
            SetStaticDefaults();
        }

        public override void Unload() {
            TypeToInstance.Clear();
            FullNameToInstance.Clear();
            KindToInstance.Clear();
        }

        #endregion

        #region 子类重写接口
        /// <summary>
        /// 在指定的鼠标世界坐标处尝试探测一个该种类的可骇入目标
        /// <br/>返回 null 表示当前鼠标下无此种类目标
        /// </summary>
        public abstract IHackTarget TryDetectHovered(Vector2 mouseWorld);

        /// <summary>
        /// 选中该目标后的反馈钩子（如音效、震屏等）
        /// <br/>默认播放骇客时间通用音效
        /// </summary>
        public virtual void OnSelectFeedback(IHackTarget target) {
            if (!VaultUtils.isServer) {
                Terraria.Audio.SoundEngine.PlaySound(Common.CWRSound.Hacker);
            }
        }

        #endregion

        #region 全局调度

        /// <summary>
        /// 遍历所有已注册的目标种类，按 <see cref="HoverPriority"/> 从高到低尝试探测
        /// <br/>返回最高优先级的命中结果，若无任何种类命中则返回 null
        /// </summary>
        public static IHackTarget DetectTopmostHover(Vector2 mouseWorld) {
            IHackTarget bestHit = null;
            int bestPriority = int.MinValue;

            for (int i = 0; i < Instances.Count; i++) {
                var t = Instances[i];
                if (t.HoverPriority < bestPriority) continue;
                var hit = t.TryDetectHovered(mouseWorld);
                if (hit == null) continue;
                if (t.HoverPriority > bestPriority) {
                    bestPriority = t.HoverPriority;
                    bestHit = hit;
                }
            }
            return bestHit;
        }

        #endregion
    }
}
