using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Common;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using CalamityOverhaul.OtherMods.BossChecklist;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.ModifySupCalNPCs
{
    internal class TraceSupCalDeath : DeathTrackingNPC
    {
        internal static bool SupCalDefeated { get; set; }
        public override void OnKill(NPC npc) {
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

        //临时钩子，后续改用前置实现
        private static void OnBossHeadSlotHook(BossHeadSlotDelegate orig, ModNPC modNPC, ref int index) {
            originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
            originallyBossRush = CWRRef.GetBossRushActive();
            if (EbnPlayer.IsConquered(Main.player[modNPC.NPC.target]) || EbnPlayer.OnEbn(Main.player[modNPC.NPC.target])) {
                CWRRef.SetDownedCalamitas(true);//如果被攻略了的话就无条件摘掉兜帽
                CWRRef.SetBossRushActive(false);//避免BossRush状态干扰
            }
            orig.Invoke(modNPC, ref index);
            CWRRef.SetDownedCalamitas(originallyDownedCalamitas);
            CWRRef.SetBossRushActive(originallyBossRush);
        }

        /// <summary>
        /// 设置AI状态，决定是否启用修改逻辑
        /// </summary>
        /// <returns></returns>
        internal static bool SetAIState() {
            if (ModGanged.InfernumModeOpenState) {
                return false;
            }
            return true;
        }

        public override bool AI() {
            if (SetAIState()) {
                originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
                originallyBossRush = CWRRef.GetBossRushActive();
                if (originallyBossRush) {
                    if (EbnPlayer.OnEbn(Main.player[npc.target]) && CWRRef.GetSupCalGiveUpCounter(npc) > 0) {
                        CWRRef.SetDownedCalamitas(false);//设置为未击败，这样可以恢复初次见面的场景
                        CWRRef.SetBossRushActive(false);
                        TrueBossRushStateByAI = true;
                    }
                    return true;
                }
            }

            if (!CWRRef.GetBossRushActive()) {//非BossRush状态下检查永恒燃烧的现在结局，执行位置替换等操作
                foreach (var p in Main.ActivePlayers) {
                    //如果已经有人达成了永恒燃烧的现在结局，说明女巫已死，玩家替换女巫的位置
                    if (p.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                        p.Teleport(npc.Center, 999);
                        if (BCKRef.Has) {
                            BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);//对于Boss列表的适配，隐藏活跃状态，避免消失时弹出信息破坏氛围
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
            if (EbnPlayer.IsConquered(Main.player[npc.target]) || EbnPlayer.OnEbn(Main.player[npc.target])) {
                CWRRef.SetDownedCalamitas(true);//如果被攻略了的话就无条件摘掉兜帽
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
