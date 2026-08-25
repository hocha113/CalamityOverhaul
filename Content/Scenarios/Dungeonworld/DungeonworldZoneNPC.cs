using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Zones;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld
{
    //====================================================================
    //子地带刷怪权重微调(WAVE2-ENVIRONMENTS §4.2/§5.2/§6.2)。
    //与 DungeonworldNPC(守卫治理)分文件:那是 IMPL-D 的独占文件,本类只管地带权重。
    //
    //ZoneRegistry 服务端专有,EditSpawnPool 恰好只在服务端/单机执行,权威端天然正确;
    //联机客户端本表恒空,TryGetAt 永假,零副作用。
    //采样点=候选刷怪 tile(地带效果跟地不跟人):派系底色仍由墙变体驱动(F28),
    //这里只把各地带的招牌原版怪拉到可感知密度,不写任何 AI(D 路地盘)。
    //====================================================================
    internal class DungeonworldZoneNPC : GlobalNPC
    {
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo) {
            if (!Dungeonworld.Active) {
                return;
            }
            if (!ZoneRegistry.TryGetAt(spawnInfo.SpawnTileX, spawnInfo.SpawnTileY, out ZoneKind kind)) {
                return;
            }
            switch (kind) {
                case ZoneKind.DrownedCulvert:
                    //尖刺球=窄渠水雷:原版聚合权重 pool[0]=1 的四成,水雷感明显不刷屏
                    pool[NPCID.SpikeBall] = 0.4f;
                    break;
                case ZoneKind.AshfallStratum:
                    //地牢史莱姆从灰里滚出来:原版 1/35 幸运掷提到可感知
                    pool[NPCID.DungeonSlime] = 0.25f;
                    break;
                case ZoneKind.SlagVentBelt:
                    //困难前烈焰轮成群:Tiled 派系密度极点的叙事兑现
                    pool[NPCID.BlazingWheel] = 0.3f;
                    break;
            }
        }
    }
}
