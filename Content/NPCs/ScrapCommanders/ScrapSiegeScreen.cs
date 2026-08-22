using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders
{
    /// <summary>
    /// 废钢统帅战场滤镜系统：锈尘暮色基调 + 过载橙边晕 + impact frame + 死亡转灰。
    /// 全部是客户端表现，各端从同步的 NPC 状态自行推导，无网络通道。
    /// 状态代码通过 Push/Trigger 静态口喂通道，帧末聚合衰减
    /// </summary>
    internal class ScrapSiegeScreen : ModSystem, ICWRLoader
    {
        internal const string FilterName = "CWRMod:ScrapSiege";

        private static float siege;
        private static float overloadHeat;
        private static float impact;
        private static float gray;
        private static float overloadAccum;
        private static float grayAccum;

        void ICWRLoader.LoadAsset() {
            if (EffectLoader.ScrapSiegeFilter == null) {
                return;
            }
            //第二个参数是"通道名"不是技术名：ShaderData.Apply 按 Passes[名字] 查表，
            //查空会 NRE 并把 FilterManager 的批撂在半开状态，连锁 Begin/End 崩溃（2026-08 实测）
            Filters.Scene[FilterName] = new Filter(
                new ScreenShaderData(EffectLoader.ScrapSiegeFilter, "SiegePass"), EffectPriority.High);
        }

        void ICWRLoader.UnLoadData() {
            siege = overloadHeat = impact = gray = 0f;
            overloadAccum = grayAccum = 0f;
        }

        /// <summary>过载热边晕（状态每帧举，帧末聚合）</summary>
        internal static void PushOverloadHeat(float value) => overloadAccum = Math.Max(overloadAccum, value);

        /// <summary>死亡转灰（死亡演出每帧举）</summary>
        internal static void PushGray(float value) => grayAccum = Math.Max(grayAccum, value);

        /// <summary>impact frame：黑白双阶调一瞬，全场只该打满一次</summary>
        internal static void TriggerImpactFrame(float strength = 1f) {
            if (!Main.dedServ) {
                impact = Math.Max(impact, MathHelper.Clamp(strength, 0f, 1f));
            }
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool bossAlive = false;
            if (!Main.gameMenu) {
                int type = ModContent.NPCType<ScrapCommander>();
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.type == type) {
                        bossAlive = true;
                        break;
                    }
                }
            }

            siege = MathHelper.Lerp(siege, bossAlive ? 1f : 0f, bossAlive ? 0.025f : 0.045f);
            overloadHeat = MathHelper.Lerp(overloadHeat, overloadAccum, 0.1f);
            gray = MathHelper.Lerp(gray, grayAccum, 0.06f);
            overloadAccum = 0f;
            grayAccum = 0f;
            //impact frame 指数退潮：~12 帧读完
            impact *= 0.84f;
            if (impact < 0.02f) {
                impact = 0f;
            }

            Filter filter = Filters.Scene[FilterName];
            if (filter == null) {
                return;
            }
            bool want = siege > 0.02f || impact > 0f || gray > 0.02f;
            if (want && !filter.IsActive()) {
                Filters.Scene.Activate(FilterName);
            }
            else if (!want && filter.IsActive()) {
                Filters.Scene.Deactivate(FilterName);
            }
            if (want) {
                Effect fx = EffectLoader.ScrapSiegeFilter?.Value;
                if (fx != null) {
                    fx.Parameters["uSiege"]?.SetValue(siege);
                    fx.Parameters["uOverloadHeat"]?.SetValue(overloadHeat);
                    fx.Parameters["uImpact"]?.SetValue(impact);
                    fx.Parameters["uGrayness"]?.SetValue(gray);
                }
            }
        }
    }
}
