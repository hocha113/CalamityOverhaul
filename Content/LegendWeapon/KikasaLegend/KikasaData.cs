using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.LegendWeapon.TrialQuests;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>鬼伞传奇成长与唤雨符数据:等级由沉宴试炼路线推进,符位表随伞存档/联机</summary>
    internal class KikasaData : LegendData
    {
        private const string InstanceIdTag = "Kikasa:InstanceId";
        private const string EditRevisionTag = "Kikasa:EditRevision";

        internal override IReadOnlyList<LegendTrialDefinition> TrialDefinitions
            => LegendTrialRouteCatalog.KikasaProgression;

        public override int TargetLevel => GetVersionedTrialTargetLevel();

        /// <summary>祈雨绳符位表，无出厂符，默认全空</summary>
        public KikasaTalismanStore Talismans { get; private set; } = new();

        /// <summary>物品编辑会话的实例身份</summary>
        internal long InstanceId { get; private set; }

        /// <summary>挂符字段修订</summary>
        internal uint EditRevision { get; private set; }

        public KikasaData() {
            InstanceId = CreateInstanceId();
        }

        public override LegendData Clone(Item item) {
            KikasaData clone = (KikasaData)base.Clone(item);
            clone.Talismans = new KikasaTalismanStore();
            clone.Talismans.CopyFrom(Talismans);
            return clone;
        }

        public static KikasaData TryGet(Item item) {
            if (item == null || item.IsAir) {
                return null;
            }
            return item.CWR()?.LegendData as KikasaData;
        }

        public override void SaveData(Item item, TagCompound tag) {
            base.SaveData(item, tag);
            tag[InstanceIdTag] = InstanceId;
            tag[EditRevisionTag] = (long)EditRevision;
            Talismans.SaveData(tag);
        }

        public override void LoadData(Item item, TagCompound tag) {
            base.LoadData(item, tag);
            InstanceId = tag.TryGet(InstanceIdTag, out long instanceId) && instanceId != 0
                ? instanceId : CreateInstanceId();
            EditRevision = tag.TryGet(EditRevisionTag, out long revision)
                && revision >= 0 && revision <= uint.MaxValue
                ? (uint)revision : 0u;
            //旧档无符数据 → LoadData 消毒后即全空，无需出厂折算
            Talismans.LoadData(tag);
        }

        public override void SendLegend(Item item, BinaryWriter writer) {
            writer.Write(InstanceId);
            writer.Write(EditRevision);
            Talismans.NetSend(writer);
        }

        public override void ReceiveLegend(Item item, BinaryReader reader) {
            long instanceId = reader.ReadInt64();
            uint editRevision = reader.ReadUInt32();
            if (instanceId == 0) {
                throw new IOException("Kikasa instance id cannot be zero");
            }
            Talismans.NetReceive(reader);
            InstanceId = instanceId;
            EditRevision = editRevision;
        }

        internal void AdvanceEditRevision() => EditRevision++;

        internal void ApplyEditRevision(uint editRevision) => EditRevision = editRevision;

        internal void RenewIdentity() {
            InstanceId = CreateInstanceId();
            EditRevision = 0;
        }

        internal void PreserveEditedStateFrom(KikasaData source) {
            if (source == null || source.InstanceId != InstanceId) {
                return;
            }
            ApplyEditedState(source.Talismans, source.EditRevision);
        }

        internal void ApplyEditedState(KikasaTalismanStore talismans, uint editRevision) {
            Talismans.CopyFrom(talismans);
            EditRevision = editRevision;
        }

        private static long CreateInstanceId() {
            long value = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            return value != 0 ? value : 1;
        }
    }
}
