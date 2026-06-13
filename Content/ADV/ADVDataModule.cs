using System.Reflection;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// ADV 数据模块基类
    /// 各剧情线/功能模块继承并声明存档字段，由 ADVSave 自动发现
    /// </summary>
    public abstract class ADVDataModule
    {
        /// <summary>
        /// 存档唯一标识键，发布后勿改
        /// </summary>
        public virtual string SaveKey => GetType().Name;

        /// <summary>
        /// 将公共字段写入 TagCompound
        /// </summary>
        public TagCompound SaveFields() {
            TagCompound tag = [];
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (field.FieldType == typeof(bool)) {
                    tag[field.Name] = field.GetValue(this);
                }
                else if (field.FieldType == typeof(int)) {
                    tag[field.Name] = field.GetValue(this);
                }
            }
            return tag;
        }

        /// <summary>
        /// 从 TagCompound 加载公共字段
        /// </summary>
        public void LoadFields(TagCompound tag) {
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in fields) {
                if (field.FieldType == typeof(bool)) {
                    if (tag.TryGet(field.Name, out bool value)) {
                        field.SetValue(this, value);
                    }
                }
                else if (field.FieldType == typeof(int)) {
                    if (tag.TryGet(field.Name, out int value)) {
                        field.SetValue(this, value);
                    }
                }
            }
        }
    }
}
