using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Passes;
using CalamityOverhaul.Content.Scenarios.Dungeonworld.UI;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld
{
    //垂直分层大地牢子世界,M0=骨架可走+运行时基线
    //蓝图Doc\plans\Dungeonworld\STRUCTURES.md,镜像CybCourseWorld惯例
    internal class Dungeonworld : Subworld
    {
        public override int Width => DungeonworldMetrics.Width;
        public override int Height => DungeonworldMetrics.Height;

        //进度回放制:世界不落盘,每次进入按(种子,进度)决定论重生成(§4.5)
        public override bool ShouldSave => false;
        //默认false:液体流动/接线定时器/随机tile更新停摆(F16/F17),需要的子系统日后在Update手动驱动
        public override bool NormalUpdates => false;

        //六阶段切口(STRUCTURES §1.5)Wave-1现行序:
        //P10骨架→P20宏观路网→P30层规划(纯数据+足印预留,禁室选址在此定点)
        //→P45禁室盖章(坐标取P30已定值)→P50层内容入口(L1/L2路,未落盘则空脊)
        //→P55撒布装饰→P80校验(帧修+洪泛+门/家具审计+GenReport)
        //TimedPass=逐pass耗时记录(R5预算<3min),不改被包pass
        public override List<GenPass> Tasks => [
            new TimedPass(new SkeletonPass()),
            new TimedPass(new MacroRoutePass()),
            new TimedPass(new LayerPlanPass()),
            new TimedPass(new Gen.BossRooms.GaolBossRoomPass(() => GaolBossRoomSiting.LastOrigin)),
            new TimedPass(new LayerContentPass()),
            new TimedPass(new ScatterPass()),
            new ValidatePass()
        ];

        public static bool Active => SubworldSystem.IsActive<Dungeonworld>();

        //进出统一走这两个入口,快照/加载屏复位/跨世界引用清理不漏
        public static void EnterWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            DungeonworldGuard.Snapshot();
            DungeonworldLoadingScreen.Enter();
            SubworldSystem.Enter<Dungeonworld>();
        }

        public static void ExitWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            DungeonworldLoadingScreen.Exit();
            SubworldSystem.Exit();
        }

        //B路加载屏薄转发(接线方式见DungeonworldLoadingScreen头注释)
        public override void DrawSetup(GameTime gameTime) => DungeonworldLoadingScreen.DrawSetup(gameTime);
        public override bool ChangeAudio() => DungeonworldLoadingScreen.ChangeAudio();

        //跨世界chest/sign/TileEntity索引不清会FindRecipes NRE(F34)
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
            //SubLib自带Return按钮可绕开ExitWorld,这里兜底清引用+复位加载屏(重复调用无害)
            if (!Main.dedServ) {
                ClearCrossWorldRefs(Main.LocalPlayer);
                DungeonworldLoadingScreen.Exit();
            }
        }

        public override void OnLoad() {
            Main.dayTime = false;
            Main.time = 0;
            //worldSurface压到天空缓冲带底,全图判"地下",ZoneDungeon三条件之一(F11/§1.3)
            //太高会误判地表,太低会把上层判进地狱带(F25先例注释)
            Main.worldSurface = DungeonworldMetrics.WorldSurfaceRow;
            Main.rockLayer = DungeonworldMetrics.RockLayerRow;
            //M0实测断言:SubLib默认拷贝集含downedBoss3(SubworldSystem.CopyDowned已核源码),进世界看此行
            CWRMod.Instance.Logger.Info(
                $"[Dungeonworld] OnLoad downedBoss3={NPC.downedBoss3} hardMode={Main.hardMode}"
                + $" worldSurface={Main.worldSurface} rockLayer={Main.rockLayer}");
        }
    }
}
