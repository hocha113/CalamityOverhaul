using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Cyberwares;
using CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans;
using CalamityOverhaul.Content.Cyberwares.Implementation.SelfHackCrystals;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.Items.Melee.Arbiters;
using CalamityOverhaul.Content.Items.Melee.WeaverGrievanceses;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Banish;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.DomainFreeze;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.Modifys;
using CalamityOverhaul.Content.NPCs.Modifys.Crabulons;
using CalamityOverhaul.Content.NPCs.TBUGs;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.Draedon;
using CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower;
using CalamityOverhaul.Content.Scenarios.Draedon.Tzeentch;
using CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines;
using CalamityOverhaul.Content.Scenarios.OldDuke;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Wraiths.Runtime;
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
        //保留已发布的协议编号，客户端不再发送该请求
        ReservedToriiShrineGenerationRequest,
        ToriiShrineSync,
        Wraith,
        //保留编号：原鬼切教程练习鬼影通道已删除，客户端不再发送
        ReservedOnikiriTutorial,
        //保留编号：原真夜赠礼资格下发通道已删除，赠礼登记全程本地
        ReservedHimayoGiftEntitlements,
        WeaverGrievancesManifestation,
        OnikiriTutorialTarget,
        OnikiriItemOperation,
        SoyMilkBossPowerDamageReport,
        Ram,
        Cyberware,
        Sandevistan,
        SelfHackCrystal,
        CyberspaceAction,
        SHPCModuleSync,
        OnikiriDomain,
        KikasaDomain,
        TBUGShop,
        SHPCNPCEffect,
        KikasaLakeFX,
        KikasaDrown,
        //弹药置换的扣弹意图：服务端判定 → 喂弹者本机结算（背包归客户端所有）
        MunitionSwapConsume,
        //炮台联网的齐射瞄准：施法者客户端慢节拍上行光标
        TurretMeshAim,
        //地牢水牢层的阀门：客户端只上行"我拉了这根杆"，水位由服务端裁决并回播区块
        DungeonworldWaterValve,
        //鬼伞·大范围重启：客户端请求 → 服务器圈定并广播 → 各端本地倒带
        KikasaReset,
        //世界鬼伞的权威世界态下发（是否已生成+锚点），镜像 ToriiShrineSync
        OniUmbrellaSync,
        //鬼伞·唤雨符：挂/摘请求→服务端校验→回执，另有符箧快照上行
        KikasaTalisman,
        //断罪师显现：认领请求→服务端许可→归属端拔斧结算，镜像 WeaverGrievancesManifestation
        ArbiterManifestation,
        //鬼伞·唤雨符 NPC 叠层（洇痕/渍/霉蚀）：归属端写入→紧凑广播，服务端承载并转播给旁观端
        KikasaTalismanStack,
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
            else if (type == CWRMessageType.MunitionSwapConsume) {
                Content.HackTimes.Protocols.MunitionSwap.HandleConsume(reader, whoAmI);
            }
            else if (type == CWRMessageType.TurretMeshAim) {
                Content.HackTimes.Protocols.TurretMesh.HandleAim(reader, whoAmI);
            }
            else if (type == CWRMessageType.ToriiShrineSync) {
                ToriiShrine.ReceiveShrineSync(reader);
            }
            else if (type == CWRMessageType.OniUmbrellaSync) {
                Content.Scenarios.OniRainWorlds.OniUmbrellaWorldSpawn.ReceiveUmbrellaSync(reader);
            }
            else if (type == CWRMessageType.DungeonworldWaterValve) {
                Content.Scenarios.Dungeonworld.Machines.DungeonworldWaterGate.HandleValveRequest(reader, whoAmI);
            }

            ModifyCrabulon.NetHandle(type, reader, whoAmI);
            DraedonEffect.NetHandle(type, reader, whoAmI);
            TzeentchEffect.NetHandle(type, reader, whoAmI);
            SignalTowerTargetManager.NetHandle(type, reader, whoAmI);
            OldDukeEffect.NetHandle(type, reader, whoAmI);
            MachineEffect.NetHandle(type, reader, whoAmI);
            WraithNet.NetHandle(type, reader, whoAmI);
            WGManifestationNet.NetHandle(type, reader, whoAmI);
            ArbiterManifestationNet.NetHandle(type, reader, whoAmI);
            OnikiriTutorialNet.NetHandle(type, reader, whoAmI);
            OnikiriNet.NetHandle(type, reader, whoAmI);
            OniDomainNet.NetHandle(type, reader, whoAmI);
            KikasaDomainNet.NetHandle(type, reader, whoAmI);
            KikasaLakeNet.NetHandle(type, reader, whoAmI);
            KikasaDrownNet.NetHandle(type, reader, whoAmI);
            KikasaResetNet.NetHandle(type, reader, whoAmI);
            KikasaTalismanNet.NetHandle(type, reader, whoAmI);
            KikasaTalismanStackNPC.NetHandle(type, reader, whoAmI);
            RamNet.NetHandle(type, reader, whoAmI);
            CyberwareNet.NetHandle(type, reader, whoAmI);
            SandevistanNet.NetHandle(type, reader, whoAmI);
            SelfHackCrystalNet.NetHandle(type, reader, whoAmI);
            CyberspaceActionNet.NetHandle(type, reader, whoAmI);
            SHPCModuleNet.NetHandle(type, reader, whoAmI);
            SHPCNPCEffects.NetHandle(type, reader, whoAmI);
            TBUGShopNet.NetHandle(type, reader, whoAmI);
        }
    }
}
