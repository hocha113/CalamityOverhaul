using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiame
{
    /// <summary>
    /// 鬼雨里的死亡语义：HP 归零一律不死，走「惊醒」送回主世界，含溺水。
    /// 不掉落、不留墓碑、无醒后减益：被雨赶出来已是足额代价（镜像 KiyumeDreamWake）。<br/>
    /// 链路：<see cref="KiamePlayer"/>.PreKill 拦截 → 各端锁血 1 + 180t 无敌 →
    /// 被害端本机演出（黑入 30t / 全黑 30t / 末拍调 <see cref="KiameWorld.ExitWorld"/> 收尾 30t，
    /// 黑-湿青渐变与 KiameEntryReveal 同族、方向相反）。<br/>
    /// 联机：PreKill 是 per-player hook，在每个跑到 KillMe 的端各自取消（原版死亡包根本不发）；
    /// SubworldSystem.Exit 只在被害端本机调，全程零新包
    /// </summary>
    internal class KiameWake : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        internal static LocalizedText WakeOni { get; private set; }
        internal static LocalizedText WakeDrowned { get; private set; }
        internal static LocalizedText WakeGeneric { get; private set; }

        /// <summary>拦截后无敌（tick），盖过整段演出防连帧再触发</summary>
        private const int WakeImmuneTicks = 180;
        private const int FadeInTicks = 30;
        private const int HoldTicks = 30;
        private const int AfterglowTicks = 30;

        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);
        //湿骨灰小字，与加载屏正文同族
        private static readonly Color AshText = new(150, 166, 168);

        //本地演出进度，非 per-player 游戏状态，static 合法；
        //刻意不在 OnWorldUnload 兜零——收尾段要跨过世界切换在主世界睁眼
        private static int phase;
        private static int timer;
        private static LocalizedText wakeLine;

        public override void SetStaticDefaults() {
            WakeOni = this.GetLocalization(nameof(WakeOni), () => "伞下的东西松了手，你醒了。");
            WakeDrowned = this.GetLocalization(nameof(WakeDrowned), () => "黑水漫过头顶，你醒了。");
            WakeGeneric = this.GetLocalization(nameof(WakeGeneric), () => "你从雨里醒来。");
        }

        /// <summary>
        /// 死亡拦截（KiamePlayer.PreKill 转发，调用方已判 KiameWorld.Active）。
        /// 恒返回 true=拦下这次死亡；锁血在每个跑到 KillMe 的端各自执行，
        /// 演出只在被害端本机启动
        /// </summary>
        internal static bool InterceptDeath(Player player, PlayerDeathReason source) {
            if (player.statLife < 1) {
                player.statLife = 1;
            }
            player.GivePlayerImmuneState(WakeImmuneTicks);
            if (player.whoAmI == Main.myPlayer && !Main.dedServ && phase == 0) {
                wakeLine = ResolveWakeLine(player, source);
                phase = 1;
                timer = 0;
            }
            return true;
        }

        //死因选文案：近期被伞鬼打中 / 溺水（原版 ByOther(1)）/ 其余一律「你从雨里醒来。」
        private static LocalizedText ResolveWakeLine(Player player, PlayerDeathReason source) {
            if (player.TryGetModPlayer(out Overlay.OniRainWorldPlayer orp)
                && orp.OniHitFrames > 0) {
                return WakeOni;
            }
            if (source != null && source.SourceOtherIndex == 1) {
                return WakeDrowned;
            }
            return WakeGeneric;
        }

        public override void OnWorldUnload() {
            //黑幕段被外力送出雨（SubLib Return 键等）：跳到收尾，别把 Exit 再补调一遍
            if (phase == 1) {
                phase = 2;
                timer = 0;
            }
        }

        public override void PostUpdateEverything() {
            if (phase == 0) {
                return;
            }
            timer++;
            if (phase == 1) {
                if (timer < FadeInTicks + HoldTicks) {
                    return;
                }
                //全黑末拍：文案进聊天栏（跨世界留档可回看），本端出雨
                if (wakeLine != null) {
                    Main.NewText(wakeLine.Value, AshText.R, AshText.G, AshText.B);
                }
                if (KiameWorld.Active) {
                    KiameWorld.ExitWorld();
                }
                phase = 2;
                timer = 0;
            }
            else if (timer >= AfterglowTicks) {
                phase = 0;
                wakeLine = null;
            }
        }

        //黑幕与文案（PostDrawInterface 时批已开直接画；
        //过场期间 gameMenu=true 不画也不走帧，由加载屏接管，落地后余下的收尾继续）
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (phase == 0 || Main.dedServ || Main.gameMenu) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            var full = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            float black;
            float damp;
            float lineAlpha;
            if (phase == 1) {
                float t = MathHelper.Clamp(timer / (float)FadeInTicks, 0f, 1f);
                //黑入：前段松、后段咬死
                black = t * t;
                damp = MathF.Sin(MathHelper.Pi * t) * 0.36f;
                lineAlpha = MathHelper.Clamp((timer - FadeInTicks) / 16f, 0f, 1f);
            }
            else {
                float t = MathHelper.Clamp(timer / (float)AfterglowTicks, 0f, 1f);
                //睁眼：黑快速让开，湿青一闪即隐
                black = (1f - t) * (1f - t);
                damp = MathF.Sin(MathHelper.Pi * t) * 0.36f;
                lineAlpha = 1f - t;
            }
            spriteBatch.Draw(px, full, PixelSrc, Color.Black * black);
            spriteBatch.Draw(px, full, PixelSrc, new Color(38, 52, 56) * damp);
            if (lineAlpha > 0f && wakeLine != null) {
                Utils.DrawBorderString(spriteBatch, wakeLine.Value,
                    new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.44f),
                    AshText * lineAlpha, 0.95f, 0.5f, 0.5f);
            }
        }
    }
}
