using CalamityOverhaul.Content.HackTimes.PvP.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 隐身剥离（芯片档）：落地时防守方本机清除隐身类状态，十秒内无法再积累；
    /// 期间全员可见的轮廓光挂在防守方身上。<br/>
    /// <b>剥离面（全部是防守方自己的资源，本机写）</b>：原版隐身药水 buff 与
    /// <c>invis</c> 旗、潜行值 <c>stealth</c>（1=全可见，喷泊/星旋套的积累源）、
    /// 星旋潜行开关 <c>vortexStealthActive</c>、Calamity 盗贼潜行
    /// （<c>CalamityPlayer.rogueStealth</c>，本文件内的局部反射缓存清零，
    /// 取不到则静默跳过该项，CWRRef 的缓存是 private，收尾者可回并）。<br/>
    /// 压制在 <c>OnDefenderTick</c> 逐帧重执行（帧末，单帧内的积累残量可忽略）。<br/>
    /// <b>轮廓光是本簇唯一出防守方本机的表现</b>：走 <see cref="OnSpectatorTick"/>
    /// 镜像驱动，各端读广播镜像自绘（含防守方自己），不读防守方本机任何值
    /// </summary>
    internal class StealthStrip : PlayerHackDef
    {
        /// <summary>晶粒纹：人形轮廓 + 被切断的斗篷幕线 + 右侧曝光回声（芯片与 HUD 条目共用）</summary>
        internal const string Die =
            "M -0.46 -0.62 L -0.26 -0.62 L -0.26 -0.44 L -0.46 -0.44 Z "
            + "M -0.54 -0.34 L -0.18 -0.34 L -0.24 0.26 L -0.48 0.26 Z "
            + "M -0.72 -0.58 L -0.72 -0.28 M -0.72 -0.10 L -0.72 0.26 "
            + "M -0.78 -0.24 L -0.64 -0.14 "
            + "M 0.02 -0.62 L 0.22 -0.62 L 0.22 -0.44 "
            + "M 0.10 -0.34 L 0.34 -0.34 L 0.28 0.26 "
            + "M 0.44 -0.54 L 0.56 -0.62 M 0.48 -0.10 L 0.62 -0.10 "
            + "M 0.44 0.12 L 0.56 0.20";

        public override void SetDefaults() {
            UploadTime = 90;
            RamCost = 3;
            Category = QuickHackCategory.Covert;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 600;

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            StripOnce(defender);
            return true;
        }

        public override bool OnDefenderTick(Player defender, PlayerHackEffect effect) {
            //逐帧重剥：期间重喝隐身药、重开潜行、重新蹲积累全部当帧作废
            StripOnce(defender);
            return true;
        }

        /// <summary>剥一遍全部隐身/潜行状态。落点全是防守方本机资源</summary>
        private static void StripOnce(Player defender) {
            defender.ClearBuff(BuffID.Invisibility);
            defender.invis = false;
            defender.stealth = 1f;
            defender.vortexStealthActive = false;
            CalRogueStealth.Zero(defender);
        }

        #region 表现：全员可见的轮廓光（镜像驱动，各端自绘）

        public override void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) {
            if (Main.dedServ) {
                return;
            }
            Lighting.AddLight(defender.Center, PvPTheme.Hostile.ToVector3() * 0.26f);
            if (elapsed % 3 != 0) {
                return;
            }
            //两枚沿命中箱周界巡游的示踪光点：相位取 elapsed（各端形态一致），
            //粒子挂实体跟随，人跑起来轮廓不脱靶
            Rectangle box = defender.Hitbox;
            box.Inflate(5, 5);
            for (int k = 0; k < 2; k++) {
                float t = (elapsed * 0.011f + k * 0.5f) % 1f;
                PRTLoader.NewParticle<PRT_Light>(PerimeterPoint(box, t),
                    Vector2.Zero, PvPTheme.Hostile,
                    Main.rand.NextFloat(0.13f, 0.2f))
                    ?.Configure(16, opacity: 0.85f, _entity: defender,
                        _followingRateRatio: 1f);
            }
        }

        /// <summary>矩形周界参数化取点，t∈[0,1) 顺时针绕一圈</summary>
        private static Vector2 PerimeterPoint(Rectangle box, float t) {
            float w = box.Width;
            float h = box.Height;
            float total = 2f * (w + h);
            float d = t * total;
            if (d < w) {
                return new Vector2(box.X + d, box.Y);
            }
            d -= w;
            if (d < h) {
                return new Vector2(box.Right, box.Y + d);
            }
            d -= h;
            if (d < w) {
                return new Vector2(box.Right - d, box.Bottom);
            }
            d -= w;
            return new Vector2(box.X, box.Bottom - d);
        }

        //防守方本机角标：知道自己被点亮，才有反制决策
        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            HackTheme.DrawBadge(spriteBatch,
                new Vector2(HackTheme.UIScreenW * 0.5f - 54f, 118f),
                PvPDefenderText.Exposed.Value, PvPTheme.Hostile,
                0.75f + 0.25f * MathF.Sin(Main.GameUpdateCount * 0.12f));
        }

        #endregion

        public override string GlyphDiePath => Die;

        public override void Unload() {
            base.Unload();
            CalRogueStealth.Clear();
        }

        /// <summary>
        /// Calamity 盗贼潜行的局部反射缓存。CWRRef 的 rogueStealth 缓存是 private、
        /// 公开入口 <c>UpdateRogueStealth</c> 挂着奈落眼的豁免语义，都不合用；
        /// 这里按 CWRRef 同款纪律自建最小缓存：presence 闸 + 逐成员空值防护 + 卸载清空。
        /// 收尾者可把这三个成员回并进 CWRRef 后删掉本类
        /// </summary>
        private static class CalRogueStealth
        {
            private static bool resolved;
            private static FieldInfo rogueStealthField;
            private static ModPlayer template;

            internal static void Zero(Player player) {
                if (!CWRRef.Has) {
                    return;
                }
                if (!resolved) {
                    resolved = true;
                    Resolve();
                }
                if (rogueStealthField == null || template == null) {
                    return;   //取不到访问器：静默跳过该项（协议其余剥离面照常）
                }
                ModPlayer calPlayer = player.GetModPlayer(template);
                if (calPlayer != null) {
                    rogueStealthField.SetValue(calPlayer, 0f);
                }
            }

            private static void Resolve() {
                if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                    return;
                }
                Type type = calamity.Code?.GetType("CalamityMod.CalamityPlayer");
                if (type == null) {
                    return;
                }
                rogueStealthField = type.GetField("rogueStealth",
                    BindingFlags.Public | BindingFlags.Instance);
                foreach (ModPlayer content in calamity.GetContent<ModPlayer>()) {
                    if (content.GetType() == type) {
                        template = content;
                        break;
                    }
                }
            }

            internal static void Clear() {
                resolved = false;
                rogueStealthField = null;
                template = null;
            }
        }
    }
}
