using System;
using System.Collections.Generic;
using System.IO;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps;
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

        private readonly record struct SecondaryBudgetKey(
            int Owner, uint ActionSerial, int RootWhoAmI);

        private static uint[] nextSerial = new uint[Main.maxPlayers];
        private static readonly Dictionary<SecondaryHitKey, ulong> secondaryHits = [];
        private static readonly Dictionary<SecondaryBudgetKey, (float Used, ulong Tick)> secondaryBudgets = [];
        private static ulong lastLedgerSweep;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
            => lateInstantiation && (entity.friendly
                || entity.ModProjectile is OniFlashStep or OniFinaleSlash);

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

        public static OniMeiActionContext Get(Projectile projectile) {
            return projectile != null
                && projectile.TryGetGlobalProjectile(out OniMeiActionContext context)
                ? context
                : null;
        }

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
            if (projectile == null || owner == null
                || !projectile.TryGetGlobalProjectile(out OniMeiActionContext context)) {
                return;
            }
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
            if (parent == null || child == null
                || !parent.TryGetGlobalProjectile(out OniMeiActionContext parentContext)
                || parentContext.HasSnapshot != true
                || !child.TryGetGlobalProjectile(out OniMeiActionContext childContext)) {
                return;
            }
            childContext.CopyFrom(parentContext, secondary,
                actionKind == OniMeiActionKind.None ? parentContext.ActionKind : actionKind);
            child.netUpdate = true;
        }

        public static uint AllocateActionSerial(Player owner)
            => owner == null ? 0 : NextActionSerial(owner.whoAmI);

        /// <summary>
        /// The combo controller persists across all five beats. Each fired beat receives a fresh
        /// action serial while retaining the controller's immutable inscription and damage snapshot.
        /// </summary>
        public static void BeginSubAction(Projectile projectile, Player owner,
            OniMeiActionKind actionKind) {
            OniMeiActionContext context = Get(projectile);
            if (context?.HasSnapshot != true || owner == null) {
                return;
            }
            context.IsSecondary = false;
            context.ActionSerial = NextActionSerial(owner.whoAmI);
            context.ActionKind = actionKind;
            context.ArmedConditionMul = 1f;
            context.TideOnBeat = false;
            projectile.netUpdate = true;
        }

        /// <summary>Consumes action-start conditions once, including attacks that later miss.</summary>
        public static void ArmConditions(Projectile projectile, Player owner,
            bool allowSilent, bool allowPlanted) {
            OniMeiActionContext context = Get(projectile);
            if (context?.HasSnapshot != true || owner == null || owner.whoAmI != Main.myPlayer) {
                return;
            }
            OnikiriPlayer onikiri = owner.GetModPlayer<OnikiriPlayer>();
            OniMeiCombatProfile profile = context.Profile;
            float multiplier = onikiri.ArmMeiAction(
                in profile, allowSilent, allowPlanted);
            bool tideOnBeat = profile.TideBeat
                && OniMeiCombat.IsTideOnBeat(onikiri.TidePhaseTicks);
            context.ArmCondition(multiplier, tideOnBeat);
            projectile.netUpdate = true;
        }

        public void ArmCondition(float multiplier, bool tideOnBeat) {
            ArmedConditionMul = Math.Max(1f, multiplier);
            TideOnBeat = tideOnBeat;
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target) {
            if (!HasSnapshot || !IsSecondary || target == null || UsesOwnHitLedger(projectile)) {
                return null;
            }
            NPC resolved = OniMeiCombat.ResolveEffectRoot(target);
            if (resolved == null) {
                return null;
            }
            int root = resolved.whoAmI;
            if (secondaryBudgets.TryGetValue(
                new SecondaryBudgetKey(projectile.owner, ActionSerial, root), out var budget)
                && budget.Used >= 0.9999f) {
                return false;
            }
            return secondaryHits.ContainsKey(new SecondaryHitKey(
                projectile.owner, ActionSerial, projectile.identity, root))
                ? false
                : null;
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target,
            ref NPC.HitModifiers modifiers) {
            if (!HasSnapshot || !IsSecondary || target == null || BaseWeaponDamage <= 0
                || UsesOwnHitLedger(projectile)) {
                return;
            }
            NPC resolved = OniMeiCombat.ResolveEffectRoot(target);
            if (resolved == null) {
                return;
            }

            SecondaryBudgetKey key = new(projectile.owner, ActionSerial, resolved.whoAmI);
            float used = secondaryBudgets.TryGetValue(key, out var entry) ? entry.Used : 0f;
            float requested = Math.Max(projectile.damage / (float)BaseWeaponDamage, 0f);
            float allowed = Math.Min(requested, Math.Max(0f, 1f - used));
            if (requested > 0.0001f && allowed < requested) {
                modifiers.FinalDamage *= allowed / requested;
            }
            secondaryBudgets[key] = (Math.Min(1f, used + allowed), Main.GameUpdateCount);
            SweepLedger();
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!HasSnapshot || !IsSecondary || target == null || UsesOwnHitLedger(projectile)) {
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
            binaryWriter.Write(ActionSerial);
            binaryWriter.Write(BaseWeaponDamage);
            binaryWriter.Write(ArmedConditionMul);
            binaryWriter.Write(GetNetworkId(NakagoKey, OniMeiSlotKind.Nakago));
            binaryWriter.Write(GetNetworkId(HiKey, OniMeiSlotKind.Hi));
            binaryWriter.Write(GetNetworkId(HorimonoKey, OniMeiSlotKind.Horimono));
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) {
            HasSnapshot = bitReader.ReadBit();
            if (!HasSnapshot) {
                ResetSnapshot();
                return;
            }
            IsSecondary = bitReader.ReadBit();
            TideOnBeat = bitReader.ReadBit();
            ActionSerial = binaryReader.ReadUInt32();
            BaseWeaponDamage = Math.Max(1, binaryReader.ReadInt32());
            ArmedConditionMul = Math.Max(1f, binaryReader.ReadSingle());
            NakagoKey = ReadNetworkKey(binaryReader, OniMeiSlotKind.Nakago);
            HiKey = ReadNetworkKey(binaryReader, OniMeiSlotKind.Hi);
            HorimonoKey = ReadNetworkKey(binaryReader, OniMeiSlotKind.Horimono);
            RebuildProfile();
        }

        public override void Unload() {
            nextSerial = null;
            secondaryHits.Clear();
            secondaryBudgets.Clear();
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

        private static ushort GetNetworkId(string key, OniMeiSlotKind slot) {
            if (!string.IsNullOrEmpty(key)
                && OniMeiRegistry.TryGetNetworkId(key, out ushort id)
                && OniMeiRegistry.TryGetByNetworkId(id, out OniMeiDefinition definition)
                && definition.SlotKind == slot) {
                return id;
            }
            return ushort.MaxValue;
        }

        private static string ReadNetworkKey(BinaryReader reader, OniMeiSlotKind slot) {
            ushort id = reader.ReadUInt16();
            return id != ushort.MaxValue
                && OniMeiRegistry.TryGetByNetworkId(id, out OniMeiDefinition definition)
                && definition.SlotKind == slot
                ? definition.Key
                : null;
        }

        private static bool UsesOwnHitLedger(Projectile projectile)
            => projectile?.ModProjectile is OniMeiGroundBurn;

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
            foreach (SecondaryBudgetKey key in new List<SecondaryBudgetKey>(secondaryBudgets.Keys)) {
                if (now - secondaryBudgets[key].Tick > 600) {
                    secondaryBudgets.Remove(key);
                }
            }
        }
    }
}
