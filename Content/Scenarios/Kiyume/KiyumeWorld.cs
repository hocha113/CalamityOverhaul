using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Passes;
using CalamityOverhaul.Content.Scenarios.Kiyume.UI;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiyume
{
    //鬼梦：湖畔长卷子世界。西边是无边的血湖，往东依次是滩涂、村落、枯林、远山
    //蓝图 Doc/plans/Kiyume/DESIGN.md，骨架镜像 OldNetWorld 惯例
    internal class KiyumeWorld : Subworld
    {
        public override int Width => KiyumeMetrics.Width;
        public override int Height => KiyumeMetrics.Height;

        //梦不落盘：每次进来按（宏观种子,进度）重生成
        public override bool ShouldSave => false;
        //液体流动/接线定时器/随机 tile 更新停摆，湖水靠构造性铺设定住
        public override bool NormalUpdates => false;

        //P10 骨架 → P30 地表材质与村落 → P55 撒布 → P90 帧修收尾（帧修永远排最后一位）
        //缓存：SubLib 按 current.Tasks[i] 取值，每次 get 都 new 一份会让计时/日志对不上同一实例
        private List<GenPass> _tasks;
        public override List<GenPass> Tasks => _tasks ??= [
            new KiyumeSkeletonPass(),
            new KiyumeTerrainPass(),
            new KiyumeScatterPass(),
            new KiyumeFinalizePass(),
        ];

        public static bool Active => SubworldSystem.IsActive<KiyumeWorld>();

        //进出统一走这两个入口，快照/加载屏复位/跨世界引用清理不漏
        public static void EnterWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            KiyumeMetrics.CacheMacroSeed();
            KiyumeGuard.Snapshot();
            KiyumeLoadingScreen.Enter();
            SubworldSystem.Enter<KiyumeWorld>();
        }

        public static void ExitWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            KiyumeLoadingScreen.Exit();
            SubworldSystem.Exit();
        }

        //加载屏薄转发
        public override void DrawSetup(GameTime gameTime) => KiyumeLoadingScreen.DrawSetup(gameTime);
        public override bool ChangeAudio() => KiyumeLoadingScreen.ChangeAudio();

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
                KiyumeLoadingScreen.Exit();
                Fog.KiyumeFogSim.Reset();
            }
        }

        public override void OnLoad() {
            //时间冻在夜里：环境色由 KiyumeLightSystem 全量改写成血暮，这里只是不让原版日夜插一脚
            Main.dayTime = false;
            Main.time = 16200.0;
            //worldSurface 压到所有地板之下：玩法层判"地表"，天幕可见（同 OldNet 方向，与深牢相反）
            Main.worldSurface = KiyumeMetrics.WorldSurfaceRow;
            Main.rockLayer = KiyumeMetrics.RockLayerRow;
            CWRMod.Instance.Logger.Info(
                $"[Kiyume] OnLoad worldSurface={Main.worldSurface} rockLayer={Main.rockLayer}"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY}) macroSeed={KiyumeMetrics.MacroSeed}");
        }
    }
}
