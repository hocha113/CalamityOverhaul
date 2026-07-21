using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishFallenStar : FishSkill
    {
        public override int UnlockFishID => ItemID.FallenStarfish;
        public override int DefaultCooldown => 39 - HalibutData.GetDomainLayer() * 3;
        public override int ResearchDuration => 60 * 12;
        private static int consecutiveShots = 0; //连续射击计数
        private static int ShotsForStarRain => 15 - HalibutData.GetDomainLayer(); //每14-5次射击触发一次星雨（领域层数1-10）

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();

                //发射螺旋星星
                Vector2 direction = velocity.SafeNormalize(Vector2.Zero);
                int starDamage = (int)(damage * (0.45f + HalibutData.GetDomainLayer() * 0.12f));

                //生成主星星弹幕
                int mainStar = Projectile.NewProjectile(
                    source,
                    position,
                    direction * velocity.Length(),
                    ModContent.ProjectileType<SpiralStarProjectile>(),
                    starDamage,
                    knockback * 0.5f,
                    player.whoAmI,
                    ai0: 0 //主星星
                );

                //生成两个伴随的小星星（螺旋围绕主星星）
                for (int i = 0; i < 2; i++) {
                    float angleOffset = (i == 0 ? 1 : -1) * MathHelper.PiOver2;
                    Projectile.NewProjectile(
                        source,
                        position,
                        direction * velocity.Length(),
                        ModContent.ProjectileType<SpiralStarProjectile>(),
                        (int)(starDamage * 0.75f),
                        knockback * 0.5f,
                        player.whoAmI,
                        ai0: mainStar + 1, //主星星ID+1，0保留给主星判定
                        ai1: angleOffset //初始轨道相位
                    );
                }

                //星星发射音效
                SoundEngine.PlaySound(SoundID.Item9 with {
                    Volume = 0.6f,
                    Pitch = 0.5f
                }, position);

                //连续射击计数
                consecutiveShots++;

                //检查是否触发星雨
                if (consecutiveShots >= ShotsForStarRain) {
                    consecutiveShots = 0;
                    TriggerStarRain(player, source, damage);
                }

                //发射粒子效果
                SpawnShootEffect(position, direction);
            }

            return null;
        }

        /// <summary>触发天降星雨</summary>
        private void TriggerStarRain(Player player, EntitySource_ItemUse_WithAmmo source, int baseDamage) {
            //在鼠标周围区域生成多个下落星星
            Vector2 targetArea = Main.MouseWorld;
            int starCount = 5 + HalibutData.GetLevel() / 2; //5-10个星星

            for (int i = 0; i < starCount; i++) {
                //随机分散在目标区域上方
                Vector2 spawnPos = targetArea + new Vector2(
                    Main.rand.NextFloat(-400f, 400f),
                    Main.rand.NextFloat(-800f, -600f)
                );

                //计算指向目标区域的速度
                Vector2 toTarget = (targetArea + Main.rand.NextVector2Circular(100f, 100f) - spawnPos).SafeNormalize(Vector2.Zero);
                Vector2 velocity = toTarget * Main.rand.NextFloat(12f, 18f);

                //延迟生成（制造星雨效果）
                int delay = i * 3;

                Projectile.NewProjectile(
                    source,
                    spawnPos,
                    velocity,
                    ModContent.ProjectileType<FallingStarProjectile>(),
                    (int)(baseDamage * (1.5 + HalibutData.GetDomainLayer() * 0.3) * 3.00),
                    8f,
                    player.whoAmI,
                    ai0: delay
                );
            }

            //星雨触发音效
            SoundEngine.PlaySound(SoundID.Item88 with {
                Volume = 0.8f,
                Pitch = 0.3f
            }, targetArea);
            SoundEngine.PlaySound(SoundID.MaxMana with {
                Volume = 0.55f,
                Pitch = 0.15f
            }, targetArea);

            //目标区域指示特效
            SpawnStarRainIndicator(targetArea);
        }

        private void SpawnShootEffect(Vector2 position, Vector2 direction) {
            if (Main.dedServ) {
                return;
            }
            //枪口十字闪芒一记 + 极小新星环
            FishFallenStarVFX.CrossPop(position + direction * 14f, 0.6f, 12);
            FishFallenStarVFX.NovaRing(position + direction * 10f, 0.4f);
            //沿射向拉伸的星屑
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(position
                    , direction.RotatedByRandom(0.24f) * Main.rand.NextFloat(4f, 9f)
                    , FishFallenStarVFX.StarGold, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(false, Main.rand.Next(12, 18));
            }
        }

        private void SpawnStarRainIndicator(Vector2 position) {
            if (Main.dedServ) {
                return;
            }
            //落点新星环 + 环上细芒向心汇聚，星光式预告
            FishFallenStarVFX.NovaRing(position, 1.1f);
            FishFallenStarVFX.Converge(position, 130f, 10, 4.2f);
            FishFallenStarVFX.CrossPop(position, 0.8f, 16);
            //缓落星尘余韵
            FishFallenStarVFX.StardustBurst(position, new Vector2(0f, -1.2f), 6, 2.2f);
        }
    }

    /// <summary>螺旋星星弹幕，主星星和伴随星星</summary>
    internal class SpiralStarProjectile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FallenStar;

        private ref float MainStarID => ref Projectile.ai[0];
        private ref float AngleOffset => ref Projectile.ai[1];

        private bool IsMainStar => MainStarID == 0;
        private float spiralAngle = 0f;
        private const float SpiralRadius = 40f;
        private const float SpiralSpeed = 0.15f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //SetDefaults 时 ai 尚未写入，轨道相位差在首帧生效
                spiralAngle = AngleOffset;
            }
            Projectile.localAI[1]++;

            if (IsMainStar) {
                //主星星，直线前进
                MainStarAI();
            }
            else {
                //伴随星星，螺旋围绕主星星
                CompanionStarAI();
            }

            //旋转
            Projectile.rotation += 0.2f;

            //照明
            Lighting.AddLight(Projectile.Center, 0.7f, 0.65f, 0.42f);
        }

        private void MainStarAI() {
            //出膛过冲
            if (Projectile.localAI[1] < 12f) {
                Projectile.velocity *= 1.015f;
            }
            else {
                Projectile.velocity *= 0.995f;
            }

            //轻微波动
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perpendicular * wave * 0.1f;

            //星尾星屑
            SpawnTrailStardust(5, 0.32f);
        }

        private void CompanionStarAI() {
            //检查主星星是否存在
            int mainID = (int)MainStarID - 1;
            if (mainID < 0 || mainID >= Main.maxProjectiles || !Main.projectile[mainID].active) {
                Projectile.Kill();
                return;
            }

            Projectile mainStar = Main.projectile[mainID];

            //螺旋角度递增，过近点时一记镜面闪（每圈一次）
            float prevSin = MathF.Sin(spiralAngle);
            spiralAngle += SpiralSpeed;
            if (prevSin < 0f && MathF.Sin(spiralAngle) >= 0f && !Main.dedServ) {
                FishFallenStarVFX.CrossPop(Projectile.Center, 0.42f, 10);
            }

            //计算螺旋位置
            Vector2 forwardDir = mainStar.velocity.SafeNormalize(Vector2.Zero);
            Vector2 rightDir = forwardDir.RotatedBy(MathHelper.PiOver2);

            float radiusWave = SpiralRadius * (1f + (float)Math.Sin(spiralAngle * 2f) * 0.2f);
            Vector2 offset = new Vector2(
                (float)Math.Cos(spiralAngle) * radiusWave,
                (float)Math.Sin(spiralAngle) * radiusWave
            );

            //转换到世界坐标系
            Vector2 targetPos = mainStar.Center + forwardDir * offset.X + rightDir * offset.Y;

            //平滑移动到目标位置
            Projectile.velocity = (targetPos - Projectile.Center) * 0.3f;

            //伴随星星轨迹星屑
            SpawnTrailStardust(7, 0.24f);
        }

        /// <summary>低频星屑尾迹</summary>
        private void SpawnTrailStardust(int interval, float baseScale) {
            if (Main.dedServ) {
                return;
            }
            if ((int)Projectile.localAI[1] % interval == 0) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity * Main.rand.NextFloat(0.08f, 0.2f),
                    FishFallenStarVFX.StarGold, baseScale * Main.rand.NextFloat(0.8f, 1.25f))
                    ?.Configure(false, Main.rand.Next(13, 19));
            }
            if (Main.rand.NextBool(11)) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    FishFallenStarVFX.DeepBlue, baseScale * 0.8f)?.Configure(false, Main.rand.Next(10, 15));
            }
        }

        public override void OnKill(int timeLeft) {
            //轨迹交给独立残迹
            FishFallenStarVFX.SpawnTrace(Projectile, IsMainStar ? 13f : 9f, 15);

            if (!Main.dedServ) {
                //星星碎成星屑 + 小新星环
                FishFallenStarVFX.StardustBurst(Projectile.Center, Vector2.Zero, IsMainStar ? 6 : 4, 3.2f);
                FishFallenStarVFX.NovaRing(Projectile.Center, IsMainStar ? 0.6f : 0.45f);
                FishFallenStarVFX.CrossPop(Projectile.Center, IsMainStar ? 0.62f : 0.45f, 13);
            }

            //消失音效
            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.35f,
                Pitch = 0.6f,
                MaxInstances = 3
            }, Projectile.Center);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中
            FishFallenStarVFX.CrossPop(Projectile.Center, 0.55f, 12);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center
                    , dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(2.5f, 6f)
                    , FishFallenStarVFX.StarGold, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            if (!TextureAssets.Item[ItemID.FallenStar].IsLoaded) {
                Main.instance.LoadItem(ItemID.FallenStar);
            }
            Texture2D starTex = TextureAssets.Item[ItemID.FallenStar].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = starTex.GetRectangle((int)(Main.GameUpdateCount % 8), 8);
            Vector2 origin = sourceRect.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;
            float scale = Projectile.scale * (IsMainStar ? 1.05f : 0.75f);

            //自旋拖影
            Color ghostCol = FishFallenStarVFX.StarGold with { A = 0 };
            sb.Draw(starTex, drawPos, sourceRect, ghostCol * (alpha * 0.22f)
                , Projectile.rotation - 0.55f, origin, scale * 0.94f, SpriteEffects.None, 0);
            sb.Draw(starTex, drawPos, sourceRect, ghostCol * (alpha * 0.11f)
                , Projectile.rotation - 1.1f, origin, scale * 0.88f, SpriteEffects.None, 0);

            //星体本体
            Color bodyCol = Color.Lerp(lightColor, FishFallenStarVFX.StarGold, 0.55f);
            sb.Draw(starTex, drawPos, sourceRect, bodyCol * alpha
                , Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            //十字闪芒
            float twinkle = Main.GlobalTimeWrappedHourly * 5.6f + Projectile.whoAmI * 2.4f;
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Projectile.whoAmI) * 0.22f;
            FishFallenStarVFX.DrawStarGlint(sb, drawPos, alpha * 0.95f
                , IsMainStar ? 0.72f : 0.5f, twinkle, sway);

            return false;
        }

        /// <summary>星尾彗带，深蓝→金渐变窄条带（shader 承载），替代贴图串尾迹</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            float fade = MathHelper.Clamp(Projectile.localAI[1] / 10f, 0f, 1f) * ((255f - Projectile.alpha) / 255f);
            FishFallenStarVFX.DrawCometStrip(Projectile, IsMainStar ? 14f : 9f, fade);
        }
    }

    /// <summary>天降星星弹幕</summary>
    internal class FallingStarProjectile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FallenStar;

        private ref float SpawnDelay => ref Projectile.ai[0];
        /// <summary>预告窗（帧），天空星闪半秒再落</summary>
        public const int TelegraphTime = 22;
        private float telegraphTimer;
        private bool falling;
        /// <summary>首帧缓存的出手速度，仅无延迟星保留瞄准初速（延迟星维持既有直落行为）</summary>
        private Vector2 aimVelocity;
        private bool keepAim;
        private float trailIntensity = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255; //初始完全透明
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                aimVelocity = Projectile.velocity;
                keepAim = SpawnDelay <= 0f;
            }

            //延迟等待
            if (SpawnDelay > 0) {
                SpawnDelay--;
                Projectile.velocity = Vector2.Zero;
                return;
            }

            //预告，原地微光点闪烁，蓄而不发
            if (telegraphTimer < TelegraphTime) {
                telegraphTimer++;
                Projectile.velocity = Vector2.Zero;
                float p = telegraphTimer / TelegraphTime;
                Lighting.AddLight(Projectile.Center, 0.20f * p, 0.24f * p, 0.40f * p);
                if (telegraphTimer >= TelegraphTime) {
                    Release();
                }
                return;
            }

            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
                if (Projectile.alpha < 0) Projectile.alpha = 0;
            }

            //加速下落
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 20f) {
                Projectile.velocity.Y = 20f;
            }

            //轨迹强度增加
            trailIntensity = MathHelper.Lerp(trailIntensity, 1f, 0.1f);

            //旋转
            Projectile.rotation += 0.3f;

            //照明
            Lighting.AddLight(Projectile.Center, 0.9f, 0.8f, 0.55f);

            //空气摩擦剥落
            if (!Main.dedServ) {
                if (Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center + Main.rand.NextVector2Circular(7f, 7f),
                        -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                        FishFallenStarVFX.StarGold, Main.rand.NextFloat(0.4f, 0.68f))
                        ?.Configure(true, Main.rand.Next(16, 24));
                }
                if (Main.rand.NextBool(9)) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                        -Projectile.velocity * 0.12f, FishFallenStarVFX.DeepBlue, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(false, Main.rand.Next(12, 18));
                }
            }
        }

        /// <summary>预告结束，释放下坠</summary>
        private void Release() {
            falling = true;
            Projectile.velocity = keepAim ? aimVelocity : Vector2.Zero;
            Projectile.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Item9 with {
                Volume = 0.4f,
                Pitch = 0.7f,
                MaxInstances = 3
            }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }
            //释放一瞬，小簇外抛星屑
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center
                    , Main.rand.NextVector2Circular(2.4f, 2.4f)
                    , FishFallenStarVFX.StarGold, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(false, Main.rand.Next(12, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            //彗迹交给独立残迹
            if (falling) {
                FishFallenStarVFX.SpawnTrace(Projectile, 20f, 20);
            }

            if (!Main.dedServ) {
                Vector2 upDir = (-Projectile.velocity).SafeNormalize(-Vector2.UnitY);

                //落点小新星环 + 一记大十字闪
                FishFallenStarVFX.NovaRing(Projectile.Center, 1.25f);
                FishFallenStarVFX.CrossPop(Projectile.Center, 1.0f, 16);

                //星屑迸溅
                FishFallenStarVFX.StardustBurst(Projectile.Center, upDir * 2.6f, 9, 3.6f);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center
                        , upDir.RotatedByRandom(1.1f) * Main.rand.NextFloat(2f, 5.5f)
                        , FishFallenStarVFX.StarGold, Main.rand.NextFloat(0.45f, 0.75f))
                        ?.Configure(true, Main.rand.Next(14, 24));
                }

                //落点定向震屏，幅度克制
                FishFallenStarVFX.Punch(Projectile.Center, Projectile.velocity, 3f, 8);
            }

            //撞击音效，闷响 + 薄亮铃
            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.7f,
                Pitch = 0.3f,
                MaxInstances = 4
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.MaxMana with {
                Volume = 0.3f,
                Pitch = -0.1f,
                MaxInstances = 4
            }, Projectile.Center);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中即碎（penetrate 1），OnKill 承担主爆发，这里只补一记闪
            FishFallenStarVFX.CrossPop(Projectile.Center, 0.7f, 12);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center
                    , Main.rand.NextVector2Circular(5f, 5f)
                    , FishFallenStarVFX.StarGold, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //预告期
            if (!falling) {
                if (SpawnDelay > 0) {
                    return false;
                }
                float p = telegraphTimer / TelegraphTime;
                float blink = MathF.Pow(0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.whoAmI * 1.7f), 2f);
                FishFallenStarVFX.DrawStarGlint(sb, drawPos, p * (0.4f + 0.6f * blink)
                    , MathHelper.Lerp(0.16f, 0.5f, p), Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI
                    , Projectile.whoAmI * 0.35f);
                return false;
            }

            if (!TextureAssets.Item[ItemID.FallenStar].IsLoaded) {
                Main.instance.LoadItem(ItemID.FallenStar);
            }
            Texture2D starTex = TextureAssets.Item[ItemID.FallenStar].Value;
            Rectangle sourceRect = starTex.GetRectangle((int)(Main.GameUpdateCount % 8), 8);
            Vector2 origin = sourceRect.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;
            float scale = Projectile.scale * 1.2f;
            float velRot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //自旋拖影两枚
            Color ghostCol = FishFallenStarVFX.StarGold with { A = 0 };
            sb.Draw(starTex, drawPos, sourceRect, ghostCol * (alpha * 0.25f)
                , Projectile.rotation - 0.45f, origin, scale * 0.94f, SpriteEffects.None, 0);
            sb.Draw(starTex, drawPos, sourceRect, ghostCol * (alpha * 0.12f)
                , Projectile.rotation - 0.9f, origin, scale * 0.88f, SpriteEffects.None, 0);

            //星体本体
            Color bodyCol = Color.Lerp(lightColor, FishFallenStarVFX.StarGold, 0.6f);
            sb.Draw(starTex, drawPos, sourceRect, bodyCol * alpha
                , Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            //十字闪芒
            float twinkle = Main.GlobalTimeWrappedHourly * 6.8f + Projectile.whoAmI * 2.4f;
            FishFallenStarVFX.DrawStarGlint(sb, drawPos, alpha * trailIntensity
                , 0.95f, twinkle, velRot);

            return false;
        }

        /// <summary>坠星彗尾</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (!falling) {
                return;
            }
            FishFallenStarVFX.DrawCometStrip(Projectile, 22f, trailIntensity);
        }
    }
}
