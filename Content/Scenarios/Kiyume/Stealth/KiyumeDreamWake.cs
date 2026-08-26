using CalamityOverhaul.Content.Scenarios.Kiyume.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Stealth
{
    /// <summary>
    /// 惊醒死亡语义（P2 计划书 S5，裁决 3）：鬼梦内 HP 归零一律不死，走「惊醒」出梦，
    /// 含溺水（覆盖 DESIGN §6 溺亡真死）。不掉落、不留墓碑、无醒后减益：
    /// 被逐出梦 + 潮汐重置已是足额代价。<br/>
    /// 链路：<see cref="KiyumeStealthPlayer"/>.PreKill 拦截 → 各端锁血 1 + 180t 无敌 →
    /// 被害端本机演出（黑入 30t / 全黑 30t / 末拍调 <see cref="KiyumeWorld.ExitWorld"/> 收尾 30t，
    /// KiyumeEntryReveal 黑-烬渐变的姊妹式样、方向相反，落回主世界从黑里透烬红睁眼）。<br/>
    /// 联机：PreKill 是 per-player hook，在每个跑到 KillMe 的端各自取消（取消后原版死亡包根本不发）；
    /// SubworldSystem.Exit 只在被害端本机调，MP 客户端不代他人做任何事，全程零新包；
    /// 队友视角该玩家离开子世界，走 SubLib 原生同步
    /// </summary>
    internal class KiyumeDreamWake : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        internal static LocalizedText WakeBitten { get; private set; }
        internal static LocalizedText WakeDrowned { get; private set; }
        internal static LocalizedText WakeGeneric { get; private set; }

        //──── 数值（S5：锁血无敌 180t；演出 90t = 黑入 30 / 全黑 30 / 出梦收尾 30） ────

        /// <summary>拦截后无敌（tick），盖过整段演出防连帧再触发</summary>
        private const int WakeImmuneTicks = 180;
        /// <summary>黑入时长（tick）</summary>
        private const int FadeInTicks = 30;
        /// <summary>全黑持续（tick），末拍出梦</summary>
        private const int HoldTicks = 30;
        /// <summary>出梦收尾（tick）：过场接管前的兜底 + 落地主世界的睁眼淡出</summary>
        private const int AfterglowTicks = 30;

        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);
        //骨灰色小字（KiyumeHoundHints 同源）
        private static readonly Color AshText = new(168, 132, 128);

        //──── 演出态（本地演出进度，非 per-player 游戏状态，static 合法；
        //刻意不在 OnWorldUnload 兜零——收尾段要跨过世界切换在主世界睁眼） ────

        /// <summary>0=闲置 1=梦内黑入+全黑 2=出梦收尾</summary>
        private static int phase;
        private static int timer;
        private static LocalizedText wakeLine;

        public override void SetStaticDefaults() {
            WakeBitten = this.GetLocalization(nameof(WakeBitten), () => "狗牙合拢的那一刻，你醒了。");
            WakeDrowned = this.GetLocalization(nameof(WakeDrowned), () => "湖水漫过头顶，你醒了。");
            WakeGeneric = this.GetLocalization(nameof(WakeGeneric), () => "梦断了。");
        }

        /// <summary>
        /// 死亡拦截（KiyumeStealthPlayer.PreKill 转发，调用方已判 KiyumeWorld.Active）。
        /// 恒返回 true=拦下这次死亡；锁血在每个跑到 KillMe 的端各自执行
        /// （被害端是生命权威值，服务器/旁观端只是镜像自愈，防其本地模拟连帧重进 KillMe），
        /// 演出只在被害端本机启动
        /// </summary>
        internal static bool InterceptDeath(Player player, PlayerDeathReason source) {
            if (player.statLife < 1) {
                player.statLife = 1;
            }
            player.GivePlayerImmuneState(WakeImmuneTicks);
            if (player.whoAmI == Main.myPlayer && !Main.dedServ && phase == 0) {
                wakeLine = ResolveWakeLine(source);
                phase = 1;
                timer = 0;
            }
            return true;
        }

        //死因选文案：被犬咬 / 溺水（原版 ByOther(1)，上游源已核）/ 其余一律「梦断了。」
        private static LocalizedText ResolveWakeLine(PlayerDeathReason source) {
            if (source != null) {
                int npcIdx = source.SourceNPCIndex;
                if (npcIdx >= 0 && npcIdx < Main.maxNPCs && Main.npc[npcIdx].ModNPC is KiyumeHound) {
                    return WakeBitten;
                }
                if (source.SourceOtherIndex == 1) {
                    return WakeDrowned;
                }
            }
            return WakeGeneric;
        }

        public override void OnWorldUnload() {
            //黑幕段被外力送出梦（SubLib Return 键等）：跳到收尾，别把 Exit 再补调一遍
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
                //全黑末拍：文案进聊天栏（跨世界留档可回看），本端出梦
                if (wakeLine != null) {
                    Main.NewText(wakeLine.Value, AshText.R, AshText.G, AshText.B);
                }
                if (KiyumeWorld.Active) {
                    KiyumeWorld.ExitWorld();
                }
                phase = 2;
                timer = 0;
            }
            else if (timer >= AfterglowTicks) {
                phase = 0;
                wakeLine = null;
            }
        }

        //黑幕与文案（PostDrawInterface 时批已开直接画，EntryReveal 同款；
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
            float ember;
            float lineAlpha;
            if (phase == 1) {
                float t = MathHelper.Clamp(timer / (float)FadeInTicks, 0f, 1f);
                //黑入：前段松、后段咬死（EntryReveal 的镜像方向）
                black = t * t;
                ember = MathF.Sin(MathHelper.Pi * t) * 0.34f;
                lineAlpha = MathHelper.Clamp((timer - FadeInTicks) / 16f, 0f, 1f);
            }
            else {
                float t = MathHelper.Clamp(timer / (float)AfterglowTicks, 0f, 1f);
                //睁眼：黑快速让开，烬红一闪即隐
                black = (1f - t) * (1f - t);
                ember = MathF.Sin(MathHelper.Pi * t) * 0.34f;
                lineAlpha = 1f - t;
            }
            spriteBatch.Draw(px, full, PixelSrc, Color.Black * black);
            spriteBatch.Draw(px, full, PixelSrc, new Color(96, 20, 16) * ember);
            if (lineAlpha > 0f && wakeLine != null) {
                Utils.DrawBorderString(spriteBatch, wakeLine.Value,
                    new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * 0.44f),
                    AshText * lineAlpha, 0.95f, 0.5f, 0.5f);
            }
        }
    }
}
