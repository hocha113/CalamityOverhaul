using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇入协议类别</summary>
    internal enum QuickHackCategory
    {
        /// <summary>致命，即时伤害</summary>
        Lethal,
        /// <summary>控制，限制行动</summary>
        Control,
        /// <summary>隐匿，干扰感知</summary>
        Covert,
        /// <summary>传播，扩散附近</summary>
        Contagion,
        /// <summary>物块操控</summary>
        TileManip,
        /// <summary>灵异，非实体</summary>
        Paranormal,
    }

    /// <summary>快速骇入协议基类，VaultType 自动注册</summary>
    internal abstract class QuickHackDef : VaultType<QuickHackDef>, ILocalizedModType
    {
        #region 静态注册表

        public static readonly Dictionary<Type, int> TypeToID = [];
        public static readonly Dictionary<int, QuickHackDef> IDToInstance = [];
        /// <summary>FullName → 实例，持久化用</summary>
        public static readonly Dictionary<string, QuickHackDef> FullNameToInstance = [];

        public static int Count => Instances.Count;

        public static T Get<T>() where T : QuickHackDef {
            if (TypeToID.TryGetValue(typeof(T), out int id)
                && IDToInstance.TryGetValue(id, out var inst)
                && inst is T t) {
                return t;
            }
            return null;
        }

        public static QuickHackDef GetByIndex(int index) {
            if (index >= 0 && index < Instances.Count)
                return Instances[index];
            return null;
        }

        /// <summary>
        /// 按 FullName 取实例。<see cref="SlotIndex"/> 是反射扫描序，插一个协议类就整表位移，
        /// 所以存档一律认 FullName，只有网络包才用索引
        /// </summary>
        public static QuickHackDef GetByFullName(string fullName) {
            if (string.IsNullOrEmpty(fullName)) {
                return null;
            }
            return FullNameToInstance.TryGetValue(fullName, out var inst) ? inst : null;
        }

        #endregion

        #region 本地化

        public string LocalizationCategory => "QuickHack";
        public LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);
        public LocalizedText Description => this.GetLocalization(nameof(Description), () => "");

        #endregion

        #region 实例属性

        /// <summary>注册序号</summary>
        public int SlotIndex { get; private set; } = -1;
        /// <summary>上传时间（帧）</summary>
        public int UploadTime { get; set; } = 60;
        /// <summary>RAM 消耗</summary>
        public int RamCost { get; set; } = 2;
        public QuickHackCategory Category { get; set; } = QuickHackCategory.Lethal;
        /// <summary>支持目标类型，可按位或</summary>
        public HackTargetKind SupportedTargets { get; set; } = HackTargetKind.Npc;
        /// <summary>出厂即持有；靠芯片解锁的协议在 SetDefaults 里设 false</summary>
        public bool UnlockedByDefault { get; set; } = true;

        #endregion

        #region VaultType 生命周期

        protected sealed override void VaultRegister() {
            Instances.Add(this);
            SlotIndex = Instances.Count - 1;
            TypeToID[GetType()] = SlotIndex;
            IDToInstance[SlotIndex] = this;
            FullNameToInstance[FullName] = this;
        }

        public override void VaultSetup() {
            //触发本地化加载
            _ = DisplayName;
            _ = Description;
            SetDefaults();
        }

        public override void Unload() {
            TypeToID.Clear();
            IDToInstance.Clear();
            FullNameToInstance.Clear();
        }

        #endregion

        #region 子类重写接口（统一目标抽象）

        public virtual void SetDefaults() { }

        /// <summary>上传完成时施加效果</summary>
        public virtual bool OnApply(IHackTarget target, Player caster) => false;

        /// <summary>远端施加表现</summary>
        public virtual void OnReplicatedApply(IHackTarget target, int elapsed) { }

        /// <summary>持续帧 Tick，追踪器调用</summary>
        public virtual bool OnTick(IHackTarget target, int elapsed) => true;

        /// <summary>远端持续表现</summary>
        public virtual void OnReplicatedTick(IHackTarget target, int elapsed) { }

        /// <summary>效果移除或到期时清理</summary>
        public virtual void OnRemove(IHackTarget target) { }

        /// <summary>远端移除表现</summary>
        public virtual void OnReplicatedRemove(IHackTarget target) { }

        /// <summary>是否可对目标使用</summary>
        public virtual bool CanApplyTo(IHackTarget target) => target != null && target.IsValid;

        /// <summary>带施法者的服务端目标校验</summary>
        public virtual bool CanApplyTo(IHackTarget target, Player caster)
            => CanApplyTo(target);

        /// <summary>持续帧数，0 为即时</summary>
        public virtual int GetDuration() => 0;

        #endregion

        #region 工具方法

        /// <summary>匹配目标种类的协议索引</summary>
        public static void GetFilteredIndices(HackTargetKind kind, List<int> result) {
            result.Clear();
            for (int i = 0; i < Instances.Count; i++) {
                if ((Instances[i].SupportedTargets & kind) != 0)
                    result.Add(i);
            }
        }

        #endregion
    }
}
