using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Passes;
using CalamityOverhaul.Content.Scenarios.OldNet.UI;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    //旧网：黑墙外横向赛博考古子世界，M0=裸循环（进出/黑墙/距离底噪/采集结算）
    //蓝图 Doc/plans/OldNet/DESIGN.md，骨架镜像 Dungeonworld 惯例
    internal class OldNetWorld : Subworld
    {
        public override int Width => OldNetMetrics.Width;
        public override int Height => OldNetMetrics.Height;

        //每次深潜按（宏观种子,进度）决定论重生成，世界不落盘
        public override bool ShouldSave => false;
        //液体流动/接线定时器/随机tile更新停摆，M0 无此依赖
        public override bool NormalUpdates => false;

        //M2a 流水线：P10骨架→P20路网→P30分带规划→P50带内容→P55撒布→P80校验
        //TimedPass 记录逐pass耗时（每次深潜重生成，生成耗时=玩家等待时间）
        public override List<GenPass> Tasks => [
            new OldNetTimedPass(new OldNetSkeletonPass()),
            new OldNetTimedPass(new OldNetRoutePass()),
            new OldNetTimedPass(new OldNetZonePlanPass()),
            new OldNetTimedPass(new OldNetZoneContentPass()),
            new OldNetTimedPass(new OldNetScatterPass()),
            new OldNetValidatePass(),
        ];

        public static bool Active => SubworldSystem.IsActive<OldNetWorld>();

        //进出统一走这两个入口，快照/加载屏复位/跨世界引用清理不漏
        public static void EnterWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            OldNetMetrics.CacheMacroSeed();
            OldNetGuard.Snapshot();
            OldNetLoadingScreen.Enter();
            SubworldSystem.Enter<OldNetWorld>();
        }

        public static void ExitWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            OldNetLoadingScreen.Exit();
            SubworldSystem.Exit();
        }

        //加载屏薄转发
        public override void DrawSetup(GameTime gameTime) => OldNetLoadingScreen.DrawSetup(gameTime);
        public override bool ChangeAudio() => OldNetLoadingScreen.ChangeAudio();

        //跨世界 chest/sign/TileEntity 索引不清会 FindRecipes NRE（F34）
        internal static void ClearCrossWorldRefs(Player player) {
            if (player == null || !player.active) {
                return;
            }
            player.chest = -1;
            player.sign = -1;
            player.SetTalkNPC(-1, fromNet: false);
            player.tileEntityAnchor.Clear();
            Main.npcChatText = string.Empty;
        }

        public override void OnExit() {
            //SubLib 自带 Return 按钮可绕开 ExitWorld，这里兜底清引用+复位加载屏（重复调用无害）
            if (!Main.dedServ) {
                ClearCrossWorldRefs(Main.LocalPlayer);
                OldNetLoadingScreen.Exit();
            }
        }

        public override void OnLoad() {
            Main.dayTime = false;
            Main.time = 16200.0;
            //与 Dungeonworld 相反：worldSurface 压到地板带以下，玩法层判"地表"让天幕可见；
            //rockLayer 再往下满足原版分层判定的形式需求
            Main.worldSurface = OldNetMetrics.WorldSurfaceRow;
            Main.rockLayer = OldNetMetrics.RockLayerRow;
            CWRMod.Instance.Logger.Info(
                $"[OldNet] OnLoad worldSurface={Main.worldSurface} rockLayer={Main.rockLayer}"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY}) macroSeed={OldNetMetrics.MacroSeed}");
        }
    }
}
