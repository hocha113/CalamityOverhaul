using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Items.Accessories;
using CalamityMod.Items.Materials;
using CalamityMod.NPCs.NormalNPCs;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Events
{
    internal class TungstenRiot
    {
        public static TungstenRiot Instance;
        public static Dictionary<int, TungstenEventNPCValue> TungstenEventNPCDic;
        public const float MaxEventIntegration = 300;
        private bool _tungstenRiotIsOngoing;
        public bool TungstenRiotIsOngoing {
            get => _tungstenRiotIsOngoing;
            internal set => _tungstenRiotIsOngoing = value;
        }
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

        public static void SetEventNPC(NPC npc) {
            if (!Instance.TungstenRiotIsOngoing) {
                return;
            }
            if (TungstenEventNPCDic.ContainsKey(npc.type)) {
                npc.life = npc.lifeMax = (int)(npc.lifeMax * 1.2f);
                npc.defense += 3;
            }
            if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                npc.life = npc.lifeMax = npc.lifeMax * 10;
                npc.defense += 10;
                npc.scale += 0.5f;
                npc.boss = true;
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
            if (TungstenRiotIsOngoing) {

            }
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

            EventNetWorkSend();

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
            EventNetWorkSend();
            CWRUtils.Text(CWRLocText.GetTextValue("Event_TungstenRiot_Text_2"), MainColor);
        }

        public void TungstenKillNPC(NPC npc) {
            if (TungstenEventNPCDic.ContainsKey(npc.type)) {
                EventKillPoints -= TungstenEventNPCDic[npc.type].InvasionContributionPoints;
            }
            if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                EventKillPoints -= 2;
            }
            if (EventKillPoints <= 0) {
                CloseEvent();
            }
        }

        public void EventNetWorkSend(int ignoreIndex = -1) {
            if (CWRUtils.isServer) {
                NetMessage.SendData(MessageID.WorldData);
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.TungstenRiot);
                netMessage.Write(TungstenRiotIsOngoing);
                netMessage.Write(EventKillPoints);
                netMessage.Send(-1, ignoreIndex);
            }
        }

        public static void EventNetWorkReceive(BinaryReader reader) {
            Instance.TungstenRiotIsOngoing = reader.ReadBoolean();
            Instance.EventKillPoints = reader.ReadInt32();
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

        public bool? UpdateNPCPreAISet(NPC npc) {
            if (Instance.TungstenRiotIsOngoing && npc.target >= 0 && npc.target < Main.player.Length) {
                Player player = Main.player[npc.target];
                if (TungstenEventNPCDic.ContainsKey(npc.type)) {
                    if (Main.GameUpdateCount % 60 == 0 && npc.type == ModContent.NPCType<WulfrumDrone>()) {
                        SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.7f, Pitch = -0.2f }, npc.Center);
                        if (!CWRUtils.isClient) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + Vector2.UnitX * 6f * npc.spriteDirection
                            , npc.SafeDirectionTo(player.Center, Vector2.UnitY) * 6f, ProjectileID.SaucerLaser, 12, 0f);
                        }
                    }
                }
                if (npc.type == ModContent.NPCType<WulfrumAmplifier>()) {
                    CWRUtils.WulfrumAmplifierAI(npc, 700, 300);
                    if (Main.GameUpdateCount % 60 == 0) {
                        SoundEngine.PlaySound(ScorchedEarthEcType.ShootSound with { Volume = 0.4f, Pitch = 0.6f }, npc.Center);
                        if (!CWRUtils.isClient) {
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + Vector2.UnitX * 6f * npc.spriteDirection
                            , npc.SafeDirectionTo(player.Center, Vector2.UnitY) * 6f, ProjectileID.SaucerMissile, 12, 0f);
                        }
                    }
                    return false;
                }
            }
            return null;
        }
    }
}
