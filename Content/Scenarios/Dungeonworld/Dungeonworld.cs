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

        //六阶段切口(STRUCTURES §1.5)现行序(Wave-2追加泄洪堂/子地带两刀):
        //P10骨架→P20宏观路网→P30层规划(纯数据+足印预留,禁室与泄洪堂选址在此定点)
        //→P45禁室盖章→泄洪堂盖章(Wave-2 C路,坐标均取P30已定值)→P50层内容入口(七层全接线)
        //→P52夹层带/P54封存副翼(填充体系,消费P50留下的空闲足印)
        //→P54.5子地带(Wave-2 B路,消费副翼吃剩的死岩)→P55撒布装饰
        //→P58引路痕迹(发现引导批:三Boss房沿脊渐变面包屑,纯hash零genRand,
        //  排尾部不动既有随机流与pass相对顺序)
        //→P80校验(帧修+洪泛+门/家具审计+GenReport)
        //两个填充pass的位置是硬约束:P50之后才拿得到"全部主内容足印"这份底稿,
        //P55之前才吃得到撒布;Wave-2各新增随机消耗集中在各自接线点(R4账见
        //LayerPlanPass/ZonePass头注释;裁决§1-12:允许改变既有种子结果)
        //TimedPass=逐pass耗时记录(R5预算<3min),不改被包pass
        public override List<GenPass> Tasks => [
            new TimedPass(new SkeletonPass()),
            new TimedPass(new MacroRoutePass()),
            new TimedPass(new LayerPlanPass()),
            new TimedPass(new Gen.BossRooms.GaolBossRoomPass(() => GaolBossRoomSiting.LastOrigin)),
            new TimedPass(new Gen.BossRooms.FloodGalleryPass(() => Gen.BossRooms.FloodGallerySiting.LastOrigin)),
            new TimedPass(new Gen.BossRooms.ProofingHallPass(() => Gen.BossRooms.ProofingHallSiting.LastOrigin)),
            new TimedPass(new LayerContentPass()),
            new TimedPass(new Gen.Infill.IntersticePass()),
            new TimedPass(new Gen.Infill.AnnexPass()),
            new TimedPass(new Gen.Zones.ZonePass()),
            new TimedPass(new ScatterPass()),
            new TimedPass(new GuideTrailPass()),
            new ValidatePass()
        ];

        public static bool Active => SubworldSystem.IsActive<Dungeonworld>();

        //NormalUpdates=false 把原版世界更新整段停掉(F16/F17),需要逐帧的子系统在这里手动驱动。
        //SLib 在 ModSystem.PreUpdateWorld 之后、PostUpdateWorld 之前调本方法,每帧必到
        public override void Update() {
            //扩容行地图目标的主线程补建/设备重置自愈(6000 高世界,详见 SubworldMapGrid)
            SubworldMapGrid.Upkeep();
            Machines.DungeonworldMachines.Update();
            //Machines.DungeonworldWaterGate.Update();
        }

        //进出统一走这两个入口,快照/加载屏复位/跨世界引用清理不漏
        //过渡链路修复:先遮再冻，压黑门(0.45s 渐入全黑)完成后的下一帧才真正提交过渡,
        //把 SLib 接管前后的主线程长帧冻结藏进有意为之的入井压黑(见 DungeonworldTransitionGate)
        public static void EnterWorld() {
            DungeonworldTransitionGate.Begin(true, static () => {
                ClearCrossWorldRefs(Main.LocalPlayer);
                DungeonworldGuard.Snapshot();
                DungeonworldLoadingScreen.Enter();
                return SubworldSystem.Enter<Dungeonworld>();
            });
        }

        public static void ExitWorld() {
            DungeonworldTransitionGate.Begin(false, static () => {
                ClearCrossWorldRefs(Main.LocalPlayer);
                DungeonworldLoadingScreen.Exit();
                SubworldSystem.Exit();
                return true;
            });
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
            //小地图渲染目标网格扩容:原版[5,2]只盖3600行,6000高世界不扩必崩(DrawToMap_Section越界)
            SubworldMapGrid.Sync();
            Main.dayTime = false;
            Main.time = 0;
            //运行时子系统随世界重置(回放制:每次进入都是新生成的一份)
            Machines.DungeonworldMachines.Reset();
            //Machines.DungeonworldWaterGate.Reset();
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
