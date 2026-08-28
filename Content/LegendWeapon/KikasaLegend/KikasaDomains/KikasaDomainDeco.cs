using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains
{
    /// <summary>
    /// 血湖领域装饰：湖面涟漪、水线行波、贴水血雾、破水抛洒的血水滴。
    /// 血滴走真抛物线并落回湖面荡出微圈，湖是实体存在，不是贴图。
    /// </summary>
    internal static class KikasaDomainDeco
    {
        /// <summary>破水抛出的血水滴：重力抛物线，速度拉伸软体，落回湖面即被收走</summary>
        private class BloodDrop
        {
            public Vector2 Pos;
            public Vector2 Vel;
            /// <summary>基准半径（像素）</summary>
            public float Size;
            /// <summary>所属湖面的世界 Y，落回即回收</summary>
            public float LakeY;
            public float Seed;
            public int Life;
            public int MaxLife;
        }

        private class Ripple
        {
            public Vector2 Pos;
            public float Scale;
            public int Life;
            public int MaxLife;
        }

        /// <summary>水线行波源：一次落点扰动让水面本身局部起伏、向两侧荡开。
        /// 由 <see cref="RippleAt"/> 统一登记，经 <see cref="FillWaveUniforms"/> 喂给湖面与天空着色器</summary>
        private struct LineWave
        {
            public float WorldX;
            public float AmpPx;
            /// <summary>波长/传播距离的等比乘数：大扰动荡长浪，只抬幅度会变成大号示波器</summary>
            public float RangeMul;
            public int Life;
            public int MaxLife;
        }

        private static readonly List<BloodDrop> drops = new();
        private static readonly List<Ripple> ripples = new();
        //槽位数与着色器 uLineWave[4] 对齐；复用上传缓冲避免逐帧分配
        private static readonly LineWave[] lineWaves = new LineWave[4];
        private static readonly Vector4[] waveUpload = new Vector4[4];
        private static readonly Vector4[] waveUploadWorld = new Vector4[4];

        private const int DropCap = 140;
        private const int RippleCap = 16;

        //血系配色随观看域的鬼雨异化冷化（血珠→尸雨灰白、血雾→潮雾沉青、血光→冷青微光）
        private static Color SplashPale => KikasaDomain.CoolTint(new(214, 118, 106), new(170, 185, 190));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));
        private static Color RippleGlow => KikasaDomain.CoolTint(new(198, 88, 82), new(126, 152, 158));
        //血滴双层：深血本体沉、血光高光亮，异化时同步转浊水色
        private static Color DropBody => KikasaDomain.CoolTint(new(96, 18, 22, 0), new(58, 74, 80, 0));
        private static Color DropSheen => KikasaDomain.CoolTint(new(226, 96, 84, 0), new(150, 178, 184, 0));

        private static int mistTimer;
        private static int rippleTimer;
        //满幕雨帘的补投累积
        private static float rainCarry;

        public static void Clear() {
            drops.Clear();
            ripples.Clear();
            Array.Clear(lineWaves, 0, lineWaves.Length);
            rainCarry = 0f;
        }

        /// <summary>水面破水溅花：血珠扇形溅起 + 潮雾；量级到破水级（count≥5）时
        /// 复合一蓬有物理的血水滴，抛起再落回湖面</summary>
        public static void SplashAt(Vector2 world, int count) {
            for (int i = 0; i < count; i++) {
                float angle = -MathHelper.Pi * (0.15f + 0.7f * i / MathF.Max(count - 1, 1));
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(1.8f, 4.2f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    world + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f),
                    vel, SplashPale * Main.rand.NextFloat(0.4f, 0.62f),
                    Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(18, 30), vel.X);
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(
                world + new Vector2(0f, -6f),
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -0.1f),
                MistBlood * 0.8f, Main.rand.NextFloat(0.6f, 0.9f))
                ?.Configure(Main.rand.Next(70, 110));

            if (count >= 5) {
                BloodBurst(world, Math.Min(4 + count, 26),
                    MathHelper.Clamp(count / 16f, 0.5f, 1.6f));
            }
        }

        /// <summary>抛洒一蓬血水滴：向上扇形喷出，重力拉回湖面。power 控制喷射高度与滴径</summary>
        public static void BloodBurst(Vector2 world, int count, float power) {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null) {
                return;
            }
            float lakeY = kdp.LakeWorldY;
            for (int i = 0; i < count; i++) {
                if (drops.Count >= DropCap) {
                    return;
                }
                //中央高抛、两翼低抛的扇形，喷柱有主次
                float ang = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.95f, 0.95f);
                float centered = 1f - MathF.Abs(ang + MathHelper.PiOver2) / 0.95f;
                float speed = Main.rand.NextFloat(2.4f, 5.2f)
                    * (0.65f + power * 0.55f) * (0.7f + centered * 0.6f);
                Vector2 vel = ang.ToRotationVector2() * speed;
                drops.Add(new BloodDrop {
                    Pos = world + new Vector2(Main.rand.NextFloat(-10f, 10f), -Main.rand.NextFloat(0f, 4f)),
                    Vel = vel,
                    Size = Main.rand.NextFloat(1.8f, 3.8f) * (0.8f + power * 0.35f),
                    LakeY = lakeY,
                    Seed = Main.rand.NextFloat(10f),
                    MaxLife = 110,
                });
            }
        }

        /// <summary>沸腾气泡：沿水线随机散点破水的碎泡，颜色随镜面预览向目标形态先行渐变。
        /// 出生线取 <see cref="KikasaDomainPlayer.VisualLakeY"/>：上涌期从抬高后的面上破水</summary>
        public static void BoilBurst(KikasaDomainPlayer kdp, float strength, float coldMix) {
            Color bubble = Color.Lerp(new(214, 118, 106), new(170, 185, 190), coldMix);

            int count = 2 + (int)(strength * 4f);
            float left = Main.screenPosition.X;
            float lakeY = kdp.VisualLakeY;
            for (int i = 0; i < count; i++) {
                float x = left + Main.rand.NextFloat(0f, Main.screenWidth);
                Vector2 pos = new(x, lakeY - Main.rand.NextFloat(0f, 4f));
                Vector2 vel = new(Main.rand.NextFloat(-0.8f, 0.8f),
                    -Main.rand.NextFloat(1.6f, 3.6f) * (0.6f + strength * 0.6f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel,
                    bubble * Main.rand.NextFloat(0.4f, 0.62f),
                    Main.rand.NextFloat(0.45f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 30), vel.X);
            }
            //滚水自己也荡圈
            if (Main.rand.NextBool(5)) {
                RippleAt(new Vector2(left + Main.rand.NextFloat(0f, Main.screenWidth), lakeY),
                    Main.rand.NextFloat(0.4f, 0.9f) * (0.5f + strength * 0.5f));
            }
        }

        /// <summary>沸腾蒸汽：贴水上浮的翻滚潮气，同样从抬高后的观感水面出生</summary>
        public static void BoilSteam(KikasaDomainPlayer kdp, float strength, float coldMix) {
            Color steam = Color.Lerp(new(58, 18, 20), new(52, 62, 66), coldMix);
            int count = 1 + (int)(strength * 2f);
            for (int i = 0; i < count; i++) {
                float x = Main.screenPosition.X + Main.rand.NextFloat(0f, Main.screenWidth);
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(x, kdp.VisualLakeY - Main.rand.NextFloat(2f, 24f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                        -Main.rand.NextFloat(0.25f, 0.7f) * (0.5f + strength)),
                    steam * Main.rand.NextFloat(0.5f, 0.8f),
                    Main.rand.NextFloat(0.6f, 1.0f))
                    ?.Configure(Main.rand.Next(50, 90));
            }
        }

        /// <summary>湖面荡开一圈涟漪：加色双环，量级够时（scale≥0.3）水线行波一并登记
        /// 血滴回落的微圈只画环不起浪，四个波槽留给真正的事件</summary>
        public static void RippleAt(Vector2 world, float scale) {
            if (scale >= 0.3f) {
                SpawnLineWave(world.X, scale);
            }
            if (ripples.Count >= RippleCap) {
                return;
            }
            ripples.Add(new Ripple {
                Pos = world,
                Scale = scale,
                MaxLife = Main.rand.Next(34, 50)
            });
        }

        /// <summary>踏水碎星：沿行进反方向踢起几滴，行走涟漪的配菜</summary>
        public static void FootSplash(Vector2 world, float strength, float velX) {
            int count = 2 + (int)(strength * 2f);
            for (int i = 0; i < count; i++) {
                Vector2 vel = new(
                    -MathF.Sign(velX) * Main.rand.NextFloat(0.4f, 1.5f) - velX * 0.12f,
                    -Main.rand.NextFloat(1.2f, 2.4f + strength * 1.6f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    world + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                    vel, SplashPale * Main.rand.NextFloat(0.35f, 0.55f),
                    Main.rand.NextFloat(0.32f, 0.52f))
                    ?.Configure(Main.rand.Next(14, 24), vel.X);
            }
        }

        //登记一道水线行波，满编时顶掉进度最深的旧波

        private static void SpawnLineWave(float worldX, float scale) {
            int slot = 0;
            float worst = -1f;
            for (int i = 0; i < lineWaves.Length; i++) {
                ref LineWave w = ref lineWaves[i];
                if (w.MaxLife <= 0 || w.Life >= w.MaxLife) {
                    slot = i;
                    worst = float.MaxValue;
                    break;
                }
                float progress = w.Life / (float)w.MaxLife;
                if (progress > worst) {
                    worst = progress;
                    slot = i;
                }
            }
            lineWaves[slot] = new LineWave {
                WorldX = worldX,
                //克制的涌动幅度：叠满四源也不过一格上下，别荡成示波器
                AmpPx = MathF.Min(1.2f + scale * 3f, 15f),
                //大水花的浪更长更远，寿命也更久
                RangeMul = MathHelper.Clamp(0.8f + scale * 0.28f, 0.9f, 1.9f),
                Life = 0,
                MaxLife = 55 + (int)(MathF.Min(scale, 3.5f) * 25f),
            };
        }

        /// <summary>
        /// 把活跃行波打包进着色器的 uLineWave[4]（x=源 uv.x，y=寿命进度，z=幅度 uv.y，w=范围乘数）。
        /// 湖面与天空的像素空间不同，各自传入自家相机原点与视口尺寸，投影结果逐像素一致
        /// </summary>
        internal static void FillWaveUniforms(Effect effect, Vector2 cameraPos, Vector2 viewSize) {
            for (int i = 0; i < lineWaves.Length; i++) {
                ref LineWave w = ref lineWaves[i];
                waveUpload[i] = Vector4.Zero;
                if (w.MaxLife <= 0 || w.Life >= w.MaxLife) {
                    continue;
                }
                float uvX = Vector2.Transform(
                    new Vector2(w.WorldX - cameraPos.X, 0f),
                    Main.GameViewMatrix.TransformationMatrix).X / viewSize.X;
                //长浪波及可达约 0.6 视宽，出界更远的源不值得占用指令
                if (uvX < -0.9f || uvX > 1.9f) {
                    continue;
                }
                waveUpload[i] = new Vector4(
                    uvX, w.Life / (float)w.MaxLife, w.AmpPx / viewSize.Y, w.RangeMul);
            }
            effect.Parameters["uLineWave"]?.SetValue(waveUpload);
        }

        /// <summary>
        /// 跟脚潮让位坑参数打包（uv 域）：x=中心 uv.x，y=半宽 uv.x，z=坑深 uv.y（负=蓄势隆起），
        /// w=坑唇幅度 uv.y。湖面与天空像素空间不同，各自传入自家相机原点与视口尺寸，
        /// 投影结果逐像素一致（同 <see cref="FillWaveUniforms"/> 之约）
        /// </summary>
        internal static void FillTideUniforms(Effect effect, KikasaDomainPlayer kdp,
            Vector2 cameraPos, Vector2 viewSize) {
            float centerUv = Vector2.Transform(
                new Vector2(kdp.TideTroughCenterX - cameraPos.X, 0f),
                Main.GameViewMatrix.TransformationMatrix).X / viewSize.X;
            effect.Parameters["uTideTrough"]?.SetValue(new Vector4(
                centerUv,
                MathF.Max(kdp.TideTroughHalfWidthPx * Main.GameViewMatrix.Zoom.X / viewSize.X, 1e-4f),
                kdp.TideTroughDepthPx * Main.GameViewMatrix.Zoom.Y / viewSize.Y,
                kdp.TideLipAmpPx * Main.GameViewMatrix.Zoom.Y / viewSize.Y));
        }

        /// <summary>让位坑参数打包（世界像素域），供世界锚定 quad 的鬼火层自算，
        /// 与 uv 版坑轮廓常数同源（同 <see cref="FillWaveUniformsWorld"/> 之约）</summary>
        internal static void FillTideUniformsWorld(Effect effect, KikasaDomainPlayer kdp) {
            effect.Parameters["uTideTroughW"]?.SetValue(new Vector4(
                kdp.TideTroughCenterX,
                MathF.Max(kdp.TideTroughHalfWidthPx, 1f),
                kdp.TideTroughDepthPx,
                kdp.TideLipAmpPx));
        }

        /// <summary>
        /// 行波以世界像素域打包（x=源世界X y=寿命进度 z=幅度px w=范围乘数），
        /// 供世界锚定 quad 的着色器（鬼火层）自算涌动，与屏幕 uv 版波形常数同源
        /// </summary>
        internal static void FillWaveUniformsWorld(Effect effect) {
            for (int i = 0; i < lineWaves.Length; i++) {
                ref LineWave w = ref lineWaves[i];
                waveUploadWorld[i] = Vector4.Zero;
                if (w.MaxLife <= 0 || w.Life >= w.MaxLife) {
                    continue;
                }
                waveUploadWorld[i] = new Vector4(
                    w.WorldX, w.Life / (float)w.MaxLife, w.AmpPx, w.RangeMul);
            }
            effect.Parameters["uLineWave"]?.SetValue(waveUploadWorld);
        }

        public static void Update() {
            KikasaDomainPlayer kdp = KikasaDomain.Viewed;
            if (kdp == null) {
                if (drops.Count > 0 || ripples.Count > 0) {
                    Clear();
                }
                return;
            }

            bool steady = kdp.Phase == KikasaDomainPhase.Open;
            bool lakeReady = kdp.RiseT > 0.95f;

            //死水偶发的自发涟漪与贴水血雾

            if (steady && lakeReady) {
                if (--rippleTimer <= 0) {
                    rippleTimer = Main.rand.Next(50, 130);
                    float x = Main.screenPosition.X + Main.rand.NextFloat(0f, Main.screenWidth);
                    RippleAt(new Vector2(x, kdp.LakeWorldY), Main.rand.NextFloat(0.5f, 1.1f));
                }
                if (--mistTimer <= 0) {
                    mistTimer = Main.rand.Next(40, 90);
                    float x = Main.screenPosition.X + Main.rand.NextFloat(0f, Main.screenWidth);
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        new Vector2(x, kdp.LakeWorldY - Main.rand.NextFloat(4f, 30f)),
                        new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.03f, 0.10f)),
                        MistBlood * Main.rand.NextFloat(0.45f, 0.7f),
                        Main.rand.NextFloat(0.55f, 0.95f))
                        ?.Configure(Main.rand.Next(90, 150));
                }
            }

            UpdateRainCurtain(kdp);
            UpdateBloodDrops(lakeReady);
            UpdateRipples();
            EmitLakeLight(kdp);
        }

        /// <summary>
        /// 湖是领域自己的光源：沿水线向两岸渗一层低光，夜晚与地下"湖亮、世界黑"的
        /// 断裂由它缝合。血湖暖红、鬼雨冷青，随涨水渐亮；鬼梦无湖不发光。
        /// 照明只在观看端，同鬼火水线金光之例（<see cref="KikasaWisps.KikasaWispFX"/>）
        /// </summary>
        private static void EmitLakeLight(KikasaDomainPlayer kdp) {
            if (kdp.DreamWorldVisual || kdp.RiseT <= 0.05f) {
                return;
            }
            float k = kdp.PresenceSmooth * MathHelper.Clamp(kdp.RiseT * 1.4f, 0f, 1f);
            if (k <= 0.02f) {
                return;
            }
            Vector3 glow = Vector3.Lerp(
                new Vector3(0.34f, 0.085f, 0.09f),
                new Vector3(0.13f, 0.19f, 0.23f), kdp.RainBlend) * k;
            float casterX = kdp.Player.Center.X;
            float xMin = MathF.Max(Main.screenPosition.X - 60f,
                casterX - KikasaLakeSurface.HalfWidth);
            float xMax = MathF.Min(Main.screenPosition.X + Main.screenWidth + 60f,
                casterX + KikasaLakeSurface.HalfWidth);
            if (xMax - xMin < 16f) {
                return;
            }
            float lakeY = kdp.VisualLakeY;
            for (float x = xMin; x <= xMax; x += 170f) {
                Lighting.AddLight(new Vector2(x, lakeY - 8f), glow.X, glow.Y, glow.Z);
            }
        }

        /// <summary>异化态满幕雨帘：密度吃领域的雨帘包络，做法镜像鬼雨世界常驻雨（湿墨色板）</summary>
        private static void UpdateRainCurtain(KikasaDomainPlayer kdp) {
            float density = kdp.RainCurtainDensity;
            //大范围重启演出期间雨帘拉满：冲刷与倒带都要有足量的雨可看
            if (KikasaReset.LocallyViewed) {
                density = MathF.Max(density, 1.35f);
            }
            if (density < 0.02f) {
                rainCarry = 0f;
                return;
            }

            float left = Main.screenPosition.X - 160f;
            float right = Main.screenPosition.X + Main.screenWidth + 160f;
            rainCarry += density * 0.02f * (right - left);
            int count = Math.Min((int)rainCarry, 72);
            rainCarry -= count;
            //进量超帧上限时截断积欠，防翻转叠加下无限攒债
            rainCarry = MathF.Min(rainCarry, 30f);
            if (count <= 0) {
                return;
            }

            Color pale = new(170, 185, 190);
            Color corpse = new(140, 170, 165);
            float wind = MathF.Sin(Main.worldID % 255 * 0.37f) * 2.2f * density;
            bool rewinding = KikasaReset.RainRewindActive;
            for (int i = 0; i < count; i++) {
                Vector2 pos = new(Main.rand.NextFloat(left, right),
                    Main.screenPosition.Y - Main.rand.NextFloat(10f, 220f));
                Vector2 vel = new(wind + Main.rand.NextFloat(-0.35f, 0.35f),
                    Main.rand.NextFloat(11f, 17f));
                Color color = (Main.rand.NextBool(7) ? corpse : pale)
                    * Main.rand.NextFloat(0.42f, 0.65f);
                float scale = Main.rand.NextFloat(0.8f, 1.25f);
                //纵深分层：约四成滴退成远幕（小、淡、慢），雨量不减而读出雨墙层次，
                //扑在玩家活动层上的遮挡近乎减半；近幕保持原尺寸原速
                float depthMul = 1f;
                if (Main.rand.NextFloat() < 0.42f) {
                    depthMul = Main.rand.NextFloat(0.68f, 0.8f);
                    vel *= depthMul;
                    color *= 0.62f;
                    scale *= 0.66f;
                }
                int life = Main.rand.Next(70, 110);
                if (rewinding) {
                    //约半数从地表反向扁溅重生：砸过地的雨被时间收回去，聚成珠再升空。
                    //只是整幕向上飞会读成"一波雨飞回天上"，缺了从地面收回来的那一层
                    if (Main.rand.NextBool()
                        && TryFindRainGround(pos.X, out float groundY)) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            new Vector2(pos.X, groundY - 2f), Vector2.Zero, color, scale)
                            ?.Configure(life, vel.X).AsCurtain(depthMul).BeginRebirth();
                        continue;
                    }
                    //其余在幕内出生向上飞，补足空中密度
                    pos.Y = Main.screenPosition.Y
                        + Main.rand.NextFloat(Main.screenHeight * 0.2f, Main.screenHeight + 60f);
                    vel.Y = -vel.Y;
                }
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel, color, scale)
                    ?.Configure(life, vel.X).AsCurtain(depthMul);
            }
        }

        /// <summary>
        /// 大范围重启借雨的一次性补雨：照片背后按稳态同款色板/风偏/纵深分层，
        /// 在整个视区高度上撒满下落中的雨滴，冲刷揭出的空中已是一场下了很久的雨，
        /// 而不是刚从天顶起头。与稳态雨帘共享 PRT 池预算；出生点陷在实心方块里的滴直接略过
        /// </summary>
        internal static void PrefillRainCurtain() {
            float left = Main.screenPosition.X - 160f;
            float right = Main.screenPosition.X + Main.screenWidth + 160f;
            float top = Main.screenPosition.Y - 40f;
            float bottom = Main.screenPosition.Y + Main.screenHeight + 40f;
            Color pale = new(170, 185, 190);
            Color corpse = new(140, 170, 165);
            //风偏与演出期稳态雨帘同口径（重启强拉的密度 1.35）
            float wind = MathF.Sin(Main.worldID % 255 * 0.37f) * 2.2f * 1.35f;
            for (int i = 0; i < 300; i++) {
                Vector2 pos = new(Main.rand.NextFloat(left, right),
                    Main.rand.NextFloat(top, bottom));
                if (Collision.SolidCollision(pos - new Vector2(1f, 1f), 2, 2)) {
                    continue;
                }
                Vector2 vel = new(wind + Main.rand.NextFloat(-0.35f, 0.35f),
                    Main.rand.NextFloat(11f, 17f));
                Color color = (Main.rand.NextBool(7) ? corpse : pale)
                    * Main.rand.NextFloat(0.42f, 0.65f);
                float scale = Main.rand.NextFloat(0.8f, 1.25f);
                float depthMul = 1f;
                if (Main.rand.NextFloat() < 0.42f) {
                    depthMul = Main.rand.NextFloat(0.68f, 0.8f);
                    vel *= depthMul;
                    color *= 0.62f;
                    scale *= 0.66f;
                }
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos, vel, color, scale)
                    ?.Configure(Main.rand.Next(70, 110), vel.X).AsCurtain(depthMul);
            }
        }

        /// <summary>倒带雨的重生落点：从视区上部向下找首个实心面（鬼梦地雾同型扫描）</summary>
        private static bool TryFindRainGround(float x, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)((Main.screenPosition.Y + Main.screenHeight * 0.10f) / 16f);
            int span = Main.screenHeight * 95 / 100 / 16 + 2;
            for (int i = 0; i < span; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        //血滴抛物线：重力+微阻；落回湖面收走并按概率荡个微圈（不占行波槽）

        private static void UpdateBloodDrops(bool lakeReady) {
            float screenBottom = Main.screenPosition.Y + Main.screenHeight;

            for (int i = drops.Count - 1; i >= 0; i--) {
                BloodDrop d = drops[i];
                d.Life++;
                d.Vel.X *= 0.985f;
                d.Vel.Y = MathF.Min(d.Vel.Y + 0.30f, 12f);
                d.Pos += d.Vel;

                //落回湖面：微圈留给量感，池余量优先保主涟漪
                if (lakeReady && d.Vel.Y > 0f && d.Pos.Y >= d.LakeY - 1f) {
                    if (ripples.Count < RippleCap - 4 && Main.rand.NextBool(3)) {
                        RippleAt(new Vector2(d.Pos.X, d.LakeY),
                            Main.rand.NextFloat(0.14f, 0.24f));
                    }
                    drops.RemoveAt(i);
                    continue;
                }

                if (d.Life >= d.MaxLife || d.Pos.Y > screenBottom + 120f) {
                    drops.RemoveAt(i);
                }
            }
        }

        private static void UpdateRipples() {
            for (int i = ripples.Count - 1; i >= 0; i--) {
                Ripple r = ripples[i];
                r.Life++;
                if (r.Life >= r.MaxLife) {
                    ripples.RemoveAt(i);
                }
            }
            for (int i = 0; i < lineWaves.Length; i++) {
                ref LineWave w = ref lineWaves[i];
                if (w.MaxLife > 0 && w.Life < w.MaxLife) {
                    w.Life++;
                }
            }
        }

        public static void Draw(SpriteBatch spriteBatch) {
            if (drops.Count == 0 && ripples.Count == 0) {
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;

            //血滴本体：深血软椭圆沿速度拉伸（乘混合压暗），水下段由湖面着色器接管再染

            if (glow != null && drops.Count > 0) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);

                Vector2 gOrigin = glow.Size() * 0.5f;
                foreach (BloodDrop d in drops) {
                    float speed = d.Vel.Length();
                    float rot = d.Vel.ToRotation();
                    float len = d.Size * (2.0f + speed * 0.26f);
                    float wid = d.Size * 1.7f;
                    float fade = 1f - d.Life / (float)d.MaxLife * 0.35f;
                    Vector2 scale = new(len * 2f / glow.Width, wid * 2f / glow.Height);
                    spriteBatch.Draw(glow, d.Pos - Main.screenPosition, null,
                        DropBody * (0.9f * fade), rot, gOrigin, scale, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
            }

            //湖面涟漪双环与血滴的光泽拖尾：同一加色批

            if ((ring != null && ripples.Count > 0) || (glow != null && drops.Count > 0)) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);

                if (ring != null) {
                    Vector2 rOrigin = ring.Size() * 0.5f;
                    foreach (Ripple r in ripples) {
                        float lifeF = r.Life / (float)r.MaxLife;
                        float radius = MathHelper.Lerp(8f, 86f, 1f - (1f - lifeF) * (1f - lifeF)) * r.Scale;
                        float alpha = MathF.Sin(MathHelper.Clamp(lifeF, 0f, 1f) * MathHelper.Pi) * 0.42f;
                        //真加色批源因子是 SourceAlpha：A 置零=整圈不画，A 随强度走
                        Color c = RippleGlow * alpha;
                        Vector2 scale = new(radius * 2f / ring.Width, radius * 0.44f / ring.Height);
                        spriteBatch.Draw(ring, r.Pos - Main.screenPosition, null, c,
                            0f, rOrigin, scale, SpriteEffects.None, 0f);

                        //内环滞后荡开，第二道波给涟漪层次
                        float lag = MathHelper.Clamp((lifeF - 0.18f) / 0.82f, 0f, 1f);
                        if (lag > 0f) {
                            float radius2 = MathHelper.Lerp(6f, 58f, 1f - (1f - lag) * (1f - lag)) * r.Scale;
                            float alpha2 = MathF.Sin(lag * MathHelper.Pi) * 0.26f;
                            Vector2 scale2 = new(radius2 * 2f / ring.Width, radius2 * 0.40f / ring.Height);
                            spriteBatch.Draw(ring, r.Pos - Main.screenPosition, null, RippleGlow * alpha2,
                                0f, rOrigin, scale2, SpriteEffects.None, 0f);
                        }
                    }
                }

                //血滴光泽：飞行拖尾 + 受光小亮斑，滴上有湿光才读作液体而非色块
                if (glow != null) {
                    Vector2 gOrigin = glow.Size() * 0.5f;
                    foreach (BloodDrop d in drops) {
                        float speed = d.Vel.Length();
                        float rot = d.Vel.ToRotation();
                        float tailLen = d.Size * (1.6f + speed * 0.5f);
                        Vector2 tailScale = new(tailLen * 2f / glow.Width, d.Size * 2.0f / glow.Height);
                        spriteBatch.Draw(glow, d.Pos - Main.screenPosition - d.Vel * 1.1f, null,
                            DropSheen * 0.16f, rot, gOrigin, tailScale, SpriteEffects.None, 0f);

                        float sheen = d.Size * 0.95f;
                        Vector2 sheenScale = new(sheen * 2f / glow.Width, sheen * 2f / glow.Height);
                        spriteBatch.Draw(glow, d.Pos - Main.screenPosition + new Vector2(0f, -d.Size * 0.3f),
                            null, DropSheen * 0.34f, 0f, gOrigin, sheenScale, SpriteEffects.None, 0f);
                    }
                }

                spriteBatch.End();
            }
        }
    }
}
