using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.TBUGs
{
    /// <summary>
    /// TBUG 出场裂缝；材质是"世界渲染漏掉的一块"——纯黑内里 + 报错内容，不是发光的门。
    /// <see cref="TBUGRiftSpawner"/> 主端生成，SpitFrame 主端 NewNPC 把她吐出来
    /// <br/>ai0=facing±1 ai1=TBUG whoAmI(主写客读) ai2=尺寸缩放
    /// </summary>
    internal class TBUGRiftPortalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>总生命，与阶段帧同步</summary>
        public const int TotalLife = 240;
        /// <summary>0..TearEnd 撕开</summary>
        public const int TearEnd = 24;
        /// <summary>InhaleStart..SpitFrame 向内急收，吐出前的反预备</summary>
        public const int InhaleStart = 92;
        /// <summary>主端此帧 NewNPC 并给抛射初速</summary>
        public const int SpitFrame = 100;
        /// <summary>CollapseStart..TotalLife 方块化坍塌</summary>
        public const int CollapseStart = 165;

        /// <summary>竖缝横向半宽 px</summary>
        public const float BaseHalfWidth = 40f;
        /// <summary>高宽比，&gt;1 竖直细高</summary>
        public const float AspectRatio = 2.4f;
        /// <summary>竖向半高 px</summary>
        public const float BaseHalfHeight = BaseHalfWidth * AspectRatio;
        /// <summary>裂缝中心离地高度；下沿悬空 8px，读作浮在世界上的洞，也给吐出留下坠距离</summary>
        public const float CenterAboveGround = BaseHalfHeight + 8f;

        /// <summary>吐出初速；X 朝 facing 方向，Y 向上一点做抛物线</summary>
        private static readonly Vector2 SpitVelocity = new(3.2f, -4.5f);

        private float Facing => Projectile.ai[0] >= 0f ? 1f : -1f;
        /// <summary>主端写 whoAmI，负值未生成，-2 放弃生成</summary>
        public int BoundTBUGWhoAmI {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }
        private float Scale => Projectile.ai[2] <= 0.01f ? 1f : Projectile.ai[2];

        private int AgeFrame => TotalLife - Projectile.timeLeft;

        /// <summary>每端独立渲染种子，不同步</summary>
        private float visualSeed;

        public override void SetStaticDefaults() {
            //半离屏仍绘制
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
            ProjectileID.Sets.TrailingMode[Type] = -1;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            //城镇出场，需客户端稳收弹幕
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;

        public override void AI() {
            int age = AgeFrame;

            //首帧种子；主端 ai1 默认 0 与 whoAmI=0 冲突，置 -1
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                visualSeed = Main.rand.NextFloat();
                if (Main.netMode != NetmodeID.MultiplayerClient && BoundTBUGWhoAmI == 0) {
                    BoundTBUGWhoAmI = -1;
                    Projectile.netUpdate = true;
                }
                PlaySfxTearStart();
            }

            if (age == TearEnd) {
                PlaySfxTearDone();
                ShakeLocalNear(3f, 1100f);
            }

            if (age == InhaleStart) {
                PlaySfxInhale();
            }

            //主端 SpitFrame 生成并同步 whoAmI
            if (age == SpitFrame && Main.netMode != NetmodeID.MultiplayerClient && BoundTBUGWhoAmI < 0) {
                SpawnTBUGOnServer();
            }

            if (age == SpitFrame && !VaultUtils.isServer) {
                SpitBurst();
            }

            //吐出帧起持续认领：客户端可能晚一两帧才收到 NPC
            EnsureEntry(age);
            SpawnAmbientParticles(age);

            if (age == CollapseStart) {
                PlaySfxCollapse();
            }
        }

        /// <summary>主端 NewNPC 给抛射初速，写 ai1</summary>
        private void SpawnTBUGOnServer() {
            int npcType = ModContent.NPCType<TBUG>();
            if (NPC.AnyNPCs(npcType)) {
                BoundTBUGWhoAmI = -2;//放弃生成，动画继续
                Projectile.netUpdate = true;
                return;
            }

            //NewNPC 的 (x,y) 是脚底中心；从裂缝中心吐出
            int x = (int)Projectile.Center.X;
            int y = (int)Projectile.Center.Y;

            int index = NPC.NewNPC(new EntitySource_WorldEvent(), x, y, npcType);
            if (index < 0 || index >= Main.maxNPCs) {
                BoundTBUGWhoAmI = -2;
                Projectile.netUpdate = true;
                return;
            }

            NPC t = Main.npc[index];
            t.alpha = 255;
            t.direction = t.spriteDirection = Facing >= 0f ? 1 : -1;
            t.velocity = new Vector2(SpitVelocity.X * Facing, SpitVelocity.Y);
            //NewNPC 默认 homeless=false + homeTile=-1，会把家钉死在落点；
            //置 true 交给原版分房流程搬进玩家房屋
            t.homeless = true;
            t.netUpdate = true;

            //首次登场后转正常城镇 NPC，死后由原版住房系统重生
            if (!TBUGWorldState.HasArrived) {
                TBUGWorldState.HasArrived = true;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }

            BoundTBUGWhoAmI = index;
            Projectile.netUpdate = true;
        }

        /// <summary>吐出帧起每帧认领绑定 NPC，触发其入场状态机（BeginEntry 自带一次性护栏）</summary>
        private void EnsureEntry(int age) {
            if (age < SpitFrame) {
                return;
            }
            int who = BoundTBUGWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                return;
            }
            NPC n = Main.npc[who];
            if (!n.active || n.type != ModContent.NPCType<TBUG>()) {
                return;
            }
            if (n.ModNPC is TBUG t) {
                t.BeginEntry(Facing >= 0f ? 1 : -1);
            }
        }

        #region 粒子与音效

        private void SpawnAmbientParticles(int age) {
            if (VaultUtils.isServer) {
                return;
            }

            //撕开密、稳定稀、吸气与坍塌向内爆
            int interval;
            if (age < TearEnd) interval = 1;
            else if (age < InhaleStart) interval = 4;
            else if (age < SpitFrame) interval = 1;
            else if (age < CollapseStart) interval = 5;
            else interval = 2;

            if (Projectile.timeLeft % interval != 0) {
                return;
            }

            float halfW = BaseHalfWidth * Scale;
            float halfH = BaseHalfHeight * Scale;

            int count = age < TearEnd ? Main.rand.Next(2, 4)
                : age >= CollapseStart ? Main.rand.Next(3, 6)
                : Main.rand.Next(1, 3);

            bool inward = (age >= InhaleStart && age < SpitFrame) || age >= CollapseStart;

            for (int i = 0; i < count; i++) {
                //沿左右两条竖边取样，两端收窄
                float v = Main.rand.NextFloat(-1f, 1f);
                float taper = MathF.Sqrt(MathF.Max(0f, 1f - v * v));
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 edge = new(side * halfW * taper * Main.rand.NextFloat(0.8f, 1.1f), v * halfH);
                Vector2 spawn = Projectile.Center + edge;
                //横向为主的外飞，故障切片是从缝里被挤出来的
                Vector2 vel = new(side * Main.rand.NextFloat(1.2f, 3.6f), Main.rand.NextFloat(-0.8f, 0.8f));
                if (inward) {
                    vel = -vel * 1.5f;
                }

                float scl = Main.rand.NextFloat(0.5f, 1.3f);
                int life = Main.rand.Next(18, 36);
                PRTLoader.NewParticle<PRT_TBUGGlitch>(spawn, vel, Color.White, scl).Configure(life);
            }
        }

        /// <summary>吐出帧的客户端演出：朝向半球的碎渣爆发 + 震动</summary>
        private void SpitBurst() {
            for (int i = 0; i < 22; i++) {
                //偏向 facing 一侧的扇面
                float ang = Facing >= 0f
                    ? Main.rand.NextFloat(-1.1f, 1.1f)
                    : MathHelper.Pi + Main.rand.NextFloat(-1.1f, 1.1f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_TBUGGlitch>(Projectile.Center, vel, Color.White,
                    Main.rand.NextFloat(0.8f, 1.6f)).Configure(Main.rand.Next(20, 40));
            }
            SoundEngine.PlaySound(CWRSound.Faultrelease with { Volume = 0.6f, Pitch = 0.05f }, Projectile.Center);
            ShakeLocalNear(6f, 1200f);
        }

        private void PlaySfxTearStart() {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
        }
        private void PlaySfxTearDone() {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.3f, Pitch = -0.15f }, Projectile.Center);
        }
        private void PlaySfxInhale() {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(CWRSound.ShortCircuit with { Volume = 0.4f, Pitch = -0.35f }, Projectile.Center);
        }
        private void PlaySfxCollapse() {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.5f, Pitch = -0.45f }, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.ShortCircuit with { Volume = 0.35f, Pitch = 0.1f }, Projectile.Center);
        }

        private void ShakeLocalNear(float strength, float maxDist) {
            if (VaultUtils.isServer) {
                return;
            }
            Player lp = Main.LocalPlayer;
            if (lp?.active != true || lp.dead) {
                return;
            }
            float dist = lp.Distance(Projectile.Center);
            if (dist >= maxDist) {
                return;
            }
            lp.CWR().GetScreenShake(strength * (1f - dist / maxDist));
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 开口度：撕开 0→1（带过冲），稳定微呼吸，吸气急收到 0.62，
        /// 吐出瞬间弹回过冲，坍塌交给 collapseT
        /// </summary>
        private float ComputeAperture(int age) {
            if (age < TearEnd) {
                float t = age / (float)TearEnd;
                //ease-out 撕开，末段轻微过冲
                float ease = 1f - MathF.Pow(1f - t, 2.6f);
                return ease * (1f + 0.08f * MathF.Sin(t * MathHelper.Pi));
            }
            if (age < InhaleStart) {
                //稳定 1±0.03 呼吸
                return 1f + 0.03f * MathF.Sin((age - TearEnd) * 0.09f);
            }
            if (age < SpitFrame) {
                //吸气：8 帧急收
                float t = (age - InhaleStart) / (float)(SpitFrame - InhaleStart);
                return MathHelper.Lerp(1f, 0.62f, t * t);
            }
            if (age < CollapseStart) {
                //吐出回弹：0.62 → 过冲 1.3 → 收敛 1
                float t = age - SpitFrame;
                return 1f + 0.42f * MathF.Exp(-t / 7f) * MathF.Cos(t * 0.35f);
            }
            return 1f;
        }

        /// <summary>吐出闪光脉冲，约 30 帧衰减</summary>
        private float ComputeSpitPulse(int age) {
            int diff = age - SpitFrame;
            if (diff < 0 || diff > 40) return 0f;
            return MathF.Exp(-diff / 10f);
        }

        /// <summary>坍塌进度；量化成 8 档，收束是一格一格掉帧消失的</summary>
        private float ComputeCollapseT(int age) {
            if (age < CollapseStart) return 0f;
            float t = MathHelper.Clamp((age - CollapseStart) / (float)(TotalLife - CollapseStart), 0f, 1f);
            return MathF.Floor(t * 8f) / 8f;
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = AgeFrame;
            float aperture = ComputeAperture(age);
            float spitPulse = ComputeSpitPulse(age);
            float collapseT = ComputeCollapseT(age);

            //着色器缺失走程序化兜底
            Effect shader = EffectLoader.TBUGCorruptRift?.Value;
            if (shader == null || VaultAsset.placeholder2?.Value == null
                || CWRAsset.PerlinNoise?.Value == null) {
                DrawGlitchRift(aperture, spitPulse, collapseT);
                return false;
            }

            DrawShaderRift(shader, aperture, spitPulse, collapseT);
            return false;
        }

        /// <summary>quad 半径 = 裂缝×此值，给边缘错位和吸入痕留余量</summary>
        private const float QuadOverRift = 1.35f;

        private void DrawShaderRift(Effect shader, float aperture, float spitPulse, float collapseT) {
            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.PerlinNoise.Value;

            float halfW = BaseHalfWidth * Scale;
            float halfH = BaseHalfHeight * Scale;
            float quadW = halfW * QuadOverRift * 2f;
            float quadH = halfH * QuadOverRift * 2f;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + visualSeed * 12f);
            shader.Parameters["seed"]?.SetValue(visualSeed);
            shader.Parameters["openProgress"]?.SetValue(MathHelper.Clamp(aperture, 0f, 1.4f));
            shader.Parameters["spitPulse"]?.SetValue(MathHelper.Clamp(spitPulse, 0f, 1f));
            shader.Parameters["collapse"]?.SetValue(MathHelper.Clamp(collapseT, 0f, 1f));
            shader.Parameters["riftSize"]?.SetValue(new Vector2(halfW, halfH));
            shader.Parameters["quadSize"]?.SetValue(new Vector2(halfW, halfH) * QuadOverRift);
            shader.Parameters["facing"]?.SetValue(Facing);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            //预乘输出走 AlphaBlend：黑墙是吸光暗体，Additive 下黑色不可见
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 scale = new(quadW / canvas.Width, quadH / canvas.Height);
            sb.Draw(canvas, drawPos, null, Color.White, 0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //冷蓝点光，强度随开口
            Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.38f, 0.72f) * MathHelper.Clamp(aperture, 0f, 1f));
        }

        private static float Hash(float p) {
            p = MathF.Abs(p * 0.1031f % 1f);
            p *= p + 33.33f;
            p *= p + p;
            return MathF.Abs(p % 1f);
        }

        /// <summary>
        /// 程序化黑墙裂缝：水平切片堆出量化轮廓，黑体走 AlphaBlend 压暗背景
        /// （黑色在 Additive 下不可见），绿沿与品红坏块提供故障信号
        /// </summary>
        private void DrawGlitchRift(float aperture, float spitPulse, float collapseT) {
            Texture2D px = VaultAsset.placeholder2.Value;
            if (px == null) {
                return;
            }

            Vector2 center = Projectile.Center - Main.screenPosition;
            float open = MathHelper.Clamp(aperture, 0f, 1.4f);
            float shrink = 1f - collapseT;
            float halfW = BaseHalfWidth * Scale * open * shrink;
            float halfH = BaseHalfHeight * Scale * MathF.Min(1f, open * 1.15f) * (1f - collapseT * collapseT);
            if (halfW < 1f || halfH < 2f) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            Rectangle onePx = new(0, 0, 1, 1);

            //错位每 0.1s 换一次布局，撕裂但不癫痫
            int seedFrame = (int)(Main.GlobalTimeWrappedHourly * 10f);

            const int slices = 14;
            float sliceH = halfH * 2f / slices + 1f;
            for (int i = 0; i < slices; i++) {
                float v = (i + 0.5f) / slices;
                //两端收窄并量化成 4 档，方块化轮廓
                float taper = MathF.Sin(v * MathHelper.Pi);
                taper = MathF.Ceiling(taper * 4f) / 4f;

                float jit = Hash(i * 7.31f + seedFrame * 0.917f + visualSeed * 31f) - 0.5f;
                float w = halfW * 2f * taper * (0.9f + 0.2f * Hash(i * 3.7f + seedFrame));
                float xOff = jit * halfW * 0.55f;
                Vector2 pos = center + new Vector2(xOff, -halfH + v * halfH * 2f);

                //黑墙主体：不透明度接近实心，这是"没被渲染的一块"
                sb.Draw(px, pos, onePx, Color.Black * 0.94f, 0f,
                    new Vector2(0.5f), new Vector2(w, sliceH), SpriteEffects.None, 0f);

                //切片两侧蓝沿，吐出脉冲时增亮
                Color rim = new Color(72, 158, 255) * (0.30f + spitPulse * 0.55f);
                sb.Draw(px, pos - new Vector2(w * 0.5f, 0f), onePx, rim, 0f,
                    new Vector2(0.5f), new Vector2(2f, sliceH), SpriteEffects.None, 0f);
                sb.Draw(px, pos + new Vector2(w * 0.5f, 0f), onePx, rim * 0.6f, 0f,
                    new Vector2(0.5f), new Vector2(2f, sliceH), SpriteEffects.None, 0f);

                //黑底上的报错行：细横线，偶发整行品红反白
                float lineRoll = Hash(i * 11.3f + seedFrame * 1.31f + visualSeed * 7f);
                if (lineRoll > 0.45f && w > 8f) {
                    bool errorLine = lineRoll > 0.9f;
                    Color lineCol = errorLine
                        ? new Color(255, 62, 118) * 0.85f
                        : new Color(60, 140, 235) * 0.5f;
                    float lineW = w * Main.rand.NextFloat(0.25f, 0.8f);
                    float lineX = (Hash(i * 5.9f + seedFrame * 0.77f) - 0.5f) * (w - lineW) * 0.8f;
                    sb.Draw(px, pos + new Vector2(lineX, 0f), onePx, lineCol, 0f,
                        new Vector2(0.5f), new Vector2(lineW, errorLine ? 3f : 1.5f), SpriteEffects.None, 0f);
                }
            }

            //偶发"未初始化显存"色块：品红硬边方块闪一两帧
            if (Hash(seedFrame * 1.71f + visualSeed * 13f) > 0.78f) {
                float bx = (Hash(seedFrame * 2.13f) - 0.5f) * halfW;
                float by = (Hash(seedFrame * 3.57f) - 0.5f) * halfH * 1.4f;
                Vector2 bsize = new(Main.rand.Next(6, 18), Main.rand.Next(4, 10));
                sb.Draw(px, center + new Vector2(bx, by), onePx, new Color(255, 62, 118) * 0.8f, 0f,
                    new Vector2(0.5f), bsize, SpriteEffects.None, 0f);
            }

            //冷蓝点光，强度随开口
            Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.38f, 0.72f) * MathF.Min(open, 1f));
        }

        #endregion

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            //TBUG 被吐出后压在裂缝上
            behindNPCs.Add(index);
        }
    }
}
