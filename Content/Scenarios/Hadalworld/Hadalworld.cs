using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen;
using CalamityOverhaul.Content.Scenarios.Hadalworld.UI;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld
{
    //深渊海沟子世界(潜渊症式深海氛围),M0=三路并行锚定骨架
    //镜像Dungeonworld惯例;所有权与契约见父会话brief
    //对外API冻结:Active/EnterWorld/ExitWorld/尺寸读Metrics/Tasks只调管线
    internal class Hadalworld : Subworld
    {
        public override int Width => HadalworldMetrics.Width;
        public override int Height => HadalworldMetrics.Height;

        //回放制:不落盘,每次进入按种子决定论重生成(镜像Dungeonworld)
        public override bool ShouldSave => false;
        //液体流动/随机tile更新停摆,需要逐帧的子系统日后在Update手动驱动
        public override bool NormalUpdates => false;

        public override List<GenPass> Tasks => HadalGenPipeline.BuildTasks();

        public static bool Active => SubworldSystem.IsActive<Hadalworld>();

        //NormalUpdates=false 把原版世界更新整段停掉,需要逐帧的子系统在这里手动驱动。
        //SLib 在 ModSystem.PreUpdateWorld 之后、PostUpdateWorld 之前调本方法,每帧必到;
        //C 路氛围若需逐帧推进,通过报告申请在此接线(镜像 Dungeonworld.Update 的驱动方式)
        public override void Update() {
        }

        //进出统一走这两个入口,快照/加载屏复位/跨世界引用清理不漏
        //过渡链路:先遮再冻,压黑门(0.45s 渐入全黑)完成后的下一帧才真正提交过渡,
        //把 SLib 接管前后的主线程长帧冻结藏进有意为之的入水压黑(见 HadalworldTransitionGate)
        public static void EnterWorld() {
            HadalworldTransitionGate.Begin(true, static () => {
                ClearCrossWorldRefs(Main.LocalPlayer);
                HadalworldGuard.Snapshot();
                HadalworldLoadingScreen.Enter();
                return SubworldSystem.Enter<Hadalworld>();
            });
        }

        public static void ExitWorld() {
            HadalworldTransitionGate.Begin(false, static () => {
                ClearCrossWorldRefs(Main.LocalPlayer);
                HadalworldLoadingScreen.Exit();
                SubworldSystem.Exit();
                return true;
            });
        }

        //加载屏薄转发(接线方式见HadalworldLoadingScreen头注释)
        public override void DrawSetup(GameTime gameTime) => HadalworldLoadingScreen.DrawSetup(gameTime);
        public override bool ChangeAudio() => HadalworldLoadingScreen.ChangeAudio();

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
                HadalworldLoadingScreen.Exit();
            }
        }

        public override void OnLoad() {
            //正午定格:日光带天光基准,昼夜循环留待后续裁决
            Main.dayTime = true;
            Main.time = 27000;
            //worldSurface放日光带中部让浅海吃到天光,rockLayer放暮光带顶附近
            //(背景/地下判定,取值理由见HadalworldMetrics头注释与DungeonworldMetrics排查法)
            Main.worldSurface = HadalworldMetrics.WorldSurfaceRow;
            Main.rockLayer = HadalworldMetrics.RockLayerRow;
            //深渊带屏幕底可越过UnderworldLayer=4800线(最深可玩4780行+半屏≈4814),
            //SubLib进子世界默认置true拦地狱背景/地狱光,这里显式重申契约(镜像OldNetWorld)
            SubworldSystem.hideUnderworld = true;
            //出生点兜底:SLib LoadSubworld生成前把spawn钉在世界正中(SubworldSystem.cs:1303),
            //生成pass负责覆写(B路协议:同时写Main.spawnTileX/Y与Metrics.SpawnTile);
            //若pass漏写(仍在正中哨兵值)则用Metrics.SpawnTile顶上,
            //最后统一回写SpawnTile,保证三路运行期读数一致
            if (Main.spawnTileX == HadalworldMetrics.Width / 2
                && Main.spawnTileY == HadalworldMetrics.Height / 2) {
                Main.spawnTileX = HadalworldMetrics.SpawnTile.X;
                Main.spawnTileY = HadalworldMetrics.SpawnTile.Y;
            }
            HadalworldMetrics.SpawnTile = new(Main.spawnTileX, Main.spawnTileY);
            CWRMod.Instance.Logger.Info(
                $"[Hadalworld] OnLoad worldSurface={Main.worldSurface} rockLayer={Main.rockLayer}"
                + $" spawn=({Main.spawnTileX},{Main.spawnTileY})"
                + $" hideUnderworld={SubworldSystem.hideUnderworld} dayTime={Main.dayTime} time={Main.time}");
        }
    }
}
