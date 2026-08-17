using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    //滤镜着色数据：Apply 在 FilterManager.EndCapture 时刻被调，
    //在这里上载绘制时相机仿射/密度纹理（帧内零滞后），再走原版参数链
    internal sealed class KiyumeFogShaderData : ScreenShaderData
    {
        public KiyumeFogShaderData(Asset<Effect> shader, string passName) : base(shader, passName) { }

        public override void Apply() {
            //uniform 与 s1/s2 绑定都在此刻完成（EndCapture 帧末会统一清空纹理槽）
            KiyumeFogSystem.BindFogTextures(Main.instance.GraphicsDevice);
            KiyumeFogSystem.ApplyPassUniforms(Shader, front: true);
            base.Apply();
        }
    }

    /// <summary>
    /// 鬼梦湖雾系统：潮汐推进、模拟驱动、Filter 激活、PostDrawTiles 背景雾层与 CPU 回退。
    /// 纯客户端表现，零同步包。<br/>
    /// 双层夹心：背景雾画在墙/砖/NPC 之后、玩家/弹幕之前——村子被裹进雾里，玩家走在雾前；
    /// 前景瘴气经 Filters.Scene 盖在世界最上层，并在那一层把亮点晕开（雾吃光）
    /// </summary>
    internal class KiyumeFogSystem : ModSystem, ICWRLoader
    {
        internal const string FilterName = "CWRMod:KiyumeFog";

        //前景层噪声去相相位（背景层 0,0）：两层雾形错开，防同相读成一张贴纸
        private static readonly Vector2 frontPhase = new(0.37f, 0.61f);

        private static float presence;

        /// <summary>
        /// 雾自己的淡入淡出，不复用 <see cref="KiyumeAmbienceSystem.Presence"/>——
        /// 主世界看样开关只该开雾，不该把整个世界染红
        /// </summary>
        internal static float Presence => presence;

        void ICWRLoader.LoadAsset() {
            if (EffectLoader.KiyumeFog == null) {
                return;
            }
            //第二参数是"pass 名"不是 technique 名：ShaderData.Apply 按 Passes[名字] 查表，
            //传错会 NRE 并把 FilterManager 的批撂在半开状态连锁崩溃
            Filters.Scene[FilterName] = new Filter(
                new KiyumeFogShaderData(EffectLoader.KiyumeFog, "FogFilter"), EffectPriority.High);
        }

        void ICWRLoader.UnLoadData() {
            presence = 0f;
            KiyumeFogSuppression.Clear();
            KiyumeFogSim.Unload();
        }

        public override void OnWorldLoad() => HardReset();
        public override void OnWorldUnload() => HardReset();

        private static void HardReset() {
            presence = 0f;
            KiyumeFogSuppression.Clear();
            KiyumeFogTide.Reset();
            KiyumeFogSim.Reset();
            KiyumeHoundShade.Clear();
        }

        private static bool WantFog => KiyumeWorld.Active || KiyumeFogDebug.ForceEnable;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool want = WantFog;
            presence = MathHelper.Lerp(presence, want ? 1f : 0f, want ? 0.06f : 0.10f);
            if (!want && presence < 0.004f) {
                presence = 0f;
                SetFilterActive(false);
                return;
            }

            KiyumeFogTide.Update();
            KiyumeFogSuppression.Update();
            PushFogAroundPlayers();
            KiyumeFogSim.Tick();
            KiyumeHoundShade.Update();

            bool shaderReady = EffectLoader.KiyumeFog?.Value != null && KiyumeFogSim.Ready;
            SetFilterActive(shaderReady && presence > 0.02f);
        }

        //玩家推雾：身位圆每帧续订，移动时再拖一个速度反向的尾流圆——
        //站定身周雾薄一圈，跑动身后留一条正在回聚的雾沟（回聚滞后由模拟的不对称时序免费提供）。
        //纯本地表现：每端都看得到彼此推开的雾，不需要同步
        private static void PushFogAroundPlayers() {
            float radius = KiyumeFogDebug.PlayerPushRadius;
            float feather = KiyumeFogDebug.PlayerPushFeather;
            float strength = KiyumeFogDebug.PlayerPushStrength;
            if (strength <= 0.01f) {
                return;
            }
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                KiyumeFogSuppression.RequestCircle(player.Center, radius, 4, feather, strength);
                float speed = player.velocity.Length();
                if (speed > 1.5f) {
                    //尾流拖在行进反方向，速度越快甩得越远；略小略弱，读成"被身体带起来的搅动"
                    Vector2 wake = player.Center - player.velocity * MathHelper.Clamp(speed * 0.9f, 3f, 9f);
                    KiyumeFogSuppression.RequestCircle(wake, radius * 0.7f, 4, feather * 0.8f, strength * 0.8f);
                }
            }
        }

        private static void SetFilterActive(bool active) {
            //索引器对未注册键返回 null；Activate/Deactivate 对未注册键抛异常，必须先判空
            Filter filter = Filters.Scene[FilterName];
            if (filter == null) {
                return;
            }
            if (active && !filter.IsActive()) {
                Filters.Scene.Activate(FilterName);
            }
            else if (!active && filter.IsActive()) {
                Filters.Scene.Deactivate(FilterName);
            }
        }

        //=== 渲染 ===

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || presence < 0.01f) {
                return;
            }
            //犬影必须画在背景雾层之前——"在雾墙后面"这句话的全部实现就是这个顺序
            KiyumeHoundShade.Draw(Main.spriteBatch);

            Effect fx = EffectLoader.KiyumeFog?.Value;
            if (fx != null && KiyumeFogSim.Ready) {
                DrawOverlayShader(fx);
            }
            else {
                KiyumeFogSim.DrawFallback(Main.spriteBatch, presence);
            }
        }

        //背景雾层：全屏 quad，预乘 AlphaBlend（暗雾必须能压暗，加色批物理上画不出暗）
        private static void DrawOverlayShader(Effect fx) {
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (canvas == null || canvas.IsDisposed || noise == null || noise.IsDisposed) {
                KiyumeFogSim.DrawFallback(Main.spriteBatch, presence);
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            //PostDrawTiles 时主批已收（TML 契约），这里自开自收；恒等变换=quad 直接覆满目标
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            BindFogTextures(gd);
            ApplyPassUniforms(fx, front: false);
            fx.CurrentTechnique.Passes["FogOverlay"].Apply();
            sb.Draw(canvas, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();
            //收批即解绑：防下个 update 对已绑定纹理 SetData 抛异常
            //（FilterManager 只在有滤镜捕获的帧末清空纹理槽）
            gd.Textures[1] = null;
            gd.Textures[2] = null;
        }

        /// <summary>两条通道共用的 s1/s2 绑定（滤镜通道在 EndCapture 时刻、背景通道在自开批内）</summary>
        internal static void BindFogTextures(GraphicsDevice gd) {
            Texture2D densityTex = KiyumeFogSim.Texture;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (densityTex != null && !densityTex.IsDisposed) {
                gd.Textures[1] = densityTex;
                gd.SamplerStates[1] = SamplerState.LinearClamp;
            }
            if (noise != null && !noise.IsDisposed) {
                gd.Textures[2] = noise;
                gd.SamplerStates[2] = SamplerState.LinearWrap;
            }
        }

        /// <summary>
        /// 两条通道共用的 uniform 上载（设备全局状态，每调用点全参数重设）。<br/>
        /// front=true 滤镜通道：EndCapture 已把重力翻转预翻正，仿射只逆 ZoomMatrix；<br/>
        /// front=false 背景通道：逆完整 TransformationMatrix（含 EffectMatrix，翻转自动覆盖）
        /// </summary>
        internal static void ApplyPassUniforms(Effect fx, bool front) {
            if (fx == null) {
                return;
            }
            Matrix inv = Matrix.Invert(front
                ? Main.GameViewMatrix.ZoomMatrix
                : Main.GameViewMatrix.TransformationMatrix);
            Point origin = KiyumeFogSim.OriginCell;
            float winW = KiyumeFogSim.WindowW;
            float winH = KiyumeFogSim.WindowH;
            const float capW = KiyumeFogSim.CapW;
            const float capH = KiyumeFogSim.CapH;
            const float cellPx = KiyumeFogSim.CellPx;

            fx.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            //目标px→世界px 仿射：world = offset + p*scale（矩阵为缩放+平移，无旋转项）
            fx.Parameters["uWorldScale"]?.SetValue(new Vector2(inv.M11, inv.M22));
            fx.Parameters["uWorldOffset"]?.SetValue(new Vector2(
                inv.M41 + Main.screenPosition.X, inv.M42 + Main.screenPosition.Y));
            fx.Parameters["uFogOrigin"]?.SetValue(new Vector2(origin.X, origin.Y) * cellPx);
            fx.Parameters["uFogUvMul"]?.SetValue(new Vector2(1f / (capW * cellPx), 1f / (capH * cellPx)));
            //半 texel 内缩钳制到窗口实际子矩形（容量纹理只用左上角）
            fx.Parameters["uFogUvClamp"]?.SetValue(new Vector4(
                0.5f / capW, 0.5f / capH,
                MathHelper.Max(winW - 0.5f, 0.5f) / capW, MathHelper.Max(winH - 0.5f, 0.5f) / capH));
            fx.Parameters["uPhase"]?.SetValue(front ? frontPhase : Vector2.Zero);
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uLayerMul"]?.SetValue(front
                ? KiyumeFogDebug.FrontLayerAlpha
                : KiyumeFogDebug.BackLayerAlpha);
            fx.Parameters["uPresence"]?.SetValue(KiyumeFogSim.Ready ? presence : 0f);

            //雾面几何：着色器要自己重建那条水平面，才画得出看得见的液面
            fx.Parameters["uFogLineY"]?.SetValue(KiyumeFogTide.LineWorldY);
            fx.Parameters["uLakeRightPx"]?.SetValue(KiyumeMetrics.LakeRightPx);
            fx.Parameters["uTiltPx"]?.SetValue(KiyumeMetrics.LakeTiltPx);
            fx.Parameters["uTiltSpanPx"]?.SetValue(KiyumeMetrics.TiltSpanPx);
            fx.Parameters["uSurfaceGlow"]?.SetValue(KiyumeFogDebug.SurfaceGlow);
            //吃光只有滤镜通道做得了——背景通道没有拷屏可采
            fx.Parameters["uEatLight"]?.SetValue(front ? KiyumeFogDebug.EatLight : 0f);
            fx.Parameters["uEatSpread"]?.SetValue(KiyumeFogDebug.EatSpread);
            //血湖水面：真水面反射带与雾面亮边在岸线处互补交接（湖上 rim 归零，岸上 water 归零）。
            //主世界看样没有血湖：渐隐起点推到负无穷让 rimGate 恒 1，水面项全灭
            bool inKiyume = KiyumeWorld.Active;
            fx.Parameters["uLakeWaterY"]?.SetValue(KiyumeMetrics.LakeWaterYPx);
            fx.Parameters["uRimFadeStartPx"]?.SetValue(inKiyume ? KiyumeMetrics.WaterRightPx : -1e9f);
            fx.Parameters["uRimFadeSpanPx"]?.SetValue(KiyumeMetrics.RimFadeSpanPx);
            fx.Parameters["uWaterGlow"]?.SetValue(inKiyume ? KiyumeFogDebug.WaterGlow : 0f);
        }
    }
}
