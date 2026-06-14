using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇入 RAM 成本评估，1.0x~3.0x</summary>
    internal class HackCostEvaluator : ICWRLoader
    {
        //boss 倍率表，SetupData 填充
        private static readonly Dictionary<int, float> bossMultiplierTable = new();

        void ICWRLoader.SetupData() {
            bossMultiplierTable.Clear();
            RegisterVanillaBossTiers();
            RegisterCalamityBossTiers();
        }

        #region 分级注册

        private static void RegisterVanillaBossTiers() {
            //前肉山 Boss（1.5x）
            SetTier(NPCID.KingSlime, 1.5f);
            SetTier(NPCID.EyeofCthulhu, 1.5f);
            SetTier(NPCID.EaterofWorldsHead, 1.5f);
            SetTier(NPCID.EaterofWorldsBody, 1.5f);
            SetTier(NPCID.EaterofWorldsTail, 1.5f);
            SetTier(NPCID.BrainofCthulhu, 1.5f);
            SetTier(NPCID.SkeletronHead, 1.5f);
            SetTier(NPCID.SkeletronHand, 1.5f);
            SetTier(NPCID.QueenBee, 1.5f);
            SetTier(NPCID.Deerclops, 1.5f);

            //肉山（1.8x）
            SetTier(NPCID.WallofFlesh, 1.8f);
            SetTier(NPCID.WallofFleshEye, 1.8f);

            //前期肉山后 Boss（2.0x）
            SetTier(NPCID.QueenSlimeBoss, 2.0f);
            SetTier(NPCID.TheDestroyer, 2.0f);
            SetTier(NPCID.TheDestroyerBody, 2.0f);
            SetTier(NPCID.TheDestroyerTail, 2.0f);
            SetTier(NPCID.Retinazer, 2.0f);
            SetTier(NPCID.Spazmatism, 2.0f);
            SetTier(NPCID.SkeletronPrime, 2.0f);
            SetTier(NPCID.PrimeSaw, 2.0f);
            SetTier(NPCID.PrimeCannon, 2.0f);
            SetTier(NPCID.PrimeLaser, 2.0f);
            SetTier(NPCID.PrimeVice, 2.0f);

            //肉山后中期 Boss（2.2x）
            SetTier(NPCID.Plantera, 2.2f);
            SetTier(NPCID.Golem, 2.2f);
            SetTier(NPCID.GolemHead, 2.2f);
            SetTier(NPCID.GolemFistLeft, 2.2f);
            SetTier(NPCID.GolemFistRight, 2.2f);

            //后期 Boss（2.5x）
            SetTier(NPCID.DukeFishron, 2.5f);
            SetTier(NPCID.HallowBoss, 2.5f);
            SetTier(NPCID.CultistBoss, 2.5f);

            //月球领主（2.8x）
            SetTier(NPCID.MoonLordCore, 2.8f);
            SetTier(NPCID.MoonLordHand, 2.8f);
            SetTier(NPCID.MoonLordHead, 2.8f);
            SetTier(NPCID.MoonLordLeechBlob, 2.8f);
        }

        private static void RegisterCalamityBossTiers() {
            if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;

            //前肉山 Cal Boss（1.5x）
            TryRegisterMod(cal, "DesertScourgeHead", 1.5f);
            TryRegisterMod(cal, "Crabulon", 1.5f);
            TryRegisterMod(cal, "HiveMind", 1.5f);
            TryRegisterMod(cal, "PerforatorHive", 1.5f);
            TryRegisterMod(cal, "SlimeGodCore", 1.5f);
            TryRegisterMod(cal, "SlimeGod", 1.5f);
            TryRegisterMod(cal, "SlimeGodRun", 1.5f);

            //肉山后早期 Cal Boss（2.0x）
            TryRegisterMod(cal, "Cryogen", 2.0f);
            TryRegisterMod(cal, "AquaticScourgeHead", 2.0f);
            TryRegisterMod(cal, "BrimstoneElemental", 2.0f);
            TryRegisterMod(cal, "CalamitasClone", 2.0f);

            //后期 Cal Boss（2.3x）
            TryRegisterMod(cal, "Leviathan", 2.3f);
            TryRegisterMod(cal, "Siren", 2.3f);
            TryRegisterMod(cal, "AstrumAureus", 2.3f);
            TryRegisterMod(cal, "PlaguebringerGoliath", 2.3f);
            TryRegisterMod(cal, "Ravager", 2.3f);

            //后肉山后期（2.5x）
            TryRegisterMod(cal, "AstrumDeusHead", 2.5f);

            //后月球领主 Cal Boss（2.8x）
            TryRegisterMod(cal, "ProfanedGuardianCommander", 2.8f);
            TryRegisterMod(cal, "ProviderenceKitten", 2.8f);
            TryRegisterMod(cal, "Providence", 2.8f);
            TryRegisterMod(cal, "CeaselessVoid", 2.8f);
            TryRegisterMod(cal, "StormWeaverHead", 2.8f);
            TryRegisterMod(cal, "Signus", 2.8f);
            TryRegisterMod(cal, "Polterghast", 2.8f);
            TryRegisterMod(cal, "OldDuke", 2.8f);
            TryRegisterMod(cal, "DevourerofGodsHead", 3.0f);
            TryRegisterMod(cal, "Yharon", 3.0f);
            TryRegisterMod(cal, "ThanatosHead", 3.0f);
            TryRegisterMod(cal, "AresBody", 3.0f);
            TryRegisterMod(cal, "Apollo", 3.0f);
            TryRegisterMod(cal, "Artemis", 3.0f);
            TryRegisterMod(cal, "CalamitasClone2", 3.0f);
            TryRegisterMod(cal, "CalamitasShadow2", 3.0f);
            TryRegisterMod(cal, "Draedon", 3.0f);
            TryRegisterMod(cal, "PrimordialWyrmHead", 3.0f);
        }

        private static void SetTier(int npcType, float multiplier) {
            bossMultiplierTable[npcType] = multiplier;
        }

        private static void TryRegisterMod(Mod mod, string npcName, float multiplier) {
            if (mod.TryFind<ModNPC>(npcName, out ModNPC npc))
                bossMultiplierTable[npc.Type] = multiplier;
        }

        #endregion

        #region 对外接口

        /// <summary>目标 RAM 成本，含倍率，至少 1</summary>
        public static int GetActualCost(QuickHackDef hack, IHackTarget target) {
            float multiplier = GetMultiplier(target);
            return Math.Max(1, (int)(hack.RamCost * multiplier + 0.5f));
        }

        /// <summary>成本倍率 1.0x~3.0x</summary>
        public static float GetMultiplier(IHackTarget target) {
            if (target is not NpcScannable npcScan) return 1.0f;
            int idx = npcScan.NpcIndex;
            if (idx < 0 || idx >= Main.maxNPCs) return 1.0f;
            NPC npc = Main.npc[idx];
            if (!npc.active) return 1.0f;
            return npc.boss ? EvaluateBossMultiplier(npc) : EvaluateRegularNpcMultiplier(npc);
        }

        #endregion

        #region 内部评估逻辑

        private static float EvaluateBossMultiplier(NPC npc) {
            //优先用自身类型查表
            if (bossMultiplierTable.TryGetValue(npc.type, out float m)) return m;
            //体段经 realLife 找头部查表
            int anchorIdx = NpcGroupHelper.GetAnchorIndex(npc);
            if (anchorIdx != npc.whoAmI && anchorIdx >= 0) {
                int anchorType = Main.npc[anchorIdx].type;
                if (bossMultiplierTable.TryGetValue(anchorType, out float am)) return am;
            }
            return EstimateBossMultiplierByStats(npc);
        }

        //未登记的 boss 按 lifeMax 粗估
        private static float EstimateBossMultiplierByStats(NPC npc) {
            int life = npc.lifeMax;
            if (life >= 500_000) return 3.0f;
            if (life >= 200_000) return 2.8f;
            if (life >= 50_000) return 2.5f;
            if (life >= 20_000) return 2.0f;
            if (life >= 5_000) return 1.8f;
            return 1.5f;
        }

        private static float EvaluateRegularNpcMultiplier(NPC npc) {
            Player local = Main.LocalPlayer;
            if (local == null) return 1.0f;
            float playerDR = Math.Clamp(local.endurance, 0f, 0.99f);
            float effectiveDmg = Math.Max(1f, npc.damage - local.statDefense * 0.5f) * (1f - playerDR);
            float hitImpact = effectiveDmg / Math.Max(local.statLifeMax, 1);
            float hpRatio = (float)npc.lifeMax / Math.Max(local.statLifeMax, 1);
            float durabilityIndex = MathF.Log2(1f + hpRatio);
            float defenseIndex = npc.defense / 50f;
            float threat = hitImpact * 50f + durabilityIndex * 5f + defenseIndex * 5f;
            if (threat >= 40f) return 1.5f;
            if (threat >= 20f) return 1.4f;
            if (threat >= 8f) return 1.2f;
            return 1.0f;
        }

        #endregion
    }
}
