using System.Collections.Generic;
using System.Linq;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠框架装配器：加载期反射扫描全部 <see cref="GodSmithArmorScheme"/> 子类实例化并注册本地化，
    /// 建胸甲索引、接管方案的头盔索引与单件索引；
    /// 卸载期清空框架全部静态注册表（武器方案表由 InnoVault 生命周期驱动，仅镜像表在此清理）
    /// </summary>
    internal class GodSmithLoader : ICWRLoader
    {
        void ICWRLoader.LoadData() {
            List<GodSmithArmorScheme> found = VaultUtils.GetDerivedInstances<GodSmithArmorScheme>();
            //按全名排序保证注册顺序确定性
            GodSmithArmorScheme.Schemes = [.. found.OrderBy(scheme => scheme.FullName)];
            GodSmithArmorScheme.SchemesByBody = [];
            GodSmithArmorScheme.SchemeByHead = [];
            GodSmithArmorScheme.SchemeByPiece = [];
            foreach (GodSmithArmorScheme scheme in GodSmithArmorScheme.Schemes) {
                scheme.Load();
                foreach (int body in scheme.BodyIDs) {
                    if (!GodSmithArmorScheme.SchemesByBody.TryGetValue(body, out var list)) {
                        GodSmithArmorScheme.SchemesByBody[body] = list = [];
                    }
                    list.Add(scheme);
                }
                if (!scheme.OverridesVanilla) {
                    continue;
                }
                //接管方案：头盔与全部单件各登记一份，重复认领只记日志不中断加载
                foreach (int head in scheme.HeadIDs) {
                    Claim(GodSmithArmorScheme.SchemeByHead, head, scheme);
                    Claim(GodSmithArmorScheme.SchemeByPiece, head, scheme);
                }
                foreach (int body in scheme.BodyIDs) {
                    Claim(GodSmithArmorScheme.SchemeByPiece, body, scheme);
                }
                foreach (int legs in scheme.LegsIDs) {
                    Claim(GodSmithArmorScheme.SchemeByPiece, legs, scheme);
                }
            }
        }

        private static void Claim(Dictionary<int, GodSmithArmorScheme> table, int itemType, GodSmithArmorScheme scheme) {
            if (table.TryAdd(itemType, scheme)) {
                return;
            }
            CWRMod.Instance.Logger.Error(
                $"[GodSmith] 盔甲单件 {itemType} 被重复接管：{table[itemType].FullName} 与 {scheme.FullName}，前者生效");
        }

        void ICWRLoader.UnLoadData() {
            GodSmithArmorScheme.Schemes = [];
            GodSmithArmorScheme.SchemesByBody = [];
            GodSmithArmorScheme.SchemeByHead = [];
            GodSmithArmorScheme.SchemeByPiece = [];
            GodSmithScheme.ClearRegistry();
            GodSmithProjRouter.ClearRegistry();
        }
    }
}
