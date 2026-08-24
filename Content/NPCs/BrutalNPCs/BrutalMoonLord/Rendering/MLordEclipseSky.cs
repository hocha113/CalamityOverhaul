using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>
    /// 日蚀天幕强度中枢：原版召唤倒计时驱动"日蚀渐临"，开战后由核心状态续租推满。
    /// 纯本地表现，客户端观察状态驱动，不新增网络包
    /// </summary>
    internal static class MLordEclipse
    {
        private const float FadeInPerTick = 1f / 60f;
        private const float FadeOutPerTick = 1f / 120f;

        /// <summary>天幕强度 0~1</summary>
        public static float Intensity { get; private set; }
        /// <summary>蓄力扰动 0~1（冕环躁动）</summary>
        public static float ChargeAgitation { get; private set; }
        /// <summary>仍需在场（含渐出尾巴）</summary>
        public static bool Visible => Intensity > 0.004f;

        //核心每帧续租的驱动值（下一帧未续租自动过期）
        private static float bossLease;
        private static float chargeLease;
        private static bool leaseAlive;

        /// <summary>核心 AI 客户端逐帧上报驱动值</summary>
        public static void ReportBossDrive(int coreWhoAmI, float drive, float charge) {
            _ = coreWhoAmI;
            bossLease = Math.Max(bossLease, drive);
            chargeLease = Math.Max(chargeLease, charge);
            leaseAlive = true;
        }

        internal static void Update() {
            float target = 0f;

            //召唤倒计时：日蚀渐临（最高咬到 0.6，把"满蚀"留给登场演出）
            if (NPC.MoonLordCountdown > 0 && GameModes.GameModeSystem.BrutalActive) {
                float progress = 1f - NPC.MoonLordCountdown / (float)NPC.MaxMoonLordCountdown;
                target = Math.Max(target, progress * 0.6f);
            }

            //开战续租
            if (leaseAlive) {
                target = Math.Max(target, bossLease);
                ChargeAgitation = MathHelper.Lerp(ChargeAgitation, chargeLease, 0.2f);
            }
            else {
                ChargeAgitation = MathHelper.Lerp(ChargeAgitation, 0f, 0.1f);
            }
            bossLease = 0f;
            chargeLease = 0f;
            leaseAlive = false;

            float step = Intensity < target ? FadeInPerTick : -FadeOutPerTick;
            Intensity = MathHelper.Clamp(
                Math.Abs(target - Intensity) <= FadeInPerTick ? target : Intensity + step, 0f, 1f);
        }

        internal static void Reset() {
            Intensity = 0f;
            ChargeAgitation = 0f;
            bossLease = 0f;
            chargeLease = 0f;
            leaseAlive = false;
        }
    }

    internal class MLordEclipseSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            MLordEclipse.Update();
        }

        public override void ClearWorld() {
            if (Main.dedServ) {
                return;
            }
            MLordEclipse.Reset();
        }

        //日光被蚀：向深空紫暗收拢
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float eclipse = MLordEclipse.Intensity;
            if (eclipse <= 0.001f) {
                return;
            }
            Color dimTile = new(96, 92, 132);
            Color dimBg = new(52, 44, 88);
            tileColor = Color.Lerp(tileColor, dimTile, eclipse * 0.5f);
            backgroundColor = Color.Lerp(backgroundColor, dimBg, eclipse * 0.62f);
        }
    }

    //日蚀在场期间启用天空替换
    internal class MLordEclipseSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && MLordEclipse.Visible;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(MLordEclipseSky.Name, isActive);
    }

    /// <summary>
    /// 日蚀天幕：MLordEclipseSky.fx 全覆盖绘制（蚀盘吞日+冕环+星野浮现），
    /// 强度由 <see cref="MLordEclipse.Intensity"/> 驱动
    /// </summary>
    internal class MLordEclipseSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:MLordEclipseSky";

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册（ManageSpecialBiomeVisuals 不检查 Filter 空引用）
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.08f, 0.05f, 0.16f)
                .UseOpacity(0f), EffectPriority.High);
        }

        /// <summary>卸载复位强度中枢</summary>
        internal static void ResetDrive() => MLordEclipse.Reset();

        /// <summary>核心 AI 逐帧上报（转发到强度中枢）</summary>
        internal static void ReportBossDrive(int coreWhoAmI, float drive, float charge)
            => MLordEclipse.ReportBossDrive(coreWhoAmI, drive, charge);

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        //IsActive 只反映激活态，外部强度并入会短路 Activate（ToriiDuskSky 实测坑）
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.14f * MLordEclipse.Intensity);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨 0 深度切片只画一次
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float presence = MLordEclipse.Intensity;
            if (presence <= 0.004f) {
                return;
            }
            Effect shader = EffectLoader.MLordEclipseSky?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || white == null || noise == null) {
                DrawFallback(spriteBatch, presence);
                return;
            }

            var gd = Main.instance.GraphicsDevice;
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["uEclipse"]?.SetValue(presence);
            shader.Parameters["uAgitation"]?.SetValue(MLordEclipse.ChargeAgitation);
            shader.Parameters["uScreenSize"]?.SetValue(new Vector2(vpW, vpH));
            shader.Parameters["uCamX"]?.SetValue(Main.screenPosition.X * 0.00004f);
            shader.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(white, new Rectangle(0, 0, vpW, vpH), Color.White);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.BackgroundViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺失：纯色压暗退避</summary>
        private static void DrawFallback(SpriteBatch spriteBatch, float presence) {
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }
            var gd = Main.instance.GraphicsDevice;
            spriteBatch.Draw(white, new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height),
                new Color(10, 8, 24) * (0.55f * presence));
        }
    }
}
