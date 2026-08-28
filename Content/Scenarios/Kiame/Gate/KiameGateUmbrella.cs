using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.Scenarios.OniRainWorlds;
using CalamityOverhaul.Content.Scenarios.Shenyo;
using InnoVault.Cinematics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gate
{
    /// <summary>
    /// 鬼域门伞：获得鬼伞之后，主世界随黎明游走的那把入口伞。<br/>
    /// 视觉与故事伞同一副身体（插地鬼伞+黑水洼+鬼眼，继承全部表现管线），
    /// 门禁相反：故事伞获伞即隐，门伞获伞才见——鬼雨初遇是一次性叙事空间，
    /// 这把是它的常设回门。<br/>
    /// 右键触发确认帧演出，聚雨三十来帧后把本地玩家送进 <see cref="KiameWorld"/>；
    /// 锚点维护与黎明换位在 <see cref="KiameGateSpawn"/>
    /// </summary>
    internal class KiameGateUmbrella : OniRainWorldUmbrella
    {
        //入雨确认帧：右键后雨向伞聚拢这么多帧，再落进子世界
        private const int EnterDelayFrames = 36;

        //纯本地演出量：触发者本机走表，其余端不知也不需要知
        private int pendingEnter;

        /// <summary>本地玩家是否已真正获得鬼伞（门伞可见性与交互的共用门）</summary>
        internal static bool LocalPlayerHasKikasa() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return false;
            }
            return ShenyoStorySync.KikasaGranted
                || player.HasItem(ModContent.ItemType<KikasaItem>());
        }

        internal override bool VisibleToLocalPlayer() => LocalPlayerHasKikasa();

        /// <summary>聚雨演出躁动：确认帧走表时从 0 涨到 1，伞与水洼一起躁起来</summary>
        protected override float AgitationLevel
            => pendingEnter > 0 ? 1f - pendingEnter / (float)EnterDelayFrames : 0f;

        protected override bool InteractEligible(Player player)
            => pendingEnter <= 0
            && !OniRainWorldState.LocalIn
            && !OniRainWorldTransition.Active
            && !OniRainDescentTransition.Active
            && !CutsceneDirector.IsPlaying;

        //去向同一句话：撑伞入雨（故事伞入叠加层，门伞入子世界，动作是同一个）
        protected override string HintText => OniRainWorldSystem.InteractHint.Value;

        protected override void OnInteract(Player player) {
            pendingEnter = EnterDelayFrames;
            //聚雨拍：一声远雷压过来
            SoundEngine.PlaySound(SoundID.Thunder with {
                Pitch = -0.9f,
                Volume = 0.4f,
                MaxInstances = 2,
            }, CanopyAnchor);
        }

        public override void AI() {
            base.AI();
            if (Main.dedServ || pendingEnter <= 0) {
                return;
            }
            if (--pendingEnter == 0 && !KiameWorld.Active
                && Main.LocalPlayer?.active == true) {
                KiameWorld.EnterWorld();
            }
        }
    }
}
