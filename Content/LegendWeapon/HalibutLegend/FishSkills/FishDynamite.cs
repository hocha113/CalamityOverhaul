using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDynamite : FishSkill
    {
        public override int UnlockFishID => ItemID.DynamiteFish;
        public override int DefaultCooldown => 60 * (20 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 18;
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (Cooldown > 0) return false;
                item.UseSound = null;
                Use(item, player);
                return false;
            }
            return base.CanUseItem(item, player);
        }

        public override void Use(Item item, Player player) {
            SetCooldown();

            //计算投掷方向和速度
            Vector2 direction = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 velocity = direction * 18f; //投掷速度
            Vector2 spawnPos = player.Center + direction * 40f;

            //生成雷管鱼弹幕
            int damage = 1;
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                spawnPos,
                velocity,
                ModContent.ProjectileType<DynamiteFishProjectile>(),
                damage,
                8f,
                player.whoAmI
            );

            //出手硝烟与点火火星
            for (int i = 0; i < 5; i++) {
                Vector2 sv = direction.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(3f, 7f);
                FishDynamiteVFX.FuseSpark(spawnPos, sv);
            }
            for (int i = 0; i < 3; i++) {
                FishDynamiteVFX.Smoke(spawnPos + Main.rand.NextVector2Circular(6f, 6f)
                    , direction * Main.rand.NextFloat(0.5f, 1.5f) + new Vector2(0f, -0.5f)
                    , Main.rand.NextFloat(0.16f, 0.26f), Main.rand.Next(24, 36)
                    , FishDynamiteVFX.SmokeHot, FishDynamiteVFX.SmokeCold);
            }
            FishDynamiteVFX.FusePop(spawnPos, 0.5f);

            //投掷音效+引信点燃的短促火花声
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = -0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.25f, Pitch = 0.65f }, player.Center);
        }
    }

    /// <summary>雷管鱼滞留爆炸弹幕</summary>
    internal class DynamiteFishProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private ref float State => ref Projectile.ai[0]; //0=飞行中, 1=已着陆
        private ref float DetonationTimer => ref Projectile.ai[1];
        private const int MaxLifeTime = 600; //10秒生命期
        private const int LandingTime = 30; //着陆稳定时间
        private const float ProximityDetectionRange = 200f; //感应范围
        private bool hasDetonated = false;
        private int warningPulseTimer = 0;
        private float spinRate = 0f;        //当前翻滚角速度，引信火星的甩出切速用
        private float armedIntensity = 1f;  //引信输出强度
        private float blinkIntensity = 0f;  //警灯爆闪当前亮度

        //引信端点，贴图上端，随翻滚甩动
        private Vector2 FusePos => Projectile.Center + (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 15f;
        private int Age => MaxLifeTime - Projectile.timeLeft;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true; //初始不造成伤害
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            //状态机
            if (State == 0) {
                FlightPhaseAI();
            }
            else {
                LandedPhaseAI(owner);
            }

            EmitFuse();

            //照明
            if (State == 1 && DetonationTimer >= LandingTime) {
                Lighting.AddLight(Projectile.Center, 1.1f * blinkIntensity, 0.24f * blinkIntensity, 0.12f * blinkIntensity);
            }
            else {
                Lighting.AddLight(Projectile.Center, 0.5f, 0.3f, 0.1f);
            }
        }

        private void FlightPhaseAI() {
            //重力
            Projectile.velocity.Y += 0.4f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }

            //空气阻力
            Projectile.velocity.X *= 0.99f;

            //翻滚
            float spinDir = Projectile.velocity.X >= 0f ? 1f : -1f;
            spinRate = MathHelper.Lerp(0.46f, 0.2f, Math.Min(Age / 50f, 1f)) * spinDir;
            Projectile.rotation += spinRate;
        }

        private void LandedPhaseAI(Player owner) {
            DetonationTimer++;
            warningPulseTimer++;

            //着陆后短暂稳定期
            if (DetonationTimer < LandingTime) {
                Projectile.velocity *= 0.8f;

                if (DetonationTimer == 1) {
                    SpawnLandingEffect();
                }
                SettleRotation();
                return;
            }

            //完全停止
            Projectile.velocity = Vector2.Zero;
            SettleRotation();

            //接近检测，寻找附近的敌人
            bool enemyNearby = false;
            float nearestDistance = ProximityDetectionRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.lifeMax > 5 && !npc.dontTakeDamage) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);

                    if (distance < ProximityDetectionRange) {
                        enemyNearby = true;
                        if (distance < nearestDistance) {
                            nearestDistance = distance;
                        }
                    }
                }
            }

            //引信增压
            float armedFrac = Utils.GetLerpValue(300f, 60f, Projectile.timeLeft, true);
            float proxFrac = enemyNearby ? 1f - nearestDistance / ProximityDetectionRange : 0f;
            armedIntensity = 1f + armedFrac * 1.1f + proxFrac * 1.4f;

            //警灯爆闪
            int blinkPeriod = Math.Max(6, (int)(36f / armedIntensity));
            float blinkPhase = warningPulseTimer % blinkPeriod / (float)blinkPeriod;
            blinkIntensity = MathF.Pow(1f - blinkPhase, 4f);

            //警告节拍（敌人接近时）
            if (enemyNearby) {
                if (warningPulseTimer % (int)MathHelper.Lerp(15, 5, 1f - nearestDistance / ProximityDetectionRange) == 0) {
                    SpawnWarningPulse();
                }

                //敌人非常接近时立即引爆
                if (nearestDistance < ProximityDetectionRange * 0.4f) {
                    Detonate();
                }
            }

            //超时自动引爆
            if (Projectile.timeLeft < 60) {
                Detonate();
            }
        }

        //躺平
        private void SettleRotation() {
            spinRate *= 0.8f;
            float target = MathF.Round(Projectile.rotation / MathHelper.PiOver2) * MathHelper.PiOver2;
            float delta = MathHelper.WrapAngle(target - Projectile.rotation);
            Projectile.rotation += delta * 0.22f + spinRate;
        }

        //引信持续输出
        private void EmitFuse() {
            if (VaultUtils.isServer || hasDetonated) {
                return;
            }

            float intensity = State == 1 && DetonationTimer >= LandingTime ? armedIntensity : 1f;
            Vector2 fusePos = FusePos;
            Vector2 fuseDir = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();

            //火星
            int sparkEvery = Math.Max(1, (int)(3f / intensity));
            if (Age % sparkEvery == 0) {
                Vector2 tangent = new Vector2(-fuseDir.Y, fuseDir.X) * spinRate * 15f;
                Vector2 vel = tangent * Main.rand.NextFloat(0.5f, 0.9f)
                    + Projectile.velocity * 0.35f + Main.rand.NextVector2Circular(1.2f, 1.2f);
                if (State == 1) {
                    vel += new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.8f));
                }
                FishDynamiteVFX.FuseSpark(fusePos, vel);
            }

            //白热爆点，噼啪的心跳
            int popEvery = Math.Max(3, (int)(9f / intensity));
            if (Age % popEvery == 0) {
                FishDynamiteVFX.FusePop(fusePos, Main.rand.NextFloat(0.34f, 0.5f));
            }

            //细烟线
            if (Age % 4 == 0) {
                Vector2 vel = State == 0
                    ? -Projectile.velocity * 0.08f + new Vector2(0f, -0.5f) + Main.rand.NextVector2Circular(0.3f, 0.3f)
                    : new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.7f, 1.1f));
                FishDynamiteVFX.Smoke(fusePos, vel, Main.rand.NextFloat(0.15f, 0.24f)
                    , Main.rand.Next(28, 44), FishDynamiteVFX.SmokeHot, FishDynamiteVFX.SmokeCold, 0.012f);
            }

            //灰烬从引信上剥落
            if (Age % 9 == 0) {
                FishDynamiteVFX.Ash(fusePos, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.6f, 1f));
            }
        }

        private void SpawnLandingEffect() {
            //砸地扬尘
            for (int i = 0; i < 10; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 vel = new Vector2(side * Main.rand.NextFloat(1.2f, 3.6f), -Main.rand.NextFloat(0.4f, 1.4f));
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(side * Main.rand.NextFloat(0f, 10f), 8f),
                    DustID.Smoke, vel, 120, new Color(150, 140, 130), Main.rand.NextFloat(1.0f, 1.6f));
                dust.noGravity = true;
            }
            for (int i = 0; i < 3; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                FishDynamiteVFX.Smoke(Projectile.Center + new Vector2(side * Main.rand.NextFloat(4f, 14f), 6f)
                    , new Vector2(side * Main.rand.NextFloat(0.8f, 2.0f), -Main.rand.NextFloat(0.3f, 0.9f))
                    , Main.rand.NextFloat(0.22f, 0.34f), Main.rand.Next(26, 40)
                    , FishDynamiteVFX.DustWallHot, FishDynamiteVFX.DustWallCold);
            }
            //火星被颠出来
            for (int i = 0; i < 3; i++) {
                FishDynamiteVFX.FuseSpark(FusePos, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 3.5f)));
            }

            //着陆音效
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
        }

        private void SpawnWarningPulse() {
            //引信增压一拍
            FishDynamiteVFX.FusePop(FusePos, Main.rand.NextFloat(0.5f, 0.7f));
            for (int i = 0; i < 3; i++) {
                FishDynamiteVFX.FuseSpark(FusePos, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)));
            }

            //警告音效
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.3f, Pitch = 0.8f }, Projectile.Center);
        }

        private void Detonate() {
            if (hasDetonated) return;
            hasDetonated = true;

            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;

            //爆炸展示层弹幕
            //仅弹幕主人生成并经网络同步，避免各端本地重复生成导致远端叠亮
            if (Projectile.owner == Main.myPlayer) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<DynamiteExplosionEffect>(),
                    0,
                    0f,
                    Projectile.owner
                );
            }

            SpawnExplosionParticles();

            //克制的震屏，随距离衰减，尊重服务器配置
            if (!Main.dedServ && CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                    , Main.rand.NextVector2Unit(), 7f, 8f, 14, 1600f, FullName));
            }

            Projectile.damage = (int)(Main.player[Projectile.owner].GetShootState().WeaponDamage * (10f + HalibutData.GetDomainLayer() * 6f));//实际爆炸伤害
            Projectile.Explode(350, default, false);

            //爆炸三层
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.3f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 1.0f, Pitch = -0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.9f }, Projectile.Center);

            //延迟Kill，等伤害判定
            Projectile.timeLeft = 3;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
        }

        private void SpawnExplosionParticles() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 c = Projectile.Center;

            //弹片流光
            const int shrapnelCount = 13;
            for (int i = 0; i < shrapnelCount; i++) {
                float ang = MathHelper.TwoPi * i / shrapnelCount + Main.rand.NextFloat(-0.18f, 0.18f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(13f, 25f);
                PRTLoader.NewParticle<PRT_FishDynamiteShrapnel>(c + vel * 0.6f, vel
                    , FishDynamiteVFX.ShrapnelEdge, Main.rand.NextFloat(0.85f, 1.35f))
                    ?.Configure(Main.rand.Next(20, 32));
            }

            //火球
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 10f);
                FishDynamiteVFX.Smoke(c + vel * 1.5f, vel, Main.rand.NextFloat(0.5f, 0.85f)
                    , Main.rand.Next(20, 32), FishDynamiteVFX.FireHot, FishDynamiteVFX.SmokeCold, 0.03f);
            }
            //外圈慢烟压底
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                FishDynamiteVFX.Smoke(c + vel * 3f, vel, Main.rand.NextFloat(0.6f, 1.0f)
                    , Main.rand.Next(40, 62), FishDynamiteVFX.SmokeHot, FishDynamiteVFX.SmokeCold);
            }

            //金色火星喷射
            for (int i = 0; i < 9; i++) {
                FishDynamiteVFX.FuseSpark(c, Main.rand.NextVector2Unit() * Main.rand.NextFloat(7f, 16f));
            }

            //Dust作廉价底噪填充
            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 13f);
                Dust d = Dust.NewDustPerfect(c, DustID.Smoke, vel, 140, new Color(90, 82, 76), Main.rand.NextFloat(1.6f, 2.6f));
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == 0) {
                //碰撞后进入着陆状态
                State = 1;
                DetonationTimer = 0;

                //反弹效果（轻微）
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                    Projectile.velocity.X = -oldVelocity.X * 0.3f;
                }
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.3f;
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            Detonate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //对敌人施加火焰debuff，爆压给一记短顿帧
            target.AddBuff(BuffID.OnFire3, 180);
            TimeFreezeSystem.RefreshNPC<FishDynamite>(target, 4);
        }

        public override bool PreDraw(ref Color lightColor) {
            //引爆后的3帧伤害判定期不再画弹体，爆闪接管画面
            if (hasDetonated) {
                return false;
            }

            Main.instance.LoadItem(ItemID.DynamiteFish);
            Texture2D fishTex = TextureAssets.Item[ItemID.DynamiteFish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = fishTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //飞行期翻滚拖影
            if (State == 0) {
                for (int i = 6; i >= 2; i -= 2) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    float ghostAlpha = (1f - i / 8f) * 0.30f;
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Main.EntitySpriteDraw(fishTex, ghostPos, sourceRect, lightColor * ghostAlpha
                        , Projectile.oldRot[i], origin, Projectile.scale * (1f - i * 0.02f), effects, 0);
                }

                //旋转拖影
                float spinT = MathHelper.Clamp(MathF.Abs(spinRate) / 0.46f, 0f, 1f);
                Color smear = FishDynamiteVFX.SparkDeep with { A = 0 };
                for (int i = 1; i <= 3; i++) {
                    float fade = (0.26f - i * 0.07f) * spinT;
                    if (fade <= 0.01f) continue;
                    Main.EntitySpriteDraw(fishTex, drawPos, sourceRect, smear * fade
                        , Projectile.rotation - spinRate * i * 2.4f, origin, Projectile.scale, effects, 0);
                }
            }

            //主体，待命期叠警灯红爆闪
            Color mainColor = lightColor;
            if (blinkIntensity > 0.02f) {
                mainColor = Color.Lerp(lightColor, FishDynamiteVFX.WarnRed, blinkIntensity * 0.45f);
            }

            //着陆余摆
            Vector2 drawScale = new Vector2(Projectile.scale);
            if (State == 1) {
                float ft = (DetonationTimer - 6f) / 14f;
                if (ft > 0f && ft < 1f) {
                    float pulse = MathF.Sin(ft * MathHelper.Pi);
                    drawScale = new Vector2(Projectile.scale * (1f + pulse * 0.14f), Projectile.scale * (1f - pulse * 0.18f));
                }
            }

            Main.EntitySpriteDraw(fishTex, drawPos, sourceRect, mainColor
                , Projectile.rotation, origin, drawScale, effects, 0);

            //引信端常燃小火点
            if (CWRAsset.SoftGlow?.Value is Texture2D glow) {
                Vector2 fpos = FusePos - Main.screenPosition;
                float flick = 0.75f + 0.25f * MathF.Sin(Age * 0.7f + Projectile.whoAmI);
                Main.EntitySpriteDraw(glow, fpos, null, FishDynamiteVFX.SparkDeep with { A = 0 } * (0.55f * flick)
                    , 0f, glow.Size() / 2f, 0.30f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, fpos, null, FishDynamiteVFX.SparkGold with { A = 0 } * (0.9f * flick)
                    , 0f, glow.Size() / 2f, 0.12f, SpriteEffects.None, 0);

                //警灯红点
                if (blinkIntensity > 0.02f) {
                    Vector2 lampPos = drawPos - (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 8f;
                    Main.EntitySpriteDraw(glow, lampPos, null, FishDynamiteVFX.WarnRed with { A = 0 } * (0.85f * blinkIntensity)
                        , 0f, glow.Size() / 2f, 0.22f, SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }

    /// <summary>雷管鱼爆炸展示层</summary>
    internal class DynamiteExplosionEffect : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int EffectDuration = 150;
        private const int FlashFrames = 2; //纯白只许过冲两帧
        private int Age => EffectDuration - Projectile.timeLeft;
        private bool initialized;
        private bool grounded;
        private float groundY;
        private float scorchRot;
        private float burstSeed;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EffectDuration;
            Projectile.alpha = 0;
        }

        public override void AI() {
            //按标志初始化
            if (!initialized) {
                initialized = true;
                DetectGround();
                scorchRot = Main.rand.NextFloat(-0.35f, 0.35f);
                burstSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            SpawnTimedSmoke();

            //照明
            float lightT = 1f - Math.Min(Age / 30f, 1f);
            if (lightT > 0f) {
                float k = MathF.Pow(lightT, 2.2f);
                Lighting.AddLight(Projectile.Center, 2.4f * k, 1.2f * k, 0.4f * k);
            }
        }

        //向下探测地表
        private void DetectGround() {
            Point tp = Projectile.Center.ToTileCoordinates();
            for (int i = 0; i < 9; i++) {
                if (WorldGen.SolidTile(tp.X, tp.Y + i)) {
                    grounded = true;
                    groundY = (tp.Y + i) * 16f;
                    return;
                }
            }
            grounded = false;
        }

        private void SpawnTimedSmoke() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 c = Projectile.Center;

            //尘墙波前
            if (Age >= 1 && Age <= 9) {
                float front = 26f + Age * 30f;
                if (grounded) {
                    for (int side = -1; side <= 1; side += 2) {
                        Vector2 pos = new Vector2(c.X + side * front, groundY - Main.rand.NextFloat(4f, 14f));
                        Vector2 vel = new Vector2(side * (2.4f + Age * 0.16f), -Main.rand.NextFloat(0.6f, 1.8f));
                        FishDynamiteVFX.Smoke(pos, vel, 0.30f + Age * 0.03f, Main.rand.Next(26, 42)
                            , FishDynamiteVFX.DustWallHot, FishDynamiteVFX.DustWallCold, 0.025f);
                        Dust d = Dust.NewDustPerfect(pos, DustID.Smoke, vel * 1.3f, 150, new Color(120, 106, 90), Main.rand.NextFloat(1.2f, 2f));
                        d.noGravity = true;
                    }
                }
                else {
                    for (int i = 0; i < 3; i++) {
                        Vector2 dir = Main.rand.NextVector2Unit();
                        FishDynamiteVFX.Smoke(c + dir * front, dir * 2.6f, 0.3f, Main.rand.Next(24, 38)
                            , FishDynamiteVFX.DustWallHot, FishDynamiteVFX.DustWallCold, 0.025f);
                    }
                }
            }

            //烟柱
            if (Age >= 2 && Age <= 48 && Age % 3 == 0) {
                float t = Age / 48f;
                Vector2 pos = c + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-6f, 6f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(2.2f, 3.4f));
                Color hot = Color.Lerp(FishDynamiteVFX.FireHot, FishDynamiteVFX.SmokeHot, Math.Min(t * 2f, 1f));
                FishDynamiteVFX.Smoke(pos, vel, 0.45f + t * 0.3f, Main.rand.Next(55, 85)
                    , hot, FishDynamiteVFX.SmokeCold, 0.018f);
            }

            //蘑菇帽，柱顶横向摊开
            if (Age >= 26 && Age <= 52 && Age % 4 == 0) {
                Vector2 pos = c + new Vector2(Main.rand.NextFloat(-34f, 34f), -80f - Main.rand.NextFloat(0f, 70f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-1.1f, 1.1f), -Main.rand.NextFloat(0.4f, 0.8f));
                FishDynamiteVFX.Smoke(pos, vel, Main.rand.NextFloat(0.6f, 0.9f), Main.rand.Next(60, 92)
                    , FishDynamiteVFX.SmokeHot, FishDynamiteVFX.SmokeCold, 0.014f);
            }

            //余韵
            if (Age >= 30 && Age <= 126) {
                if (Age % 9 == 0) {
                    Vector2 pos = (grounded ? new Vector2(c.X, groundY) : c) + new Vector2(Main.rand.NextFloat(-16f, 16f), -4f);
                    FishDynamiteVFX.Smoke(pos, new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -0.85f)
                        , Main.rand.NextFloat(0.16f, 0.28f), Main.rand.Next(44, 66)
                        , FishDynamiteVFX.SmokeHot, FishDynamiteVFX.SmokeCold, 0.01f);
                }
                if (Age % 14 == 0) {
                    FishDynamiteVFX.Ash(c + Main.rand.NextVector2Circular(50f, 30f)
                        , new Vector2(0f, Main.rand.NextFloat(0.3f, 0.8f)), Main.rand.NextFloat(0.6f, 1f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            int age = Age;
            float life = age / (float)EffectDuration;

            //焦痕
            if (grounded && CWRAsset.TearSpread01?.Value is Texture2D scorch && age >= 3) {
                float reveal = Math.Min((age - 3) / 8f, 1f);
                float fade = 1f - Utils.GetLerpValue(0.6f, 1f, life, true);
                Vector2 spos = new Vector2(center.X, groundY - Main.screenPosition.Y + 2f);
                Color scol = new Color(16, 13, 11) * (0.52f * reveal * fade);
                sb.Draw(scorch, spos, null, scol, scorchRot, scorch.Size() / 2f, new Vector2(2.4f, 0.85f), SpriteEffects.None, 0f);
                sb.Draw(scorch, spos, null, scol * 0.7f, -scorchRot * 1.4f, scorch.Size() / 2f, new Vector2(1.7f, 0.6f), SpriteEffects.None, 0f);
            }

            //冲击环
            if (CWRAsset.Ring01?.Value is Texture2D ring && age <= 16) {
                float rt = age / 16f;
                float ease = 1f - MathF.Pow(1f - rt, 3f);
                float radius = MathHelper.Lerp(30f, 330f, ease);
                float alpha = MathF.Pow(1f - rt, 1.7f) * 0.5f;
                float rscale = radius * 2f / ring.Width;
                //白热只许起步两帧，气浪迅速落回扬尘色
                Color rcol = Color.Lerp(FishDynamiteVFX.HotWhite, FishDynamiteVFX.DustWallHot, Math.Min(rt * 8f, 1f)) with { A = 0 };
                Vector2 rsc = grounded ? new Vector2(rscale, rscale * 0.45f) : new Vector2(rscale);
                sb.Draw(ring, center, null, rcol * alpha, 0f, ring.Size() / 2f, rsc, SpriteEffects.None, 0f);
            }

            //核心
            if (age <= 10) {
                float ct = age / 10f;
                if (CWRAsset.Fog?.Value is Texture2D cloud) {
                    sb.Draw(cloud, center, null, new Color(30, 26, 24) * (0.5f * (1f - ct)), burstSeed
                        , cloud.Size() / 2f, 0.3f + ct * 0.24f, SpriteEffects.None, 0f);
                }
                if (CWRAsset.SoftGlow?.Value is Texture2D glow) {
                    //柔光只作底层
                    sb.Draw(glow, center, null, FishDynamiteVFX.FireHot with { A = 0 } * (0.5f * (1f - ct))
                        , 0f, glow.Size() / 2f, 3.2f, SpriteEffects.None, 0f);
                }
                if (CWRAsset.StarFlare02?.Value is Texture2D flare) {
                    Color ccol = (age < FlashFrames ? FishDynamiteVFX.HotWhite : FishDynamiteVFX.FireHot) with { A = 0 };
                    float calpha = MathF.Pow(1f - ct, 2.6f);
                    sb.Draw(flare, center, null, ccol * calpha, -burstSeed, flare.Size() / 2f, 1.1f - ct * 0.5f, SpriteEffects.None, 0f);
                }
            }

            //放射爆点
            if (CWRAsset.RayBurst01?.Value is Texture2D rays && age <= 12) {
                float rt = age / 12f;
                float alpha = MathF.Pow(1f - rt, 2.1f);
                float scale = 1.6f + rt * 1.3f;
                Color rayCol = (age < FlashFrames
                    ? FishDynamiteVFX.HotWhite
                    : Color.Lerp(FishDynamiteVFX.FireHot, FishDynamiteVFX.SparkDeep, rt)) with { A = 0 };
                sb.Draw(rays, center, null, rayCol * (alpha * 0.9f), burstSeed, rays.Size() / 2f, scale, SpriteEffects.None, 0f);
                sb.Draw(rays, center, null, rayCol * (alpha * 0.5f), burstSeed + 0.5f, rays.Size() / 2f, scale * 0.7f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
