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

namespace CalamityOverhaul.Content.Events
{
    internal class TungstenRiot
    {
        public static TungstenRiot Instance;

        public bool TungstenRiotIsOngoing;

        public const float MaxEventIntegration = 300;

        public int EventKillPoints;

        public float EventKillRatio => EventKillPoints / MaxEventIntegration;

        public Color MainColor => new Color(28, 169, 175);

        public int Time;

        public struct TungstenEventNPCValue
        {
            public int InvasionContributionPoints { get; set; }
            public float SpawnRate { get; set; }
            public TungstenEventNPCValue(int totalPoints, float spawnRate) {
                InvasionContributionPoints = totalPoints;
                SpawnRate = spawnRate;
            }
        }

        public static Dictionary<int, TungstenEventNPCValue> TungstenEventNPCDic;

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

        public void TryStartEvent() {
            if (TungstenRiotIsOngoing || (!NPC.downedBoss1 && !Main.hardMode && !DownedBossSystem.downedAquaticScourge) 
                || BossRushEvent.BossRushActive || AcidRainEvent.AcidRainEventIsOngoing) {
                return;
            }

            int playerCount = 0;
            for (int i = 0; i < Main.player.Length; i++) {
                if (Main.player[i].active)
                    playerCount++;
            }

            if (playerCount > 0) {
                "大量钨钢生物正在入侵!".Domp(MainColor);
                SoundEngine.PlaySound(SoundID.Roar);
                TungstenRiotIsOngoing = true;
                EventKillPoints = 300;
            }
        }

        public void CloseEvent() {
            foreach (var n in Main.npc) {
                if (TungstenEventNPCDic.Keys.Contains(n.type)) {
                    n.active = false;
                }
            }
            TungstenRiotIsOngoing = false;
            EventKillPoints = 0;
            "钨钢军团已被击退!".Domp(MainColor);
        }

        public void TungstenKillNPC(NPC npc) {
            if (TungstenEventNPCDic.ContainsKey(npc.type)) {
                EventKillPoints -= TungstenEventNPCDic[npc.type].InvasionContributionPoints;
            }
            if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                EventKillPoints -= 2;
            }
            if (EventKillPoints < 0) {
                EventKillPoints = 0;
                TungstenRiotIsOngoing = false;
                "钨钢军团已被击退!".Domp(MainColor);
            }
        }
    }
}
