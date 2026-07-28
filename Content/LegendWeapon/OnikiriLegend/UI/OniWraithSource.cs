using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 点鬼簿适配器+厉鬼接线.
    /// 簿面映 <see cref="OnikiriData"/>;(引用,版本)脏检;
    /// 注册 <see cref="WraithVessels"/> / <see cref="WraithRites"/> 演出
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

        public string AttunedKey {
            get {
                TryRefresh();
                return cachedData?.Wraiths.AttunedKey ?? string.Empty;
            }
        }

        public bool TryAttune(string key) {
            OnikiriData data = ResolveLocalData();
            if (data == null || !data.Wraiths.TryAttune(key)) {
                return false;
            }
            WraithVessels.SyncSlot(Main.LocalPlayer, Main.LocalPlayer.GetItem());
            TryRefresh();
            return true;
        }

        void ICWRLoader.SetupData() {
            OniRegistry.SetSource(this);
            //载体解析缝:手持=仪式与借力门控,随身=反噬判定(刀在身上,鬼就在身边)
            WraithVessels.Register(ResolveHeldVessel, ResolveCarriedVessel);
            //数据已由 WraithRites 落簿,这里弹铭刻窗;演出中不受理借力;改铭台开着也算忙
            WraithRites.RitePresenter = PresentRite;
            WraithRites.PresentationBusy = static ()
                => (OniEngraveRiteUI.Instance?.Active ?? false) || (OniRegisterUI.Instance?.IsOpen ?? false)
                || (OniMeiUI.Instance?.IsOpen ?? false);
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

        /// <summary>手持解析,本地含鼠标项</summary>
        private static WraithVesselHandle ResolveHeldVessel(Player player) {
            Item item = player.HeldItem;
            OnikiriData data = OnikiriData.TryGet(item);
            return data == null ? default : new WraithVesselHandle(item, data.Wraiths);
        }

        /// <summary>随身解析,手中优先背包兜底</summary>
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

        /// <summary>读刚落簿记录交铭刻弹窗</summary>
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

        /// <summary>本地持刀数据,服务器/菜单/未持 null</summary>
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
                    entry.CanAttune = definition.Ability != null && WraithDirector.ContentActiveFor(definition);
                    //Bound 即见来历赋力;PactRenewed 仍落档但不挡簿面
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
