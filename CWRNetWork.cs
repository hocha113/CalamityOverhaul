using CalamityOverhaul.Content;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Tzeentch;
using CalamityOverhaul.Content.Industrials.Modifys;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Modifys;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using CalamityOverhaul.Content.RemakeItems;
using System.IO;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public enum CWRMessageType : byte
    {
        NPCbasicData,
        ModifiIntercept_InGame,
        ModifiIntercept_EnterWorld_Request,
        ModifiIntercept_EnterWorld_ToClient,
        ProjectileDyeItemID,
        KillTileEntity,
        TruffleSleep,
        GlobalSleep,
        CrabulonFeed,
        CrabulonModifyNetWork,
        HalibutMouseWorld,
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
        SpwanOldDukeWannaToFight,
        StartCampsiteFindMeScenario,
        ResurrectionRate,
        DespawnDestroyer,
        MachineEffect,
    }

    public static class CWRNetWork
    {
        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            CWRMessageType type = (CWRMessageType)reader.ReadByte();

            if (type == CWRMessageType.NPCbasicData) {
                CWRNpc.NPCbasicDataHandler(reader);
            }
            else if (type == CWRMessageType.ModifiIntercept_InGame) {
                HandlerCanOverride.NetModifiIntercept_InGame(reader, whoAmI);
            }
            else if (type == CWRMessageType.ModifiIntercept_EnterWorld_Request) {
                HandlerCanOverride.NetModifiInterceptEnterWorld_Server(reader, whoAmI);
            }
            else if (type == CWRMessageType.ModifiIntercept_EnterWorld_ToClient) {
                HandlerCanOverride.NetModifiInterceptEnterWorld_Client(reader, whoAmI);
            }
            else if (type == CWRMessageType.ProjectileDyeItemID) {
                CWRProjectile.HandleProjectileDyeItemID(reader, whoAmI);
            }
            else if (type == CWRMessageType.KillTileEntity) {
                ModifyTurretLoader.HandlerNetKillTE(reader, whoAmI);
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
            else if (type == CWRMessageType.SpwanOldDukeWannaToFight) {
                ModifyOldDuke.SpwanOldDukeByWannaToFightNetWork(reader, whoAmI);
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

            ModifyCrabulon.NetHandle(type, reader, whoAmI);
            HalibutPlayer.NetHandle(type, reader, whoAmI);
            DraedonEffect.NetHandle(type, reader, whoAmI);
            TzeentchEffect.NetHandle(type, reader, whoAmI);
            SignalTowerTargetManager.NetHandle(type, reader, whoAmI);
            ADVSave.NetHandle(type, reader, whoAmI);
            OldDukeEffect.NetHandle(type, reader, whoAmI);
            MachineEffect.NetHandle(type, reader, whoAmI);
        }
    }
}
