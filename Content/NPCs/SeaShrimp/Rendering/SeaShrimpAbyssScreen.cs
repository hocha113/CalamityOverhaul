using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering
{
    /// <summary>
    /// 渊晶海虾战场滤镜：深海分级（P1 青→P2 深蓝→P3 渊黑）+ 焦散微光
    /// + 深渊边晕 + impact frame（声致发光白闪，全场只在死亡内爆打满一次）+ 死亡沉暗。
    /// 全部客户端表现，各端从同步的 NPC 阶段自行推导；状态经 Push/Trigger 静态口喂通道
    /// </summary>
    internal class SeaShrimpAbyssScreen : SeaShrimpModSystem, ICWRLoader
    {
        internal const string FilterName = "CWRMod:SeaShrimpAbyss";

        private static float abyss;
        private static float depth;
        private static float impact;
        private static float gloom;
        private static float depthAccum;
        private static float gloomAccum;

        void ICWRLoader.LoadAsset() {
            if (!SeaShrimpGate.Enabled || EffectLoader.SeaShrimpAbyssFilter == null) {
                return;
            }
            //第二参数是通道名（pass 名），与 fx 内 pass 一致，传错 Apply 期 NRE
            Filters.Scene[FilterName] = new Filter(
                new ScreenShaderData(EffectLoader.SeaShrimpAbyssFilter, "AbyssPass"), EffectPriority.High);
        }

        void ICWRLoader.UnLoadData() {
            abyss = depth = impact = gloom = 0f;
            depthAccum = gloomAccum = 0f;
        }

        /// <summary>阶段深度 0..1（boss 每帧按 ai[2] 推导举旗）</summary>
        internal static void PushDepth(float value) => depthAccum = Math.Max(depthAccum, value);

        /// <summary>死亡沉暗（死亡演出每帧举）</summary>
        internal static void PushGloom(float value) => gloomAccum = Math.Max(gloomAccum, value);

        /// <summary>impact frame：声致发光白闪，全场只该打满一次</summary>
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
                int type = ModContent.NPCType<SeaShrimpBoss>();
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.type == type) {
                        bossAlive = true;
                        break;
                    }
                }
            }

            abyss = MathHelper.Lerp(abyss, bossAlive ? 1f : 0f, bossAlive ? 0.02f : 0.045f);
            depth = MathHelper.Lerp(depth, depthAccum, 0.03f);
            gloom = MathHelper.Lerp(gloom, gloomAccum, 0.06f);
            depthAccum = 0f;
            gloomAccum = 0f;
            //impact frame 指数退潮：~13 帧读完
            impact *= 0.85f;
            if (impact < 0.02f) {
                impact = 0f;
            }

            Filter filter = Filters.Scene[FilterName];
            if (filter == null) {
                return;
            }
            bool want = abyss > 0.02f || impact > 0f || gloom > 0.02f;
            if (want && !filter.IsActive()) {
                Filters.Scene.Activate(FilterName);
            }
            else if (!want && filter.IsActive()) {
                Filters.Scene.Deactivate(FilterName);
            }
            if (want) {
                Effect fx = EffectLoader.SeaShrimpAbyssFilter?.Value;
                if (fx != null) {
                    fx.Parameters["uAbyss"]?.SetValue(abyss);
                    fx.Parameters["uDepth"]?.SetValue(depth);
                    fx.Parameters["uImpact"]?.SetValue(impact);
                    fx.Parameters["uGloom"]?.SetValue(gloom);
                }
            }
        }
    }
}
