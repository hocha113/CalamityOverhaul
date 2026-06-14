using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.AcheronProtocols
{
    /// <summary>
    /// 虫群绘制：泰伦式星际虫族：比银河系更巨大的生物群落正在吞噬一切 ★ SoftGlow 使用规范 ★ SoftGlow 是一张"黑底圆形渐变"纹理，直接以 AlphaBlend 绘制会产生明显的黑色方框 正确做法：将绘制颜色的 A 分量设为 0 原理：tModLoader/XNA 默认使用"预乘Alpha"混合方程： result = src.rgb + dest.rgb × (1 - src.a) 当 src.a = 0 时，方程退化为纯加法（result = src.rgb + dest.rgb）， 黑像素 RGB=0 对加法无贡献，方框自然消失 本文件所有 SoftGlow 绘制均遵循此规范，不得例外
    /// </summary>
    internal partial class GalacticCrisisRender
    {
        #region 虫群参数与数据

        private const int SwarmMainTendrilCount = 8;    //主触手（粗大，深入银河系）
        private const int SwarmSubTendrilCount = 22;    //次级触手（中等）
        private const int SwarmMicroTendrilCount = 45;  //微触须（细小，最前沿探针）
        private const int SwarmMaxParticles = 420;

        private static readonly List<SwarmTendril> swarmMainTendrils = [];
        private static readonly List<SwarmTendril> swarmSubTendrils = [];
        private static readonly List<SwarmTendril> swarmMicroTendrils = [];
        private static readonly List<SwarmParticle> swarmParticles = [];

        private static float swarmApproachProgress;
        private static float swarmPulseTimer;
        private static float swarmBreathTimer;  //整体呼吸节律（慢速）
        private static float swarmCreepTimer;   //蠕动相位（中速）

        //虫族主入侵方向
        private const float SwarmCenterAngle = -MathHelper.PiOver4;
        //虫族可见弧宽：它比银河系大得多，弧宽超过180°
        private const float SwarmArcWidth = MathHelper.Pi * 1.25f;

        private struct SwarmTendril
        {
            public float BaseAngle;
            public float Length;        //当前长度（0-1相对值）
            public float MaxLength;
            public float GrowSpeed;
            public float BaseWidth;     //根部宽度（逻辑像素）
            public float WavePhase;
            public float WaveFreq;
            public float WaveAmp;       //振幅倍率（相对于 BaseWidth）
            public int SegmentCount;
            public float BiolumPhase;   //生物荧光脉冲相位
            public int TendrilClass;    //0=Main, 1=Sub, 2=Micro
        }

        private struct SwarmParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float BiolumLevel;   //0=普通暗色生物体，>0.4=发光神经节细胞
        }

        #endregion

        #region 初始化与清理

        private static void InitSwarm() {
            swarmApproachProgress = 0f;
            swarmPulseTimer = 0f;
            swarmBreathTimer = 0f;
            swarmCreepTimer = 0f;
            GenerateSwarmTendrils();
            swarmParticles.Clear();
        }

        private static void CleanupSwarm() {
            swarmMainTendrils.Clear();
            swarmSubTendrils.Clear();
            swarmMicroTendrils.Clear();
            swarmParticles.Clear();
        }

        private static void GenerateSwarmTendrils() {
            swarmMainTendrils.Clear();
            swarmSubTendrils.Clear();
            swarmMicroTendrils.Clear();

            //主触手：均匀分布在中心方向附近，穿透最深
            for (int i = 0; i < SwarmMainTendrilCount; i++) {
                float t = (i + 0.5f) / SwarmMainTendrilCount;
                float angle = SwarmCenterAngle + MathHelper.Lerp(-0.65f, 0.65f, t);
                swarmMainTendrils.Add(new SwarmTendril {
                    BaseAngle = angle,
                    Length = 0f,
                    MaxLength = Main.rand.NextFloat(0.50f, 0.80f),
                    GrowSpeed = Main.rand.NextFloat(0.0025f, 0.0045f),
                    BaseWidth = Main.rand.NextFloat(28f, 55f),
                    WavePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    WaveFreq = Main.rand.NextFloat(3.0f, 5.5f),
                    WaveAmp = Main.rand.NextFloat(0.35f, 0.65f),
                    SegmentCount = 30,
                    BiolumPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    TendrilClass = 0
                });
            }

            //次级触手：更宽的分布角度
            for (int i = 0; i < SwarmSubTendrilCount; i++) {
                float halfArc = SwarmArcWidth * 0.38f;
                float angle = SwarmCenterAngle + Main.rand.NextFloat(-halfArc, halfArc);
                swarmSubTendrils.Add(new SwarmTendril {
                    BaseAngle = angle,
                    Length = 0f,
                    MaxLength = Main.rand.NextFloat(0.22f, 0.52f),
                    GrowSpeed = Main.rand.NextFloat(0.0035f, 0.0065f),
                    BaseWidth = Main.rand.NextFloat(10f, 26f),
                    WavePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    WaveFreq = Main.rand.NextFloat(4.0f, 7.5f),
                    WaveAmp = Main.rand.NextFloat(0.55f, 1.05f),
                    SegmentCount = 22,
                    BiolumPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    TendrilClass = 1
                });
            }

            //微触须：最广的覆盖，代表虫族最前沿的探测纤维
            for (int i = 0; i < SwarmMicroTendrilCount; i++) {
                float halfArc = SwarmArcWidth * 0.45f;
                float angle = SwarmCenterAngle + Main.rand.NextFloat(-halfArc, halfArc);
                swarmMicroTendrils.Add(new SwarmTendril {
                    BaseAngle = angle,
                    Length = 0f,
                    MaxLength = Main.rand.NextFloat(0.12f, 0.38f),
                    GrowSpeed = Main.rand.NextFloat(0.005f, 0.010f),
                    BaseWidth = Main.rand.NextFloat(3.5f, 10f),
                    WavePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    WaveFreq = Main.rand.NextFloat(5.0f, 12f),
                    WaveAmp = Main.rand.NextFloat(0.9f, 1.8f),
                    SegmentCount = 16,
                    BiolumPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    TendrilClass = 2
                });
            }
        }

        #endregion

        #region 逻辑更新

        private static void UpdateSwarmLogic() {
            swarmBreathTimer += 0.016f;
            swarmCreepTimer += 0.022f;

            //更新粒子
            for (int i = swarmParticles.Count - 1; i >= 0; i--) {
                SwarmParticle p = swarmParticles[i];
                p.Life++;
                p.Position += p.Velocity;
                //粒子受微弱的侧向漂移（模拟虫族群体涌动感）
                float driftAngle = MathF.Sin(p.Life * 0.04f + p.BiolumLevel * MathHelper.TwoPi) * 0.03f;
                float cos = MathF.Cos(driftAngle), sin = MathF.Sin(driftAngle);
                p.Velocity = new Vector2(
                    p.Velocity.X * cos - p.Velocity.Y * sin,
                    p.Velocity.X * sin + p.Velocity.Y * cos
                );
                swarmParticles[i] = p;
                if (p.Life >= p.MaxLife)
                    swarmParticles.RemoveAt(i);
            }
        }

        private static void UpdateSwarmApproachPhase() {
            swarmApproachProgress = MathF.Min(swarmApproachProgress + 0.004f, 1f);
            swarmPulseTimer += 0.04f;
            phaseProgress = swarmApproachProgress;

            float growMult = MathHelper.Lerp(0.6f, 1.4f, swarmApproachProgress);

            //更新主触手
            for (int i = 0; i < swarmMainTendrils.Count; i++) {
                SwarmTendril t = swarmMainTendrils[i];
                t.Length = MathF.Min(t.Length + t.GrowSpeed * growMult, t.MaxLength * swarmApproachProgress);
                t.WavePhase += 0.018f;
                swarmMainTendrils[i] = t;
            }
            //更新次级触手
            for (int i = 0; i < swarmSubTendrils.Count; i++) {
                SwarmTendril t = swarmSubTendrils[i];
                t.Length = MathF.Min(t.Length + t.GrowSpeed * growMult, t.MaxLength * swarmApproachProgress);
                t.WavePhase += 0.028f;
                swarmSubTendrils[i] = t;
            }
            //更新微触须
            for (int i = 0; i < swarmMicroTendrils.Count; i++) {
                SwarmTendril t = swarmMicroTendrils[i];
                t.Length = MathF.Min(t.Length + t.GrowSpeed * growMult, t.MaxLength * swarmApproachProgress);
                t.WavePhase += 0.048f;
                swarmMicroTendrils[i] = t;
            }

            glitchIntensity = MathHelper.Lerp(0.02f, 0.15f, swarmApproachProgress);
            SpawnSwarmParticles();
        }

        private static void UpdateIdlePhase() {
            swarmPulseTimer += 0.03f;
        }

        private static void SpawnSwarmParticles() {
            if (swarmParticles.Count >= SwarmMaxParticles) return;
            int batch = Main.rand.Next(1, 4);
            for (int b = 0; b < batch; b++) {
                if (swarmParticles.Count >= SwarmMaxParticles) break;

                float halfArc = SwarmArcWidth * 0.42f;
                float spawnAngle = SwarmCenterAngle + Main.rand.NextFloat(-halfArc, halfArc);
                float spawnDist = GalaxyRadius * (1.30f + Main.rand.NextFloat(0.55f));
                Vector2 spawnPos = new Vector2(MathF.Cos(spawnAngle), MathF.Sin(spawnAngle)) * spawnDist;

                //粒子整体向银河系方向漂移，带有随机偏角
                float towardAngle = MathF.Atan2(-spawnPos.Y, -spawnPos.X);
                float sideOffset = Main.rand.NextFloat(-0.35f, 0.35f);
                float speed = Main.rand.NextFloat(0.35f, 1.4f);
                Vector2 velocity = new Vector2(
                    MathF.Cos(towardAngle + sideOffset),
                    MathF.Sin(towardAngle + sideOffset)
                ) * speed;

                swarmParticles.Add(new SwarmParticle {
                    Position = spawnPos,
                    Velocity = velocity,
                    Life = 0f,
                    MaxLife = Main.rand.NextFloat(90f, 260f),
                    Size = Main.rand.NextFloat(0.8f, 3.8f),
                    BiolumLevel = Main.rand.NextFloat() < 0.12f
                        ? Main.rand.NextFloat(0.55f, 1.0f)  //少量亮神经节
                        : Main.rand.NextFloat(0.0f, 0.25f)  //大量暗生物体
                });
            }
        }

        #endregion

        #region 绘制

        private static void DrawSwarm(SpriteBatch sb, Vector2 center, float alpha) {
            float swarmAlpha = alpha * MathF.Min(swarmApproachProgress * 2.5f, 1f);
            if (swarmAlpha < 0.01f) return;

            //绘制顺序：背景生物质量 → 触手群 → 粒子群 → 接触前沿荧光
            DrawSwarmBiomass(sb, center, swarmAlpha);
            DrawAllTendrils(sb, center, swarmAlpha);
            DrawSwarmParticles(sb, center, swarmAlpha);
            DrawSwarmFrontEdge(sb, center, swarmAlpha);
        }

        /// <summary>

        /// 计算虫族主体中心和可见半径（触手根部与前沿弧线共用）

        /// </summary>
        private static void GetSwarmCenterAndRadius(Vector2 center, out Vector2 swarmCenter, out float massRadius) {
            //虫族主体从极远处逼近，整个过程中主体中心始终在面板外侧
            float swarmDist = GalaxyRadius * MathHelper.Lerp(2.3f, 1.35f, swarmApproachProgress);
            swarmCenter = center + new Vector2(MathF.Cos(SwarmCenterAngle), MathF.Sin(SwarmCenterAngle)) * swarmDist;
            //虫族截面半径远超银河系：从视觉上让人感受到这是一个无穷大的存在
            massRadius = GalaxyRadius * MathHelper.Lerp(1.3f, 1.9f, swarmApproachProgress);
        }

        private static void DrawSwarmBiomass(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D sg = CWRAsset.SoftGlow?.Value;
            if (sg == null) return;

            GetSwarmCenterAndRadius(center, out Vector2 swarmCenter, out float massRadius);
            Vector2 sgOrigin = new(sg.Width * 0.5f, sg.Height * 0.5f);
            float breath = MathF.Sin(swarmBreathTimer * 0.9f) * 0.04f + 0.96f;

            //── 层1：极大尺度外层遮蔽 ──
            //这层营造出"背景被排挤掉"的压迫感，颜色极暗以免喧宾夺主
            {
                Color c = new Color(3, 1, 7); c.A = 0;
                sb.Draw(sg, swarmCenter, null, c * (alpha * 0.6f), 0f, sgOrigin,
                    massRadius * 0.030f * breath, SpriteEffects.None, 0f);
            }
            {
                Color c = new Color(5, 2, 10); c.A = 0;
                sb.Draw(sg, swarmCenter, null, c * (alpha * 0.45f), 0f, sgOrigin,
                    massRadius * 0.024f * breath, SpriteEffects.None, 0f);
            }

            //── 层2：深紫色生物腺体高光 ──
            //虫族内部腺体在半透明外皮下透出的颜色
            float pulse = MathF.Sin(swarmPulseTimer * 1.2f) * 0.18f + 0.82f;
            {
                Color c = new Color(30, 6, 50); c.A = 0;
                sb.Draw(sg, swarmCenter, null, c * (alpha * 0.7f * pulse), 0f, sgOrigin,
                    massRadius * 0.015f, SpriteEffects.None, 0f);
            }
            {
                Color c = new Color(55, 12, 85); c.A = 0;
                sb.Draw(sg, swarmCenter, null, c * (alpha * 0.5f * pulse), 0f, sgOrigin,
                    massRadius * 0.009f, SpriteEffects.None, 0f);
            }

            //── 层3：生物电浆核心（泰伦虫族最具辨识度的亮绿黄色） ──
            float bioElec = MathF.Sin(swarmPulseTimer * 2.2f + swarmBreathTimer * 1.5f) * 0.28f + 0.72f;
            {
                //大范围淡绿弥散
                Color c = new Color(18, 55, 8); c.A = 0;
                sb.Draw(sg, swarmCenter, null, c * (alpha * 0.35f * bioElec), 0f, sgOrigin,
                    massRadius * 0.010f, SpriteEffects.None, 0f);
            }
            {
                //集中亮绿核心
                Color c = new Color(35, 100, 15); c.A = 0;
                sb.Draw(sg, swarmCenter, null, c * (alpha * 0.22f * bioElec), 0f, sgOrigin,
                    massRadius * 0.005f, SpriteEffects.None, 0f);
            }

            //── 层4：边缘腺体/脓包blob群 ──
            //沿虫族前沿分布的不规则有机突起，强调生物质量感
            int blobCount = 20;
            for (int i = 0; i < blobCount; i++) {
                float blobAngle = MathHelper.TwoPi * i / blobCount
                    + MathF.Sin(swarmCreepTimer * 0.7f + i * 0.65f) * 0.18f
                    + globalTimer * 0.035f * (i % 2 == 0 ? 1f : -1f);

                float wobble = MathF.Sin(swarmPulseTimer * 1.6f + i * 1.05f) * 0.22f + 0.78f;
                float dist = massRadius * (0.38f + wobble * 0.28f);
                Vector2 blobPos = swarmCenter + new Vector2(MathF.Cos(blobAngle), MathF.Sin(blobAngle)) * dist;

                //三种腺体颜色交替出现
                Color blobC;
                switch (i % 3) {
                    case 0:
                        blobC = new Color(14, 45, 6); blobC.A = 0;   //生物电浆绿
                        break;
                    case 1:
                        blobC = new Color(35, 8, 55); blobC.A = 0;   //深紫生物腺
                        break;
                    default:
                        blobC = new Color(8, 2, 15); blobC.A = 0;    //极暗有机组织
                        break;
                }
                float blobScale = massRadius * (0.0038f + MathF.Sin(swarmCreepTimer * 1.2f + i * 1.4f) * 0.0014f);
                float blobAlpha = alpha * (i % 3 == 0 ? 0.48f : 0.38f) * wobble;
                sb.Draw(sg, blobPos, null, blobC * blobAlpha, 0f, sgOrigin, blobScale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>

        /// 按从细到粗的顺序绘制三层触手，粗触手遮盖细触手产生深度感

        /// </summary>
        private static void DrawAllTendrils(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D sg = CWRAsset.SoftGlow?.Value;
            if (sg == null) return;

            foreach (var tendril in swarmMicroTendrils)
                DrawSingleTendril(sb, center, tendril, sg, alpha);
            foreach (var tendril in swarmSubTendrils)
                DrawSingleTendril(sb, center, tendril, sg, alpha);
            foreach (var tendril in swarmMainTendrils)
                DrawSingleTendril(sb, center, tendril, sg, alpha);
        }

        private static void DrawSingleTendril(SpriteBatch sb, Vector2 center, in SwarmTendril tendril, Texture2D sg, float alpha) {
            if (tendril.Length <= 0.01f) return;

            Vector2 sgOrigin = new(sg.Width * 0.5f, sg.Height * 0.5f);

            //触手起点在虫族主体前沿，终点向银河系内部穿透
            float startDist = GalaxyRadius * MathHelper.Lerp(1.95f, 1.45f, swarmApproachProgress);
            float maxPenetration = tendril.TendrilClass switch {
                0 => 1.05f,  //Main: 最深，可穿越银河系中段
                1 => 0.85f,  //Sub
                _ => 0.65f   //Micro
            };
            float minEndDist = tendril.TendrilClass switch {
                0 => GalaxyRadius * 0.35f,
                1 => GalaxyRadius * 0.52f,
                _ => GalaxyRadius * 0.68f
            };
            float endDist = MathF.Max(
                GalaxyRadius * (1.45f - tendril.Length * maxPenetration),
                minEndDist
            );

            Vector2 baseDir = new Vector2(MathF.Cos(tendril.BaseAngle), MathF.Sin(tendril.BaseAngle));
            Vector2 perpDir = new Vector2(-baseDir.Y, baseDir.X);

            Vector2 startPos = center + baseDir * startDist;
            Vector2 endPos = center + baseDir * endDist;

            //颜色定义（全部 A = 0）
            Color coreC, midC;
            switch (tendril.TendrilClass) {
                case 0: //Main: 最暗最厚
                    coreC = new Color(10, 3, 18); coreC.A = 0;
                    midC = new Color(40, 10, 65); midC.A = 0;
                    break;
                case 1: //Sub
                    coreC = new Color(8, 2, 14); coreC.A = 0;
                    midC = new Color(30, 7, 48); midC.A = 0;
                    break;
                default: //Micro
                    coreC = new Color(6, 2, 10); coreC.A = 0;
                    midC = new Color(18, 4, 30); midC.A = 0;
                    break;
            }
            //生物电浆绿（所有触手共用，但亮度不同）
            Color bioC = new Color(14, 50, 6); bioC.A = 0;

            int segs = tendril.SegmentCount;
            float progressRatio = tendril.Length / MathF.Max(tendril.MaxLength, 0.0001f);

            for (int seg = 0; seg < segs; seg++) {
                float t = seg / (float)segs;
                if (t > progressRatio) break;

                //双频叠加波形模拟肌肉运动波传导
                float wave1 = MathF.Sin(tendril.WavePhase + t * tendril.WaveFreq) * tendril.WaveAmp;
                float wave2 = MathF.Sin(tendril.WavePhase * 0.55f + t * tendril.WaveFreq * 1.8f + swarmCreepTimer) * tendril.WaveAmp * 0.38f;
                float waveOffset = (wave1 + wave2) * tendril.BaseWidth;
                Vector2 segPos = Vector2.Lerp(startPos, endPos, t) + perpDir * waveOffset;

                //触手宽度：根部最粗，向末端锥形收细，叠加微小脉冲起伏
                float taper = 1f - t * 0.82f;
                float pulseMod = 1f + MathF.Sin(t * 5.5f + tendril.WavePhase + swarmCreepTimer * 0.8f) * 0.07f;
                float segWidth = tendril.BaseWidth * taper * pulseMod;

                float segAlpha = alpha * (1f - t * 0.45f);

                //─ 层1：暗紫黑色核心（最细，塑造触手实体感）
                sb.Draw(sg, segPos, null, coreC * (segAlpha * 0.85f), 0f, sgOrigin,
                    segWidth * 0.022f, SpriteEffects.None, 0f);
                //─ 层2：紫色中间层（更宽，形成触手主体体积）
                sb.Draw(sg, segPos, null, midC * (segAlpha * 0.55f), 0f, sgOrigin,
                    segWidth * 0.042f, SpriteEffects.None, 0f);
                //─ 层3：生物电浆荧光脉冲（沿触手向末端传播）
                float bioPhase = tendril.BiolumPhase + t * 9f - swarmPulseTimer * 1.8f;
                float bioPulse = MathF.Max(0f, MathF.Sin(bioPhase));
                if (bioPulse > 0.04f) {
                    float bioIntensity = tendril.TendrilClass switch {
                        0 => 0.75f, //主触手荧光最亮
                        1 => 0.55f,
                        _ => 0.38f
                    };
                    sb.Draw(sg, segPos, null, bioC * (segAlpha * bioPulse * bioIntensity), 0f, sgOrigin,
                        segWidth * 0.014f, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawSwarmParticles(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D sg = CWRAsset.SoftGlow?.Value;
            if (sg == null) return;
            Vector2 sgOrigin = new(sg.Width * 0.5f, sg.Height * 0.5f);

            foreach (var p in swarmParticles) {
                float lifeRatio = p.Life / p.MaxLife;
                float fade = MathF.Sin(lifeRatio * MathHelper.Pi);
                float partAlpha = fade * alpha;
                if (partAlpha < 0.01f) continue;

                Vector2 screenPos = center + p.Position;

                if (p.BiolumLevel > 0.38f) {
                    //发光神经节细胞：亮绿色核心 + 外层弱光晕
                    byte rB = (byte)(25 + p.BiolumLevel * 35);
                    byte gB = (byte)(75 + p.BiolumLevel * 65);
                    byte bB = (byte)(10 + p.BiolumLevel * 15);
                    Color innerC = new Color(rB, gB, bB); innerC.A = 0;
                    Color outerC = new Color(8, 30, 3); outerC.A = 0;

                    sb.Draw(sg, screenPos, null, innerC * (partAlpha * p.BiolumLevel), 0f, sgOrigin,
                        p.Size * 0.048f, SpriteEffects.None, 0f);
                    sb.Draw(sg, screenPos, null, outerC * (partAlpha * 0.28f), 0f, sgOrigin,
                        p.Size * 0.12f, SpriteEffects.None, 0f);
                }
                else {
                    //普通暗色生物体：深紫色调
                    Color c = new Color(14, 3, 22); c.A = 0;
                    sb.Draw(sg, screenPos, null, c * (partAlpha * 0.55f), 0f, sgOrigin,
                        p.Size * 0.038f, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawSwarmFrontEdge(SpriteBatch sb, Vector2 center, float alpha) {
            Texture2D sg = CWRAsset.SoftGlow?.Value;
            if (sg == null) return;
            Vector2 sgOrigin = new(sg.Width * 0.5f, sg.Height * 0.5f);

            //前沿位于虫族主体前方，随进度推进至银河系边缘
            float frontDist = GalaxyRadius * MathHelper.Lerp(1.55f, 1.05f, swarmApproachProgress);
            float pulse = MathF.Sin(swarmPulseTimer * 1.7f) * 0.22f + 0.78f;

            //弧度随进度扩张（虫族前沿接触面越来越宽）
            float arcSpread = MathHelper.ToRadians(85f) + swarmApproachProgress * MathHelper.ToRadians(45f);
            int edgeCount = 28;

            for (int i = 0; i < edgeCount; i++) {
                float t = i / (float)edgeCount;
                float edgeAngle = SwarmCenterAngle - arcSpread * 0.5f + arcSpread * t;
                Vector2 edgePos = center + new Vector2(MathF.Cos(edgeAngle), MathF.Sin(edgeAngle)) * frontDist;

                //沿弧线的强度包络（中间最亮，两端衰减）
                float envelope = MathF.Sin(t * MathHelper.Pi);
                //局部脉冲（各段独立呼吸）
                float localPulse = MathF.Sin(swarmPulseTimer * 2.8f + t * 7f + swarmCreepTimer * 0.5f) * 0.35f + 0.65f;

                Color edgeC;
                if (i % 3 == 0) {
                    //生物电浆绿节点
                    float gb = localPulse;
                    edgeC = new Color((byte)(16 * gb), (byte)(65 * gb), (byte)(6 * gb)); edgeC.A = 0;
                }
                else {
                    //腺体紫节点
                    float pb = localPulse;
                    edgeC = new Color((byte)(70 * pb), (byte)(16 * pb), (byte)(105 * pb)); edgeC.A = 0;
                }

                float edgeAlpha = alpha * 0.6f * pulse * envelope;
                float edgeScale = 0.28f + MathF.Sin(swarmPulseTimer * 3.2f + t * 5.5f) * 0.07f;
                sb.Draw(sg, edgePos, null, edgeC * edgeAlpha, 0f, sgOrigin, edgeScale, SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}
