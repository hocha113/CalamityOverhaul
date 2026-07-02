using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using CSR = CalamityOverhaul.Content.Demo.CrimsonSlashRenderer;
using SlashDef = CalamityOverhaul.Content.Demo.CrimsonSlashRenderer.SlashDef;

namespace CalamityOverhaul.Content.Demo
{
    /// <summary>
    /// 绯红裂空斩：完整连段演出编排器（跟随玩家，一条时间轴调度四段弧形变奏）<br/>
    /// 设计原则：四段全部是"同一个弧形挥舞"在不同挥砍平面上的投影变形（压扁率/滚转角/挥向交替），
    /// 拒绝平面半圆刀光 —— 环旋段的远半侧真实绘制于玩家身后（空间穿插），近亮远暗<br/>
    /// 节拍（60fps）：<br/>
    /// T0-6   横旋回环 —— 透视压扁的全周回旋，尾部高蒸发读作旋转拖尾，远半侧被角色遮挡<br/>
    /// T10-14 纵斩下劈 —— 正面纵切平面（竖长椭圆），自头顶前压至脚下<br/>
    /// T18-21 反手上撩 —— 同一平面反向，自脚下撩至头顶收势，覆盖正面 ±100°<br/>
    /// T26-29 月牙终结 —— 满弧重月牙正面自上而下重裂，T29 冲击帧：爆点全层 + 世界顿帧 + 白闪<br/>
    /// T31-37 负片收缩暗核；T55-75 余韵光球内爆 + 侵蚀烟化长尾<br/>
    /// 屏幕级只保留短白闪与 Bloom —— 不做震屏/压暗/变焦，防眩晕<br/>
    /// ai[0]=瞄准角(弧度) ai[1]=挥动镜像(±1) ai[2]=尺寸倍率
    /// </summary>
    internal class CrimsonRendSlash : ModProjectile, IPrimitiveDrawable, ICrimsonFarDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //==== 时间轴常量 ====
        private const int HitstopFrames = 4;
        private const int BurstFadeFrames = 16;
        private const int FinisherIndex = 3;
        private const int TotalLifetime = 84;

        private SlashDef[] slashes;
        private int timer;
        private int hitstopHold;
        private bool impactFired;
        private int finisherImpactFrame;
        private Rectangle[] speedLineRects;
        private float[] speedLineOffsets;

        private float AimAngle => Projectile.ai[0];
        private float Flip => Projectile.ai[1] < 0f ? -1f : 1f;
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;
        private Vector2 AimDir => AimAngle.ToRotationVector2();

        private Vector2 ImpactWorldPos {
            get {
                float outer = slashes != null ? slashes[FinisherIndex].HalfX * 0.90f : 200f * SizeMul;
                return Projectile.Center + AimDir * outer * 0.92f;
            }
        }

