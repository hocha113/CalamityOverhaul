using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.PvP.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 熔断标记（芯片档，Lethal）：四秒可见引信，到期在防守方本机结一次八十点伤害。<br/>
    /// <b>引信全程可见是设计不是慷慨——这就是反制窗</b>：四秒内强制卸载拔掉即拆弹
    /// （<see cref="OnDefenderRemove"/> 只在 <see cref="PlayerHackRemoveReason.Expired"/>
    /// 分支起爆，Uninstalled 走拆弹演出），高防高血硬吃也是一种答案；
    /// 效果已落地，打死攻击方也不解除——跟时间赛跑。<br/>
    /// <b>结算端</b>：防守方本机 <c>Hurt(pvp:true, quiet:false)</c>——生命归属方写，
    /// msg 16 自报 + 117 广播（不经 117 的双 hostile 转播闸，中途关 PvP 也照结，
    /// 设计 §7.5 的文档级 FAQ）；伤害经 <see cref="HackPvPRules.ClampLifeDamage"/>，
    /// 死因记攻击方。防守方 HUD 倒计时走 <see cref="DrawDefenderOverlay"/>，
    /// 各端引信光走 <see cref="OnSpectatorTick"/>
    /// </summary>
    internal class MeltdownBrand : PlayerHackDef
    {
        /// <summary>到期伤害（进 120 预算的红线 Clamp，走正常防御减免）</summary>
        internal const int BrandDamage = 80;

        private static readonly Color Fuse = new(255, 120, 40);

        /// <summary>晶粒纹：躯体胸口盘着引信卷，火星顺线上蹿；左侧一列表盘刻线在数拍子</summary>
        internal const string Die =
            "M -0.16 -0.30 L 0.24 -0.30 M -0.16 0.50 L 0.24 0.50 "
            + "M -0.16 -0.30 Q -0.26 0.10 -0.16 0.50 M 0.24 -0.30 Q 0.34 0.10 0.24 0.50 "
            + "M 0.04 0.10 Q 0.18 0.06 0.16 -0.06 Q 0.14 -0.16 0.02 -0.12 Q -0.06 -0.09 0 -0.02 "
            + "M 0.16 -0.06 L 0.44 -0.44 "
            + "M 0.38 -0.54 L 0.52 -0.40 M 0.52 -0.54 L 0.38 -0.40 M 0.45 -0.60 L 0.45 -0.56 "
            + "M -0.62 -0.46 L -0.50 -0.40 M -0.72 -0.22 L -0.58 -0.18 "
            + "M -0.76 0.04 L -0.62 0.04 M -0.72 0.30 L -0.58 0.26";

        /// <summary>防守方侧 per-effect 状态：心跳节拍已播到哪一秒（7.5 律，防重放）</summary>
        private sealed class BrandState
        {
            public int LastBeatSecond = int.MaxValue;
        }

        public override void SetDefaults() {
            UploadTime = 150;
            RamCost = 5;
            Category = QuickHackCategory.Lethal;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 240;

        #region 防守方通道（引信心跳 + 到期本机结算）

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            effect.ProtocolState = new BrandState();
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.9f, Pitch = -0.5f },
                defender.Center);
            return true;
        }

        public override bool OnDefenderTick(Player defender, PlayerHackEffect effect) {
            if (effect.ProtocolState is not BrandState state) return true;
            //每进入新的一秒敲一声心跳，越接近到期音越高——耳朵也能读引信
            int second = effect.RemainingFrames / 60;
            if (second < state.LastBeatSecond) {
                state.LastBeatSecond = second;
                float pitch = -0.4f + (3 - Math.Min(second, 3)) * 0.25f;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.9f, Pitch = pitch },
                    defender.Center);
            }
            return true;
        }

        public override void OnDefenderRemove(Player defender, PlayerHackEffect effect,
            PlayerHackRemoveReason reason) {
            //强制卸载 = 拆弹成功：只演不炸（默认档反制的 marquee 组合）
            if (reason == PlayerHackRemoveReason.Uninstalled) {
                EmitDefuse(defender);
                return;
            }
            //死亡/断线/看门狗清账不结算；只有自然走完引信才起爆
            if (reason != PlayerHackRemoveReason.Expired) return;

            int damage = HackPvPRules.ClampLifeDamage(BrandDamage, 0);
            if (damage > 0 && !defender.dead) {
                PlayerDeathReason deathReason = PlayerDeathReason.ByCustomReason(
                    NetworkText.FromKey(DeathReason.Key, defender.name,
                        ResolveCasterName(effect)));
                //dodgeable:false——引信不是弹幕，闪避帧吃不掉计时爆破
                defender.Hurt(deathReason, damage, 0, pvp: true, quiet: false,
                    dodgeable: false);
            }
            EmitDetonation(defender.Center);
        }

        private static string ResolveCasterName(PlayerHackEffect effect) {
            if (!string.IsNullOrEmpty(effect.CasterName)) return effect.CasterName;
            return effect.CasterIndex >= 0 && effect.CasterIndex < Main.maxPlayers
                ? Main.player[effect.CasterIndex]?.name ?? "?" : "?";
        }

        #endregion

        #region 表现（防守方 HUD 倒计时 + 各端引信光）

        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float seconds = effect.RemainingFrames / 60f;
            float urgency = 1f - MathHelper.Clamp(seconds / 4f, 0f, 1f);
            //心跳缩放：越到最后跳得越快越狠
            float beat = MathF.Sin(Main.GlobalTimeWrappedHourly
                * (5f + urgency * 9f)) * 0.5f + 0.5f;
            float scale = 1.15f + urgency * 0.25f + beat * (0.05f + urgency * 0.1f);
            Color body = Color.Lerp(PvPTheme.HostileAlt, Color.White, beat * 0.35f);

            string text = FuseTag.Format(seconds.ToString("0.0"));
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new((HackTheme.UIScreenW - size.X) * 0.5f,
                HackTheme.UIScreenH * 0.26f);
            spriteBatch.DrawString(font, text, pos + new Vector2(2f, 2f),
                HackTheme.BgDarkest * 0.85f, 0f, Vector2.Zero, scale,
                SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, pos, body, 0f, Vector2.Zero, scale,
                SpriteEffects.None, 0f);

            //引信条：烧剩多少一目了然
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) return;
            int barW = 180;
            int x = (int)((HackTheme.UIScreenW - barW) * 0.5f);
            int y = (int)(pos.Y + size.Y + 6f);
            spriteBatch.Draw(pixel, new Rectangle(x, y, barW, 4), HackTheme.SrcPixel,
                HackTheme.BgDarkest * 0.9f);
            int lit = (int)(barW * effect.RemainingRatio);
            spriteBatch.Draw(pixel, new Rectangle(x, y, lit, 4), HackTheme.SrcPixel,
                Color.Lerp(PvPTheme.Hostile, Fuse, beat));
            spriteBatch.Draw(pixel, new Rectangle(x + lit - 2, y - 1, 3, 6),
                HackTheme.SrcPixel, Color.White * (0.6f + beat * 0.4f));
        }

        public override void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) {
            if (Main.dedServ || duration <= 0) return;
            float progress = MathHelper.Clamp(elapsed / (float)duration, 0f, 1f);

            //引信火星越烧越密：从偶发到每帧
            int gap = Math.Max(1, 10 - (int)(progress * 9f));
            if (elapsed % gap == 0) {
                float angle = elapsed * 0.23f;
                Vector2 orbit = angle.ToRotationVector2()
                    * (26f + MathF.Sin(elapsed * 0.11f) * 6f);
                PRTLoader.NewParticle<PRT_Spark>(defender.Center + orbit,
                    new Vector2(0f, -0.8f - progress), Fuse,
                    0.5f + progress * 0.5f)?.Configure(false, 14);
            }

            //到期爆点各端可见；防守方本机的那份在 OnDefenderRemove 里，别放两遍
            if (elapsed == duration - 1 && defender.whoAmI != Main.myPlayer) {
                EmitDetonation(defender.Center);
            }
        }

        /// <summary>起爆演出：火花环 + 故障渣 + 爆响（结算与表现分开，这里不碰数值）</summary>
        internal static void EmitDetonation(Vector2 center) {
            if (Main.dedServ) return;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f }, center);
            for (int i = 0; i < 18; i++) {
                PRTLoader.NewParticle<PRT_Spark>(center,
                    Main.rand.NextVector2CircularEdge(6f, 6f)
                        * Main.rand.NextFloat(0.5f, 1f),
                    i % 3 == 0 ? Color.White : Fuse,
                    Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, 20);
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_TBUGGlitch>(
                    center + Main.rand.NextVector2Circular(10f, 14f),
                    Main.rand.NextVector2Circular(2.4f, 2.4f), default,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(22);
            }
        }

        /// <summary>拆弹演出：泄气的短噗 + 几粒冷渣，与起爆读感截然分开</summary>
        private static void EmitDefuse(Player defender) {
            if (Main.dedServ) return;
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.6f, Pitch = 0.4f },
                defender.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_TBUGGlitch>(
                    defender.Center + Main.rand.NextVector2Circular(12f, 16f),
                    new Vector2(0f, Main.rand.NextFloat(0.6f, 1.6f)), default,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(18);
            }
        }

        #endregion

        internal LocalizedText FuseTag
            => this.GetLocalization(nameof(FuseTag), () => "MELTDOWN {0}s");

        internal LocalizedText DeathReason
            => this.GetLocalization(nameof(DeathReason),
                () => "{0} was blown apart by {1}'s meltdown brand.");

        public override string GlyphDiePath => Die;
    }
}
