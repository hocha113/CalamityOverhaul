using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Scenarios.Helen;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Scenarios.SupCal.Quest.DoGQuest;
using CalamityOverhaul.Content.Scenarios.SupCal.Quest.PallbearerQuest;
using CalamityOverhaul.Content.Scenarios.SupCal.Quest.YharonQuest;
using InnoVault.Narrative.Core;
using InnoVault.Narrative.Runtime;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal
{
    internal sealed class SupCalNarrativeTicker : ModSystem
    {
        public override void OnWorldLoad() {
            FirstMetSupCal.ThisIsToFight = false;
            FirstMetSupCalNPC.ResetWorldState();
            SupCalDefeatNPC.ResetWorldState();
            SupCalVictoryNPC.ResetWorldState();
            SupCalPlayerDefeatTracker.ResetWorldState();
            SupCalMoonLordRewardNPC.ResetWorldState();
            SupCalQuestRewardTracker.ResetWorldState();
            SCalAltarScenario.ResetWorldState();
            WitchFarewell.ResetWorldState();
            HelenEpilogue.ResetWorldState();
        }

        public override void PreUpdatePlayers() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }

            TickFirstMet();
            TickSupCalDefeat();
            TickSupCalVictory();
            TickSupCalPlayerDefeat();
            TickSupCalMoonLordReward();
            SupCalQuestRewardTracker.Tick();
            TickWitchFarewell();
            TickHelenEpilogue();
        }

        private static void TickFirstMet() {
            if (HalibutStorySync.ReadSupCal(d => d.FirstMetSupCal, d => d.FirstMetSupCal)) {
                return;
            }

            if (InWorldBossPhase.Downed30.Invoke()) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player.TryGetOverride(out HalibutPlayer halibutPlayer)
                && halibutPlayer.HeldHalibut
                && !HalibutStorySync.ReadGift(d => d.CalamitasCloneGift, d => d.CalamitasCloneGift)) {
                return;
            }

            if (!FirstMetSupCalNPC.Spawned) {
                return;
            }

            if (NPC.AnyNPCs(CWRID.NPC_SupremeCalamitas)) {
                FirstMetSupCalNPC.Spawned = false;
                return;
            }

            if (--FirstMetSupCalNPC.RandomTimer > 0 || CWRWorld.HasBoss || NarrativeRunner.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<FirstMetSupCal>()) {
                HalibutStorySync.WriteSupCal(
                    d => d.FirstMetSupCal = true,
                    d => d.FirstMetSupCal = true);
                FirstMetSupCalNPC.Spawned = false;
            }
        }

        private static void TickSupCalDefeat() {
            if (HalibutStorySync.ReadSupCal(d => d.SupCalDefeat, d => d.SupCalDefeat)) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!player.TryGetOverride(out HalibutPlayer halibutPlayer) || !halibutPlayer.HeldHalibut) {
                return;
            }

            if (halibutPlayer.SeaDomainLayers < 10 || !SupCalDefeatNPC.Spawned) {
                return;
            }

            if (--SupCalDefeatNPC.RandomTimer > 0 || CWRWorld.HasBoss || NarrativeRunner.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<SupCalDefeat>()) {
                SupCalDefeatNPC.Spawned = false;
            }
        }

        private static void TickSupCalVictory() {
            if (HalibutStorySync.ReadSupCal(d => d.SupCalDefeat, d => d.SupCalDefeat)) {
                return;
            }

            if (!HalibutStorySync.ReadSupCal(d => d.SupCalChoseToFight, d => d.SupCalChoseToFight)) {
                return;
            }

            if (NPC.downedMoonlord || !SupCalVictoryNPC.Spawned) {
                return;
            }

            if (--SupCalVictoryNPC.RandomTimer > 0 || CWRWorld.HasBoss || NarrativeRunner.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<SupCalVictory>()) {
                SupCalVictoryNPC.Spawned = false;
            }
        }

        private static void TickSupCalPlayerDefeat() {
            if (!HalibutStorySync.ReadSupCal(d => d.SupCalChoseToFight, d => d.SupCalChoseToFight)) {
                return;
            }

            if (HalibutStorySync.ReadSupCal(d => d.SupCalDefeat, d => d.SupCalDefeat)) {
                return;
            }

            if (!SupCalPlayerDefeatTracker.Spawned) {
                return;
            }

            if (--SupCalPlayerDefeatTracker.RandomTimer > 0 || CWRWorld.HasBoss || NarrativeRunner.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<SupCalPlayerDefeat>()) {
                SupCalPlayerDefeatTracker.Spawned = false;
            }
        }

        private static void TickSupCalMoonLordReward() {
            if (HalibutStorySync.ReadSupCal(d => d.SupCalMoonLordReward, d => d.SupCalMoonLordReward)) {
                return;
            }

            if (!HalibutStorySync.ReadSupCal(d => d.FirstMetSupCal, d => d.FirstMetSupCal)) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player.TryGetOverride(out HalibutPlayer halibutPlayer)
                && halibutPlayer.HeldHalibut
                && !HalibutStorySync.ReadGift(d => d.MoonLordGift, d => d.MoonLordGift)) {
                return;
            }

            if (!HalibutStorySync.ReadSupCal(d => d.SupCalChoseToFight, d => d.SupCalChoseToFight)) {
                return;
            }

            if (!SupCalMoonLordRewardNPC.Spawned) {
                return;
            }

            if (--SupCalMoonLordRewardNPC.RandomTimer > 0 || CWRWorld.HasBoss || NarrativeRunner.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<SupCalMoonLordReward>()) {
                SupCalMoonLordRewardNPC.Spawned = false;
            }
        }

        private static void TickWitchFarewell() {
            if (!WitchFarewell.SpawnPending || NarrativeRunner.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<WitchFarewell>()) {
                WitchFarewell.SpawnPending = false;
            }
        }

        private static void TickHelenEpilogue() {
            if (!HelenEpilogue.SpawnPending || CWRWorld.HasBoss || NarrativeRunner.IsBusy) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!player.TryGetOverride(out HalibutPlayer halibutPlayer) || !halibutPlayer.HasHalubut) {
                return;
            }

            if (NarrativeRouter.Begin<HelenEpilogue>()) {
                HelenEpilogue.SpawnPending = false;
            }
        }
    }

    internal static class SupCalQuestRewardTracker
    {
        private static bool pallbearerSpawned;
        private static bool dogSpawned;
        private static bool yharonSpawned;
        private static int pallbearerTimer;
        private static int dogTimer;
        private static int yharonTimer;

        public static void ResetWorldState() {
            pallbearerSpawned = false;
            dogSpawned = false;
            yharonSpawned = false;
            pallbearerTimer = 0;
            dogTimer = 0;
            yharonTimer = 0;
        }

        public static void NotifyPallbearerComplete() {
            pallbearerSpawned = true;
            pallbearerTimer = 60 * Main.rand.Next(3, 5);
        }

        public static void NotifyDoGComplete() {
            dogSpawned = true;
            dogTimer = 60 * Main.rand.Next(3, 5);
        }

        public static void NotifyYharonComplete() {
            yharonSpawned = true;
            yharonTimer = 60 * Main.rand.Next(3, 5);
        }

        public static void Tick() {
            TickReward<SupCalQuestReward>(
                ref pallbearerSpawned,
                ref pallbearerTimer,
                () => HalibutStorySync.ReadSupCal(d => d.SupCalQuestReward, d => d.SupCalQuestReward),
                () => HalibutStorySync.ReadSupCal(d => d.SupCalQuestRewardSceneComplete, d => d.SupCalQuestRewardSceneComplete),
                _ => true);

            TickReward<SupCalDoGQuestReward>(
                ref dogSpawned,
                ref dogTimer,
                () => HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestReward, d => d.SupCalDoGQuestReward),
                () => HalibutStorySync.ReadSupCal(d => d.SupCalDoGQuestRewardSceneComplete, d => d.SupCalDoGQuestRewardSceneComplete),
                player => {
                    if (!player.TryGetOverride(out HalibutPlayer halibutPlayer) || !halibutPlayer.HeldHalibut) {
                        return true;
                    }

                    return HalibutStorySync.ReadGift(d => d.DevourerOfGodsGift, d => d.DevourerOfGodsGift);
                });

            TickReward<SupCalYharonQuestReward>(
                ref yharonSpawned,
                ref yharonTimer,
                () => HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestReward, d => d.SupCalYharonQuestReward),
                () => HalibutStorySync.ReadSupCal(d => d.SupCalYharonQuestRewardSceneComplete, d => d.SupCalYharonQuestRewardSceneComplete),
                player => {
                    if (!player.TryGetOverride(out HalibutPlayer halibutPlayer) || !halibutPlayer.HeldHalibut) {
                        return true;
                    }

                    return HalibutStorySync.ReadGift(d => d.YharonGift, d => d.YharonGift);
                });
        }

        private static void TickReward<T>(
            ref bool spawned,
            ref int timer,
            System.Func<bool> isComplete,
            System.Func<bool> isSceneComplete,
            System.Func<Player, bool> extraCondition) where T : NarrativeScenario {
            if (!isComplete() || isSceneComplete() || !spawned) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!extraCondition(player)) {
                return;
            }

            if (--timer > 0 || CWRWorld.HasBoss || NarrativeRunner.IsBusy) {
                return;
            }

            if (NarrativeRouter.Begin<T>()) {
                spawned = false;
            }
        }
    }

    internal sealed class FirstMetSupCalNPC : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned;
        public static int RandomTimer;

        public static void ResetWorldState() {
            Spawned = false;
            RandomTimer = 0;
        }

        void IWorldInfo.OnWorldLoad() => ResetWorldState();

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == CWRID.NPC_CalamitasClone;

        public override void OnNPCDeath(NPC npc) {
            if (npc.type == CWRID.NPC_CalamitasClone && !CWRWorld.BossRush) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);
            }
        }
    }

    internal sealed class SupCalDefeatNPC : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned;
        public static int RandomTimer;

        public static void ResetWorldState() {
            Spawned = false;
            RandomTimer = 0;
        }

        void IWorldInfo.OnWorldLoad() => ResetWorldState();

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == CWRID.NPC_SupremeCalamitas;

        public override void OnNPCDeath(NPC npc) {
            if (npc.type == CWRID.NPC_SupremeCalamitas
                && Main.LocalPlayer.GetItem().type == HalibutOverride.ID
                && !CWRWorld.BossRush) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);
            }
        }
    }

    internal sealed class SupCalVictoryNPC : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned;
        public static int RandomTimer;

        public static void ResetWorldState() {
            Spawned = false;
            RandomTimer = 0;
        }

        void IWorldInfo.OnWorldLoad() => ResetWorldState();

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == CWRID.NPC_SupremeCalamitas;

        public override void OnNPCDeath(NPC npc) {
            if (!FirstMetSupCal.ThisIsToFight || npc.type != CWRID.NPC_SupremeCalamitas) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (!HalibutStorySync.ReadSupCal(d => d.SupCalChoseToFight, d => d.SupCalChoseToFight)) {
                return;
            }

            Spawned = true;
            RandomTimer = 60 * Main.rand.Next(2, 4);
            FirstMetSupCal.ThisIsToFight = false;
        }
    }

    internal sealed class SupCalPlayerDefeatTracker : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned;
        public static int RandomTimer;
        public static bool HasRecordedDeath;

        public static void ResetWorldState() {
            Spawned = false;
            RandomTimer = 0;
            HasRecordedDeath = false;
        }

        void IWorldInfo.OnWorldLoad() => ResetWorldState();

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == CWRID.NPC_SupremeCalamitas;

        public override bool PreAI(NPC npc) {
            if (!FirstMetSupCal.ThisIsToFight || npc.type != CWRID.NPC_SupremeCalamitas || !npc.active || HasRecordedDeath) {
                return true;
            }

            foreach (Player player in Main.ActivePlayers) {
                if (player == null || !player.dead) {
                    continue;
                }

                if (!HalibutStorySync.ReadSupCal(d => d.SupCalChoseToFight, d => d.SupCalChoseToFight)) {
                    continue;
                }

                if (HalibutStorySync.ReadSupCal(d => d.SupCalDefeat, d => d.SupCalDefeat)) {
                    continue;
                }

                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(5, 8);
                HasRecordedDeath = true;
                FirstMetSupCal.ThisIsToFight = false;
                break;
            }

            return true;
        }

        public override void OnNPCDeath(NPC npc) {
            if (npc.type == CWRID.NPC_SupremeCalamitas) {
                HasRecordedDeath = false;
                Spawned = false;
            }
        }
    }

    internal sealed class SupCalMoonLordRewardNPC : DeathTrackingNPC, IWorldInfo
    {
        public static bool Spawned;
        public static int RandomTimer;

        public static void ResetWorldState() {
            Spawned = false;
            RandomTimer = 0;
        }

        void IWorldInfo.OnWorldLoad() => ResetWorldState();

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.MoonLordCore;

        public override void OnNPCDeath(NPC npc) {
            if (npc.type == NPCID.MoonLordCore && !CWRWorld.BossRush) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);
            }
        }
    }
}