        /// <summary>
        /// 触发接口：在持有者客户端调用（例如 testItem 的 Shoot/UseItem 内 <c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；整套连段跟随玩家移动
        /// </summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="origin">起手锚点（生成后每帧跟随玩家中心）</param>
        /// <param name="aim">瞄准方向（无需归一化，终结月牙冲击端落在该方向）</param>
        /// <param name="damage">单段伤害（连段可多次命中）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="flip">挥动镜像 ±1</param>
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
            Projectile.localNPCHitCooldown = 10;   //连段各节拍可分别结算
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>四段弧形变奏：同一招式在不同挥砍平面上的投影（压扁/滚转/挥向/尺寸/节奏全部变化）</summary>
        private void BuildSchedule() {
            float s = SizeMul;
            float a = AimAngle;
            float f = Flip;

            slashes = new SlashDef[4];

            //0 横旋回环：透视压扁 0.42 的全周回旋，远半侧入玩家身后层，尾部高蒸发 → 旋转拖尾
            slashes[0] = new SlashDef {
                Birth = 0, SweepFrames = 6, Life = 24, ErodeStart = 8, ErodeFrames = 14,
                ColorShiftDelay = 6, ColorShiftFrames = 12, DamageStart = 2, DamageEnd = 8,
                Mode = 0f, Rot = a - f * 2.95f, Span = 5.90f, Thick = 0.20f,
                HalfX = 175f * s, HalfY = 74f * s, Flip = f,
                Opacity = 0.80f, FrontGlow = 1.7f, OffsetAlongAim = 0f, Seed = 0.13f,
                TailErode = 0.85f, FlashPower = 0.45f, FarDim = 0.52f,
            };

            //1 纵斩下劈：正面纵切平面（沿瞄准方向纵深压扁 → 竖长椭圆），自头顶前压至脚下
            slashes[1] = new SlashDef {
                Birth = 10, SweepFrames = 4, Life = 26, ErodeStart = 8, ErodeFrames = 14,
                ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 1, DamageEnd = 9,
                Mode = 0f, Rot = a + f * 0.15f, Span = 3.60f, Thick = 0.30f,
                HalfX = 150f * s, HalfY = 208f * s, Flip = f,
                Opacity = 0.92f, FrontGlow = 2.2f, OffsetAlongAim = 30f * s, Seed = 0.47f,
                TailErode = 0.50f, FlashPower = 0.8f, FarDim = 0f,
            };

            //2 反手上撩（草图定义）：同一正面纵切平面反向——自脚下回拉撩至头顶收势，
            //  覆盖玩家前方约 ±100°，开口朝向玩家，更大更立，节奏收紧
            slashes[2] = new SlashDef {
                Birth = 18, SweepFrames = 3, Life = 26, ErodeStart = 8, ErodeFrames = 14,
                ColorShiftDelay = 7, ColorShiftFrames = 12, DamageStart = 1, DamageEnd = 8,
                Mode = 0f, Rot = a - f * 0.10f, Span = 3.55f, Thick = 0.33f,
                HalfX = 172f * s, HalfY = 238f * s, Flip = -f,
                Opacity = 0.96f, FrontGlow = 2.4f, OffsetAlongAim = 44f * s, Seed = 0.71f,
                TailErode = 0.45f, FlashPower = 0.9f, FarDim = 0f,
            };

            //3 月牙终结：满弧厚重主月牙（力量核心），正面自上而下的重裂（"裂空"本义），
            //  刃锋过中线时爆点落在瞄准点，随后向下贯穿收势
            slashes[3] = new SlashDef {
                Birth = 26, SweepFrames = 3, Life = 54, ErodeStart = 10, ErodeFrames = 30,
                ColorShiftDelay = 6, ColorShiftFrames = 18, DamageStart = 1, DamageEnd = 12,
                Mode = 0f, Rot = a, Span = 3.55f, Thick = 0.36f,
                HalfX = 245f * s, HalfY = 245f * s, Flip = f,
                Opacity = 1f, FrontGlow = 2.6f, OffsetAlongAim = 0f, Seed = 0.88f,
                TailErode = 0.42f, FlashPower = 1f, FarDim = 0f,
            };

            finisherImpactFrame = slashes[FinisherIndex].Birth + slashes[FinisherIndex].SweepFrames;
        }

        private Vector2 GetCenter(in SlashDef d) => Projectile.Center + AimDir * d.OffsetAlongAim;

