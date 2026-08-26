using System.Collections.Generic;
using System.Linq;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠框架装配器：加载期反射扫描全部 <see cref="GodSmithArmorScheme"/> 子类实例化并注册本地化；
    /// 卸载期清空框架全部静态注册表（武器方案表由 InnoVault 生命周期驱动，仅镜像表在此清理）
    /// </summary>
    internal class GodSmithLoader : ICWRLoader
    {
        void ICWRLoader.LoadData() {
            List<GodSmithArmorScheme> found = VaultUtils.GetDerivedInstances<GodSmithArmorScheme>();
            //按全名排序保证注册顺序确定性
            GodSmithArmorScheme.Schemes = [.. found.OrderBy(scheme => scheme.FullName)];
            GodSmithArmorScheme.SchemesByBody = [];
            foreach (GodSmithArmorScheme scheme in GodSmithArmorScheme.Schemes) {
                scheme.Load();
                if (!GodSmithArmorScheme.SchemesByBody.TryGetValue(scheme.BodyID, out var list)) {
                    GodSmithArmorScheme.SchemesByBody[scheme.BodyID] = list = [];
                }
                list.Add(scheme);
            }
        }

        void ICWRLoader.UnLoadData() {
            GodSmithArmorScheme.Schemes = [];
            GodSmithArmorScheme.SchemesByBody = [];
            GodSmithScheme.ClearRegistry();
            GodSmithProjRouter.ClearRegistry();
        }
    }
}
