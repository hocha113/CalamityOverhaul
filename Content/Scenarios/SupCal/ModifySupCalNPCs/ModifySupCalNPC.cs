using CalamityOverhaul.Content.Narrative.Common;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.OtherMods.BossChecklist;
using CalamityOverhaul.OtherMods.InfernumMode;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.ModifySupCalNPCs
{
    internal class TraceSupCalDeath : DeathTrackingNPC
    {
        internal static bool SupCalDefeated { get; set; }
        public override void OnNPCDeath(NPC npc) {
            if (npc.type == CWRID.NPC_SupremeCalamitas) {
                SupCalDefeated = true;
            }
        }
    }

    internal class ModifySupCalNPC : NPCOverride, ICWRLoader
    {
        public override int TargetID => CWRID.NPC_SupremeCalamitas;

        private static bool originallyDownedCalamitas = false;
        private static bool originallyBossRush = false;
        public static bool TrueBossRushStateByAI;

        private delegate void BossHeadSlotDelegate(ModNPC modNPC, ref int index);

        void ICWRLoader.LoadData() {
            var type = CWRRef.GetNPC_SupCal_Type();
            if (type != null) {
                var meth = type.GetMethod("BossHeadSlot", BindingFlags.Instance | BindingFlags.Public);
                VaultHook.Add(meth, OnBossHeadSlotHook);
            }
        }

        //???????????????
        private static void OnBossHeadSlotHook(BossHeadSlotDelegate orig, ModNPC modNPC, ref int index) {
            originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
            originallyBossRush = CWRRef.GetBossRushActive();
            if (EbnState.IsConquered(Main.player[modNPC.NPC.target]) || EbnState.OnEbn(Main.player[modNPC.NPC.target])) {
                CWRRef.SetDownedCalamitas(true);//???????????????????????????
                CWRRef.SetBossRushActive(false);//????BossRush??????
            }
            orig.Invoke(modNPC, ref index);
            CWRRef.SetDownedCalamitas(originallyDownedCalamitas);
            CWRRef.SetBossRushActive(originallyBossRush);
        }

        /// <summary>??????? SupCal NPC ???</summary>
        internal static bool SetAIState() {
            if (InfernumRef.InfernumModeOpenState) {
                return false;
            }
            return true;
        }

        public override bool AI() {
            if (SetAIState()) {
                originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
                originallyBossRush = CWRRef.GetBossRushActive();
                if (originallyBossRush) {
                    if (EbnState.OnEbn(Main.player[npc.target]) && CWRRef.GetSupCalGiveUpCounter(npc) > 0) {
                        CWRRef.SetDownedCalamitas(false);//??????????????????????????????????
                        CWRRef.SetBossRushActive(false);
                        TrueBossRushStateByAI = true;
                    }
                    return true;
                }
            }

            if (!CWRRef.GetBossRushActive()) {//??BossRush??????????????????????????????I?????
                foreach (var p in Main.ActivePlayers) {
                    //??????????????????????????????????????????????I????????
                    if (EbnState.OnEbn(p)) {
                        p.Teleport(npc.Center, 999);
                        if (BCKRef.Has) {
                            BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);//????Boss??????????????????????????????????????????
                        }
                        npc.active = false;
                        npc.netUpdate = true;
                        return false;
                    }
                }
            }

            return true;
        }

        public override void PostAI() {
            if (SetAIState()) {
                CWRRef.SetDownedCalamitas(originallyDownedCalamitas);
                CWRRef.SetBossRushActive(originallyBossRush);
            }

            if (EbnEffect.IsActive) {
                if (CWRRef.GetSupCalGiveUpCounter(npc) < 120) {
                    CWRRef.SetSupCalGiveUpCounter(npc, 120);
                }
            }

            TrueBossRushStateByAI = false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
            originallyBossRush = CWRRef.GetBossRushActive();
            if (EbnState.IsConquered(Main.player[npc.target]) || EbnState.OnEbn(Main.player[npc.target])) {
                CWRRef.SetDownedCalamitas(true);//???????????????????????????
                CWRRef.SetBossRushActive(false);
            }
            return base.Draw(spriteBatch, screenPos, drawColor);
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            CWRRef.SetDownedCalamitas(originallyDownedCalamitas);
            CWRRef.SetBossRushActive(originallyBossRush);
            return base.PostDraw(spriteBatch, screenPos, drawColor);
        }
    }
}