        //==================== 时间轴推进 ====================

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                BuildSchedule();
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
            }

            //整套连段跟随玩家
            Player owner = Main.player[Projectile.owner];
            if (owner.active && !owner.dead) {
                Projectile.Center = owner.Center;
            }

            //顿帧保持：终结冲击后世界冻结期间时间轴挂起，姿态定格
            if (impactFired && hitstopHold > 0 && CWRWorld.TimeFrozenTick > 0) {
                hitstopHold--;
                Projectile.timeLeft++;
                PushScreenState();
                return;
            }

            timer++;
            DispatchBeats();

            if (!Main.dedServ) {
                SpawnSweepSparks();
                SpawnEdgeSmoke();
            }

            Lighting.AddLight(ImpactWorldPos, new Vector3(1.0f, 0.25f, 0.18f));
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.12f, 0.10f));

            PushScreenState();
        }

        /// <summary>时间轴节拍分发：起挥哨声逐段升调（accelerando），每段收势一次确认，终结满击</summary>
        private void DispatchBeats() {
            if (slashes == null) {
                return;
            }

            //各段起挥哨声：音高逐段上行，营造连段加速感
            if (timer == slashes[1].Birth) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.38f, Volume = 0.5f }, Projectile.Center);
            }
            else if (timer == slashes[2].Birth) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.55f, Volume = 0.55f }, Projectile.Center);
            }
            else if (timer == slashes[FinisherIndex].Birth) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.95f }, Projectile.Center);
            }

            //各段收势确认：力度递增的小节拍
            TryPing(0, flash: 0.15f, sparks: 5, pitch: 0.65f, hitFlash: false);
            TryPing(1, flash: 0.22f, sparks: 7, pitch: 0.5f, hitFlash: false);
            TryPing(2, flash: 0.30f, sparks: 10, pitch: 0.75f, hitFlash: true);

            if (!impactFired && timer >= finisherImpactFrame) {
                DoFinisherImpact();
            }
        }

        /// <summary>第 index 段扫掠完成瞬间的轻确认（白闪/火花/音效），不顿帧</summary>
        private void TryPing(int index, float flash, int sparks, float pitch, bool hitFlash) {
            ref readonly SlashDef d = ref slashes[index];
            if (timer != d.Birth + d.SweepFrames) {
                return;
            }
            int lt = timer - d.Birth;
            Vector2 pos = CSR.PointAt(in d, GetCenter(in d), 0.94f, lt);

            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = pitch, Volume = 0.38f }, pos);
            CrimsonImpactFX.PushImpact(pos, flash);

            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f) * SizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 130, 90)
                    , Main.rand.NextFloat(0.35f, 0.65f) * SizeMul)
                    ?.Configure(Main.rand.Next(14, 22), affectedByGravity: false);
            }
            if (hitFlash) {
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero
                    , new Color(255, 200, 180), 1.0f * SizeMul);
            }
        }

        /// <summary>终结冲击帧：世界顿帧 + 白闪 + 爆点全层（无震屏/压暗/变焦）</summary>
        private void DoFinisherImpact() {
            impactFired = true;
            hitstopHold = HitstopFrames;
            CWRWorld.TimeFrozenTick = HitstopFrames;

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.35f, Volume = 0.9f }, ImpactWorldPos);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.55f, Volume = 0.45f }, ImpactWorldPos);

            CrimsonImpactFX.PushImpact(ImpactWorldPos, 0.5f);

            if (Main.dedServ) {
                return;
            }

            Vector2 impact = ImpactWorldPos;
            Vector2 aimDir = AimDir;

            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(impact, Vector2.Zero
                , new Color(255, 225, 205), 1.5f * SizeMul);
            for (int i = 0; i < 2; i++) {
                Vector2 off = Main.rand.NextVector2Circular(24f, 24f) * SizeMul;
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(impact + off, off * 0.05f
                    , new Color(255, 140, 110), Main.rand.NextFloat(0.55f, 0.8f) * SizeMul);
            }

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

        /// <summary>屏幕级演出包络：仅 Bloom + 终结脉冲（白闪由节拍触发）</summary>
        private void PushScreenState() {
            float bloom = 0.28f;
            if (impactFired) {
                float bp = MathHelper.Clamp((timer - finisherImpactFrame) / (float)BurstFadeFrames, 0f, 1f);
                bloom += 0.38f * (1f - bp) * (1f - bp);
            }
            if (timer > TotalLifetime - 14) {
                bloom *= (TotalLifetime - timer) / 14f;
            }
            CrimsonImpactFX.PushAmbience(ImpactWorldPos, MathF.Max(bloom, 0f));
        }

        /// <summary>各扫开中的刀光前缘火花</summary>
        private void SpawnSweepSparks() {
            if (slashes == null) {
                return;
            }
            for (int i = 0; i < slashes.Length; i++) {
                ref readonly SlashDef d = ref slashes[i];
                int lt = timer - d.Birth;
                if (lt < 0 || lt > d.SweepFrames + 1) {
                    continue;
                }
                Vector2 center = GetCenter(in d);
                float edgeU = MathHelper.Clamp(CSR.Sweep(in d, lt) * 1.05f, 0.06f, 0.94f);
                Vector2 pos = CSR.PointAt(in d, center, edgeU, lt);
                Vector2 tangent = (CSR.PointAt(in d, center, MathHelper.Clamp(edgeU + 0.03f, 0f, 1f), lt) - pos)
                    .SafeNormalize(AimDir);

                for (int k = 0; k < 2; k++) {
                    Vector2 vel = tangent * Main.rand.NextFloat(4f, 11f) + Main.rand.NextVector2Circular(1.2f, 1.2f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 120, 80)
                        , Main.rand.NextFloat(0.3f, 0.6f) * SizeMul)
                        ?.Configure(Main.rand.Next(10, 18), affectedByGravity: false);
                }
            }
        }

        /// <summary>终结月牙侵蚀期沿外缘生成细碎烟屑，后期停喷</summary>
        private void SpawnEdgeSmoke() {
            if (slashes == null || timer % 2 != 0) {
                return;
            }
            ref readonly SlashDef fin = ref slashes[FinisherIndex];
            int lt = timer - fin.Birth;
            if (lt <= fin.ErodeStart) {
                return;
            }
            float erode = CSR.Erode(in fin, lt);
            if (erode > 0.78f) {
                return;
            }
            Vector2 finCenter = GetCenter(in fin);
            for (int i = 0; i < 2; i++) {
                float uc = Main.rand.NextFloat(0.12f, 0.96f);
                Vector2 mid = CSR.PointAt(in fin, finCenter, uc, lt);
                Vector2 dir = (mid - Projectile.Center).SafeNormalize(AimDir);
                Vector2 pos = mid + dir * fin.HalfX * 0.06f;
                Vector2 vel = dir * Main.rand.NextFloat(0.3f, 1.1f) + Main.rand.NextVector2Circular(0.35f, 0.35f);

                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel
                    , Color.White, Main.rand.NextFloat(0.055f, 0.105f) * SizeMul)
                    ?.Configure(Main.rand.Next(16, 26)
                        , new Color(150, 26, 34), new Color(46, 16, 24)
                        , Main.rand.NextFloat(0.01f, 0.024f));
            }
        }

        //==================== 判定 ====================

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (slashes == null) {
                return false;
            }

            for (int i = 0; i < slashes.Length; i++) {
                ref readonly SlashDef d = ref slashes[i];
                int lt = timer - d.Birth;
                if (lt < d.DamageStart || lt > d.DamageEnd) {
                    continue;
                }
                float sweepU = MathHelper.Clamp(CSR.Sweep(in d, lt) * 1.05f, 0f, 1f);
                Vector2 center = GetCenter(in d);

                //弧/椭圆：折线采样
                const int samples = 15;
                Vector2 prev = Vector2.Zero;
                bool hasPrev = false;
                float thickWorld = d.Thick * d.HalfX;
                for (int k = 0; k < samples; k++) {
                    float uc = 0.05f + 0.90f * (k / (float)(samples - 1));
                    if (uc > sweepU) {
                        break;
                    }
                    Vector2 mid = CSR.PointAt(in d, center, uc, lt);
                    if (hasPrev) {
                        float cp = 0f;
                        if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                            , prev, mid, MathF.Max(28f, thickWorld * 0.8f), ref cp)) {
                            return true;
                        }
                    }
                    prev = mid;
                    hasPrev = true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //受击目标单独短暂顿帧（局部卡肉，不动镜头）
            target.CWR().TimeFrozenTick = HitstopFrames + 2;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.3f, Volume = 0.75f }, target.Center);

            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = AimDir.RotatedByRandom(0.65) * Main.rand.NextFloat(4f, 12f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(target.Center, vel, new Color(255, 96, 60)
                    , Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(16, 28), affectedByGravity: true);
            }
        }

        //==================== 绘制 ====================
        //近半侧/无透视刀光 → EndEntityDraw 弹幕扩展层（覆盖实体）
        //远半侧（FarDim>0 的环旋）→ CrimsonFarLayerRender 玩家绘制前（被角色遮挡）

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || slashes == null) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (CSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                for (int i = 0; i < slashes.Length; i++) {
                    ref readonly SlashDef d = ref slashes[i];
                    int lt = timer - d.Birth;
                    if (lt < 0 || lt >= d.Life) {
                        continue;
                    }
                    //有透视分层的只画近半侧，其余整体
                    CSR.DrawThreeLayers(device, fx, in d, GetCenter(in d), lt, d.FarDim > 0f ? 1f : 0f);
                }
                CSR.EndDraw(device, pb, pr, pd);
            }

            DrawAdditiveLayers();
            DrawCollapseCore();
        }

        void ICrimsonFarDrawable.DrawFarSlashes() {
            if (Main.dedServ || slashes == null) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!CSR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            for (int i = 0; i < slashes.Length; i++) {
                ref readonly SlashDef d = ref slashes[i];
                if (d.FarDim <= 0f) {
                    continue;
                }
                int lt = timer - d.Birth;
                if (lt < 0 || lt >= d.Life) {
                    continue;
                }
                CSR.DrawThreeLayers(device, fx, in d, GetCenter(in d), lt, -1f);
            }
            CSR.EndDraw(device, pb, pr, pd);
        }

        /// <summary>终结爆点 + 余韵光球，自管加色批次</summary>
        private void DrawAdditiveLayers() {
            bool burstActive = impactFired && timer - finisherImpactFrame < BurstFadeFrames;
            bool afterglowActive = impactFired && timer - finisherImpactFrame is >= 26 and < 46;
            if (!burstActive && !afterglowActive) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (burstActive) {
                DrawImpactBurst(sb);
            }

            //余韵：暗紫红光球内爆收束（参考序列尾帧）
            if (afterglowActive && DemoAssets.StarFlare01?.Value is Texture2D orb) {
                float t = (timer - finisherImpactFrame - 26) / 20f;
                float oA = MathF.Sin(t * MathF.PI) * 0.42f;
                float oS = MathHelper.Lerp(0.9f, 0.18f, CSR.EaseOutCubic(t)) * SizeMul;
                Color oc = Color.Lerp(new Color(210, 70, 130), new Color(70, 24, 66), t);
                sb.Draw(orb, ImpactWorldPos - Main.screenPosition, null, oc * oA
                    , t * 2.4f, orb.Size() * 0.5f, oS, SpriteEffects.None, 0);
            }

            sb.End();
        }

        /// <summary>终结爆点全 layer：星爆核心/放射尖刺/十字闪/扩散环/撕裂形/速度线</summary>
        private void DrawImpactBurst(SpriteBatch sb) {
            float bt = MathHelper.Clamp(timer - finisherImpactFrame, 0f, BurstFadeFrames);
            float bp = bt / BurstFadeFrames;
            if (bp >= 1f) {
                return;
            }

            Vector2 impact = ImpactWorldPos - Main.screenPosition;
            Vector2 aimDir = AimDir;
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
            if (bt < 7f && DemoAssets.HitJagged01?.Value is Texture2D jag) {
                float jA = MathF.Pow(1f - bt / 7f, 2f) * 0.5f;
                sb.Draw(jag, impact, null, new Color(255, 80, 55) * jA, AimAngle + MathHelper.Pi
                    , jag.Size() * 0.5f, (1.8f + easeOut * 0.6f) * SizeMul, SpriteEffects.None, 0);
            }

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

        /// <summary>负片收缩：爆闪第2~8帧，暗核压在加色星爆之上，只留红边<br/>
        /// 注意：AlphaBlend 压暗必须用 alpha 通道承载形状的贴图（SmokeSheet01），
        /// 黑底不透明的亮度型贴图会把整个 quad 糊成暗色方框</summary>
        private void DrawCollapseCore() {
            float bt = timer - finisherImpactFrame;
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
