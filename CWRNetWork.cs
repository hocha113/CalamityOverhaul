using CalamityOverhaul.Content;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.Content.Industrials.Modifys;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.NPCs.Modifys;
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
        GiftScenarioNPC,
        FirstMetSupCalNPC,
        SupCalDefeatNPC,
        SupCalPlayerDefeatTracker,
        SupCalVictoryNPC,
        DoGQuestTracker,
        EternalBlazingNowNPC,
        BaseDamageTracker,
        DraedonEffect,
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
            ModifyCrabulon.NetHandle(type, reader, whoAmI);
            GiftScenarioNPC.NetHandle(type, reader, whoAmI);
            FirstMetSupCalNPC.NetHandle(type, reader, whoAmI);
            SupCalDefeatNPC.NetHandle(type, reader, whoAmI);
            SupCalPlayerDefeatTracker.NetHandle(type, reader, whoAmI);
            SupCalVictoryNPC.NetHandle(type, reader, whoAmI);
            HalibutPlayer.NetHandle(type, reader, whoAmI);
            DraedonEffect.NetHandle(type, reader, whoAmI);
        }
    }
}
