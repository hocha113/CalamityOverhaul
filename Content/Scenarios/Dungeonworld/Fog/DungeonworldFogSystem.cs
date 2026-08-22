using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog
{
    //滤镜着色数据：Apply 在 FilterManager.EndCapture 时刻被调，
    //在这里上载绘制时相机仿射/密度纹理（帧内零滞后），再走原版参数链
    internal sealed class DungeonworldFogShaderData : ScreenShaderData
    {
        public DungeonworldFogShaderData(Asset<Effect> shader, string passName) : base(shader, passName) { }

        public override void Apply() {
            //uniform 与 s1/s2 绑定都在此刻完成（EndCapture 帧末会统一清空纹理槽）
            DungeonworldFogSystem.BindFogTextures(Main.instance.GraphicsDevice);
            DungeonworldFogSystem.ApplyPassUniforms(Shader, front: true);
            base.Apply();
        }
    }

    /// <summary>
    /// 深牢迷雾系统：模拟驱动、Filter 激活、PostDrawTiles 背景雾层与 CPU 回退、
    /// 深牢怨灵禁室压雾消费者。纯客户端表现，零同步包（FOG.md）。<br/>
    /// 双层夹心：背景雾画在墙/砖/NPC 之后、弹幕/玩家之前（TML Main.cs L59957 时序）
    /// 敌人被裹进雾里，玩家走在雾前；前景瘴气经 Filters.Scene 盖在世界最上层
    /// </summary>
    internal class DungeonworldFogSystem : ModSystem, ICWRLoader
    {
        internal const string FilterName = "CWRMod:DungeonworldFog";

        //前景层噪声去相相位（背景层 0,0）：两层雾形错开，防同相读成一张贴纸
        private static readonly Vector2 frontPhase = new(0.37f, 0.61f);

        private static float presence;
        private static int bossScanTimer;
        //-2=未解析 -1=解析失败 ≥0=NPC类型id
        private static int wraithType = -2;

        /// <summary>全局淡入淡出 0~1（进出子世界/调试开关的包络）</summary>
        internal static float Presence => presence;

        void ICWRLoader.LoadAsset() {
            if (EffectLoader.DungeonworldFog == null) {
                return;
            }
            //第二参数是"pass 名"不是 technique 名：ShaderData.Apply 按 Passes[名字] 查表，
            //传错会 NRE 并把 FilterManager 的批撂在半开状态连锁崩溃（ScrapSiege 2026-08 事故）
            Filters.Scene[FilterName] = new Filter(
                new DungeonworldFogShaderData(EffectLoader.DungeonworldFog, "FogFilter"), EffectPriority.High);
        }

        void ICWRLoader.UnLoadData() {
            presence = 0f;
            bossScanTimer = 0;
            wraithType = -2;
            FogSuppression.Clear();
            DungeonworldFogSim.Unload();
        }

        public override void OnWorldLoad() => HardReset();
        public override void OnWorldUnload() => HardReset();

        private static void HardReset() {
            presence = 0f;
            FogSuppression.Clear();
            DungeonworldFogSim.Reset();
        }

        private static bool WantFog => Dungeonworld.Active || DungeonworldFogDebug.ForceEnable;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool want = WantFog;
            presence = MathHelper.Lerp(presence, want ? 1f : 0f, want ? 0.07f : 0.10f);
            if (!want && presence < 0.004f) {
                presence = 0f;
                SetFilterActive(false);
                return;
            }

            FogSuppression.Update();
            UpdateBossSuppression();
            DungeonworldFogSim.Tick();

            bool shaderReady = EffectLoader.DungeonworldFog?.Value != null && DungeonworldFogSim.Ready;
            SetFilterActive(shaderReady && presence > 0.02f);
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

        //禁室压雾消费者：Boss 存活即按帧续订（集成期已换类型化引用）
        private static void UpdateBossSuppression() {
            if (wraithType == -1 || Main.gameMenu) {
                return;
            }
            if (wraithType == -2) {
                wraithType = ModContent.NPCType<NPCs.DeepGaolWraith>();
            }
            if (++bossScanTimer < 4) {
                return;
            }
            bossScanTimer = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type == wraithType) {
                    //禁室 62×42tile=992×672px：半径+羽化以 Boss 为心盖满全房
                    FogSuppression.RequestCircle(npc.Center, 820f, 12, 260f);
                }
            }
        }

        //=== 渲染 ===

        public override void PostDrawTiles() {
            if (Main.dedServ || Main.gameMenu || presence < 0.01f) {
                return;
            }
            Effect fx = EffectLoader.DungeonworldFog?.Value;
            if (fx != null && DungeonworldFogSim.Ready) {
                DrawOverlayShader(fx);
            }
            else {
                DungeonworldFogSim.DrawFallback(Main.spriteBatch, presence);
            }
        }

        //背景雾层：全屏 quad，预乘 AlphaBlend（暗雾必须能压暗，加色批物理上画不出暗）
        private static void DrawOverlayShader(Effect fx) {
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (canvas == null || canvas.IsDisposed || noise == null || noise.IsDisposed) {
                DungeonworldFogSim.DrawFallback(Main.spriteBatch, presence);
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
            Texture2D densityTex = DungeonworldFogSim.Texture;
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
            Point origin = DungeonworldFogSim.OriginCell;
            float winW = DungeonworldFogSim.WindowW;
            float winH = DungeonworldFogSim.WindowH;
            const float capW = DungeonworldFogSim.CapW;
            const float capH = DungeonworldFogSim.CapH;
            const float cellPx = DungeonworldFogSim.CellPx;

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
                ? DungeonworldFogDebug.FrontLayerAlpha
                : DungeonworldFogDebug.BackLayerAlpha);
            fx.Parameters["uPresence"]?.SetValue(DungeonworldFogSim.Ready ? presence : 0f);
        }
    }
}
