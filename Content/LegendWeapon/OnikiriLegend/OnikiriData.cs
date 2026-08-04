using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>鬼切传奇成长与改铭数据。</summary>
    internal class OnikiriData : LegendData
    {
        private const string InstanceIdTag = "Onikiri:InstanceId";
        private const string EditRevisionTag = "Onikiri:EditRevision";
        private const string MeiInitTag = "OnikiriMei:Init1";

        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions
            => LegendTrialRouteCatalog.OnikiriProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        public OniMeiStore Mei { get; private set; } = new();

        /// <summary>物品编辑会话的实例身份。</summary>
        internal long InstanceId { get; private set; }

        /// <summary>改铭字段修订。</summary>
        internal uint EditRevision { get; private set; }

        public OnikiriData() {
            InstanceId = CreateInstanceId();
            SeedFactoryMei();
        }

        public override LegendData Clone(Item item) {
            OnikiriData clone = (OnikiriData)base.Clone(item);
            clone.Mei = new OniMeiStore();
            clone.Mei.CopyFrom(Mei);
            return clone;
        }

        public static OnikiriData TryGet(Item item) {
            if (item == null || item.IsAir) {
                return null;
            }
            return item.CWR()?.LegendData as OnikiriData;
        }

        private void SeedFactoryMei() {
            Mei.Clear();
            Mei.Engrave(OniMeiSlotKind.Nakago, nameof(MeiOnikiri));
        }

        public override void SaveData(Item item, TagCompound tag) {
            base.SaveData(item, tag);
            tag[InstanceIdTag] = InstanceId;
            tag[EditRevisionTag] = (long)EditRevision;
            tag[MeiInitTag] = true;
            Mei.SaveData(tag);
        }

        public override void LoadData(Item item, TagCompound tag) {
            base.LoadData(item, tag);
            InstanceId = tag.TryGet(InstanceIdTag, out long instanceId) && instanceId != 0
                ? instanceId : CreateInstanceId();
            EditRevision = tag.TryGet(EditRevisionTag, out long revision)
                && revision >= 0 && revision <= uint.MaxValue
                ? (uint)revision : 0u;
            if (tag.ContainsKey(MeiInitTag)) {
                Mei.LoadData(tag);
            }
            else {
                SeedFactoryMei();
            }
        }

        public override void SendLegend(Item item, BinaryWriter writer) {
            writer.Write(InstanceId);
            writer.Write(EditRevision);
            Mei.NetSend(writer);
        }

        public override void ReceiveLegend(Item item, BinaryReader reader) {
            long instanceId = reader.ReadInt64();
            uint editRevision = reader.ReadUInt32();
            if (instanceId == 0) {
                throw new IOException("Onikiri instance id cannot be zero");
            }
            Mei.NetReceive(reader);
            InstanceId = instanceId;
            EditRevision = editRevision;
        }

        internal void AdvanceEditRevision() => EditRevision++;

        internal void ApplyEditRevision(uint editRevision) => EditRevision = editRevision;

        internal void RenewIdentity() {
            InstanceId = CreateInstanceId();
            EditRevision = 0;
        }

        internal void PreserveEditedStateFrom(OnikiriData source) {
            if (source == null || source.InstanceId != InstanceId) {
                return;
            }
            ApplyEditedState(source.Mei, source.EditRevision);
        }

        internal void ApplyEditedState(OniMeiStore mei, uint editRevision) {
            Mei.CopyFrom(mei);
            EditRevision = editRevision;
        }

        private static long CreateInstanceId() {
            long value = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            return value != 0 ? value : 1;
        }
    }
}
