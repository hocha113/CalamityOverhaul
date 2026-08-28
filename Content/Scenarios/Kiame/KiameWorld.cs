using CalamityOverhaul.Content.Scenarios.Kiame.Gen;
using CalamityOverhaul.Content.Scenarios.Kiame.Gen.Passes;
using CalamityOverhaul.Content.Scenarios.Kiame.UI;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    //鬼雨：洼地废村子世界。台地入口向东，废村连绵、洼积黑水、黑雨不歇
    //蓝图 Doc/plans/Kiame/DESIGN.md，骨架镜像 KiyumeWorld 惯例（姊妹世界不互引）
    internal class KiameWorld : Subworld
    {
        public override int Width => KiameMetrics.Width;
        public override int Height => KiameMetrics.Height;

        //雨不落盘：每次进来按（宏观种子,进度）重生成
        public override bool ShouldSave => false;
        //液体流动/接线定时器/随机 tile 更新停摆，洼水靠构造性铺设定住
        public override bool NormalUpdates => false;

        //P10 骨架 → P30 地表与洼水 → P40 废村 → P55 撒布 → P90 帧修收尾（帧修永远排最后一位）
        //缓存：SubLib 按 current.Tasks[i] 取值，每次 get 都 new 一份会让计时/日志对不上同一实例
        private List<GenPass> _tasks;
        public override List<GenPass> Tasks => _tasks ??= [
            new KiameSkeletonPass(),
            new KiameTerrainPass(),
            new KiameStructurePass(),
            new KiameScatterPass(),
            new KiameFinalizePass(),
        ];

        public static bool Active => SubworldSystem.IsActive<KiameWorld>();

        //进出统一走这两个入口，快照/加载屏复位/跨世界引用清理不漏
        public static void EnterWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            KiameMetrics.CacheMacroSeed();
            KiameGuard.Snapshot();
            KiameLoadingScreen.Enter();
            SubworldSystem.Enter<KiameWorld>();
        }

        public static void ExitWorld() {
            ClearCrossWorldRefs(Main.LocalPlayer);
            KiameLoadingScreen.Exit();
            SubworldSystem.Exit();
        }

        //加载屏薄转发
        public override void DrawSetup(GameTime gameTime) => KiameLoadingScreen.DrawSetup(gameTime);
        public override bool ChangeAudio() => KiameLoadingScreen.ChangeAudio();

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
                KiameLoadingScreen.Exit();
            }
        }

        public override void OnLoad() {
            //时间冻在夜里：环境色由 KiameAmbience 全量改写成湿墨冷灰青，这里只是不让原版日夜插一脚
            Main.dayTime = false;
            Main.time = 16200.0;
            //原版降雨旗标常开：NormalUpdates=false 下天气不走表，钉住即恒雨
            //（雨声氛围底与云暗压沉都是免费的；雨的视觉本体走 KiameAmbience 的 PRT 雨帘）
            Main.raining = true;
            Main.maxRaining = 0.9f;
            Main.cloudAlpha = 0.9f;
            Main.rainTime = 86400.0;
            //worldSurface 压到所有地板之下：玩法层判"地表"，天幕可见
            Main.worldSurface = KiameMetrics.WorldSurfaceRow;
            Main.rockLayer = KiameMetrics.RockLayerRow;
            CWRMod.Instance.Logger.Info(
                $"[Kiame] OnLoad worldSurface={Main.worldSurface} rockLayer={Main.rockLayer}"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY}) macroSeed={KiameMetrics.MacroSeed}");
        }
    }
}
