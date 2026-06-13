using System;
using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// ADV 数据聚合器，自动管理 <see cref="ADVDataModule"/> 子类
    /// </summary>
    public class ADVSave
    {
        internal const string VersionKey = "__version";
        private const int CurrentVersion = 2;

        private readonly Dictionary<Type, ADVDataModule> _modules = [];
        private readonly Dictionary<string, ADVDataModule> _modulesByKey = [];

        public ADVSave() {
            List<ADVDataModule> dataModules = VaultUtils.GetDerivedInstances<ADVDataModule>();
            foreach (var module in dataModules) {
                if (_modulesByKey.TryGetValue(module.SaveKey, out ADVDataModule value)) {
                    throw new Exception($"ADVDataModule SaveKey conflict: '{module.SaveKey}' " + $"(Type {module.GetType().Name} vs {value.GetType().Name})");
                }
                _modules[module.GetType()] = module;
                _modulesByKey[module.SaveKey] = module;
            }
        }

        /// <summary>
        /// 获取指定类型的数据模块
        /// </summary>
        public T Get<T>() where T : ADVDataModule {
            return (T)_modules[typeof(T)];
        }

        /// <summary>
        /// 枚举所有已注册的ADV数据模块
        /// </summary>
        public IEnumerable<ADVDataModule> AllModules => _modules.Values;

        public virtual TagCompound SaveData() {
            TagCompound tag = [];
            tag[VersionKey] = CurrentVersion;
            foreach (var module in _modules.Values) {
                tag[module.SaveKey] = module.SaveFields();
            }
            return tag;
        }

        public virtual void LoadData(TagCompound tag) {
            // v0/v1 扁平格式走 ADVLegacyMigration
            if (ADVLegacyMigration.TryLoadFromFlatFormat(tag, _modules.Values)) {
                return;
            }
            // v2：按 SaveKey 读子 TagCompound
            foreach (var module in _modules.Values) {
                if (tag.TryGet<TagCompound>(module.SaveKey, out var moduleTag)) {
                    module.LoadFields(moduleTag);
                }
            }
        }

        /// <summary>
        /// 创建当前ADVSave的深拷贝（通过序列化往返实现，所有模块数据独立）
        /// </summary>
        public ADVSave DeepCopy() {
            var copy = new ADVSave();
            copy.LoadData(SaveData());
            return copy;
        }

    }
}
