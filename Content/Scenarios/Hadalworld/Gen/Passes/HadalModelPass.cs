using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core;
using Terraria;
using Terraria.IO;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Passes
{
    //P10:核心模型演算(Terraria无关层),规划+雕刻全在字节栅格上完成
    //种子自genRand抽取两次拼64位:同世界种子⇒同地形(蓝图H6决定论链)
    internal class HadalModelPass : GenPass
    {
        public HadalModelPass() : base("Hadalworld Model", 3f) { }

        protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration) {
            progress.Message = "深海地形演算...";
            HadalGenClock.Reset();
            HadalGenContext.ResetForNewGen();

            ulong seed = ((ulong)(uint)WorldGen.genRand.Next() << 32) | (uint)WorldGen.genRand.Next();
            var p = new HadalGenParams {
                Width = HadalworldMetrics.Width,
                Height = HadalworldMetrics.Height,
                SeaLevelRow = HadalworldMetrics.SeaLevelRow,
                SunlitBottom = HadalworldMetrics.SunlitBottom,
                TwilightBottom = HadalworldMetrics.TwilightBottom,
                MidnightBottom = HadalworldMetrics.MidnightBottom,
                AbyssalBottom = HadalworldMetrics.AbyssalBottom,
                DeepestPlayableRow = HadalworldMetrics.DeepestPlayableRow,
                Seed = seed,
            };
            progress.Set(0.1);
            HadalTerrainModel model = HadalTerrain.Build(p);
            HadalGenContext.Model = model;
            progress.Set(1.0);

            HadalTerrainPlan plan = model.Plan;
            var chokes = new System.Text.StringBuilder();
            foreach ((int y, string name) in plan.Chokes) {
                if (chokes.Length > 0) {
                    chokes.Append(',');
                }
                chokes.Append(name).Append('@').Append(y);
            }
            CWRMod.Instance.Logger.Info(
                $"[Hadalworld] P10 Model seed={seed} mouthX={plan.MouthX}"
                + $" 支沟={plan.Galleries.Count} 溶洞群={plan.CaveFields.Count}"
                + $" 竖井={plan.Shafts.Count} 下厅={plan.Halls.Count} 盆地={plan.Basins.Count}"
                + $" 礁丘={plan.Reefs.Count} 假裂缝={plan.FalseCracks.Count}"
                + $" 窄喉[{chokes}] 平原=({plan.Plain.Top}-{plan.Plain.Bottom},cx={(int)plan.Plain.CenterX})"
                + $" spawn=({model.SpawnX},{model.SpawnY})");
        }
    }
}
