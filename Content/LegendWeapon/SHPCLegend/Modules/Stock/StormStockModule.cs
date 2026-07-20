using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>风暴枪托：持续开火积聚周身风暴场，气流吹偏敌弹，计量充满天降落雷</summary>
    internal sealed class StormStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //风暴蓝靛
        public override Color TintColor => new(85, 160, 230);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.1f;
            ctx.ManaCostMul += 0.2f;
        }

        public override void OnPlayerUpdate(Player player) {
            if (player.whoAmI != Main.myPlayer) return;
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            int fieldType = ModContent.ProjectileType<SHPCStormFieldProj>();
            if (player.ownedProjectileCounts[fieldType] >= 1) return;
            Projectile.NewProjectile(player.GetSource_FromThis(),
                player.Center, Vector2.Zero, fieldType, 0, 0f, player.whoAmI);
        }
    }

    /// <summary>
    /// 风暴场领域：跟随玩家的环形气旋，开火充能、停火消散
    /// 场内敌弹被气流持续吹偏（只转向不删弹），落雷计量充满时召唤 <see cref="SHPCStormBoltProj"/>
    /// SHPCModStormField.fx
    /// </summary>
    internal sealed class SHPCStormFieldProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        #region 可调参数

        /// <summary>持续开火充满强度所需帧数</summary>
        private const float FillFrames = 210f;
        /// <summary>停火后衰空强度所需帧数</summary>
        private const float DrainFrames = 150f;
        /// <summary>停火到开始衰减的宽限帧数</summary>
        private const int GraceFrames = 45;
        /// <summary>零强度/满强度对应的领域半径（像素）</summary>
        private const float MinRadius = 170f;
        private const float MaxRadius = 330f;
        /// <summary>满强度下场心处敌弹每帧最大偏转弧度</summary>
        private const float MaxTurnRad = 0.034f;
        /// <summary>敌弹径向外推速度增量（×强度，像素/帧）</summary>
        private const float RadialPush = 0.09f;
        /// <summary>满强度下落雷计量充满所需帧数</summary>
        private const float GaugeFillFrames = 140f;
        /// <summary>低于此强度落雷计量不推进</summary>
        private const float GaugeMinIntensity = 0.25f;
        /// <summary>计量充满但无可视目标时保留的计量比例，待机重试而非整管作废</summary>
        private const float GaugeKeepOnNoTarget = 0.8f;
        /// <summary>吹偏风纹粒子每帧全局配额，防弹幕地狱下刷屏</summary>
        private const int MaxWindTrailsPerFrame = 3;
        /// <summary>落雷伤害 = 武器伤害 × 此倍率</summary>
        private const float BoltDamageMul = 2.2f;
        private const float BoltKnockback = 4f;
        /// <summary>三档强度阈值，升档时播报</summary>
        private static readonly float[] TierThresholds = [0.30f, 0.62f, 0.94f];

        #endregion

        //风暴配色：暗雨蓝底、风暴主蓝、电光青白
        private static readonly Color StormDeep = new(24, 44, 82);
        private static readonly Color StormMain = new(70, 150, 220);
        private static readonly Color StormArc = new(170, 230, 255);

        /// <summary>风暴强度 0~1，各端按同步的开火状态独立推进</summary>
        private float intensity;
        /// <summary>落雷计量 0~1，充满即劈雷</summary>
        private float boltGauge;
        private int graceTimer;
        /// <summary>当前强度档位 0~3，升档演出用</summary>
        private int tier;
        /// <summary>自管理视觉时间，强度越高气旋转越快</summary>
        private float visualTime;

        private float CurrentRadius => MathHelper.Lerp(MinRadius, MaxRadius, intensity);

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead
                || owner.HeldItem == null || owner.HeldItem.type != SHPCOverride.ID
                || !SHPCModificationSystem.HasModule<StormStockModule>(owner)) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 30;
            Projectile.Center = owner.Center;

            float timeScale = TimeGear.TimeScale;

            //充能：任意开火行为（左键光束/激光/右键蓄力）都在喂养风暴
            //itemAnimation 状态跨端同步，各端强度推进输入一致
            if (owner.ItemAnimationActive) {
                intensity = MathF.Min(intensity + timeScale / FillFrames, 1f);
                graceTimer = GraceFrames;
            }
            else if (graceTimer > 0) {
                graceTimer--;
            }
            else {
                intensity = MathF.Max(intensity - timeScale / DrainFrames, 0f);
            }

            //升档播报：跨过阈值瞬间给出音效与扩散环，降档静默
            int newTier = 0;
            for (int i = 0; i < TierThresholds.Length; i++) {
                if (intensity >= TierThresholds[i]) newTier = i + 1;
            }
            if (newTier > tier && Main.netMode != NetmodeID.Server) {
                TierUpFx(newTier);
            }
            tier = newTier;

            //气旋转速随强度提升
            visualTime += (0.010f + intensity * 0.014f) * timeScale;

            if (intensity < 0.02f) {
                boltGauge = 0f;
                return;
            }

            DeflectHostiles(timeScale);

            //落雷计量：强度是唯一燃料，强度越高雷越频繁
            if (intensity >= GaugeMinIntensity) {
                boltGauge += intensity * timeScale / GaugeFillFrames;
                if (boltGauge >= 1f) {
                    //可视目标检查是确定性的（NPC 位置+tile 输入），各端跑出一致的计量结果；
                    //随机落点参数只在所有者端 roll，弹幕生成自动同步
                    NPC target = FindStrikeTarget(owner);
                    if (target != null) {
                        boltGauge = 0f;
                        if (Projectile.owner == Main.myPlayer) {
                            SummonBolt(owner, target);
                        }
                    }
                    else {
                        //场内没有劈得到的敌人：保留大部分计量待机重试，不整管作废
                        boltGauge = GaugeKeepOnNoTarget;
                    }
                }
            }
            else {
                boltGauge = MathF.Max(boltGauge - timeScale / 90f, 0f);
            }

            Lighting.AddLight(Projectile.Center, StormMain.ToVector3() * 0.55f * intensity);

            if (Main.netMode != NetmodeID.Server) {
                SpawnFieldParticles();
                //计量临界预兆：边缘电光噼啪，提示落雷将至
                if (boltGauge > 0.85f && Main.rand.NextBool(9)) {
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.18f, Pitch = 0.7f }, Projectile.Center);
                    Vector2 edgePos = Projectile.Center
                        + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * CurrentRadius * Main.rand.NextFloat(0.85f, 1f);
                    PRTLoader.NewParticle<PRT_Spark>(edgePos, Main.rand.NextVector2CircularEdge(2.5f, 2.5f),
                        StormArc, Main.rand.NextFloat(0.5f, 1f)).Configure(false, Main.rand.Next(8, 14));
                }
            }
        }

        /// <summary>
        /// 场内敌弹吹偏：只旋转速度方向不改模长，附加轻微径向外推
        /// 无随机、输入各端一致，所有端（含服务器）同跑保证弹道一致
        /// </summary>
        private void DeflectHostiles(float timeScale) {
            float radius = CurrentRadius;
            float radiusSq = radius * radius;
            //风纹粒子全帧配额：弹幕地狱同屏大量敌弹时不至于刷屏
            int windTrailBudget = MaxWindTrailsPerFrame;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile hostile = Main.projectile[i];
                if (!hostile.active || !hostile.hostile || hostile.friendly || hostile.damage <= 0) continue;
                Vector2 rel = hostile.Center - Projectile.Center;
                float distSq = rel.LengthSquared();
                if (distSq > radiusSq || distSq < 64f) continue;
                if (hostile.velocity.LengthSquared() < 0.01f) continue;

                float dist = MathF.Sqrt(distSq);
                //场心气流最强、边缘渐弱
                float falloff = 0.35f + 0.65f * (1f - dist / radius);
                float turn = MaxTurnRad * intensity * falloff * timeScale;
                //统一顺时针卷入气旋（与着色器旋转方向一致），速度方向融合径向外推后回归原模长
                float speed = hostile.velocity.Length();
                Vector2 newDir = (hostile.velocity.RotatedBy(turn)
                    + rel / dist * RadialPush * intensity).SafeNormalize(Vector2.UnitX);
                hostile.velocity = newDir * speed;

                //低频风纹标记：让"弹被吹弯了"肉眼可读
                if (windTrailBudget > 0 && Main.netMode != NetmodeID.Server && Main.rand.NextBool(7)) {
                    windTrailBudget--;
                    PRTLoader.NewParticle<PRT_Spark>(hostile.Center,
                        hostile.velocity * 0.2f + Main.rand.NextVector2Circular(1f, 1f),
                        StormArc * 0.8f, Main.rand.NextFloat(0.35f, 0.7f)).Configure(false, Main.rand.Next(6, 12));
                }
            }
        }

        /// <summary>
        /// 落雷目标：场内最近且与玩家有视线连通的敌人（不隔墙劈、无超界宽容，与文案一致）
        /// FindClosestNPC 的 ignoreTiles=false 分支即 Collision.CanHit 过滤
        /// </summary>
        private NPC FindStrikeTarget(Player owner)
            => owner.Center.FindClosestNPC(CurrentRadius, ignoreTiles: false);

        /// <summary>落雷召唤：劈向指定目标，随机视觉参数经 ai 槽同步</summary>
        private void SummonBolt(Player owner, NPC target) {
            Item held = owner.HeldItem;
            int weaponDmg = held != null && held.type == SHPCOverride.ID
                ? owner.GetWeaponDamage(held) : 30;
            int dmg = Math.Max((int)(weaponDmg * BoltDamageMul), 1);

            Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCStormBoltProj>(),
                dmg, BoltKnockback, Projectile.owner,
                ai0: Main.rand.NextFloat(100f),
                ai1: Main.rand.NextFloat(-130f, 130f));
        }

        private void TierUpFx(int newTier) {
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.35f, Pitch = -0.2f + newTier * 0.25f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                StormMain with { A = 0 }, 0.05f).Configure(0.05f, CurrentRadius / 380f, 20);
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * CurrentRadius * 0.6f;
                //切向速度顺气旋方向
                Vector2 vel = (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(3f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, StormArc,
                    Main.rand.NextFloat(0.6f, 1.1f)).Configure(false, Main.rand.Next(12, 22));
            }
        }

        /// <summary>环流风丝与斜雨：数量随强度增长，速度沿气旋切向</summary>
        private void SpawnFieldParticles() {
            if (intensity < 0.15f) return;
            float radius = CurrentRadius;
            //风丝：环带内切向奔流
            if (Main.rand.NextBool(2)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float r = radius * Main.rand.NextFloat(0.35f, 1f);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
                //顺时针切向（与吹偏、着色器同向）
                Vector2 vel = (ang + MathHelper.PiOver2).ToRotationVector2() * (2f + intensity * 4f + Main.rand.NextFloat(2f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(StormMain, StormArc, Main.rand.NextFloat()) * (0.4f + intensity * 0.5f),
                    Main.rand.NextFloat(0.4f, 0.9f)).Configure(false, Main.rand.Next(14, 26));
            }
            //斜雨：高强度时上半场落下被风吹斜的雨丝
            if (intensity > 0.5f && Main.rand.NextBool(3)) {
                float x = Main.rand.NextFloat(-0.8f, 0.8f);
                Vector2 pos = Projectile.Center + new Vector2(x * radius, -radius * Main.rand.NextFloat(0.4f, 0.9f));
                Vector2 vel = new(3.5f * intensity, 5f + Main.rand.NextFloat(2f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, StormMain * 0.55f,
                    Main.rand.NextFloat(0.3f, 0.55f)).Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server || intensity < 0.2f) return;
            //风暴散逸：一圈切向风丝飘散，避免领域凭空消失
            for (int i = 0; i < 14; i++) {
                float ang = MathHelper.TwoPi * i / 14f;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * CurrentRadius * Main.rand.NextFloat(0.4f, 0.9f);
                Vector2 vel = (ang + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, StormMain * 0.7f,
                    Main.rand.NextFloat(0.4f, 0.8f)).Configure(false, Main.rand.Next(10, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (intensity < 0.02f) return false;
            Effect shader = EffectLoader.SHPCModStormField?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            //着色器内 dist=0.86 为边界环，世界半径向外留出辉光带
            float drawRadius = CurrentRadius / 0.86f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            shader.Parameters["uTime"]?.SetValue(visualTime);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(intensity / 0.15f, 0f, 1f));
            shader.Parameters["intensity"]?.SetValue(intensity);
            shader.Parameters["boltGauge"]?.SetValue(boltGauge);
            shader.Parameters["deepColor"]?.SetValue(StormDeep.ToVector3());
            shader.Parameters["stormColor"]?.SetValue(StormMain.ToVector3());
            shader.Parameters["arcColor"]?.SetValue(StormArc.ToVector3());
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            shader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawRadius * 2f, drawRadius * 2f),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (intensity < 0.05f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            //风眼微光：随强度呼吸的中心气压核
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float breathe = 0.85f + 0.15f * MathF.Sin(visualTime * 4f);
            spriteBatch.Draw(glow, drawPos, null, StormMain * (0.25f * intensity * breathe), 0f,
                glow.Size() * 0.5f, 1.6f + intensity * 0.8f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, StormArc * (0.14f * intensity * breathe), 0f,
                glow.Size() * 0.5f, 0.8f + intensity * 0.4f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 界内落雷：从领域上空劈向落点的折跳闪电，短暂全屏微亮+雷鸣
    /// ai0 视觉种子、ai1 天空端水平偏移；SHPCModStormBolt.fx
    /// </summary>
    internal sealed class SHPCStormBoltProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 26;
        /// <summary>伤害判定窗口帧数，其后进入残辉</summary>
        private const int DamageWindow = 8;
        /// <summary>天空端最大高度（像素），洞穴内被顶壁压短</summary>
        private const float SkyHeight = 430f;
        private const float HitWidth = 30f;
        private const int PointCount = 16;
        /// <summary>逐目标递减伤害表，表长即单雷命中数上限；防蠕虫多节段吃满面板堆叠</summary>
        private static readonly float[] HitFalloff = [1f, 0.7f, 0.5f, 0.35f, 0.25f];

        private static readonly Color BoltCore = new(235, 245, 255);
        private static readonly Color BoltGlow = new(120, 185, 255);
        private static readonly Color BoltAura = new(40, 70, 160);

        private Vector2[] boltPoints;
        private Trail trail;
        private float fadeAlpha;
        /// <summary>全屏微亮强度，首帧置 1 后指数衰减</summary>
        private float skyFlash;
        /// <summary>实际天空端高度，首帧向上探测顶壁得出；tile 输入各端一致故判定一致</summary>
        private float skyLen = SkyHeight;

        private float VisualSeed => Projectile.ai[0];
        /// <summary>天空端水平偏移按高度等比收缩，低洞顶时雷更接近垂直</summary>
        private Vector2 SkyAnchor => Projectile.Center + new Vector2(Projectile.ai[1] * (skyLen / SkyHeight), -skyLen);
        private Vector2 GroundPoint => Projectile.Center;
        private int Age => Lifetime - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一道雷对每个敌人只结算一次
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                skyFlash = 1f;
                skyLen = ProbeSkyLength(GroundPoint);
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.75f, Pitch = 0.1f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
                    SpawnImpactFx();
                    //屏震随本地玩家与落点的距离衰减，远处的雷不撼动全屏
                    float distToLocal = Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center);
                    float shakeFade = MathHelper.Clamp(1f - distToLocal / 1200f, 0f, 1f);
                    if (shakeFade > 0.05f) {
                        SHPCNaturalFx.Shake(5f * shakeFade);
                    }
                }
                RebuildBolt();
            }

            //放电期折点高频重掷，残辉期定格
            if (Age <= DamageWindow && Age % 3 == 0) {
                RebuildBolt();
            }

            fadeAlpha = Age <= DamageWindow
                ? 1f
                : 1f - (Age - DamageWindow) / (float)(Lifetime - DamageWindow);
            skyFlash *= 0.78f;

            //沿雷径照明
            for (int i = 0; i < 4; i++) {
                Vector2 lightPos = Vector2.Lerp(SkyAnchor, GroundPoint, i / 3f);
                Lighting.AddLight(lightPos, BoltGlow.ToVector3() * 0.9f * fadeAlpha);
            }

            //放电期沿雷径蹦电火花
            if (Age <= DamageWindow && Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 2; i++) {
                    Vector2 pos = Vector2.Lerp(SkyAnchor, GroundPoint, Main.rand.NextFloat())
                        + Main.rand.NextVector2Circular(18f, 18f);
                    PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2CircularEdge(3.5f, 3.5f),
                        BoltGlow, Main.rand.NextFloat(0.5f, 1f)).Configure(true, Main.rand.Next(8, 16));
                }
            }
        }

        /// <summary>
        /// 从落点向上探测顶壁：洞穴内雷从洞顶劈下而非穿透岩层
        /// 纯 tile 输入无随机，各端结果一致，雷径判定随之一致
        /// </summary>
        private static float ProbeSkyLength(Vector2 ground) {
            const float MinLen = 64f;
            for (float len = MinLen; len < SkyHeight; len += 16f) {
                Point tile = (ground - Vector2.UnitY * len).ToTileCoordinates();
                if (!WorldGen.InWorld(tile.X, tile.Y, 10)) {
                    return len;
                }
                Tile t = Framing.GetTileSafely(tile.X, tile.Y);
                if (t.HasUnactuatedTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return len;
                }
            }
            return SkyHeight;
        }

        /// <summary>重建折跳雷径：两端锚定、中段法线摆动，落点前小段收束</summary>
        private void RebuildBolt() {
            boltPoints ??= new Vector2[PointCount];
            Vector2 sky = SkyAnchor;
            Vector2 ground = GroundPoint;
            Vector2 dir = (ground - sky).SafeNormalize(Vector2.UnitY);
            Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
            float length = Vector2.Distance(sky, ground);
            for (int i = 0; i < PointCount; i++) {
                float t = i / (float)(PointCount - 1);
                //sin 包络让两端钉死，落点端 pow 提前收窄形成"劈入地面"的收束感
                float swing = MathF.Sin(t * MathHelper.Pi) * (1f - MathF.Pow(t, 3f) * 0.5f);
                float offset = Main.rand.NextFloat(-1f, 1f) * 34f * swing;
                boltPoints[i] = sky + dir * (length * t) + normal * offset;
            }
        }

        private void SpawnImpactFx() {
            //落点冲击：扩散环+爆散电火花+方形碎片
            PRTLoader.NewParticle<PRT_StarPulseRing>(GroundPoint, Vector2.Zero,
                BoltGlow with { A = 0 }, 0.05f).Configure(0.05f, 0.5f, 18);
            PRTLoader.NewParticle<PRT_StarPulseRing>(GroundPoint, Vector2.Zero,
                BoltCore with { A = 0 }, 0.05f).Configure(0.05f, 0.3f, 14);
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f) - Vector2.UnitY * 2.5f;
                PRTLoader.NewParticle<PRT_Spark>(GroundPoint, vel,
                    Color.Lerp(BoltCore, BoltGlow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.6f, 1.3f)).Configure(true, Main.rand.Next(14, 26));
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_CyberSquare>(GroundPoint,
                    Main.rand.NextVector2CircularEdge(4.5f, 4.5f),
                    BoltCore, Main.rand.NextFloat(0.7f, 1.5f)).Configure(BoltGlow, Main.rand.Next(14, 26));
            }
            //天空端云间闪光
            PRTLoader.NewParticle<PRT_StarPulseRing>(SkyAnchor, Vector2.Zero,
                BoltGlow with { A = 0 }, 0.05f).Configure(0.05f, 0.35f, 12);
        }

        public override bool? CanDamage() => Age <= DamageWindow;

        /// <summary>单雷命中数封顶，超出递减表长度的目标不再结算</summary>
        public override bool? CanHitNPC(NPC target) => Projectile.numHits >= HitFalloff.Length ? false : null;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //第 N 个目标按递减表折损，蠕虫横穿雷线时总伤封顶约 2.8 倍单发
            int idx = Math.Min(Projectile.numHits, HitFalloff.Length - 1);
            modifiers.FinalDamage *= HitFalloff[idx];
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float _ = 0f;
            //雷径线段判定，落点向下多延 30px 覆盖贴地目标
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                SkyAnchor, GroundPoint + Vector2.UnitY * 30f, HitWidth, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.4f, Pitch = 0.2f }, target.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2CircularEdge(5f, 5f),
                    BoltCore, Main.rand.NextFloat(0.6f, 1.2f)).Configure(true, Main.rand.Next(10, 20));
            }
        }

        private float WidthFunction(float progress) {
            //主干上细下粗，落点端收尖
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return (12f + taper * 16f) * MathHelper.Clamp(fadeAlpha + 0.15f, 0f, 1f);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (boltPoints == null || fadeAlpha < 0.02f) return;
            Effect shader = EffectLoader.SHPCModStormBolt?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trail ??= new Trail(boltPoints, WidthFunction, ColorFunction);
            trail.TrailPositions = boltPoints;

            //下劈波前：前 4 帧从天空冲到地面；×1.1 让波前冲过落点，尾段完全点亮
            float strikeProgress = MathHelper.Clamp((Age + 1) / 4f, 0f, 1f) * 1.1f;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["strikeProgress"]?.SetValue(strikeProgress);
            shader.Parameters["boltSeed"]?.SetValue(VisualSeed);
            shader.Parameters["coreColor"]?.SetValue(BoltCore.ToVector3());
            shader.Parameters["glowColor"]?.SetValue(BoltGlow.ToVector3());
            shader.Parameters["auraColor"]?.SetValue(BoltAura.ToVector3());
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f && skyFlash < 0.02f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;

            //短暂全屏微亮：以落点为中心的超大低透明白幕，模拟雷光照亮天地
            //12000px 保证 4K 屏最远缩放下仍满幅覆盖
            if (white != null && skyFlash > 0.02f) {
                Vector2 flashPos = GroundPoint - Main.screenPosition;
                spriteBatch.Draw(white, flashPos, null, BoltCore * (skyFlash * 0.07f), 0f,
                    white.Size() * 0.5f, new Vector2(12000f, 12000f), SpriteEffects.None, 0f);
            }
            if (glow == null) return;
            //落点电极光球与天空端辉光
            Vector2 groundScreen = GroundPoint - Main.screenPosition;
            Vector2 skyScreen = SkyAnchor - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            spriteBatch.Draw(glow, groundScreen, null, BoltGlow * fadeAlpha * 0.85f, 0f, origin, 1.6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, groundScreen, null, BoltCore * fadeAlpha * 0.9f, 0f, origin, 0.8f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, skyScreen, null, BoltGlow * fadeAlpha * 0.5f, 0f, origin, 1.2f, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
