using CalamityOverhaul.Content.HackTimes.PvP.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 读数污染（默认档）：八秒内污染防守方本机 HUD 渲染层，原版生命/魔力层被
    /// <see cref="GaugePollutionLayerHook"/> 整层熄灭，代之以随机漂移 ±40% 的伪读数；
    /// 快捷栏与增益图标区盖故障切条。<b>真实数值一字不改</b>，纯读数攻击；
    /// 真实槽位、真实 buff 都原样工作。<br/>
    /// 防守方每次受击/大额回血后 20f 内显示真值（防纯盲盒，留读屏窗口）
    /// 真值窗口内原版层原样放行，掉血急救永远看得到真血线。<br/>
    /// 红线遵守：只污染 HUD 读数区，永不遮挡角色本体与来袭弹幕的可见性
    /// </summary>
    internal class GaugePollution : PlayerHackDef
    {
        /// <summary>受击触发真值窗口的最小掉血</summary>
        private const int HurtTruthThreshold = 5;
        /// <summary>回血触发真值窗口的最小回量（滤掉自然回血的 1 点噪声）</summary>
        private const int HealTruthThreshold = 20;
        private const int TruthWindowFrames = 20;

        /// <summary>per-effect 状态：挂在帐本条目上，随条目自清</summary>
        private sealed class GaugeState
        {
            public int LastLife;
            public int TruthFrames;
            public float NoiseSeed;
        }

        public override void SetDefaults() {
            UploadTime = 90;
            RamCost = 3;
            Category = QuickHackCategory.Covert;
            UnlockedByDefault = true;
        }

        public override int GetDuration() => 480;

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            effect.ProtocolState = new GaugeState {
                LastLife = defender.statLife,
                NoiseSeed = (effect.ActivationId % 1000) * 0.137f,
            };
            return true;
        }

        public override bool OnDefenderTick(Player defender, PlayerHackEffect effect) {
            if (effect.ProtocolState is not GaugeState state) return true;
            int delta = defender.statLife - state.LastLife;
            state.LastLife = defender.statLife;
            //受击/大额回血 → 20f 真值窗口（自然回血的 +1 噪声不触发）
            if (delta <= -HurtTruthThreshold || delta >= HealTruthThreshold) {
                state.TruthFrames = TruthWindowFrames;
            }
            else if (state.TruthFrames > 0) {
                state.TruthFrames--;
            }
            return true;
        }

        /// <summary>
        /// 本机是否应熄灭原版生命/魔力层（在册且不在真值窗口）。
        /// 帐本只在防守方自己的客户端非空，其他端天然 false
        /// </summary>
        internal static bool ShouldMaskVanillaGauges() {
            PlayerHackEffect effect = PvPDefenderLocal.FindEffect<GaugePollution>();
            return effect?.ProtocolState is GaugeState state && state.TruthFrames <= 0;
        }

        #region 防守方本机 HUD 加扰（UI 空间，PlayerHackHud 按帐本条目调进来）

        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            if (effect.ProtocolState is not GaugeState state) return;
            //两名攻击方同时挂本协议时帐本有两条，只让最早那条画（伪读数同锚重影会糊字）
            if (!ReferenceEquals(effect, PvPDefenderLocal.FindEffect<GaugePollution>())) {
                return;
            }
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) return;

            float time = Main.GameUpdateCount * 0.05f + state.NoiseSeed;
            //真值窗口：读数区干净，只留一枚角标提示"暂时可信"。
            //y 错开 24px：78f 位被内存烧蚀的 RAM 封锁角标占着（同锚会叠字），
            //本角标只活 20f，让它下移比让常驻十秒的封锁角标压小地图划算
            if (state.TruthFrames > 0) {
                HackTheme.DrawBadge(spriteBatch,
                    new Vector2(HackTheme.UIScreenW - 118f, 102f),
                    PvPHudText.GaugeSyncTag.Value, HackTheme.AccentAlt, 0.85f);
                return;
            }

            //1 生命/魔力读数区（右上）：伪读数 + 故障切条
            DrawFakeReadout(spriteBatch, time, defender);
            //2 快捷栏（左上 10 格带）：图标区盖乱序故障条，真实槽位不动
            DrawGlitchBand(spriteBatch, pixel,
                new Rectangle(18, 16, 470, 52), time, 6);
            //3 buff 图标行
            DrawGlitchBand(spriteBatch, pixel,
                new Rectangle(28, 74, 400, 32), time * 1.3f + 5f, 4);
        }

        private static void DrawFakeReadout(SpriteBatch sb, float time, Player defender) {
            //原版生命/魔力层已被 GaugePollutionLayerHook 熄灭，这里的"仪表片"
            //就是污染期唯一的读数来源；锚在原版读数区左近，不追原版字模
            float x = HackTheme.UIScreenW - 322f;
            var font = FontAssets.MouseText.Value;

            int fakeLife = DriftValue(defender.statLife, time, 0.4f);
            int fakeLifeMax = DriftValue(defender.statLifeMax2, time + 3.7f, 0.4f);
            string lifeText = $"{PvPHudText.GaugeLifeTag.Value} {fakeLife}/{fakeLifeMax}";
            DrawJitterText(sb, font, lifeText, new Vector2(x, 22f), HackTheme.Danger, time);

            int fakeMana = DriftValue(defender.statMana, time + 9.1f, 0.4f);
            string manaText = $"{PvPHudText.GaugeManaTag.Value} {fakeMana}";
            DrawJitterText(sb, font, manaText, new Vector2(x, 44f),
                PvPTheme.HostileAlt, time + 2f);
        }

        private static void DrawJitterText(SpriteBatch sb, DynamicSpriteFont font,
            string text, Vector2 pos, Color color, float time) {
            //离散抖动：整字换行不换形，读作错帧而不是漂浮
            float jx = Hash(time * 0.7f) > 0.8f ? (Hash(time) - 0.5f) * 6f : 0f;
            Vector2 jittered = new((int)(pos.X + jx), (int)pos.Y);
            sb.DrawString(font, text, jittered + new Vector2(1f, 1f),
                HackTheme.BgDarkest * 0.8f, 0f, Vector2.Zero, 0.78f,
                SpriteEffects.None, 0f);
            sb.DrawString(font, text, jittered, color * 0.92f, 0f, Vector2.Zero,
                0.78f, SpriteEffects.None, 0f);
        }

        private static void DrawGlitchBand(SpriteBatch sb, Texture2D pixel,
            Rectangle region, float time, int strips) {
            //横向故障切条：亮色 additive 感的细条 + 亮芯线，绝不做暗羽化方块
            for (int i = 0; i < strips; i++) {
                float h1 = Hash(time * 0.31f + i * 7.77f);
                if (h1 < 0.35f) continue;
                float h2 = Hash(time * 0.53f + i * 3.19f);
                int y = region.Y + (int)(h2 * region.Height);
                int x = region.X + (int)(Hash(i * 11.3f + time * 0.2f)
                    * region.Width * 0.4f);
                int w = (int)(region.Width * (0.25f + h1 * 0.5f));
                w = Math.Min(w, region.Right - x);
                int h = 2 + (int)(h1 * 3f);
                Color body = i % 3 == 0 ? HackTheme.Danger : PvPTheme.HostileAlt;
                sb.Draw(pixel, new Rectangle(x, y, w, h), HackTheme.SrcPixel,
                    body * 0.5f);
                sb.Draw(pixel, new Rectangle(x, y + h / 2, w, 1), HackTheme.SrcPixel,
                    Color.Lerp(body, Color.White, 0.6f) * 0.7f);
            }
        }

        private static int DriftValue(int truth, float time, float amplitude) {
            //每 ~8 帧换一次伪值，量化读作数字乱跳而不是平滑摆动
            float step = MathF.Floor(time * 1.6f);
            float noise = Hash(step) * 2f - 1f;
            return Math.Max(0, (int)(truth * (1f + noise * amplitude)));
        }

        private static float Hash(float p) {
            p = MathF.Abs(p * 0.1031f % 1f);
            p *= p + 33.33f;
            p *= p + p;
            return MathF.Abs(p % 1f);
        }

        #endregion

        //攻击方与旁观者的外化：防守方头顶冒故障像素（镜像驱动，各端可见）
        public override void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) {
            if (Main.dedServ || !Main.rand.NextBool(9)) return;
            Vector2 pos = defender.Top + new Vector2(
                Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-22f, -6f));
            PRTLoader.NewParticle<PRT_TBUGGlitch>(pos,
                new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -0.3f),
                default, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(24);
        }

        public override string GlyphDiePath =>
            //晶粒纹：跳动的仪表折线 + 断裂的读数框
            "M -0.6 0.2 L -0.3 0.2 L -0.18 -0.3 L 0 0.34 L 0.14 -0.1 L 0.3 0.05 L 0.6 0.05 "
            + "M -0.55 -0.5 L 0.1 -0.5 M 0.26 -0.5 L 0.55 -0.5 "
            + "M -0.55 0.55 L -0.1 0.55 M 0.05 0.55 L 0.55 0.55";
    }

    /// <summary>
    /// 读数污染的原版层熄灭钩子（防守方本机）。真读数与伪读数并排各显各的，
    /// 污染就成了摆设，污染期把 "Vanilla: Resource Bars"（GUIBarsDraw，
    /// 生命/魔力显示）整层熄灭，伪读数成为唯一读数；真值窗口内原样放行。
    /// 只翻本帧 Active 位，图层表每帧由 tML 重建，效果一到期自动复原
    /// （形状同 <see cref="MapBlackoutRenderer.ModifyInterfaceLayers"/>）。
    /// 局限：只熄原版层，第三方血条模组的读数不受影响
    /// </summary>
    internal sealed class GaugePollutionLayerHook : ModSystem
    {
        private const string ResourceBarsLayer = "Vanilla: Resource Bars";

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!GaugePollution.ShouldMaskVanillaGauges()) {
                return;
            }
            for (int i = 0; i < layers.Count; i++) {
                if (layers[i].Name == ResourceBarsLayer) {
                    layers[i].Active = false;
                    return;
                }
            }
        }
    }
}
