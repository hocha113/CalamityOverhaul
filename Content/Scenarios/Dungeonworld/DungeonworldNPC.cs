using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs.Elites;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld
{
    //守卫治理第一层(§4.5/R3):子世界内不无条件刷地牢守卫
    //Wave-2 追加(WAVE2-ENEMIES §3.1/§4):提灯巡守警报浓度阀走本文件(投放规则归 IMPL-D 独占);
    //精英自身的分层权重在各怪 SpawnChance 自持,是对原版怪表的"叠加"而非"替换",pool 不再动别的条目
    internal class DungeonworldNPC : GlobalNPC
    {
        //未败骷髅王时原版地牢分支把每次自然刷怪替换为守卫68且spawnRate=10(TML NPC.cs L75817/L73961)
        //pool[0]=原版选择的整体权重,清零即屏蔽该分支;已败后原版怪表照常生效
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!Dungeonworld.Active) {
                return;
            }
            if (!NPC.downedBoss3 && spawnInfo.Player.ZoneDungeon) {
                pool[0] = 0f;
            }
        }

        //警报增援浓度阀(服务器消费):追缉期间对 1500px 内玩家 spawnRate ×2.5、maxSpawns +2,
        //持续至追缉结束 +8s(残留由 Director 过期表达);即时增援保底通道在 LanternWarden 鸣警尾拍
        //生效节奏[待游戏内检查]
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns) {
            if (!Dungeonworld.Active || !DungeonworldEliteGate.Enabled) {
                return;
            }
            if (DungeonworldEliteDirector.AlarmSurging(player)) {
                spawnRate = Math.Max(1, (int)(spawnRate / 2.5f));
                maxSpawns += 2;
            }
        }
    }
}
