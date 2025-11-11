using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal
{
    internal class ModifySupCalSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (NPC.AnyNPCs(CWRID.NPC_WITCH)) {
                int witch = NPC.FindFirstNPC(CWRID.NPC_WITCH);
                if (witch == -1) {
                    return;
                }
                bool hasEbn = false;
                foreach (var p in Main.ActivePlayers) {
                    //如果已经有人达成了永恒燃烧的现在结局，说明女巫已死，玩家替换女巫的位置
                    if (p.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                        hasEbn = true;
                        p.Teleport(Main.npc[witch].Center, 999);
                    }
                }
                if (hasEbn) {
                    Main.npc[witch].active = false;
                    Main.npc[witch].netUpdate = true;
                }
            }
        }
    }

    internal class ModifySupCalNPC : NPCOverride
    {
        public override int TargetID => CWRID.NPC_SupremeCalamitas;
        private static bool originallyDownedCalamitas = false;
        public override bool AI() {
            if (CWRRef.GetBossRushActive()) {
                return true;//Boss Rush模式下不进行任何修改
            }
            foreach (var p in Main.ActivePlayers) {
                //如果已经有人达成了永恒燃烧的现在结局，说明女巫已死，玩家替换女巫的位置
                if (p.TryGetADVSave(out var save) && save.EternalBlazingNow) {
                    p.Teleport(npc.Center, 999);
                    npc.active = false;
                    npc.netUpdate = true;
                    return false;
                }
            }
            return base.AI();
        }

        public override void PostAI() {
            if (EbnEffect.IsActive) {
                if (CWRRef.GetSupCalGiveUpCounter(npc) < 120) {
                    CWRRef.SetSupCalGiveUpCounter(npc, 120);
                }
            }
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            originallyDownedCalamitas = CWRRef.GetDownedCalamitas();
            if (EbnPlayer.IsConquered(Main.player[npc.target])) {
                CWRRef.SetDownedCalamitas(true);//如果被攻略了的话就无条件摘掉兜帽
            }
            return base.Draw(spriteBatch, screenPos, drawColor);
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            CWRRef.SetDownedCalamitas(originallyDownedCalamitas);
            return base.PostDraw(spriteBatch, screenPos, drawColor);
        }
    }
}
