using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 点鬼簿数据源适配器 + 厉鬼框架的鬼切侧接线。<br/>
    /// 簿面：读取本地玩家手中鬼切的 <see cref="OnikiriData"/> 绑定进度映射为条目，
    /// 名录与文案自 <see cref="WraithRegistry"/> 取，绑定数据自刀上的 <see cref="WraithProgressStore"/> 取，
    /// 以 (数据引用, 版本号) 做脏检查，进度变更当帧生效且无逐帧重建开销。<br/>
    /// 接线：向 <see cref="WraithVessels"/> 注册载体解析（框架不认识鬼切类型，全靠这里），
    /// 向 <see cref="WraithRites"/> 挂铭刻仪式演出与演出忙判定
    /// </summary>
    internal sealed class OniWraithSource : IOniGhostSource, ICWRLoader
    {
        //空悬铭位数量:簿面留白,等新鬼上簿
        private const int VacantSlots = 2;

        private static readonly List<OniGhostEntry> entries = [];
        private static OnikiriData cachedData;
        private static int cachedVersion = -1;
        //目录组成随调试闹鬼闸变(试件临时上目录),纳入脏检查
        private static bool cachedDebugVisible;

        public IReadOnlyList<OniGhostEntry> Entries {
            get {
                TryRefresh();
                return entries;
            }
        }

        void ICWRLoader.SetupData() {
            OniRegistry.SetSource(this);
            //载体解析缝:手持=仪式与借力门控,随身=反噬判定(刀在身上,鬼就在身边)
            WraithVessels.Register(ResolveHeldVessel, ResolveCarriedVessel);
            //仪式演出:数据已由 WraithRites 先行落簿,这里只负责铭刻弹窗;演出播放中不受理新的借力键
            WraithRites.RitePresenter = PresentRite;
            WraithRites.PresentationBusy = static ()
                => (OniEngraveRiteUI.Instance?.Active ?? false) || (OniRegisterUI.Instance?.IsOpen ?? false);
        }

        void ICWRLoader.UnLoadData() {
            OniRegistry.SetSource(null);
            WraithVessels.Clear();
            WraithRites.RitePresenter = null;
            WraithRites.PresentationBusy = null;
            entries.Clear();
            cachedData = null;
            cachedVersion = -1;
        }

        //====载体解析与仪式演出====

        /// <summary>手持解析：HeldItem 对本地玩家含鼠标项、对远端玩家取所选格，两端语义都对</summary>
        private static WraithVesselHandle ResolveHeldVessel(Player player) {
            Item item = player.HeldItem;
            OnikiriData data = OnikiriData.TryGet(item);
            return data == null ? default : new WraithVesselHandle(item, data.Wraiths);
        }

        /// <summary>随身解析：手中优先，背包（含钱币/弹药格）兜底</summary>
        private static WraithVesselHandle ResolveCarriedVessel(Player player) {
            WraithVesselHandle held = ResolveHeldVessel(player);
            if (held.IsValid) {
                return held;
            }
            foreach (Item item in player.inventory) {
                OnikiriData data = OnikiriData.TryGet(item);
                if (data != null) {
                    return new WraithVesselHandle(item, data.Wraiths);
                }
            }
            return default;
        }

        /// <summary>仪式演出：读回刚落簿的记录组一份簿面条目，连同语义交给铭刻仪式弹窗补演</summary>
        private static void PresentRite(WraithDefinition definition, WraithRiteKind kind) {
            WraithVesselHandle vessel = WraithVessels.ResolveHeld(Main.LocalPlayer);
            if (!vessel.IsValid) {
                vessel = WraithVessels.ResolveCarried(Main.LocalPlayer);
            }
            if (!vessel.IsValid || !vessel.Store.TryGet(definition.Key, out WraithProgressRecord record)) {
                return;
            }
            OniEngraveRiteUI.Play(BuildEntry(definition, record), kind);
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
            bool debugVisible = WraithDirector.DebugHauntEnabled;
            if (ReferenceEquals(data, cachedData) && version == cachedVersion && debugVisible == cachedDebugVisible) {
                return;
            }
            cachedData = data;
            cachedVersion = version;
            cachedDebugVisible = debugVisible;
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
                    //躁动由驾驭度推导,不做独立存储;阈值与反噬判定同源
                    entry.State = record.Mastery < WraithDefinition.RestlessThreshold ? OniGhostState.Restless : OniGhostState.Engraved;
                    entry.Mastery = record.Mastery;
                    //簿面按演示期原貌呈现:Bound 即见来历与赋力。
                    //残页门控(认主叙事)已按用户钦定撤下——PactRenewed 仍随仪式落档,但不再影响簿面
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
