using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering
{
    /// <summary>
    /// 昼形态世界压暗中枢：她在场时天空盖黑、日光染成暗暖、全局灯光闪烁，她成为唯一光源。
    /// 纯本地表现，主控客户端逐帧续租驱动值，下一帧未续租自动退潮
    /// </summary>
    internal static class EmpressDayDrive
    {
        private const float FadeInPerTick = 1f / 90f;
        private const float FadeOutPerTick = 1f / 150f;

        /// <summary>压暗强度 0~1</summary>
        public static float Intensity { get; private set; }
        /// <summary>临时加黑（蓄力/加速光球靠近），每帧衰减</summary>
        public static float ExtraDim { get; private set; }
        /// <summary>灯光闪烁幅度 0~1+，入场可拉到 3.5</summary>
        public static float FlickerScale { get; private set; }
        /// <summary>仍需在场（含渐出尾巴）</summary>
        public static bool Visible => Intensity > 0.004f || ExtraDim > 0.004f;

        private static float lease;
        private static float flickerLease;
        private static bool leaseAlive;
        private static float flickerIntensity;
        private static float flickerTarget;

        /// <summary>主控客户端逐帧上报：drive 压暗目标，flicker 闪烁目标</summary>
        public static void Report(float drive, float flicker) {
            lease = Math.Max(lease, drive);
            flickerLease = Math.Max(flickerLease, flicker);
            leaseAlive = true;
        }

        /// <summary>临时加黑（同帧多源累加，上限 0.6）</summary>
        public static void Dim(float v) {
            ExtraDim = MathHelper.Clamp(ExtraDim + v, 0f, 0.6f);
        }

        /// <summary>当前灯光闪烁乘数（ModifyLightingBrightness 用）</summary>
        public static float FlickerMultiplier() {
            float t = Main.GlobalTimeWrappedHourly;
            float s = MathF.Sin(t * 2f);
            return 1f - (s * s * 0.04f + flickerIntensity * 0.12f) * FlickerScale;
        }

        internal static void Update() {
            float target = leaseAlive ? lease : 0f;
            float flickerT = leaseAlive ? flickerLease : 0f;
            lease = 0f;
            flickerLease = 0f;
            leaseAlive = false;

            float step = Intensity < target ? FadeInPerTick : -FadeOutPerTick;
            Intensity = MathHelper.Clamp(Math.Abs(target - Intensity) <= FadeInPerTick ? target : Intensity + step, 0f, 1f);
            ExtraDim = ExtraDim < 0.005f ? 0f : ExtraDim * 0.96f;

            FlickerScale = MathHelper.Lerp(FlickerScale, flickerT, 0.1f);
            if (FlickerScale > 0.001f) {
                if (Main.rand.NextBool(5)) {
                    float r = Main.rand.NextFloat();
                    flickerTarget = r * r * r;
                }
                flickerIntensity = MathHelper.Lerp(flickerIntensity, flickerTarget, 0.6f);
            }
            else {
                FlickerScale = 0f;
                flickerIntensity = 0f;
            }
        }

        internal static void Reset() {
            Intensity = 0f;
            ExtraDim = 0f;
            FlickerScale = 0f;
            lease = flickerLease = 0f;
            leaseAlive = false;
            flickerIntensity = flickerTarget = 0f;
        }
    }

    internal class EmpressDaySystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            EmpressDayDrive.Update();
        }

        public override void ClearWorld() {
            if (Main.dedServ) {
                return;
            }
            EmpressDayDrive.Reset();
        }

        //日光被她夺走：瓷砖与远景向暗暖收拢（低饱和暗色，主题金白留给她自己）
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            float dark = EmpressDayDrive.Intensity;
            if (dark <= 0.001f) {
                return;
            }
            Color dimTile = new(40, 30, 22);
            Color dimBg = new(14, 10, 6);
            tileColor = Color.Lerp(tileColor, dimTile, dark * 0.9f);
            backgroundColor = Color.Lerp(backgroundColor, dimBg, dark * 0.95f);
        }

        //全局灯光随机闪烁：她来之前灯先疯
        public override void ModifyLightingBrightness(ref float scale) {
            if (EmpressDayDrive.FlickerScale <= 0.001f) {
                return;
            }
            scale *= EmpressDayDrive.FlickerMultiplier();
        }
    }

    //昼形态在场期间启用天幕替换
    internal class EmpressDaySceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) =>
            player.whoAmI == Main.myPlayer && EmpressDayDrive.Visible;
        public override void SpecialVisuals(Player player, bool isActive) =>
            player.ManageSpecialBiomeVisuals(EmpressDaySky.Name, isActive);
    }

    /// <summary>昼形态天幕：整幕盖黑（暖黑），强度由 <see cref="EmpressDayDrive"/> 驱动；太阳月亮随之隐去</summary>
    internal class EmpressDaySky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:EmpressDaySky";

        private bool active;

        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //Sky 与 Filter 必须同名成对注册（ManageSpecialBiomeVisuals 不检查 Filter 空引用）
            SkyManager.Instance[Name] = this;
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.05f, 0.03f, 0.015f)
                .UseOpacity(0f), EffectPriority.High);
        }

        void ICWRLoader.UnLoadData() => EmpressDayDrive.Reset();

        public override void Activate(Vector2 position, params object[] args) => active = true;
        public override void Deactivate(params object[] args) => active = false;
        public override bool IsActive() => active;
        public override void Reset() => active = false;

        public override void Update(GameTime gameTime) {
            Filters.Scene[Name]?.GetShader()?.UseOpacity(0.1f * EmpressDayDrive.Intensity);
        }

        public override float GetCloudAlpha() => 1f - EmpressDayDrive.Intensity * 0.9f;

        public override Color OnTileColor(Color inColor) => inColor;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            //跨 0 深度切片只画一次
            if (maxDepth < 0f || minDepth >= 0f) {
                return;
            }
            float cover = MathHelper.Clamp(EmpressDayDrive.Intensity * 0.85f + EmpressDayDrive.ExtraDim, 0f, 1f);
            if (cover <= 0.004f) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (white == null) {
                return;
            }
            var gd = Main.instance.GraphicsDevice;
            spriteBatch.Draw(white, new Rectangle(0, 0, gd.Viewport.Width, gd.Viewport.Height),
                new Color(8, 5, 3) * cover);
        }
    }
}
