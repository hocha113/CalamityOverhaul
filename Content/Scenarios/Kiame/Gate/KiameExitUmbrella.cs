using CalamityOverhaul.Content.Scenarios.Kiame.Gen;
using CalamityOverhaul.Content.Scenarios.Kiame.Overlay;
using InnoVault.Actors;
using InnoVault.Cinematics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gate
{
    /// <summary>
    /// 鬼域出口伞：立在子世界出生台地上的归途。右键收伞，三十帧后送回主世界。<br/>
    /// 身体沿用故事伞的全套表现（插地鬼伞+黑水洼+鬼眼）；对雨里的所有人可见
    /// </summary>
    internal class KiameExitUmbrella : OniRainWorldUmbrella
    {
        private const int ExitDelayFrames = 30;

        //纯本地演出量：触发者本机走表
        private int pendingExit;

        internal override bool VisibleToLocalPlayer() => KiameWorld.Active;

        protected override float AgitationLevel
            => pendingExit > 0 ? 1f - pendingExit / (float)ExitDelayFrames : 0f;

        protected override bool InteractEligible(Player player)
            => KiameWorld.Active && pendingExit <= 0 && !CutsceneDirector.IsPlaying;

        protected override string HintText => KiameExitKeeper.ExitHint.Value;

        protected override void OnInteract(Player player) {
            pendingExit = ExitDelayFrames;
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Pitch = -0.5f,
                Volume = 0.6f,
                MaxInstances = 2,
            }, CanopyAnchor);
        }

        public override void AI() {
            base.AI();
            if (Main.dedServ || pendingExit <= 0) {
                return;
            }
            if (--pendingExit == 0 && KiameWorld.Active) {
                KiameWorld.ExitWorld();
            }
        }
    }

    /// <summary>
    /// 出口伞看守：子世界权威端维持出生台地上恰好一把出口伞。
    /// 子世界不落盘，每次进来现立；锚点钉在出生点西侧十格的台地上
    /// </summary>
    internal class KiameExitKeeper : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        internal static LocalizedText ExitHint { get; private set; }

        private const int EnsureCheckInterval = 60;
        //出口伞锚点：出生点西侧格数（台地全平段内）
        private const int AnchorOffsetTiles = -10;

        private static int ensureTimer;

        public override void SetStaticDefaults() {
            ExitHint = this.GetLocalization(nameof(ExitHint), () => "[右键] 收伞归返");
        }

        public override void ClearWorld() => ensureTimer = 0;

        public override void PostUpdateEverything() {
            //生成权威：客户端不做任何裁决（实体乘生成广播过线）
            if (VaultUtils.isClient || !KiameWorld.Active) {
                return;
            }
            if (--ensureTimer > 0) {
                return;
            }
            ensureTimer = EnsureCheckInterval;
            EnsureExitUmbrella();
        }

        private static void EnsureExitUmbrella() {
            List<KiameExitUmbrella> actors = ActorLoader.GetActiveActors<KiameExitUmbrella>();
            KiameExitUmbrella keeper = null;
            foreach (KiameExitUmbrella actor in actors) {
                if (keeper == null) {
                    keeper = actor;
                    continue;
                }
                ActorLoader.KillActor(actor.WhoAmI);
            }
            if (keeper != null) {
                return;
            }

            int gx = Main.spawnTileX + AnchorOffsetTiles;
            int gy = KiamePlans.ProbeGround(gx, Main.spawnTileY - 12);
            ActorLoader.NewActor<KiameExitUmbrella>(new Vector2(gx * 16f + 8f, gy * 16f));
        }
    }
}
