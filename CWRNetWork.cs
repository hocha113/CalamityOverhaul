using CalamityOverhaul.Content;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Modifys;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using System.IO;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public enum CWRMessageType : byte
    {
        NPCbasicData,
        ProjectileDyeItemID,
        TruffleSleep,
        GlobalSleep,
        CrabulonFeed,
        CrabulonModifyNetWork,
        DraedonEffect,
        TzeentchEffect,
        SignalTowerTargetManager,
        SetNPCLoot,
        EbnTag,
        OldDukeEffect,
        OldDukeCampsiteGenerationRequest,
        OldDukeCampsiteDecorationsSync,
        OldDukeCampsiteSync,
        RequestOldDukeCampsiteData,
        HandleOldDukeCampsiteDataServer,
        HandleOldDukeCampsiteDataClient,
        StartCampsiteFindMeScenario,
        ResurrectionRate,
        DespawnDestroyer,
        MachineEffect,
        SirenMusicalBoxToggle,
        CyberspaceStateSync,
        CyberDomainFreezeStart,
        CyberBanishStart,
        CyberBossExecutionStart,
        HackProtocolApply,
    }

    public static class CWRNetWork
    {
        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            CWRMessageType type = (CWRMessageType)reader.ReadByte();

            if (type == CWRMessageType.NPCbasicData) {
                CWRNpc.NPCbasicDataHandler(reader);
            }
            else if (type == CWRMessageType.ProjectileDyeItemID) {
                CWRProjectile.HandleProjectileDyeItemID(reader, whoAmI);
            }
            else if (type == CWRMessageType.TruffleSleep) {
                ModifyTruffle.HandleNetwork(reader, whoAmI);
            }
            else if (type == CWRMessageType.GlobalSleep) {
                ModifyTruffle.HandleGlobalSleep(reader);
            }
            else if (type == CWRMessageType.SetNPCLoot) {
                CWRNpc.HandleSetNPCLoot(reader, whoAmI);
            }
            else if (type == CWRMessageType.OldDukeCampsiteGenerationRequest) {
                OldDukeCampsite.TryGenerateCampsite();
            }
            else if (type == CWRMessageType.OldDukeCampsiteSync) {
                OldDukeCampsite.ReceiveCampsiteSync(reader);
            }
            else if (type == CWRMessageType.HandleOldDukeCampsiteDataServer) {
                OldDukeCampsite.HandleOldDukeCampsiteDataServer(reader, whoAmI);
            }
            else if (type == CWRMessageType.HandleOldDukeCampsiteDataClient) {
                OldDukeCampsite.HandleOldDukeCampsiteDataClient(reader, whoAmI);
            }
            else if (type == CWRMessageType.OldDukeCampsiteDecorationsSync) {
                OldDukeCampsiteDecoration.ReceiveDecorationsSync(reader);
            }
            else if (type == CWRMessageType.StartCampsiteFindMeScenario) {
                ModifyOldDuke.StartCampsiteFindMeScenarioNetWork(reader, whoAmI);
            }
            else if (type == CWRMessageType.ResurrectionRate) {
                ResurrectionSystem.HandleResurrectionRate(reader, whoAmI);
            }
            else if (type == CWRMessageType.DespawnDestroyer) {
                DestroyerHeadAI.HandleDespawn();
            }
            else if (type == CWRMessageType.SirenMusicalBoxToggle) {
                SirenMusicalBoxTP.HandleTogglePacket(reader, whoAmI);
            }
            else if (type == CWRMessageType.EbnTag) {
                EbnPlayer.HandleNetSync(reader, whoAmI);
            }
            else if (type == CWRMessageType.CyberspaceStateSync) {
                CyberspacePlayer.HandleNetSync(reader, whoAmI);
            }
            else if (type == CWRMessageType.CyberDomainFreezeStart) {
                CyberDomainFreeze.HandleNetStart(reader, whoAmI);
            }
            else if (type == CWRMessageType.CyberBanishStart) {
                CyberBanish.HandleNetStart(reader, whoAmI);
            }
            else if (type == CWRMessageType.CyberBossExecutionStart) {
                CyberBossExecution.HandleNetStart(reader, whoAmI);
            }
            else if (type == CWRMessageType.HackProtocolApply) {
                HackTimeNetSync.HandleApplyPacket(reader, whoAmI);
            }

            ModifyCrabulon.NetHandle(type, reader, whoAmI);
            DraedonEffect.NetHandle(type, reader, whoAmI);
            TzeentchEffect.NetHandle(type, reader, whoAmI);
            SignalTowerTargetManager.NetHandle(type, reader, whoAmI);
            OldDukeEffect.NetHandle(type, reader, whoAmI);
            MachineEffect.NetHandle(type, reader, whoAmI);
        }
    }
}
