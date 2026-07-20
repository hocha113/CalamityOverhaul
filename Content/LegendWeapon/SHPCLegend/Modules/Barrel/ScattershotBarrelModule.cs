using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>霰射枪管：光束首次命中或走到尽头碎裂为锥形短程碎光弹片，贴脸满中收益最大</summary>
    internal sealed class ScattershotBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //霰射狂暴的橙色调
        public override Color TintColor => new(255, 130, 30);

        //═════ 平衡参数 ═════
        //光束碎裂的弹片数
        private const int ShardsPerBurst = 5;
        //每片弹片伤害 = 光束伤害 × 此系数
        private const float ShardDamageMul = 0.12f;
        //碎裂锥形半角（弧度，~22°）
        private const float ConeHalfAngle = 0.38f;
        //弹片初速（extraUpdates=1，实际每帧位移翻倍）
        private const float ShardSpeed = 9f;
        //右键能量球引爆时的大簇弹片数
        private const int OrbBurstShards = 10;
        //大簇每片伤害 = 能量球伤害 × 此系数
        private const float OrbShardDamageMul = 0.08f;
        //同主弹片总量上限，冲顶时挤掉最老弹片为新碎裂腾位（帧率与弹幕表保护）
        private const int MaxConcurrentShards = 100;

        public override void Apply(ref ShootContext ctx) {
            //霰弹身份：多束、广散布、单发变弱、真短程贴脸定位
            ctx.BeamCountAdd += 2;
            ctx.SpreadMul += 0.8f;
            ctx.DamageMul += -0.3f;
            //光束在 ~3400px（约两屏）内耗尽：射程惩罚可感知，走到尽头的空炸碎光
            //又能从碎裂点再延伸 300px，保留"中距离对空放霰弹"的玩法
            ctx.BeamLifeMul += -0.55f;
            ctx.ManaCostMul += 0.4f;
        }

        //说明：LaserMode 只能由 Barrel 槽模块开启，而本模块同为 Barrel 槽、互斥，
        //装备本模块时激光弹幕不可能存在，故 OnLaser* 钩子无需实现（同 Hypersonic/CoralReef 惯例）

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            //链跳模块会在本钩子前 Kill 光束并已在 OnBeamKill 碎裂过，active 拦截防双碎
            if (beam.IsDerived || !beam.Projectile.active) return;
            //每束只碎一次：numHits 在钩子结束后才递增，==0 即首次命中
            if (beam.Projectile.numHits > 0) return;
            Shatter(beam.Projectile, beam.FlightDirection,
                Math.Max((int)(beam.Projectile.damage * ShardDamageMul), 1), ShardsPerBurst, 1f);
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.SuppressDeathEffects) return;
            //只有从未命中的主束在走到尽头时碎裂；命中过的已在 OnBeamHitNPC 碎过
            if (beam.IsDerived || beam.Projectile.numHits > 0) return;
            Shatter(beam.Projectile, beam.FlightDirection,
                Math.Max((int)(beam.Projectile.damage * ShardDamageMul), 1), ShardsPerBurst, 1f);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            //右键大招联动：能量球引爆时朝飞行方向轰出一记大号霰射
            if (orb.Projectile.owner != Main.myPlayer) return;
            Vector2 dir = orb.Projectile.velocity.SafeNormalize(Vector2.Zero);
            if (dir == Vector2.Zero) {
                Player owner = Main.player[orb.Projectile.owner];
                dir = (orb.Projectile.Center - owner.Center).SafeNormalize(Vector2.UnitX);
            }
            Shatter(orb.Projectile, dir,
                Math.Max((int)(orb.Projectile.damage * OrbShardDamageMul), 1), OrbBurstShards, 1.5f);
            SHPCNaturalFx.Shake(3f);
        }

        /// <summary>
        /// 在 source 处朝 dir 碎裂：先落一记碎光爆闪（随机种子在所有者端 roll 后经 ai 传递），
        /// 再在锥形扇面内撒出非追踪短程弹片
        /// </summary>
        private static void Shatter(Projectile source, Vector2 dir, int shardDamage, int shardCount, float flashScale) {
            if (source.owner != Main.myPlayer) return;
            float baseAngle = dir.ToRotation();

            //碎裂爆闪：ai0=方向角 ai1=随机种子，视觉与音效收敛在闪光弹幕内保证联机各端一致
            Projectile.NewProjectile(source.GetSource_FromThis(),
                source.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCShardburstFlashProj>(),
                0, 0f, source.owner,
                ai0: baseAngle, ai1: Main.rand.NextFloat(), ai2: flashScale);

            //弹片池冲顶时挤掉最老的几片（多半已飞远、衰减到谷底），新碎裂永远足额有效
            int shardType = ModContent.ProjectileType<SHPCShardburstShardProj>();
            int overflow = SHPCNaturalFx.CountOwned(source.owner, shardType) + shardCount - MaxConcurrentShards;
            for (int k = 0; k < overflow; k++) {
                KillOldestShard(source.owner, shardType);
            }
            for (int i = 0; i < shardCount; i++) {
                //锥形均匀铺开 + 少量抖动；随机全部在所有者端 roll，结果烧进同步的 velocity
                float ang = baseAngle + MathHelper.Lerp(-ConeHalfAngle, ConeHalfAngle, (i + 0.5f) / shardCount)
                    + Main.rand.NextFloat(-0.06f, 0.06f);
                Vector2 vel = ang.ToRotationVector2() * ShardSpeed * Main.rand.NextFloat(0.82f, 1.18f);
                Projectile.NewProjectile(source.GetSource_FromThis(),
                    source.Center, vel,
                    ModContent.ProjectileType<SHPCShardburstShardProj>(),
                    shardDamage, 0.5f, source.owner);
            }
        }

        /// <summary>Kill 同主 timeLeft 最小的一片弹片；仅在弹片池冲顶帧调用</summary>
        private static void KillOldestShard(int owner, int type) {
            int best = -1;
            int bestTime = int.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != owner || p.type != type) continue;
                if (p.timeLeft < bestTime) {
                    bestTime = p.timeLeft;
                    best = i;
                }
            }
            if (best >= 0) {
                Main.projectile[best].Kill();
            }
        }
    }

    /// <summary>碎光弹片：非追踪锥形短程，飞得越远伤害越低，Trail+方向高光随衰减收窄</summary>
    internal sealed class SHPCShardburstShardProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //═════ 平衡参数 ═════
        //寿命（更新计，extraUpdates=1 时折合 21 帧）
        private const int LifeUpdates = 42;
        //距离衰减基准射程（像素），衰减曲线在此处到达谷底
        private const float MaxTravel = 300f;
        //贴脸免衰减的射程占比
        private const float CloseRangeFrac = 0.18f;
        //远端伤害谷底倍率
        private const float FarDamageMul = 0.35f;

        private const int TrailLen = 8;
        private static readonly Color GlassCore = new(255, 235, 190);
        private static readonly Color GlassEdge = new(200, 90, 20);
        private static readonly Vector3 CoreVec = new Color(255, 240, 205).ToVector3();
        private static readonly Vector3 GlowVec = new Color(255, 160, 60).ToVector3();
        private static readonly Vector3 AuraVec = new Color(145, 55, 12).ToVector3();

        private Vector2 spawnPos;
        private Vector2[] trailPoints;
        private Trail trail;
        private float fadeAlpha;

        /// <summary>已飞距离占比 0~1，伤害与视觉共用的衰减因子来源</summary>
        private float TravelFrac => MathHelper.Clamp(Vector2.Distance(spawnPos, Projectile.Center) / MaxTravel, 0f, 1f);

        /// <summary>当前效力 1→0.35：贴脸全额，远端只剩零头</summary>
        private float Potency {
            get {
                float t = MathHelper.Clamp((TravelFrac - CloseRangeFrac) / (1f - CloseRangeFrac), 0f, 1f);
                t = t * t * (3f - 2f * t);
                return MathHelper.Lerp(1f, FarDamageMul, t);
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = LifeUpdates;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                spawnPos = Projectile.Center;
            }
            //玻璃薄片的空气阻尼：远端明显掉速，衰减与视觉一致可读
            Projectile.velocity *= 0.988f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);

            //稀疏玻璃碎屑尾迹
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_SHPCShardGlass>(Projectile.Center,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    GlassCore, Main.rand.NextFloat(0.3f, 0.55f))
                    .Configure(GlassEdge, Main.rand.Next(12, 22));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.45f, 0.18f) * fadeAlpha * Potency);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //距离衰减：霰弹身份的核心结算，贴脸打满、远距蹭痒
            modifiers.FinalDamage *= Potency;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.22f, Pitch = 0.7f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    GlassCore, Main.rand.NextFloat(0.3f, 0.6f))
                    .Configure(GlassEdge, Main.rand.Next(10, 18), Main.rand.NextFloat(-0.2f, 0.2f), 0.7f);
            }
            PRTLoader.NewParticle<PRT_SHPCShardGlass>(target.Center,
                Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * 1.5f,
                GlassCore, Main.rand.NextFloat(0.4f, 0.7f))
                .Configure(GlassEdge, Main.rand.Next(14, 24));
        }

        private float WidthFunction(float progress) {
            //远端弹片拖尾整体收窄，把"衰减"直接画在弹片身上
            return MathHelper.Lerp(5.5f, 0f, progress) * (0.45f + 0.55f * Potency);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2 || fadeAlpha < 0.05f) return;

            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            trailPoints ??= new Vector2[TrailLen];
            Vector2 head = Projectile.Center;
            for (int i = 0; i < TrailLen; i++) {
                Vector2 raw = i < Projectile.oldPos.Length ? Projectile.oldPos[i] : Vector2.Zero;
                trailPoints[i] = raw == Vector2.Zero ? head : raw + Projectile.Size * 0.5f;
            }

            trail ??= new Trail(trailPoints, WidthFunction, ColorFunction);
            trail.TrailPositions = trailPoints;

            float potency = Potency;
            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha * (0.5f + 0.5f * potency));
            shader.Parameters["coreColor"]?.SetValue(CoreVec);
            shader.Parameters["glowColor"]?.SetValue(GlowVec);
            shader.Parameters["auraColor"]?.SetValue(AuraVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.Parameters["overdriveAmount"]?.SetValue(0f);
            shader.Parameters["glitchBurst"]?.SetValue(0f);
            shader.Parameters["odCoreColor"]?.SetValue(CoreVec);
            shader.Parameters["odGlowColor"]?.SetValue(GlowVec);
            shader.Parameters["odAuraColor"]?.SetValue(AuraVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.05f) return;
            float potency = Potency;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, screenPos,
                    new Color(255, 215, 140, 0) * fadeAlpha * (0.35f + 0.5f * potency),
                    new Color(150, 55, 15, 0) * fadeAlpha * 0.25f,
                    0.4f + 0.25f * potency, Projectile.rotation, 3);
            }
            //方向高光：玻璃棱片的锐利反光
            Texture2D shot = CWRAsset.LightShotAlt?.Value;
            if (shot != null) {
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                spriteBatch.Draw(shot, screenPos, null,
                    new Color(255, 235, 190, 0) * fadeAlpha * (0.3f + 0.5f * potency),
                    Projectile.rotation, origin, new Vector2(0.42f, 0.3f), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>碎光爆闪：SHPCModShardburst.fx 玻璃裂纹+色散波前+碎屑喷发，音效与粒子的联机收敛点</summary>
    internal sealed class SHPCShardburstFlashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 16;
        private const float BaseDrawSize = 190f;

        private static readonly Color FlashCore = new(255, 240, 200);
        private static readonly Color FlashGlow = new(255, 165, 55);

        private float BurstAngle => Projectile.ai[0];
        private float BurstSeed => Projectile.ai[1];
        private float FlashScale => Projectile.ai[2] <= 0f ? 1f : Projectile.ai[2];
        private float Progress => MathHelper.Clamp((Lifetime - Projectile.timeLeft) / (float)(Lifetime - 2), 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    //玻璃碎裂双层音：低频碎骨架 + 高频晶莹泛音
                    SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.3f * FlashScale, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.45f, Pitch = 0.55f, MaxInstances = 3 }, Projectile.Center);
                    //远处走到尽头的碎裂常在屏幕外，粒子只在可见时喷发
                    if (VaultUtils.IsPointOnScreen(Projectile.Center - Main.screenPosition, 200)) {
                        SpawnBurstParticles();
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, FlashGlow.ToVector3() * 0.7f * (1f - Progress) * FlashScale);
        }

        private void SpawnBurstParticles() {
            Vector2 dir = BurstAngle.ToRotationVector2();
            int glassCount = (int)(8 * FlashScale);
            for (int i = 0; i < glassCount; i++) {
                //锥形喷出翻滚的玻璃碎屑
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f)) * Main.rand.NextFloat(1.5f, 6.5f);
                PRTLoader.NewParticle<PRT_SHPCShardGlass>(Projectile.Center, vel,
                    FlashCore, Main.rand.NextFloat(0.45f, 0.9f))
                    .Configure(new Color(200, 90, 20), Main.rand.Next(18, 34));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(1f, 3f),
                    FlashCore, Main.rand.NextFloat(0.4f, 0.8f))
                    .Configure(FlashGlow, Main.rand.Next(12, 22), Main.rand.NextFloat(-0.25f, 0.25f), 0.9f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //屏幕外的碎裂不值得为它重启 Immediate 批次（quad 最大半径 ~145px，留余量）
            if (!VaultUtils.IsPointOnScreen(Projectile.Center - Main.screenPosition, 250)) return false;
            Effect shader = EffectLoader.SHPCModShardburst?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)Lifetime * 1.7f, 0f, 1f);
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["progress"]?.SetValue(MathHelper.Lerp(0.08f, 1f, Progress));
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["burstSeed"]?.SetValue(BurstSeed);
            shader.Parameters["coreColor"]?.SetValue(FlashCore.ToVector3());
            shader.Parameters["glowColor"]?.SetValue(FlashGlow.ToVector3());

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawSize = BaseDrawSize * FlashScale;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                BurstAngle, canvas.Size() * 0.5f,
                new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
