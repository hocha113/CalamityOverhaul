using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    internal enum OniMeiActionKind : byte
    {
        None,
        Combo,
        Zanshin,
        Annihilate,
        Finale,
        FinaleCut,
        FlashStep,
        FlashMark,
        InscriptionSecondary,
    }

    /// <summary>
    /// 鬼切动作快照。动作出生时冻结三槽铭刻与基础武器伤害；换刀、改铭或远端鼠标物品
    /// 均不能改变已经出手的招式。父弹幕生成的附属弹幕默认继承为副伤。
    /// </summary>
    internal sealed class OniMeiActionContext : GlobalProjectile
    {
        private readonly record struct SecondaryHitKey(
            int Owner, uint ActionSerial, int ProjectileIdentity, int RootWhoAmI);

        private static uint[] nextSerial = new uint[Main.maxPlayers];
        private static readonly Dictionary<SecondaryHitKey, ulong> secondaryHits = [];
        private static ulong lastLedgerSweep;

        public override bool InstancePerEntity => true;

        public bool HasSnapshot { get; private set; }
        public bool IsSecondary { get; private set; }
        public bool IsPrimary => HasSnapshot && !IsSecondary;
        public int BaseWeaponDamage { get; private set; }
        public uint ActionSerial { get; private set; }
        public OniMeiActionKind ActionKind { get; private set; }
        public string NakagoKey { get; private set; }
        public string HiKey { get; private set; }
        public string HorimonoKey { get; private set; }
        public OniMeiCombatProfile Profile { get; private set; } = OniMeiCombatProfile.Identity;
        public float ArmedConditionMul { get; private set; } = 1f;
        public bool TideOnBeat { get; private set; }

        public static OniMeiActionContext Get(Projectile projectile)
            => projectile?.GetGlobalProjectile<OniMeiActionContext>();

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (source is EntitySource_Parent { Entity: Projectile parent }) {
                OniMeiActionContext parentContext = Get(parent);
                if (parentContext?.HasSnapshot == true) {
                    CopyFrom(parentContext, secondary: true, OniMeiActionKind.InscriptionSecondary);
                }
            }
        }

        public static void Capture(Projectile projectile, Player owner, Item item, int baseWeaponDamage,
            OniMeiActionKind actionKind) {
            if (projectile == null || owner == null) {
                return;
            }
            OniMeiActionContext context = Get(projectile);
            context.HasSnapshot = true;
            context.IsSecondary = false;
            context.BaseWeaponDamage = Math.Max(1, baseWeaponDamage);
            context.ActionSerial = NextActionSerial(owner.whoAmI);
            context.ActionKind = actionKind;
            context.ArmedConditionMul = 1f;
            context.TideOnBeat = false;
            context.ReadKeys(item);
            context.RebuildProfile();
            projectile.netUpdate = true;
        }

        public static void Capture(Projectile projectile, Player owner, IEntitySource source,
            int baseWeaponDamage, OniMeiActionKind actionKind) {
            Item item = source is EntitySource_ItemUse itemUse && itemUse.Item != null
                ? itemUse.Item
                : owner?.GetItem();
            Capture(projectile, owner, item, baseWeaponDamage, actionKind);
        }

        public static void Inherit(Projectile parent, Projectile child, bool secondary,
            OniMeiActionKind actionKind = OniMeiActionKind.None) {
            if (parent == null || child == null) {
                return;
            }
            OniMeiActionContext parentContext = Get(parent);
            if (parentContext?.HasSnapshot != true) {
                return;
            }
            Get(child).CopyFrom(parentContext, secondary,
                actionKind == OniMeiActionKind.None ? parentContext.ActionKind : actionKind);
            child.netUpdate = true;
        }

        public static uint AllocateActionSerial(Player owner)
            => owner == null ? 0 : NextActionSerial(owner.whoAmI);

        public void ArmCondition(float multiplier, bool tideOnBeat) {
            ArmedConditionMul = Math.Max(1f, multiplier);
            TideOnBeat = tideOnBeat;
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target) {
            if (!HasSnapshot || !IsSecondary || target == null) {
                return null;
            }
            int root = OniMeiCombat.ResolveEffectRoot(target).whoAmI;
            return secondaryHits.ContainsKey(new SecondaryHitKey(
                projectile.owner, ActionSerial, projectile.identity, root))
                ? false
                : null;
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!HasSnapshot || !IsSecondary || target == null) {
                return;
            }
            int root = OniMeiCombat.ResolveEffectRoot(target).whoAmI;
            secondaryHits[new SecondaryHitKey(
                projectile.owner, ActionSerial, projectile.identity, root)] = Main.GameUpdateCount;
            SweepLedger();
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bitWriter.WriteBit(HasSnapshot);
            if (!HasSnapshot) {
                return;
            }
            bitWriter.WriteBit(IsSecondary);
            bitWriter.WriteBit(TideOnBeat);
            binaryWriter.Write((byte)ActionKind);
            binaryWriter.Write(ActionSerial);
            binaryWriter.Write(BaseWeaponDamage);
            binaryWriter.Write(ArmedConditionMul);
            binaryWriter.Write(NakagoKey ?? string.Empty);
            binaryWriter.Write(HiKey ?? string.Empty);
            binaryWriter.Write(HorimonoKey ?? string.Empty);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) {
            HasSnapshot = bitReader.ReadBit();
            if (!HasSnapshot) {
                ResetSnapshot();
                return;
            }
            IsSecondary = bitReader.ReadBit();
            TideOnBeat = bitReader.ReadBit();
            ActionKind = (OniMeiActionKind)binaryReader.ReadByte();
            ActionSerial = binaryReader.ReadUInt32();
            BaseWeaponDamage = Math.Max(1, binaryReader.ReadInt32());
            ArmedConditionMul = Math.Max(1f, binaryReader.ReadSingle());
            NakagoKey = EmptyToNull(binaryReader.ReadString());
            HiKey = EmptyToNull(binaryReader.ReadString());
            HorimonoKey = EmptyToNull(binaryReader.ReadString());
            RebuildProfile();
        }

        public override void Unload() {
            nextSerial = null;
            secondaryHits.Clear();
            lastLedgerSweep = 0;
        }

        private void CopyFrom(OniMeiActionContext source, bool secondary, OniMeiActionKind actionKind) {
            HasSnapshot = true;
            IsSecondary = secondary;
            BaseWeaponDamage = source.BaseWeaponDamage;
            ActionSerial = source.ActionSerial;
            ActionKind = actionKind;
            NakagoKey = source.NakagoKey;
            HiKey = source.HiKey;
            HorimonoKey = source.HorimonoKey;
            Profile = source.Profile;
            ArmedConditionMul = source.ArmedConditionMul;
            TideOnBeat = source.TideOnBeat;
        }

        private void ReadKeys(Item item) {
            OniMeiStore store = OnikiriData.TryGet(item)?.Mei;
            NakagoKey = store?.Get(OniMeiSlotKind.Nakago);
            HiKey = store?.Get(OniMeiSlotKind.Hi);
            HorimonoKey = store?.Get(OniMeiSlotKind.Horimono);
        }

        private void RebuildProfile()
            => Profile = OniMeiCombat.Resolve(NakagoKey, HiKey, HorimonoKey);

        private void ResetSnapshot() {
            IsSecondary = false;
            BaseWeaponDamage = 0;
            ActionSerial = 0;
            ActionKind = OniMeiActionKind.None;
            NakagoKey = HiKey = HorimonoKey = null;
            Profile = OniMeiCombatProfile.Identity;
            ArmedConditionMul = 1f;
            TideOnBeat = false;
        }

        private static uint NextActionSerial(int owner) {
            nextSerial ??= new uint[Main.maxPlayers];
            if (owner < 0 || owner >= nextSerial.Length) {
                return 1;
            }
            uint serial = ++nextSerial[owner];
            if (serial == 0) {
                serial = ++nextSerial[owner];
            }
            return serial;
        }

        private static string EmptyToNull(string value)
            => string.IsNullOrEmpty(value) ? null : value;

        private static void SweepLedger() {
            ulong now = Main.GameUpdateCount;
            if (now - lastLedgerSweep < 120) {
                return;
            }
            lastLedgerSweep = now;
            foreach (SecondaryHitKey key in new List<SecondaryHitKey>(secondaryHits.Keys)) {
                if (now - secondaryHits[key] > 600) {
                    secondaryHits.Remove(key);
                }
            }
        }
    }
}
