using CalamityOverhaul.Common;
using InnoVault.GameSystem;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal abstract class CWRItemOverride : ItemOverride
    {
        /// <summary>
        /// 是否受到修改实例的影响，在<see cref="CWRServerConfig.Instance.ModifiIntercept"/>启用后生效
        /// </summary>
        public static Dictionary<int, bool> CanOverrideByID { get; internal set; } = [];

        public override int TargetID => GetCalItemID(Name[1..]);

        /// <summary>
        /// 获取来自灾厄的物品名
        /// </summary>
        /// <param name="itemKey"></param>
        /// <returns></returns>
        public static string GetCalItem(string itemKey) => $"CalamityMod/{itemKey}";

        /// <summary>
        /// 获取来自灾厄的物品ID
        /// </summary>
        /// <param name="itemKey"></param>
        /// <returns></returns>
        public static int GetCalItemID(string itemKey) => VaultUtils.GetItemTypeFromFullName(GetCalItem(itemKey));

        public sealed override void PostSetStaticDefaults() {
            CanOverrideByID.Add(TargetID, true);
            AfterLoadenContent();
        }

        public sealed override bool CanOverride() {
            bool? result = CanCWROverride();
            if (result.HasValue) {
                return result.Value;
            }

            //检查单个武器的覆写开关，若字典中对应值为false则禁用该武器的修改
            if (CanOverrideByID.TryGetValue(TargetID, out bool canOverride) && !canOverride) {
                return false;
            }

            return true;
        }

        public virtual bool? CanCWROverride() {
            return null;
        }

        public virtual void AfterLoadenContent() {

        }
    }
}
