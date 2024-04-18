using CalamityMod.Events;
using CalamityMod;
using Terraria;
using System.Collections.Generic;
using Terraria.ModLoader;
using CalamityMod.NPCs.NormalNPCs;
using Microsoft.Xna.Framework;
using System.Linq;
using Terraria.Audio;
using Terraria.ID;
using CalamityOverhaul.Common;
using CalamityMod.Items.Accessories;
using CalamityMod.Items.Materials;

namespace CalamityOverhaul.Content.Events
{
    internal class TungstenRiot
    {
        public static TungstenRiot Instance;
        public static Dictionary<int, TungstenEventNPCValue> TungstenEventNPCDic;
        public const float MaxEventIntegration = 300;
        public bool TungstenRiotIsOngoing;
        public int EventKillPoints;
        public int Time;
        public Color MainColor => new Color(28, 169, 175);
        public float EventKillRatio => EventKillPoints / MaxEventIntegration;
        public struct TungstenEventNPCValue
        {
            public int InvasionContributionPoints { get; set; }
            public float SpawnRate { get; set; }
            public TungstenEventNPCValue(int totalPoints, float spawnRate) {
                InvasionContributionPoints = totalPoints;
                SpawnRate = spawnRate;
            }
        }

        public void Load() {
            Instance = this;
            TungstenEventNPCDic = new Dictionary<int, TungstenEventNPCValue>() {
                { ModContent.NPCType<WulfrumDrone>(), new TungstenEventNPCValue(1, 1) },
                { ModContent.NPCType<WulfrumGyrator>(), new TungstenEventNPCValue(1, 1) },
                { ModContent.NPCType<WulfrumHovercraft>(), new TungstenEventNPCValue(1, 1) },
                { ModContent.NPCType<WulfrumRover>(), new TungstenEventNPCValue(1, 1) },
            };
        }

        public static void UnLoad() {
            Instance = null;
            TungstenEventNPCDic = null;
        }

        public void Update() {
        }

        public bool TryStartEvent() {
            if (TungstenRiotIsOngoing || (!NPC.downedBoss1 && !Main.hardMode && !DownedBossSystem.downedAquaticScourge) 
                || BossRushEvent.BossRushActive || AcidRainEvent.AcidRainEventIsOngoing) {
                return false;
            }

            int playerCount = 0;
            for (int i = 0; i < Main.player.Length; i++) {
                if (Main.player[i].active)
                    playerCount++;
            }

            foreach (NPC n in Main.npc) {
                if (!TungstenEventNPCDic.ContainsKey(n.type) && n.active && !n.friendly && !n.boss) {
                    n.active = false;
                }
            }

            if (playerCount > 0) {
                CWRUtils.Text(CWRLocText.GetTextValue("Event_TungstenRiot_Text_1"), MainColor);
                SoundEngine.PlaySound(SoundID.Roar);
                TungstenRiotIsOngoing = true;
                EventKillPoints = (int)MaxEventIntegration;
            }

            EventNetWork();

            return true;
        }

        public void CloseEvent() {
            foreach (var n in Main.npc) {
                if (TungstenEventNPCDic.Keys.Contains(n.type)) {
                    n.active = false;
                }
            }
            TungstenRiotIsOngoing = false;
            EventKillPoints = 0;
            EventNetWork();
            CWRUtils.Text(CWRLocText.GetTextValue("Event_TungstenRiot_Text_2"), MainColor);
        }

        public void TungstenKillNPC(NPC npc) {
            if (TungstenEventNPCDic.ContainsKey(npc.type)) {
                EventKillPoints -= TungstenEventNPCDic[npc.type].InvasionContributionPoints;
            }
            if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                EventKillPoints -= 2;
            }
            if (EventKillPoints < 0) {
                CloseEvent();
            }
        }

        public void EventNetWork() {
            if (CWRUtils.isServer) {
                NetMessage.SendData(MessageID.WorldData);
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.TungstenRiotSync);
                netMessage.Write(TungstenRiotIsOngoing);
                netMessage.Write(EventKillPoints);
                netMessage.Send();
            }
        }

        public void ModifyEventNPCLoot(NPC npc, ref NPCLoot npcLoot) {
            if (npc.type == ModContent.NPCType<WulfrumDrone>()) {
                npcLoot.RemoveWhere(rule => true);
                npcLoot.Add(ModContent.ItemType<WulfrumMetalScrap>(), 1, 1, 3);
                npcLoot.AddIf(info => !Instance.TungstenRiotIsOngoing, ModContent.ItemType<WulfrumBattery>(), new Fraction(7, 100));
                npcLoot.AddIf(info => info.npc.ModNPC<WulfrumDrone>().Supercharged, ModContent.ItemType<EnergyCore>());
            }
            else if (npc.type == ModContent.NPCType<WulfrumGyrator>()) {
                npcLoot.RemoveWhere(rule => true);
                npcLoot.Add(ModContent.ItemType<WulfrumMetalScrap>(), 1, 1, 2);
                npcLoot.AddIf(info => !Instance.TungstenRiotIsOngoing, ModContent.ItemType<WulfrumBattery>(), new Fraction(7, 100));
                npcLoot.AddIf(info => info.npc.ModNPC<WulfrumGyrator>().Supercharged, ModContent.ItemType<EnergyCore>());
            }
            else if (npc.type == ModContent.NPCType<WulfrumHovercraft>()) {
                npcLoot.RemoveWhere(rule => true);
                npcLoot.Add(ModContent.ItemType<WulfrumMetalScrap>(), 1, 2, 3);
                npcLoot.AddIf(info => !Instance.TungstenRiotIsOngoing, ModContent.ItemType<WulfrumBattery>(), new Fraction(7, 100));
                npcLoot.AddIf(info => info.npc.ModNPC<WulfrumHovercraft>().Supercharged, ModContent.ItemType<EnergyCore>());
            }
            else if (npc.type == ModContent.NPCType<WulfrumRover>()) {
                npcLoot.RemoveWhere(rule => true);
                npcLoot.Add(ModContent.ItemType<WulfrumMetalScrap>(), 1, 1, 2);
                npcLoot.AddIf(info => !Instance.TungstenRiotIsOngoing, ModContent.ItemType<RoverDrive>(), 10);
                npcLoot.AddIf(info => !Instance.TungstenRiotIsOngoing, ModContent.ItemType<WulfrumBattery>(), new Fraction(7, 100));
                npcLoot.AddIf(info => info.npc.ModNPC<WulfrumRover>().Supercharged, ModContent.ItemType<EnergyCore>());
            }
            else if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                npcLoot.AddIf(info => Instance.TungstenRiotIsOngoing, ModContent.ItemType<EnergyCore>(), dropRateInt: 1, minQuantity: 3, maxQuantity: 5, false);
            }
        }
    }
}
