using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 热带梭鱼技能，生成穿梭屏幕的鱼群打击敌人
    /// </summary>
    internal class FishTropicalBarracuda : FishSkill
    {
        public override int UnlockFishID => ItemID.TropicalBarracuda;
        public override int DefaultCooldown => 25 - HalibutData.GetDomainLayer() * 2;
        public override int ResearchDuration => 60 * 16;

        private int spawnCounter = 0;
        private const int SpawnInterval = 6;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            spawnCounter++;

            if (spawnCounter >= SpawnInterval && Cooldown <= 0) {
                spawnCounter = 0;
                SetCooldown();

                //从屏幕边缘生成鱼群
                SpawnBarracudaSchool(player, source, damage, knockback);
            }

            return null;
        }

        private void SpawnBarracudaSchool(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //鱼群数量
            int schoolSize = 3 + HalibutData.GetDomainLayer() / 2;

            //随机选择一个边缘方向
            int edge = Main.rand.Next(4); // 0=左, 1=右, 2=上, 3=下
            Vector2 spawnSide = GetSpawnEdge(edge, player);
            Vector2 targetSide = GetTargetEdge(edge, player);

            for (int i = 0; i < schoolSize; i++) {
                //计算生成位置（沿边缘散布）
                Vector2 spawnPos = GetScatteredPosition(spawnSide, edge, i, schoolSize);
                Vector2 targetPos = GetScatteredPosition(targetSide, edge, i, schoolSize);

                //计算速度方向
                Vector2 direction = (targetPos - spawnPos).SafeNormalize(Vector2.Zero);
                float speed = Main.rand.NextFloat(20f, 28f);
                Vector2 velocity = direction * speed;

                int barracudaProj = Projectile.NewProjectile(
                    source,
                    spawnPos,
                    velocity,
                    ModContent.ProjectileType<TropicalBarracudaProjectile>(),
                    (int)(damage * (1.5f + HalibutData.GetDomainLayer() * 0.35f)),
                    knockback * 1.2f,
                    player.whoAmI,
                    ai0: i / (float)schoolSize //颜色偏移
                );

                if (barracudaProj >= 0) {
                    Main.projectile[barracudaProj].netUpdate = true;
                }
            }

            //生成音效
            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, player.Center);

            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.4f,
                Pitch = 0.5f
            }, player.Center);

            //生成入水特效
            SpawnSchoolEffect(spawnSide);
        }

        private Vector2 GetSpawnEdge(int edge, Player player) {
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
            float offset = 100f;

            return edge switch {
                0 => new Vector2(Main.screenPosition.X - offset, screenCenter.Y), // 左
                1 => new Vector2(Main.screenPosition.X + Main.screenWidth + offset, screenCenter.Y), // 右
                2 => new Vector2(screenCenter.X, Main.screenPosition.Y - offset), // 上
                3 => new Vector2(screenCenter.X, Main.screenPosition.Y + Main.screenHeight + offset), // 下
                _ => screenCenter
            };
        }

        private Vector2 GetTargetEdge(int edge, Player player) {
            Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
            float offset = 100f;

            return edge switch {
                0 => new Vector2(Main.screenPosition.X + Main.screenWidth + offset, screenCenter.Y), // 左->右
                1 => new Vector2(Main.screenPosition.X - offset, screenCenter.Y), // 右->左
                2 => new Vector2(screenCenter.X, Main.screenPosition.Y + Main.screenHeight + offset), // 上->下
                3 => new Vector2(screenCenter.X, Main.screenPosition.Y - offset), // 下->上
                _ => screenCenter
            };
        }

        private Vector2 GetScatteredPosition(Vector2 basePos, int edge, int index, int total) {
            float spread = 400f;
            float offset = (index - total / 2f) * (spread / total);

            return edge switch {
                0 or 1 => basePos + new Vector2(0, offset), // 垂直散布
                2 or 3 => basePos + new Vector2(offset, 0), // 水平散布
                _ => basePos
            };
        }

        private void SpawnSchoolEffect(Vector2 position) {
            //水花飞溅
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust splash = Dust.NewDustPerfect(
                    position,
                    DustID.Water,
                    velocity,
                    100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                splash.noGravity = true;
                splash.fadeIn = 1.3f;
            }

            //热带色彩粒子
            for (int i = 0; i < 10; i++) {
                float hue = Main.rand.NextFloat(1f);
                Color tropicalColor = Main.hslToRgb(hue, 1f, 0.6f);

                BasePRT tropical = new PRT_Light(
                    position,
                    Main.rand.NextVector2Circular(4f, 4f),
                    0.6f,
                    tropicalColor,
                    25,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(tropical);
            }
        }
    }

    /// <summary>
    /// 热带梭鱼弹幕 - 快速穿梭屏幕
    /// </summary>
    internal class TropicalBarracudaProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.TropicalBarracuda;

        private ref float ColorOffset => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private float swimWave = 0f;
        private float baseHue = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 20;

        //穿梭参数
        private const float MaxSpeed = 30f;
        private const float Acceleration = 0.5f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;

            baseHue = Main.rand.NextFloat(1f);
        }

        public override void AI() {
            Timer++;
            swimWave += 0.25f;

            //加速到最大速度
            if (Projectile.velocity.Length() < MaxSpeed) {
                Projectile.velocity *= 1f + Acceleration * 0.01f;
            }

            //轻微波浪游动
            Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
            float wave = (float)Math.Sin(swimWave) * 2f;
            Projectile.velocity += perpendicular * wave * 0.05f;

            //保持速度方向
            if (Projectile.velocity.LengthSquared() > 1f) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            //轻微追踪最近的敌人
            if (Timer % 10 == 0) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.4f;

                    //限制最大速度
                    if (Projectile.velocity.Length() > MaxSpeed) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
                    }
                }
            }

            //更新拖尾
            UpdateTrail();

            //热带色彩光照
            float hue = (baseHue + ColorOffset + Main.GlobalTimeWrappedHourly * 0.3f) % 1f;
            Color lightColor = Main.hslToRgb(hue, 1f, 0.6f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.8f);

            //游动轨迹粒子
            if (Main.rand.NextBool(5)) {
                SpawnSwimParticle(hue);
            }

            //离开屏幕后消失
            if (IsOffScreen()) {
                Projectile.Kill();
            }
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private bool IsOffScreen() {
            Rectangle screenRect = new Rectangle(
                (int)Main.screenPosition.X - 200,
                (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400,
                Main.screenHeight + 400
            );

            return !screenRect.Contains(Projectile.Center.ToPoint());
        }

        private void SpawnSwimParticle(float hue) {
            Color particleColor = Main.hslToRgb(hue, 1f, 0.6f);

            BasePRT trail = new PRT_Spark(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1f, 1f),
                false,
                12,
                Main.rand.NextFloat(0.6f, 1f),
                particleColor
            );
            PRTLoader.AddParticle(trail);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            float hue = (baseHue + ColorOffset + Main.GlobalTimeWrappedHourly * 0.3f) % 1f;

            //击中水花爆发
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    velocity,
                    100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                splash.noGravity = true;
            }

            //热带色彩爆发
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * 5f;
                Color hitColor = Main.hslToRgb((hue + i * 0.1f) % 1f, 1f, 0.6f);

                BasePRT hit_effect = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.7f,
                    hitColor,
                    20,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(hit_effect);
            }

            SoundEngine.PlaySound(SoundID.NPCHit25 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, Projectile.Center);

            //减少穿透次数
            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            float hue = (baseHue + ColorOffset + Main.GlobalTimeWrappedHourly * 0.3f) % 1f;

            //消失水花
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    velocity,
                    100,
                    new Color(100, 200, 255),
                    Main.rand.NextFloat(2f, 3f)
                );
                splash.noGravity = Main.rand.NextBool();
            }

            //热带色彩爆发
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Color burstColor = Main.hslToRgb((hue + i * 0.05f) % 1f, 1f, 0.6f);

                BasePRT burst = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    Main.rand.NextFloat(0.6f, 1f),
                    burstColor,
                    25,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(burst);
            }

            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.TropicalBarracuda].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;

            float hue = (baseHue + ColorOffset + Main.GlobalTimeWrappedHourly * 0.3f) % 1f;
            Color tropicalColor = Main.hslToRgb(hue, 1f, 0.7f);

            //绘制速度线拖尾
            DrawSpeedTrail(sb, fishTex, origin, tropicalColor);

            //绘制热带光晕
            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.1f + i * 0.1f);
                float glowAlpha = (1f - i * 0.3f) * 0.4f;
                Color glowColor = Main.hslToRgb((hue + i * 0.05f) % 1f, 1f, 0.6f) with { A = 0 };

                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    glowColor * glowAlpha,
                    Projectile.rotation + MathHelper.PiOver4,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //主体绘制 - 应用热带色彩
            Color mainColor = Color.Lerp(
                lightColor,
                tropicalColor,
                0.6f
            );

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //白色高光（表现高速感）
            float highlightAlpha = 0.5f * (1f + (float)Math.Sin(swimWave) * 0.3f);
            sb.Draw(
                fishTex,
                drawPos,
                null,
                Color.White * highlightAlpha,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale * 0.9f,
                SpriteEffects.None,
                0
            );

            return false;
        }

        private void DrawSpeedTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, Color baseColor) {
            if (trailPositions.Count < 2) return;

            for (int i = 1; i < trailPositions.Count; i++) {
                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * 0.6f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.5f, 1f, progress);

                //计算热带渐变色
                float hueShift = (baseHue + ColorOffset + i * 0.03f) % 1f;
                Color trailColor = Main.hslToRgb(hueShift, 1f, 0.6f) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    Projectile.rotation - i * 0.05f + MathHelper.PiOver4,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制水流拖尾线条
            DrawWaterTrail(sb, trailPositions);
        }

        private void DrawWaterTrail(SpriteBatch sb, List<Vector2> positions) {
            if (positions.Count < 3) return;

            Texture2D lineTex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < positions.Count - 1; i++) {
                float progress = 1f - i / (float)positions.Count;
                float lineAlpha = progress * 0.4f;

                Vector2 start = positions[i];
                Vector2 end = positions[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();

                if (length < 0.01f) continue;

                float rotation = diff.ToRotation();
                float width = 3f * progress;

                //水流蓝色
                Color waterColor = new Color(100, 200, 255, 0) * lineAlpha;

                sb.Draw(
                    lineTex,
                    start - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    waterColor,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
