using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Demo
{
    /// <summary>
    /// 绯红裂空斩：导演式斩击演出弹幕（月牙非武器轨迹产物，而是按帧编排的动画实体）<br/>
    /// 时间轴（60fps）：2帧起挥 → 3帧内月牙全尺寸扫开 → 冲击帧（爆闪/顿帧/变焦punch/震屏）→
    /// 负片收缩 → 长尾侵蚀消散（燃边+烟化）<br/>
    /// 屏幕级配合（压暗聚焦/白闪/Bloom）由 <see cref="CrimsonImpactFX"/> 承接<br/>
    /// ai[0]=瞄准角(弧度) ai[1]=挥动镜像(±1) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonRendSlash : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //==== 时间轴（帧） ====
        private const int SweepEndFrame = 4;      //月牙完全张开 = 冲击帧
        private const int HitstopFrames = 4;      //世界顿帧时长
        private const int BurstFadeFrames = 16;   //爆点层衰减窗口
        private const int ErodeStartFrame = 10;   //侵蚀起点
        private const int DamageEndFrame = 14;    //伤害判定窗口终点
        private const int TotalLifetime = 48;

        //==== 几何 ====
        private const float BaseOuterRadius = 200f;  //世界外缘半径（scale=1）
        private const float ArcSpan = 3.55f;         //弧跨度 ~203°
        private const float ThickRatio = 0.34f;      //月牙厚度（shader p 空间）

        //==== 调色（与参考帧对齐：白热核心/亮绯红/深红/暗酒红） ====
        private static readonly Vector3 ColHot = new(1.60f, 1.32f, 1.08f);
        private static readonly Vector3 ColBright = new(1.30f, 0.16f, 0.10f);
        private static readonly Vector3 ColDeep = new(0.62f, 0.05f, 0.07f);
        private static readonly Vector3 ColDark = new(0.16f, 0.015f, 0.035f);

        private int timer;
        private int hitstopHold;
        private bool impactFired;
        //速度线随机截条缓存（纯客户端视觉，首帧生成）
        private Rectangle[] speedLineRects;
        private float[] speedLineOffsets;

        private float AimAngle => Projectile.ai[0];
        private float Flip => Projectile.ai[1] < 0f ? -1f : 1f;
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;

        private float OuterRadius => BaseOuterRadius * SizeMul;
        //shader 里外缘落在 p≈0.90，量子化出四边形半尺寸
        private float QuadHalfSize => OuterRadius / 0.90f;
        private float QuadRotation => AimAngle - Flip * ArcSpan * 0.5f;
        private Vector2 ImpactWorldPos => Projectile.Center + AimAngle.ToRotationVector2() * OuterRadius * 0.92f;

        private float SweepProgress {
            get {
                float linear = MathHelper.Clamp((timer - 1) / (float)(SweepEndFrame - 1), 0f, 1f);
                return 1f - MathF.Pow(1f - linear, 3f);   //ease-out cubic：瞬间到位
            }
        }

        private float Erode {
            get {
                float t = MathHelper.Clamp((timer - ErodeStartFrame) / (float)(TotalLifetime - 6 - ErodeStartFrame), 0f, 1f);
                return t * t * (3f - 2f * t);
            }
        }

        private float ColorShift => MathHelper.Clamp((timer - SweepEndFrame - 2) / 20f, 0f, 1f);

        private float MasterOpacity => 1f - MathHelper.Clamp((timer - (TotalLifetime - 9)) / 8f, 0f, 1f);

        /// <summary>
        /// 触发接口：在持有者客户端调用（例如 testItem 的 Shoot/UseItem 内 <c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="origin">弧心（通常 player.Center）</param>
        /// <param name="aim">瞄准方向（无需归一化，冲击端落在该方向）</param>
        /// <param name="damage">伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="flip">挥动镜像 ±1（决定从哪一侧扫向瞄准点）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 origin, Vector2 aim, int damage, float knockback,
            float scale = 1f, int flip = 1, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_CrimsonRendSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            return Projectile.NewProjectileDirect(source, origin, Vector2.Zero
                , ModContent.ProjectileType<CrimsonRendSlash>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai1: flip, ai2: scale);
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLifetime + HitstopFrames + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;   //单次挥砍每目标只结算一次
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.15f, Volume = 0.9f }, Projectile.Center);
            }

            //顿帧保持：冲击后世界冻结期间时间轴挂起，姿态定格
            if (impactFired && hitstopHold > 0 && CWRWorld.TimeFrozenTick > 0) {
                hitstopHold--;
                Projectile.timeLeft++;
                PushScreenState();
                return;
            }

            timer++;

            if (!impactFired && timer >= SweepEndFrame) {
                DoImpact();
            }

            //侵蚀期外缘燃尽烟化
            if (!Main.dedServ && timer > ErodeStartFrame && Erode < 0.92f) {
                SpawnEdgeSmoke();
            }

            //扫掠期前缘火花
            if (!Main.dedServ && timer <= SweepEndFrame + 1) {
                SpawnSweepSparks();
            }

            Lighting.AddLight(ImpactWorldPos, new Vector3(1.1f, 0.28f, 0.20f) * MasterOpacity);
            Lighting.AddLight(Projectile.Center + AimAngle.ToRotationVector2() * OuterRadius * 0.5f
                , new Vector3(0.8f, 0.16f, 0.12f) * MasterOpacity);

            PushScreenState();
        }

        /// <summary>冲击帧：世界顿帧 + 白闪 + 变焦 punch + 定向震屏 + 爆点粒子</summary>
        private void DoImpact() {
            impactFired = true;
            hitstopHold = HitstopFrames;
            CWRWorld.TimeFrozenTick = HitstopFrames;

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.35f, Volume = 0.9f }, ImpactWorldPos);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.55f, Volume = 0.45f }, ImpactWorldPos);

            CrimsonImpactFX.PushImpact(ImpactWorldPos, 0.58f, 0.045f * MathF.Min(SizeMul, 1.6f));

            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier punch = new(ImpactWorldPos, AimAngle.ToRotationVector2()
                    , 13f, 9f, 18, 1600f, FullName);
                Main.instance.CameraModifiers.Add(punch);
            }

            if (Main.dedServ) {
                return;
            }

            Vector2 impact = ImpactWorldPos;
            Vector2 aimDir = AimAngle.ToRotationVector2();

            //手绘火花序列帧：一大两小
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(impact, Vector2.Zero
                , new Color(255, 225, 205), 1.5f * SizeMul);
            for (int i = 0; i < 2; i++) {
                Vector2 off = Main.rand.NextVector2Circular(24f, 24f) * SizeMul;
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(impact + off, off * 0.05f
                    , new Color(255, 140, 110), Main.rand.NextFloat(0.55f, 0.8f) * SizeMul);
            }

            //弹道火花：锥形喷射 + 少量逆向溅射
            for (int i = 0; i < 20; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.78) * Main.rand.NextFloat(6f, 21f) * SizeMul;
                Color c = Main.rand.NextBool(3) ? new Color(255, 236, 210) : new Color(255, 92, 58);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(impact, vel, c
                    , Main.rand.NextFloat(0.5f, 1.05f) * SizeMul)
                    ?.Configure(Main.rand.Next(22, 40), affectedByGravity: true);
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = (-aimDir).RotatedByRandom(1.1) * Main.rand.NextFloat(3f, 8f) * SizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(impact, vel, new Color(255, 70, 46)
                    , Main.rand.NextFloat(0.35f, 0.6f) * SizeMul)
                    ?.Configure(Main.rand.Next(16, 26), affectedByGravity: false);
            }
        }

        /// <summary>屏幕级演出包络：压暗快进慢出，Bloom 随爆点脉冲</summary>
        private void PushScreenState() {
            float dimIn = MathHelper.Clamp((timer - 1) / 4f, 0f, 1f);
            float dimOut = 1f - MathHelper.Clamp((timer - 20) / 14f, 0f, 1f);
            float dim = 0.66f * dimIn * dimOut;

            float bloom = 0.30f * MasterOpacity;
            if (impactFired) {
                float bp = MathHelper.Clamp((timer - SweepEndFrame) / (float)BurstFadeFrames, 0f, 1f);
                bloom += 0.38f * (1f - bp) * (1f - bp);
            }

            CrimsonImpactFX.PushAmbience(ImpactWorldPos, dim, bloom);
        }

        /// <summary>侵蚀燃尽处沿外缘生成烟雾：小尺寸高数量的细碎烟屑贴着外缘，
        /// 不再是可辨认的独立大烟团；侵蚀后期停喷，避免刀光已散烟还孤悬</summary>
        private void SpawnEdgeSmoke() {
            if (timer % 2 != 0 || Erode > 0.78f) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                float uc = Main.rand.NextFloat(0.12f, 0.96f);
                float worldAngle = QuadRotation + Flip * (uc - 0.5f) * ArcSpan;
                Vector2 dir = worldAngle.ToRotationVector2();
                Vector2 pos = Projectile.Center + dir * OuterRadius * Main.rand.NextFloat(0.92f, 1.02f);
                Vector2 vel = dir * Main.rand.NextFloat(0.3f, 1.1f) + Main.rand.NextVector2Circular(0.35f, 0.35f);

                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel
                    , Color.White, Main.rand.NextFloat(0.055f, 0.105f) * SizeMul)
                    ?.Configure(Main.rand.Next(16, 26)
                        , new Color(150, 26, 34), new Color(46, 16, 24)
                        , Main.rand.NextFloat(0.01f, 0.024f));
            }
        }

        /// <summary>扫掠前缘的细碎火花，跟随揭开边缘飞出</summary>
        private void SpawnSweepSparks() {
            float edgeU = MathHelper.Clamp(SweepProgress * 1.1f - 0.04f, 0f, 1f);
            float worldAngle = QuadRotation + Flip * (edgeU - 0.5f) * ArcSpan;
            Vector2 dir = worldAngle.ToRotationVector2();
            Vector2 tangent = dir.RotatedBy(Flip * MathHelper.PiOver2);
            Vector2 pos = Projectile.Center + dir * OuterRadius * Main.rand.NextFloat(0.72f, 0.95f);

            for (int i = 0; i < 3; i++) {
                Vector2 vel = tangent * Main.rand.NextFloat(4f, 12f) + dir * Main.rand.NextFloat(-1f, 3f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                    , Main.rand.NextFloat(0.3f, 0.65f) * SizeMul)
                    ?.Configure(Main.rand.Next(10, 20), affectedByGravity: false);
            }
        }

        //==================== 判定 ====================

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (timer < 1 || timer > DamageEndFrame) {
                return false;
            }

            float sweepU = MathHelper.Clamp(SweepProgress * 1.1f - 0.02f, 0f, 1f);
            float thickWorld = ThickRatio * QuadHalfSize;

            const int samples = 15;
            Vector2 prev = Vector2.Zero;
            bool hasPrev = false;
            for (int i = 0; i < samples; i++) {
                float uc = 0.05f + (0.95f - 0.05f) * (i / (float)(samples - 1));
                if (uc > sweepU) {
                    break;
                }
                //厚度包络与 shader 一致：峰值偏收笔端
                float env = MathF.Sin(MathF.Pow(uc, 1.85f) * MathF.PI);
                float w = thickWorld * MathF.Pow(MathF.Max(env, 0.0001f), 0.72f);
                if (w < 8f) {
                    continue;
                }

                float worldAngle = QuadRotation + Flip * (uc - 0.5f) * ArcSpan;
                Vector2 mid = Projectile.Center + worldAngle.ToRotationVector2() * (OuterRadius - w * 0.5f);

                if (hasPrev) {
                    float collisionPoint = 0f;
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                        , prev, mid, MathF.Max(30f, w), ref collisionPoint)) {
                        return true;
                    }
                }
                prev = mid;
                hasPrev = true;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //受击目标单独多顿几帧，强化"斩进肉里"的确认感
            target.CWR().TimeFrozenTick = HitstopFrames + 3;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.3f, Volume = 0.8f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            Vector2 aimDir = AimAngle.ToRotationVector2();
            for (int i = 0; i < 10; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.65) * Main.rand.NextFloat(4f, 13f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center, vel, new Color(255, 96, 60)
                    , Main.rand.NextFloat(0.4f, 0.85f))
                    ?.Configure(Main.rand.Next(18, 30), affectedByGravity: true);
            }
        }

        //==================== 绘制（EndEntityDraw 弹幕扩展层，覆盖于所有实体之上） ====================

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || MasterOpacity <= 0.01f) {
                return;
            }

            DrawCrescent();
            DrawAdditiveLayers();
            DrawCollapseCore();
        }

        /// <summary>月牙主体：四边形 + DemoCrimsonSlash 双 pass（外圈残影 + 主体），预乘 AlphaBlend</summary>
        private void DrawCrescent() {
            Effect fx = EffectLoader.DemoCrimsonSlash?.Value;
            Texture2D brush = DemoAssets.SlashBrush01?.Value;
            Texture2D noise = DemoAssets.NoiseSoft01?.Value;
            if (fx == null || brush == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;

            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSweep"]?.SetValue(SweepProgress);
            fx.Parameters["uColorShift"]?.SetValue(ColorShift);
            fx.Parameters["uFlip"]?.SetValue(Flip);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.173f % 1f);
            fx.Parameters["uArcSpan"]?.SetValue(ArcSpan);
            fx.Parameters["uThick"]?.SetValue(ThickRatio);
            fx.Parameters["uColHot"]?.SetValue(ColHot);
            fx.Parameters["uColBright"]?.SetValue(ColBright);
            fx.Parameters["uColDeep"]?.SetValue(ColDeep);
            fx.Parameters["uColDark"]?.SetValue(ColDark);
            fx.Parameters["uBrushTex"]?.SetValue(brush);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            float frontGlow = timer <= SweepEndFrame + 2
                ? 2.4f
                : 2.4f * MathF.Max(0f, 1f - (timer - SweepEndFrame - 2) / 5f);

            //外圈残影：更大更淡更碎，垫出厚度层次
            fx.Parameters["uOpacity"]?.SetValue(MasterOpacity * 0.32f);
            fx.Parameters["uErode"]?.SetValue(MathHelper.Clamp(Erode + 0.20f, 0f, 1f));
            fx.Parameters["uFrontGlow"]?.SetValue(frontGlow * 0.4f);
            DrawCrescentQuad(device, fx, QuadHalfSize * 1.07f);

            //主体
            fx.Parameters["uOpacity"]?.SetValue(MasterOpacity);
            fx.Parameters["uErode"]?.SetValue(Erode);
            fx.Parameters["uFrontGlow"]?.SetValue(frontGlow);
            DrawCrescentQuad(device, fx, QuadHalfSize);

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        private void DrawCrescentQuad(GraphicsDevice device, Effect fx, float halfSize) {
            Vector2 center = Projectile.Center;
            float rot = QuadRotation;
            Vector2 axisX = rot.ToRotationVector2();
            Vector2 axisY = axisX.RotatedBy(MathHelper.PiOver2);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - axisX * halfSize - axisY * halfSize).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + axisX * halfSize - axisY * halfSize).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - axisX * halfSize + axisY * halfSize).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + axisX * halfSize + axisY * halfSize).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        /// <summary>爆点/速度线/扩散环等加色层，自管 LinearClamp 批次<br/>
        /// 扫掠前缘光带由月牙 shader 的 uFrontGlow 承担，不再叠原始贴图精灵（raw 灰度图直出显劣质）</summary>
        private void DrawAdditiveLayers() {
            if (!impactFired) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawImpactBurst(sb);
            sb.End();
        }

        /// <summary>冲击爆点全layer：星爆核心/放射尖刺/十字闪/扩散环/撕裂形/速度线</summary>
        private void DrawImpactBurst(SpriteBatch sb) {
            float bt = MathHelper.Clamp(timer - SweepEndFrame, 0f, BurstFadeFrames);
            float bp = bt / BurstFadeFrames;
            if (bp >= 1f) {
                return;
            }

            Vector2 impact = ImpactWorldPos - Main.screenPosition;
            Vector2 aimDir = AimAngle.ToRotationVector2();
            float inv = 1f - bp;
            float easeOut = 1f - MathF.Pow(inv, 3f);
            float seedRot = Projectile.whoAmI * 1.37f;

            //白热星爆核心：前3帧过曝，随后急剧收缩
            if (DemoAssets.StarFlare02?.Value is Texture2D flare) {
                float coreA = MathF.Pow(inv, 2.0f);
                float coreS = (1.0f + easeOut * 0.75f) * SizeMul;
                sb.Draw(flare, impact, null, Color.White * coreA, seedRot
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
                sb.Draw(flare, impact, null, new Color(255, 120, 80) * (coreA * 0.55f), -seedRot * 0.6f
                    , flare.Size() * 0.5f, coreS * 1.3f, SpriteEffects.None, 0);
            }

            //放射尖刺
            if (DemoAssets.RayBurst01?.Value is Texture2D rays) {
                float rayA = MathF.Pow(inv, 1.8f);
                float rayS = (1.25f + easeOut * 1.2f) * SizeMul;
                sb.Draw(rays, impact, null, new Color(255, 190, 160) * rayA, seedRot * 0.4f
                    , rays.Size() * 0.5f, rayS, SpriteEffects.None, 0);
            }

            //十字长闪沿瞄准方向
            if (DemoAssets.RayCross01?.Value is Texture2D cross) {
                float cA = MathF.Pow(inv, 2.4f);
                sb.Draw(cross, impact, null, new Color(255, 230, 215) * cA, AimAngle
                    , cross.Size() * 0.5f, new Vector2(2.5f, 1.15f) * easeOut * SizeMul, SpriteEffects.None, 0);
            }

            //扩散环
            if (DemoAssets.Ring01?.Value is Texture2D ring) {
                float ringS = (0.4f + easeOut * 2.2f) * SizeMul;
                float ringA = MathF.Pow(inv, 2.5f) * 0.6f;
                sb.Draw(ring, impact, null, new Color(255, 90, 60) * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            //手绘撕裂形：沿瞄准方向一大一小，短命
            if (bt < 9f && DemoAssets.TearSpread01?.Value is Texture2D tear) {
                float tA = MathF.Pow(1f - bt / 9f, 1.8f) * 0.85f;
                sb.Draw(tear, impact, null, new Color(255, 150, 120) * tA, AimAngle
                    , tear.Size() * 0.5f, (1.5f + easeOut * 0.55f) * SizeMul, SpriteEffects.None, 0);
                sb.Draw(tear, impact, null, new Color(255, 60, 40) * (tA * 0.75f), AimAngle + 0.35f * Flip
                    , tear.Size() * 0.5f, (1.0f + easeOut * 0.4f) * SizeMul
                    , SpriteEffects.FlipVertically, 0);
            }

            //锯齿冲击形垫底
            //if (bt < 7f && DemoAssets.HitJagged01?.Value is Texture2D jag) {
            //    float jA = MathF.Pow(1f - bt / 7f, 2f) * 0.5f;
            //    sb.Draw(jag, impact, null, new Color(255, 80, 55) * jA, AimAngle + MathHelper.Pi
            //        , jag.Size() * 0.5f, (1.8f + easeOut * 0.6f) * SizeMul, SpriteEffects.None, 0);
            //}

            //速度线：随机截条从冲击点向后扫出
            if (DemoAssets.SpeedLines01?.Value is Texture2D lines) {
                EnsureSpeedLineRects();
                float lA = MathF.Pow(inv, 1.6f) * 0.5f;
                for (int i = 0; i < speedLineRects.Length; i++) {
                    Rectangle src = speedLineRects[i];
                    float off = speedLineOffsets[i];
                    Vector2 pos = impact - aimDir * (40f + off * 70f + easeOut * 40f) * SizeMul
                        + aimDir.RotatedBy(MathHelper.PiOver2) * (off - 0.5f) * 110f * SizeMul;
                    sb.Draw(lines, pos, src, new Color(255, 170, 140) * lA, AimAngle
                        , src.Size() * 0.5f, new Vector2(0.40f + easeOut * 0.30f, 0.42f) * SizeMul
                        , SpriteEffects.None, 0);
                }
            }
        }

        private void EnsureSpeedLineRects() {
            if (speedLineRects != null) {
                return;
            }
            speedLineRects = new Rectangle[3];
            speedLineOffsets = new float[3];
            for (int i = 0; i < 3; i++) {
                speedLineRects[i] = new Rectangle(0, Main.rand.Next(0, 1024 - 96), 1024, 96);
                speedLineOffsets[i] = Main.rand.NextFloat();
            }
        }

        /// <summary>负片收缩：爆闪第2~8帧，暗核压在加色星爆之上，只留红边 —— 参考帧2的"反相收缩"<br/>
        /// 注意：AlphaBlend 压暗必须用 alpha 通道承载形状的贴图；黑底不透明的亮度型贴图
        /// （如 StarGlow01）会把整个 quad 连背景一起糊黑，呈现为暗色方框 bug</summary>
        private void DrawCollapseCore() {
            float bt = timer - SweepEndFrame;
            if (!impactFired || bt < 2f || bt > 8f) {
                return;
            }
            Texture2D cloud = DemoAssets.SmokeSheet01?.Value;
            if (cloud == null) {
                return;
            }

            float t = (bt - 2f) / 6f;   //0..1
            //512px 帧：峰值 ~0.36 倍 ≈ 185px 暗核，收缩至 ~60px
            float coreS = MathHelper.Lerp(0.36f, 0.12f, t * t) * SizeMul;
            float coreA = MathF.Sin(t * MathF.PI) * 0.78f;
            Rectangle frame = new((Projectile.whoAmI % 2) * 512, (Projectile.whoAmI / 2 % 2) * 512, 512, 512);

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(cloud, ImpactWorldPos - Main.screenPosition, frame
                , new Color(16, 4, 9) * coreA, Projectile.whoAmI * 1.37f
                , frame.Size() * 0.5f, coreS, SpriteEffects.None, 0);
            sb.End();
        }
    }
}
