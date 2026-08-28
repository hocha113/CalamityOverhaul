using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>
    /// 分相天幕强度控制器:找本地视野内的教徒本体,阶段与在场强度纯本地推导(ai[0] 已同步)<br/>
    /// VisualPhase 带小数缓动,换相时天幕交叉渐变
    /// </summary>
    internal static class CultistSkyDriver
    {
        /// <summary>在场强度 0~1</summary>
        public static float Intensity { get; private set; }
        /// <summary>视觉阶段(缓动小数,喂 uPhase)</summary>
        public static float VisualPhase { get; private set; }

        public static bool Visible => Intensity > 0.004f;

        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                Intensity = 0f;
                return;
            }
            float target = 0f;
            float phaseTarget = VisualPhase;
            //强度已归零且近两帧无教徒盖戳时跳过全表扫描；强度未归零则继续扫，
            //时停中 AI 停摆（戳过期）也能靠这条继续找到冻结的教徒、天幕不塌
            if (Intensity > 0f || CultistBossAI.PresenceStamp.ActiveWithin()) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.type != NPCID.CultistBoss || !npc.TryGetOverride(out CultistBossAI _)) {
                        continue;
                    }
                    float distance = Vector2.Distance(npc.Center, Main.LocalPlayer.Center);
                    float near = 1f - MathHelper.Clamp((distance - 2000f) / 1200f, 0f, 1f);
                    if (near > target) {
                        target = near;
                        phaseTarget = npc.ai[0];
                    }
                }
            }
            Intensity = MathHelper.Lerp(Intensity, target, 0.04f);
            if (Intensity < 0.004f && target <= 0f) {
                Intensity = 0f;
            }
            VisualPhase = MathHelper.Lerp(VisualPhase, phaseTarget, 0.05f);
        }

        internal static void Reset() {
            Intensity = 0f;
            VisualPhase = 0f;
        }

        /// <summary>阶段滤镜色(压向天界柱色系)</summary>
        internal static Vector3 FilterColor(int phase) => phase switch {
            1 => new(0.16f, 0.03f, 0.16f),
            2 => new(0.03f, 0.08f, 0.14f),
            3 => new(0.20f, 0.07f, 0.01f),
            4 => new(0.02f, 0.06f, 0.05f),
            _ => new(0.02f, 0.08f, 0.11f),
        };
    }

    internal class CultistSkySystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                CultistSkyDriver.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                CultistSkyDriver.Reset();
            }
        }
    }

    /// <summary>
    /// 教徒在场期间启用分相天幕<br/>
    /// 不再联动原版天界塔滤镜:塔天空自带的动画背景与自绘天幕叠加读作"乱",天幕独占背景
    /// </summary>
    internal class CultistPhaseSkySceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossMedium;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && CultistSkyDriver.Visible;

        public override void SpecialVisuals(Player player, bool isActive) {
            player.ManageSpecialBiomeVisuals(CultistPhaseSkyEffect.Name, isActive);
        }
    }

    /// <summary>
    /// 分相沉浸天幕:身处星球/风暴眼内部(CultistPhaseSky.fx)<br/>
    /// Sky 与 Filter 同名成对注册(ManageSpecialBiomeVisuals 对缺 Filter 直接 NRE);
    /// IsActive 只反映激活态,渐出尾巴由 Driver.Visible 兜住
    /// </summary>
    internal class CultistPhaseSkyEffect : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:CultistPhaseSky";

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.02f, 0.08f, 0.11f)
                .UseOpacity(0f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            int phase = (int)MathF.Round(CultistSkyDriver.VisualPhase);
            Vector3 tint = CultistSkyDriver.FilterColor(phase);
            Filters.Scene[Name]?.GetShader()?
                .UseColor(tint.X, tint.Y, tint.Z)
                .UseOpacity(0.24f * CultistSkyDriver.Intensity);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨0深度切片只画一次,盖住原版背景层
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float presence = CultistSkyDriver.Intensity;
            if (presence <= 0.004f) {
                return;
            }
            Effect shader = EffectLoader.CultistPhaseSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (white == null) {
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone);

            if (shader != null && noise != null) {
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
                shader.Parameters["uIntensity"]?.SetValue(presence);
                shader.Parameters["uPhase"]?.SetValue(CultistSkyDriver.VisualPhase);
                shader.Parameters["uStorm"]?.SetValue(CultistScreenFX.StormSurge);
                shader.Parameters["uAspect"]?.SetValue(vpW / (float)vpH);
                //相机视差锚:星野/云雾按层系数取用,背景不再糊在镜头上
                shader.Parameters["uCam"]?.SetValue(Main.screenPosition / vpH);
                shader.CurrentTechnique.Passes[0].Apply();
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);
            }
            else {
                //着色器缺失:阶段色单层罩底,浓度压低避免死黑块
                int phase = (int)MathF.Round(CultistSkyDriver.VisualPhase);
                Color tint = CultistMotion.PhaseEdge(phase) * (presence * 0.38f);
                spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), tint);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }
    }
}
