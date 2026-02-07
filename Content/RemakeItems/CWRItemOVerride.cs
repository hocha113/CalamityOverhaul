using CalamityOverhaul.Common;
using InnoVault.GameSystem;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems
{
    internal abstract class CWRItemOverride : ItemOverride
    {
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
            HandlerCanOverride.CanOverrideByID.Add(TargetID, true);
            AfterLoadenContent();
        }

        public sealed override bool CanOverride() {
            bool? result = CanCWROverride();
            if (result.HasValue) {
                return result.Value;
            }

            if (!CWRServerConfig.Instance.WeaponOverhaul) {
                return false;//若全局配置未启用，则直接返回false
            }

            if (HandlerCanOverride.CanLoad) {//若启用了兜底加载器，则尝试获取兜底判断
                return HandlerCanOverride.CanOverrideByID[TargetID];
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
