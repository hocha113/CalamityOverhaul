using CalamityOverhaul.Common;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.OtherMods.Entropys
{
    internal sealed class SoyMilkBossPowerLoader : ICWRLoader
    {
        internal static int BuffType { get; private set; } = -1;

        void ICWRLoader.SetupData() {
            BuffType = -1;
            Mod entropy = CWRMod.Instance.calamityEntropy;
            if (entropy == null) {
                ModLoader.TryGetMod("CalamityEntropy", out entropy);
            }
            if (entropy != null && entropy.TryFind("SoyMilkBuff", out ModBuff buff)) {
                BuffType = buff.Type;
            }
        }

        void ICWRLoader.UnLoadData() => BuffType = -1;

        internal static bool HasBuff(Player player)
            => BuffType > 0 && player?.active == true && player.HasBuff(BuffType);
    }

    internal sealed class SoyMilkBossPowerPlayer : ModPlayer
    {
        private const int DamageWindow = 60;
        private const int ReportQualificationGrace = DamageWindow * 2;

        private Dictionary<int, DamageReport> damageReports;
        private List<DamageReport> reportBuffer;
        private int damageWindowTimer;
        private int reportQualificationTimer;

        internal bool CanSubmitReport => reportQualificationTimer > 0;

        public override void Initialize() => ResetTracking();

        public override void OnEnterWorld() => ResetTracking();

        private void ResetTracking() {
            damageReports?.Clear();
            reportBuffer?.Clear();
            damageWindowTimer = 0;
            reportQualificationTimer = 0;
        }

        public override void PostUpdate() {
            if (IsQualified(Player, Player.HeldItem)) {
                reportQualificationTimer = ReportQualificationGrace;
            }
            else if (reportQualificationTimer > 0) {
                reportQualificationTimer--;
            }

            if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (damageReports == null || damageReports.Count == 0) {
                damageWindowTimer = 0;
                return;
            }
            if (++damageWindowTimer < DamageWindow) {
                return;
            }

            FlushReports();
        }

        internal void TrackDamage(int npcIndex, int npcType, int damageDone, int lifeCeiling) {
            if (damageDone <= 0 || lifeCeiling <= 0) {
                return;
            }

            damageReports ??= [];
            if (!damageReports.TryGetValue(npcIndex, out DamageReport report)
                || report.NpcType != npcType) {
                report = new DamageReport(npcIndex, npcType, 0, lifeCeiling);
            }

            long room = long.MaxValue - report.Damage;
            report.Damage += Math.Min(room, damageDone);
            damageReports[npcIndex] = report;
        }

        internal static bool IsQualified(Player player, Item weapon) {
            if (!SoyMilkBossPowerLoader.HasBuff(player) || weapon == null || weapon.IsAir) {
                return false;
            }
            return weapon.CWR()?.LegendData != null;
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.SoyMilkBossPowerDamageReport) {
                return;
            }

            ushort count = reader.ReadUInt16();
            if (Main.netMode != NetmodeID.Server || count > Main.maxNPCs) {
                return;
            }

            bool playerIsValid = whoAmI >= 0 && whoAmI < Main.maxPlayers;
            Player player = playerIsValid ? Main.player[whoAmI] : null;
            bool canSubmit = player?.active == true
                && player.GetModPlayer<SoyMilkBossPowerPlayer>().CanSubmitReport;

            for (int i = 0; i < count; i++) {
                int npcIndex = reader.ReadInt16();
                int npcType = reader.ReadInt32();
                long damage = reader.ReadInt64();
                int lifeCeiling = reader.ReadInt32();
                if (!canSubmit || damage <= 0
                    || !TryGetReportedTarget(npcIndex, npcType, out NPC target)) {
                    continue;
                }

                long sanityLimit = Math.Max((long)target.lifeMax * DamageWindow, 1L);
                target.GetGlobalNPC<SoyMilkBossPowerGlobalNPC>()
                    .QueueHealing(target, Math.Min(damage, sanityLimit), lifeCeiling);
            }
        }

        private void FlushReports() {
            damageWindowTimer = 0;
            reportBuffer ??= [];
            reportBuffer.Clear();

            foreach (DamageReport report in damageReports.Values) {
                if (TryGetReportedTarget(report.NpcIndex, report.NpcType, out _)) {
                    reportBuffer.Add(report);
                }
            }
            damageReports.Clear();

            if (reportBuffer.Count == 0) {
                return;
            }
            if (Main.netMode == NetmodeID.SinglePlayer) {
                foreach (DamageReport report in reportBuffer) {
                    if (TryGetReportedTarget(report.NpcIndex, report.NpcType, out NPC target)) {
                        target.GetGlobalNPC<SoyMilkBossPowerGlobalNPC>()
                            .QueueHealing(target, report.Damage, report.LifeCeiling);
                    }
                }
                return;
            }

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.SoyMilkBossPowerDamageReport);
            packet.Write((ushort)reportBuffer.Count);
            foreach (DamageReport report in reportBuffer) {
                packet.Write((short)report.NpcIndex);
                packet.Write(report.NpcType);
                packet.Write(report.Damage);
                packet.Write(report.LifeCeiling);
            }
            packet.Send();
        }

        private static bool TryGetReportedTarget(int npcIndex, int npcType, out NPC target) {
            target = null;
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return false;
            }

            NPC candidate = Main.npc[npcIndex];
            if (!candidate.active || candidate.type != npcType) {
                return false;
            }
            return TryGetRecoveryTarget(candidate, out target);
        }

        internal static bool TryGetRecoveryTarget(NPC struckNpc, out NPC target) {
            target = null;
            if (!NpcGroupHelper.IsBossTier(struckNpc)) {
                return false;
            }

            int anchorIndex = NpcGroupHelper.GetAnchorIndex(struckNpc);
            if (anchorIndex < 0 || anchorIndex >= Main.maxNPCs || !Main.npc[anchorIndex].active) {
                return false;
            }

            target = Main.npc[anchorIndex];
            return true;
        }

        private struct DamageReport
        {
            internal int NpcIndex;
            internal int NpcType;
            internal long Damage;
            internal int LifeCeiling;

            internal DamageReport(int npcIndex, int npcType, long damage, int lifeCeiling) {
                NpcIndex = npcIndex;
                NpcType = npcType;
                Damage = damage;
                LifeCeiling = lifeCeiling;
            }
        }
    }

    internal sealed class SoyMilkBossPowerGlobalNPC : GlobalNPC
    {
        private const int HealDuration = 60;
        private const int LifeSyncInterval = 6;
        private const int DamageRecoveryNumerator = 7;
        private const int DamageRecoveryDenominator = 10;

        private static readonly Color SoyTint = new(238, 248, 218);

        private List<HealBatch> healBatches;
        private int healedSinceText;
        private int healTextTimer;
        private int lifeSyncTimer;
        private bool lifeDirty;
        private byte visualTimer;
        private byte visualPower;
        private byte pulseSerial;
        private byte seenPulseSerial;
        private int dustTimer;
        private bool pulsePending;
        private HitSnapshot itemHitSnapshot;
        private HitSnapshot projectileHitSnapshot;

        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC entity) => ClearState();

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            itemHitSnapshot = default;
            if (Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer
                || !SoyMilkBossPowerPlayer.IsQualified(player, item)) {
                return;
            }
            itemHitSnapshot = CaptureSnapshot(npc, player.whoAmI, item.type);
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            CommitSnapshot(player, itemHitSnapshot, player.whoAmI, item.type, damageDone);
            itemHitSnapshot = default;
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            projectileHitSnapshot = default;
            if (Main.netMode == NetmodeID.Server || !projectile.friendly
                || projectile.owner < 0 || projectile.owner >= Main.maxPlayers
                || projectile.owner != Main.myPlayer) {
                return;
            }

            Player player = Main.player[projectile.owner];
            if (!SoyMilkBossPowerPlayer.IsQualified(player, player.HeldItem)) {
                return;
            }
            projectileHitSnapshot = CaptureSnapshot(npc, projectile.owner, projectile.whoAmI);
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            Player player = projectile.owner >= 0 && projectile.owner < Main.maxPlayers
                ? Main.player[projectile.owner] : null;
            CommitSnapshot(player, projectileHitSnapshot, projectile.owner, projectile.whoAmI, damageDone);
            projectileHitSnapshot = default;
        }

        public override void PostAI(NPC npc) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                UpdateHealing(npc);
            }
            UpdateVisuals(npc);
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            SoyMilkBossPowerGlobalNPC visualState = GetPowerState(npc);
            if (visualState.visualTimer == 0) {
                return;
            }

            byte alpha = drawColor.A;
            float strength = visualState.visualPower / 255f;
            drawColor = Color.Lerp(drawColor, SoyTint, 0.12f + strength * 0.16f);
            drawColor.A = alpha;
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bool active = visualTimer > 0;
            bitWriter.WriteBit(active);
            if (!active) {
                return;
            }

            binaryWriter.Write(visualTimer);
            binaryWriter.Write(visualPower);
            binaryWriter.Write(pulseSerial);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
            if (!bitReader.ReadBit()) {
                visualTimer = 0;
                visualPower = 0;
                return;
            }

            visualTimer = binaryReader.ReadByte();
            visualPower = binaryReader.ReadByte();
            pulseSerial = binaryReader.ReadByte();
            if (pulseSerial != seenPulseSerial) {
                seenPulseSerial = pulseSerial;
                pulsePending = true;
            }
        }

        internal void QueueHealing(NPC npc, long damage, int lifeCeiling) {
            if (damage <= 0 || lifeCeiling <= 0 || npc.life <= 0 || !NpcGroupHelper.IsBossTier(npc)) {
                return;
            }

            long amount = CalculateHealing(damage);
            if (amount <= 0) {
                return;
            }

            healBatches ??= [];
            healBatches.Add(new HealBatch(amount, HealDuration,
                Math.Min(lifeCeiling, npc.lifeMax)));
            if (healTextTimer <= 0) {
                healTextTimer = HealDuration;
            }

            float lifeRatio = (float)amount / Math.Max(npc.lifeMax, 1);
            byte newPower = (byte)(MathHelper.Clamp(0.25f + lifeRatio * 8f, 0.25f, 1f) * byte.MaxValue);
            visualPower = Math.Max(visualPower, newPower);
            visualTimer = HealDuration;
            pulseSerial++;
            if (Main.netMode == NetmodeID.SinglePlayer) {
                seenPulseSerial = pulseSerial;
                pulsePending = true;
            }
            npc.netUpdate = true;
        }

        internal static bool HasActivePower(NPC npc) {
            if (npc?.active != true) {
                return false;
            }
            return GetPowerState(npc).visualTimer > 0;
        }

        private void UpdateHealing(NPC npc) {
            if (healBatches == null || healBatches.Count == 0) {
                return;
            }
            if (!npc.active || npc.life <= 0 || !NpcGroupHelper.IsBossTier(npc)) {
                ClearHealing();
                return;
            }

            int actualHeal = 0;
            for (int i = healBatches.Count - 1; i >= 0; i--) {
                HealBatch batch = healBatches[i];
                long contribution = batch.Remaining / batch.TicksRemaining;
                if (batch.Remaining % batch.TicksRemaining != 0) {
                    contribution++;
                }
                batch.Remaining -= contribution;
                batch.TicksRemaining--;
                if (batch.TicksRemaining <= 0) {
                    healBatches.RemoveAt(i);
                }
                else {
                    healBatches[i] = batch;
                }

                int room = Math.Max(Math.Min(batch.LifeCeiling, npc.lifeMax) - npc.life, 0);
                int batchHeal = (int)Math.Min(contribution, room);
                if (batchHeal > 0) {
                    npc.life += batchHeal;
                    actualHeal += batchHeal;
                }
            }
            if (actualHeal > 0) {
                healedSinceText += actualHeal;
                lifeDirty = true;
            }
            if (++lifeSyncTimer >= LifeSyncInterval || healBatches.Count == 0) {
                lifeSyncTimer = 0;
                if (lifeDirty) {
                    lifeDirty = false;
                    npc.netUpdate = true;
                }
            }

            if (healTextTimer > 0 && --healTextTimer == 0) {
                ShowHealText(npc);
                if (healBatches.Count > 0) {
                    healTextTimer = HealDuration;
                }
            }
            if (healBatches.Count == 0) {
                ShowHealText(npc);
                healTextTimer = 0;
            }
        }

        private void UpdateVisuals(NPC npc) {
            if (visualTimer == 0) {
                return;
            }

            if (!Main.dedServ) {
                float strength = visualPower / 255f;
                Lighting.AddLight(npc.Center, SoyTint.ToVector3() * (0.12f + strength * 0.25f));
                if (pulsePending) {
                    pulsePending = false;
                    SpawnPulse(npc, strength);
                }
                if (--dustTimer <= 0) {
                    dustTimer = 4 - (int)(strength * 2f);
                    SpawnMote(npc, strength);
                }
            }
            visualTimer--;
            if (visualTimer == 0) {
                visualPower = 0;
            }
        }

        private static void SpawnPulse(NPC npc, float strength) {
            Vector2 radius = new(
                MathHelper.Clamp(npc.width * 0.6f, 24f, 160f),
                MathHelper.Clamp(npc.height * 0.6f, 24f, 160f));
            int count = 10 + (int)(strength * 6f);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 offset = angle.ToRotationVector2() * radius;
                Dust dust = Dust.NewDustPerfect(npc.Center + offset, DustID.TintableDustLighted,
                    -offset.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(1.2f, 2.4f),
                    90, SoyTint, Main.rand.NextFloat(0.9f, 1.35f));
                dust.noGravity = true;
            }
        }

        private static void SpawnMote(NPC npc, float strength) {
            Vector2 position = new(
                Main.rand.NextFloat(npc.Left.X, npc.Right.X),
                Main.rand.NextFloat(npc.Top.Y, npc.Bottom.Y));
            Vector2 velocity = new(Main.rand.NextFloat(-0.35f, 0.35f), Main.rand.NextFloat(-1.8f, -0.8f));
            Dust dust = Dust.NewDustPerfect(position, DustID.TintableDustLighted, velocity,
                110, SoyTint, Main.rand.NextFloat(0.7f, 1.05f + strength * 0.35f));
            dust.noGravity = true;
            dust.fadeIn = 0.8f;

            if (Main.rand.NextBool(4)) {
                Dust cloud = Dust.NewDustPerfect(position, DustID.Cloud, velocity * 0.45f,
                    150, Color.White, Main.rand.NextFloat(0.45f, 0.8f));
                cloud.noGravity = true;
            }
        }

        private void ShowHealText(NPC npc) {
            if (healedSinceText <= 0) {
                return;
            }
            npc.HealEffect(healedSinceText, Main.netMode == NetmodeID.Server);
            healedSinceText = 0;
        }

        private void ClearState() {
            ClearHealing();
            visualTimer = 0;
            visualPower = 0;
            pulseSerial = 0;
            seenPulseSerial = 0;
            dustTimer = 0;
            pulsePending = false;
            itemHitSnapshot = default;
            projectileHitSnapshot = default;
        }

        private void ClearHealing() {
            healBatches?.Clear();
            healedSinceText = 0;
            healTextTimer = 0;
            lifeSyncTimer = 0;
            lifeDirty = false;
        }

        private static long CalculateHealing(long damage) {
            long whole = damage / DamageRecoveryDenominator * DamageRecoveryNumerator;
            long remainder = damage % DamageRecoveryDenominator * DamageRecoveryNumerator
                / DamageRecoveryDenominator;
            return whole + remainder;
        }

        private static HitSnapshot CaptureSnapshot(NPC struckNpc, int actorIndex, int sourceIndex) {
            if (!SoyMilkBossPowerPlayer.TryGetRecoveryTarget(struckNpc, out NPC target)) {
                return default;
            }
            return new HitSnapshot(target.whoAmI, target.type, target.life, actorIndex, sourceIndex);
        }

        private static SoyMilkBossPowerGlobalNPC GetPowerState(NPC npc) {
            int anchorIndex = NpcGroupHelper.GetAnchorIndex(npc);
            if (anchorIndex >= 0 && anchorIndex < Main.maxNPCs && anchorIndex != npc.whoAmI
                && Main.npc[anchorIndex].active) {
                return Main.npc[anchorIndex].GetGlobalNPC<SoyMilkBossPowerGlobalNPC>();
            }
            return npc.GetGlobalNPC<SoyMilkBossPowerGlobalNPC>();
        }

        private static void CommitSnapshot(Player player, HitSnapshot snapshot,
            int actorIndex, int sourceIndex, int damageDone) {
            if (!snapshot.Valid || player?.active != true || damageDone <= 0
                || snapshot.ActorIndex != actorIndex || snapshot.SourceIndex != sourceIndex
                || snapshot.NpcIndex < 0 || snapshot.NpcIndex >= Main.maxNPCs) {
                return;
            }

            NPC target = Main.npc[snapshot.NpcIndex];
            if (!target.active || target.type != snapshot.NpcType) {
                return;
            }

            int actualDamage = Math.Min(damageDone,
                Math.Max(snapshot.LifeBeforeHit - Math.Max(target.life, 0), 0));
            player.GetModPlayer<SoyMilkBossPowerPlayer>().TrackDamage(
                snapshot.NpcIndex, snapshot.NpcType, actualDamage, snapshot.LifeBeforeHit);
        }

        private struct HealBatch
        {
            internal long Remaining;
            internal int TicksRemaining;
            internal int LifeCeiling;

            internal HealBatch(long remaining, int ticksRemaining, int lifeCeiling) {
                Remaining = remaining;
                TicksRemaining = ticksRemaining;
                LifeCeiling = lifeCeiling;
            }
        }

        private readonly struct HitSnapshot
        {
            internal readonly int NpcIndex;
            internal readonly int NpcType;
            internal readonly int LifeBeforeHit;
            internal readonly int ActorIndex;
            internal readonly int SourceIndex;

            internal bool Valid => LifeBeforeHit > 0;

            internal HitSnapshot(int npcIndex, int npcType, int lifeBeforeHit,
                int actorIndex, int sourceIndex) {
                NpcIndex = npcIndex;
                NpcType = npcType;
                LifeBeforeHit = lifeBeforeHit;
                ActorIndex = actorIndex;
                SourceIndex = sourceIndex;
            }
        }
    }

    internal sealed class SoyMilkBossPowerGlobalProjectile : GlobalProjectile
    {
        private const int UpdateSpeedMultiplier = 2;

        private bool empoweredSource;
        private bool updateMultiplierApplied;
        private int baseMaxUpdates;

        public override bool InstancePerEntity => true;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            empoweredSource = false;
            updateMultiplierApplied = false;
            baseMaxUpdates = 0;

            if (source is not EntitySource_Parent parentSource) {
                return;
            }

            if (parentSource.Entity is NPC npc) {
                empoweredSource = SoyMilkBossPowerGlobalNPC.HasActivePower(npc);
            }
            else if (parentSource.Entity is Projectile parentProjectile) {
                empoweredSource = parentProjectile
                    .GetGlobalProjectile<SoyMilkBossPowerGlobalProjectile>().empoweredSource;
            }
        }

        public override bool PreAI(Projectile projectile) {
            ApplyUpdateMultiplier(projectile);
            return true;
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bitWriter.WriteBit(empoweredSource);
            if (!empoweredSource) {
                return;
            }

            if (baseMaxUpdates <= 0) {
                baseMaxUpdates = Math.Max(projectile.MaxUpdates, 1);
            }
            binaryWriter.Write(baseMaxUpdates);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) {
            empoweredSource = bitReader.ReadBit();
            if (!empoweredSource) {
                baseMaxUpdates = 0;
                return;
            }

            baseMaxUpdates = Math.Max(binaryReader.ReadInt32(), 1);
            if (updateMultiplierApplied) {
                projectile.MaxUpdates = GetMultipliedUpdateCount(baseMaxUpdates);
                return;
            }
            ApplyUpdateMultiplier(projectile);
        }

        private void ApplyUpdateMultiplier(Projectile projectile) {
            if (!empoweredSource || updateMultiplierApplied || !projectile.hostile) {
                return;
            }

            if (baseMaxUpdates <= 0) {
                baseMaxUpdates = Math.Max(projectile.MaxUpdates, 1);
            }
            projectile.MaxUpdates = GetMultipliedUpdateCount(baseMaxUpdates);
            updateMultiplierApplied = true;
        }

        private static int GetMultipliedUpdateCount(int maxUpdates) {
            long multipliedUpdates = Math.Max(maxUpdates, 1) * (long)UpdateSpeedMultiplier;
            return (int)Math.Min(multipliedUpdates, int.MaxValue);
        }
    }
}
