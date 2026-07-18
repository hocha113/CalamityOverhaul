using CalamityOverhaul.Content.Wraiths.Core;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 点鬼簿数据源适配器：厉鬼框架（<see cref="WraithRegistry"/>）到 <see cref="IOniGhostSource"/>。
    /// 名录与文案自框架取，绑定数据（驾驭度/铭刻态）在正式绑定层落地前由本类的占位表提供，
    /// 三屏 UI 与框架之间只隔这一层
    /// </summary>
    internal sealed class OniWraithSource : IOniGhostSource, ICWRLoader
    {
        //躁动推导阈值,与 OniRegistry.InDanger 的总驾驭阈值同源
        private const float RestlessThreshold = 0.35f;
        //空悬铭位数量:簿面留白,等新鬼上簿
        private const int VacantSlots = 2;

        //占位绑定数据(键→驾驭度):绑定层(挂 LegendData)落地后整表删除
        private static readonly Dictionary<string, float> placeholderMastery = new() {
            ["NoFace"] = 0.86f,
            ["LanternBoy"] = 0.58f,
            ["CrimsonBride"] = 0.16f,
            ["StandIn"] = 0.77f,
            ["HeadlessShade"] = 0.28f,
            ["GhostHand"] = 0.45f,
        };

        private static readonly List<OniGhostEntry> entries = [];

        public IReadOnlyList<OniGhostEntry> Entries => entries;

        void ICWRLoader.SetupData() {
            //PostSetupContent 期组装:注册表(Mod.Load 期)已就绪,文本经惰性取值不吃时序
            entries.Clear();
            foreach (WraithDefinition definition in WraithRegistry.All) {
                if (definition.HiddenFromCatalog) {
                    continue;
                }
                entries.Add(BuildEntry(definition));
            }
            for (int i = 0; i < VacantSlots; i++) {
                entries.Add(new OniGhostEntry { Key = $"Vacant{i}", State = OniGhostState.Unknown });
            }
            OniRegistry.SetSource(this);
        }

        void ICWRLoader.UnLoadData() {
            OniRegistry.SetSource(null);
            entries.Clear();
        }

        private static OniGhostEntry BuildEntry(WraithDefinition definition) {
            OniGhostEntry entry = new() {
                Key = definition.Key,
                Name = () => definition.DisplayName.Value,
            };

            if (definition.InitialBindState == WraithBindState.Sealed) {
                //封印:名讳可见,来历赋力糊住;文案走簿面的封印提示而非定义本体
                entry.State = OniGhostState.Sealed;
                entry.Origin = () => OniRegisterUI.SealedOriginHint.Value;
                entry.Power = () => OniRegisterUI.SealedPowerHint.Value;
                return entry;
            }

            if (placeholderMastery.TryGetValue(definition.Key, out float mastery)) {
                //占位期视为已铭刻;躁动由驾驭度推导,不做独立存储
                entry.State = mastery < RestlessThreshold ? OniGhostState.Restless : OniGhostState.Engraved;
                entry.Mastery = mastery;
                entry.Origin = () => definition.Origin.Value;
                entry.Power = () => definition.Power.Value;
                return entry;
            }

            //未铭刻(含 Discovered):簿面暂无对应视觉,先按空悬呈现
            entry.State = OniGhostState.Unknown;
            return entry;
        }
    }
}
