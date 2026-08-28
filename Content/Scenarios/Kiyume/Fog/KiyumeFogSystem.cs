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
    /// 鬼梦湖雾系统：潮汐推进、模拟驱动、Filter 激活、PostDrawTiles 雾层绘制与 CPU 回退。<br/>
    /// 潮汐钟已服务器权威化：dedServ 分支只推 <see cref="KiyumeFogTide.Update"/>（广播对钟见
    /// <see cref="KiyumeTideAuthority"/>），Sim/Suppression/渲染仍是纯客户端表现，不过线。<br/>
    /// 层序（PostDrawTiles，顺序即契约）：远带解析雾 → 犬影 → 鸦群 → 檐上栖鸦 → 近带雾海 → 贴地雾 → 犬目光点，
    /// 全部画在墙/砖/NPC 之后、玩家/弹幕之前；前景瘴气经 Filters.Scene 盖在世界最上层，
    /// 并在那一层把亮点晕开（雾吃光）
    /// </summary>
    internal class KiyumeFogSystem : ModSystem, ICWRLoader
    {
        internal const string FilterName = "CWRMod:KiyumeFog";

        //前景层噪声去相相位（背景层 0,0）：两层雾形错开，防同相读成一张贴纸
        private static readonly Vector2 frontPhase = new(0.37f, 0.61f);
        //远带第三相：三层各自的雾形
        private static readonly Vector2 farPhase = new(0.71f, 0.13f);

        //潮速差分缓存（uTideVel）：ApplyPassUniforms 每帧被多通道调用，差分按帧戳只走一次
        private static float tideVel;
        private static float tidePrev;
        private static uint tideStamp;

        private static float presence;

        /// <summary>
        /// 雾自己的淡入淡出，不复用 <see cref="KiyumeAmbienceSystem.Presence"/>
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
            KiyumeGroundField.Unload();
        }

        public override void OnWorldLoad() => HardReset();
        public override void OnWorldUnload() => HardReset();

        private static void HardReset() {
            presence = 0f;
            KiyumeFogSuppression.Clear();
            KiyumeFogTide.Reset();
            KiyumeFogSim.Reset();
            KiyumeHoundShade.Clear();
            Ambience.KiyumeCrowFlight.Clear();
            KiyumeGroundFogRender.Clear();
            KiyumeGroundField.Reset();
            NPCs.KiyumeHoundEyeGleam.Clear();
            tideVel = 0f;
            tidePrev = KiyumeFogTide.Tide;
            tideStamp = 0;
        }

        private static bool WantFog => KiyumeWorld.Active || KiyumeFogDebug.ForceEnable;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                //服务器只推潮汐钟（权威端，广播见 KiyumeTideAuthority）；
                //Sim/Suppression/犬影是纯客户端表现，不进
                if (KiyumeWorld.Active) {
                    KiyumeFogTide.Update();
                }
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

        //玩家推雾：身位圆每帧续订，移动时再拖一个速度反向的尾流圆
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
            Effect fx = EffectLoader.KiyumeFog?.Value;
            bool shaderReady = fx != null && KiyumeFogSim.Ready;
            //层序即契约：远带 quad → 犬影 → 鸦群 → 檐上栖鸦 → 近带雾海 → 贴地雾 → 犬目光点（后两层在方法尾）。
            //犬影/鸦群夹在远近两层之间："在雾墙后面"从此身后真有一层雾，剪影埋进雾的厚度里
            if (shaderReady) {
                DrawFarLayer(fx);
            }
            KiyumeHoundShade.Draw(Main.spriteBatch);
            Ambience.KiyumeCrowFlight.Draw(Main.spriteBatch);    //鸦群与犬影同层「雾墙后」
            Ambience.KiyumeCrowRoost.Draw(Main.spriteBatch);     //檐上栖鸦：同层，画在飞鸦之后
            if (shaderReady) {
                DrawOverlayShader(fx);
            }
            else {
                KiyumeFogSim.DrawFallback(Main.spriteBatch, presence);
            }
            //贴地残雾与瀑布雾：压在雾海之上、原版玩家绘制之前（着色器缺失自会静默不画）
            KiyumeGroundFogRender.Draw(Main.spriteBatch, presence);
            //（后续任务的层序追加行统一加在这行注释之后、方法返回之前）
            NPCs.KiyumeHoundEyeGleam.Draw(Main.spriteBatch);
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

        //远带体积雾：视差折算的解析雾 quad，画在犬影身后——雾海获得纵深，
        //远带只是体不抢面（rim/水面/倒影全关，见 ApplyPassUniforms 的 far 分支）
        private static void DrawFarLayer(Effect fx) {
            if (KiyumeFogDebug.FarLayerAlpha <= 0.003f) {
                return;
            }
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (canvas == null || canvas.IsDisposed || noise == null || noise.IsDisposed) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            BindFogTextures(gd);
            ApplyPassUniforms(fx, front: false, far: true);
            fx.CurrentTechnique.Passes["FogOverlay"].Apply();
            sb.Draw(canvas, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            sb.End();
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
        /// 三个调用点共用的 uniform 上载（设备全局状态，每调用点全参数重设）。<br/>
        /// front=true 滤镜通道：EndCapture 已把重力翻转预翻正，仿射只逆 ZoomMatrix；<br/>
        /// front=false 背景通道：逆完整 TransformationMatrix（含 EffectMatrix，翻转自动覆盖）；<br/>
        /// far=true 远带通道：背景仿射 + 视差折算 + 解析密度，rim/水面/倒影全关（只是体，不抢面）
        /// </summary>
        internal static void ApplyPassUniforms(Effect fx, bool front, bool far = false) {
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
            //目标px→世界px 仿射：world = offset + p*scale（矩阵为缩放+平移，无旋转项）。
            //远带视差：相机平移按 FarParallax 折算，锚在(湖右缘, 当前雾线)——镜头贴锚时两层重合，
            //离锚越远远带滞后越多，读作"更远的那层雾"；锚点世界确定，各端一致
            float parallax = far ? MathHelper.Clamp(KiyumeFogDebug.FarParallax, 0.5f, 1f) : 1f;
            Vector2 anchor = far
                ? new Vector2(KiyumeMetrics.LakeRightPx, KiyumeFogTide.LineWorldY)
                : Vector2.Zero;
            fx.Parameters["uWorldScale"]?.SetValue(new Vector2(inv.M11, inv.M22));
            fx.Parameters["uWorldOffset"]?.SetValue(new Vector2(
                inv.M41 + MathHelper.Lerp(anchor.X, Main.screenPosition.X, parallax),
                inv.M42 + MathHelper.Lerp(anchor.Y, Main.screenPosition.Y, parallax)));
            fx.Parameters["uFogOrigin"]?.SetValue(new Vector2(origin.X, origin.Y) * cellPx);
            fx.Parameters["uFogUvMul"]?.SetValue(new Vector2(1f / (capW * cellPx), 1f / (capH * cellPx)));
            //半 texel 内缩钳制到窗口实际子矩形（容量纹理只用左上角）
            fx.Parameters["uFogUvClamp"]?.SetValue(new Vector4(
                0.5f / capW, 0.5f / capH,
                MathHelper.Max(winW - 0.5f, 0.5f) / capW, MathHelper.Max(winH - 0.5f, 0.5f) / capH));
            fx.Parameters["uPhase"]?.SetValue(far ? farPhase : front ? frontPhase : Vector2.Zero);
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uLayerMul"]?.SetValue(far
                ? KiyumeFogDebug.FarLayerAlpha
                : front ? KiyumeFogDebug.FrontLayerAlpha : KiyumeFogDebug.BackLayerAlpha);
            fx.Parameters["uPresence"]?.SetValue(KiyumeFogSim.Ready ? presence : 0f);

            //雾面几何：着色器要自己重建那条水平面，才画得出看得见的液面
            fx.Parameters["uFogLineY"]?.SetValue(KiyumeFogTide.LineWorldY);
            fx.Parameters["uLakeRightPx"]?.SetValue(KiyumeMetrics.LakeRightPx);
            fx.Parameters["uTiltPx"]?.SetValue(KiyumeMetrics.LakeTiltPx);
            fx.Parameters["uTiltSpanPx"]?.SetValue(KiyumeMetrics.TiltSpanPx);
            fx.Parameters["uSurfaceGlow"]?.SetValue(far ? 0f : KiyumeFogDebug.SurfaceGlow);
            //吃光只有滤镜通道做得了，背景通道没有拷屏可采
            fx.Parameters["uEatLight"]?.SetValue(front ? KiyumeFogDebug.EatLight : 0f);
            fx.Parameters["uEatSpread"]?.SetValue(KiyumeFogDebug.EatSpread);
            //血湖水面：真水面反射带与雾面亮边在岸线处互补交接（湖上 rim 归零，岸上 water 归零）。
            //主世界看样没有血湖：渐隐起点推到负无穷让 rimGate 恒 1，水面项全灭；
            //远带同样推到负无穷：水面线/水下深渊只属于近景，视差层画第二份会成错位重影
            bool inKiyume = KiyumeWorld.Active;
            fx.Parameters["uLakeWaterY"]?.SetValue(KiyumeMetrics.LakeWaterYPx);
            fx.Parameters["uRimFadeStartPx"]?.SetValue(!far && inKiyume ? KiyumeMetrics.WaterRightPx : -1e9f);
            fx.Parameters["uRimFadeSpanPx"]?.SetValue(KiyumeMetrics.RimFadeSpanPx);
            fx.Parameters["uWaterGlow"]?.SetValue(!far && inKiyume ? KiyumeFogDebug.WaterGlow : 0f);

            //===雾海质感与体积（P1-pkg2）===
            fx.Parameters["uTideVel"]?.SetValue(TideVel());
            fx.Parameters["uCrestBreak"]?.SetValue(far ? 0f : KiyumeFogDebug.CrestBreak);
            fx.Parameters["uGhost"]?.SetValue(far ? 0f : KiyumeFogDebug.NearSurfaceGhost);
            //倒影/柱化只住背景近带（滤镜槽位留给浪冠与光楔）；光楔只住滤镜通道
            fx.Parameters["uReflect"]?.SetValue(!front && !far ? KiyumeFogDebug.ReflectGlow : 0f);
            fx.Parameters["uSteamCol"]?.SetValue(!front && !far ? KiyumeFogDebug.SteamColumnar : 0f);
            fx.Parameters["uGodray"]?.SetValue(front ? KiyumeFogDebug.GodrayStrength : 0f);
            //远带解析密度：视差坐标会超出密度窗口，shader 内按解析式重建（常数与 KiyumeFogSim 同源）
            fx.Parameters["uAnalyticFog"]?.SetValue(far ? 1f : 0f);
            fx.Parameters["uFalloffSpanPx"]?.SetValue(KiyumeMetrics.FalloffSpanPx);
            //主世界看样离湖极远，衰减不砍远带（预览可见）；子世界照正典
            fx.Parameters["uFarFogMul"]?.SetValue(inKiyume ? KiyumeMetrics.FarFogMul : 1f);
        }

        //潮位差分 ×2400 归一（真实潮汐主周期中段≈1），轻平滑防副摆抖动；
        //暂停帧返回缓存；掉帧补跑除以间隔 tick 数折回每 tick 速率
        //（原「非相邻帧硬置 0」在持续掉帧下会把浪冠永久饿死——掉帧机上更新:绘制恒 ≥2:1）
        private static float TideVel() {
            uint now = Main.GameUpdateCount;
            if (now != tideStamp) {
                float cur = KiyumeFogTide.Tide;
                float ticks = now - tideStamp;
                float instant = MathHelper.Clamp(
                    MathHelper.Distance(cur, tidePrev) / ticks * 2400f, 0f, 1f);
                tideVel = MathHelper.Lerp(tideVel, instant, 0.08f);
                tidePrev = cur;
                tideStamp = now;
            }
            return tideVel;
        }
    }
}
