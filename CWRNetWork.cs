using CalamityOverhaul.Content;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Modifys;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using CalamityOverhaul.Content.Scenarios.Draedon;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using CalamityOverhaul.Content.Scenarios.Draedon.Tzeentch;
using CalamityOverhaul.Content.Scenarios.Himayo;
using CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines;
using CalamityOverhaul.Content.Scenarios.OldDuke;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Wraiths.Runtime;
using CalamityOverhaul.OtherMods.Entropys;
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
        OldDukeCampsiteSync,
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
        CrabulonRecall,
        //保留已发布的协议编号，客户端不再发送该请求。
        ReservedToriiShrineGenerationRequest,
        ToriiShrineSync,
        Wraith,
        //保留编号：原鬼切教程练习鬼影通道已删除，客户端不再发送。
        ReservedOnikiriTutorial,
        HimayoGiftEntitlements,
        WeaverGrievancesManifestation,
        OnikiriTutorialTarget,
        OnikiriItemOperation,
        SoyMilkBossPowerDamageReport,
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
            else if (type == CWRMessageType.StartCampsiteFindMeScenario) {
                OldDukeTriggerService.HandleStartCampsiteFindMeScenario(reader, whoAmI);
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
                EbnState.HandleNetSync(reader, whoAmI);
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
            else if (type == CWRMessageType.ToriiShrineSync) {
                ToriiShrine.ReceiveShrineSync(reader);
            }
            else if (type == CWRMessageType.HimayoGiftEntitlements) {
                HimayoStorySync.ReceiveGiftEntitlements(reader);
            }

            ModifyCrabulon.NetHandle(type, reader, whoAmI);
            DraedonEffect.NetHandle(type, reader, whoAmI);
            TzeentchEffect.NetHandle(type, reader, whoAmI);
            SignalTowerTargetManager.NetHandle(type, reader, whoAmI);
            OldDukeEffect.NetHandle(type, reader, whoAmI);
            MachineEffect.NetHandle(type, reader, whoAmI);
            WraithNet.NetHandle(type, reader, whoAmI);
            WGManifestationNet.NetHandle(type, reader, whoAmI);
            OnikiriTutorialNet.NetHandle(type, reader, whoAmI);
            OnikiriNet.NetHandle(type, reader, whoAmI);
            SoyMilkBossPowerPlayer.NetHandle(type, reader, whoAmI);
        }
    }
}
