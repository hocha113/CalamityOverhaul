using CalamityOverhaul.Content.Wraiths.Core;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 点鬼簿数据源适配器：读取本地玩家手中鬼切的 <see cref="OnikiriData"/> 绑定进度，
    /// 映射为簿面条目。名录与文案自 <see cref="WraithRegistry"/> 取，
    /// 绑定数据（状态/驾驭度）自刀上的 <see cref="WraithProgressStore"/> 取；
    /// 以 (数据引用, 版本号) 做脏检查，进度变更当帧生效且无逐帧重建开销
    /// </summary>
    internal sealed class OniWraithSource : IOniGhostSource, ICWRLoader
    {
        //躁动推导阈值,与 OniRegistry.InDanger 的总驾驭阈值同源
        private const float RestlessThreshold = 0.35f;
        //空悬铭位数量:簿面留白,等新鬼上簿
        private const int VacantSlots = 2;

        private static readonly List<OniGhostEntry> entries = [];
        private static OnikiriData cachedData;
        private static int cachedVersion = -1;

        public IReadOnlyList<OniGhostEntry> Entries {
            get {
                TryRefresh();
                return entries;
            }
        }

        void ICWRLoader.SetupData() => OniRegistry.SetSource(this);

        void ICWRLoader.UnLoadData() {
            OniRegistry.SetSource(null);
            entries.Clear();
            cachedData = null;
            cachedVersion = -1;
        }

        /// <summary>本地玩家当前手持鬼切的数据，服务器/菜单/未持刀为 null</summary>
        private static OnikiriData ResolveLocalData() {
            if (Main.dedServ || Main.gameMenu) {
                return null;
            }
            return OnikiriData.TryGet(Main.LocalPlayer?.GetItem());
        }

        private static void TryRefresh() {
            OnikiriData data = ResolveLocalData();
            if (data == null) {
                //未持刀时保留上一份名录:封印札淡出期间读数不跳零
                return;
            }
            int version = data.Wraiths.Version;
            if (ReferenceEquals(data, cachedData) && version == cachedVersion) {
                return;
            }
            cachedData = data;
            cachedVersion = version;
            Rebuild(data.Wraiths);
        }

        private static void Rebuild(WraithProgressStore store) {
            entries.Clear();
            foreach (WraithDefinition definition in WraithRegistry.All) {
                if (definition.HiddenFromCatalog) {
                    continue;
                }
                store.TryGet(definition.Key, out WraithProgressRecord record);
                entries.Add(BuildEntry(definition, record));
            }
            for (int i = 0; i < VacantSlots; i++) {
                entries.Add(new OniGhostEntry { Key = $"Vacant{i}", State = OniGhostState.Unknown });
            }
        }

        private static OniGhostEntry BuildEntry(WraithDefinition definition, WraithProgressRecord record) {
            OniGhostEntry entry = new() {
                Key = definition.Key,
                Name = () => definition.DisplayName.Value,
            };

            switch (record?.State ?? WraithBindState.Unknown) {
                case WraithBindState.Sealed:
                    //封印:名讳可见,来历赋力糊住;文案走簿面的封印提示而非定义本体
                    entry.State = OniGhostState.Sealed;
                    entry.Origin = () => OniRegisterUI.SealedOriginHint.Value;
                    entry.Power = () => OniRegisterUI.SealedPowerHint.Value;
                    break;
                case WraithBindState.Bound:
                    //躁动由驾驭度推导,不做独立存储
                    entry.State = record.Mastery < RestlessThreshold ? OniGhostState.Restless : OniGhostState.Engraved;
                    entry.Mastery = record.Mastery;
                    entry.Origin = () => definition.Origin.Value;
                    entry.Power = () => definition.Power.Value;
                    break;
                default:
                    //Unknown 与 Discovered:簿面暂无"已发现未铭刻"的视觉,先按空悬呈现
                    entry.State = OniGhostState.Unknown;
                    break;
            }
            return entry;
        }
    }
}
