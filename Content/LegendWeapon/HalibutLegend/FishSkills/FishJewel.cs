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
    /// <summary>
    /// 珠宝鱼技能
    /// </summary>
    internal class FishJewel : FishSkill
    {
        public override int UnlockFishID => ItemID.Jewelfish;
        public override int DefaultCooldown => (int)(21 - HalibutData.GetDomainLayer() *1.3f); //更快的射击节奏
        public override int ResearchDuration => 60 * 18;

        private int gemCycle = 0; //宝石循环计数
        private const int GemTypes = 6; //6种宝石类型

        //音乐音阶配置（使用半音阶）
        private static readonly float[] MusicScale = new float[] {
            0.0f,   //C  - 红宝石
            0.1f,   //D  - 蓝宝石
            0.2f,   //E  - 绿宝石
            0.25f,  //F  - 黄玉
            0.35f,  //G  - 紫水晶
            0.45f   //A  - 钻石
        };

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                //循环切换宝石类型
                int currentGemType = gemCycle % GemTypes;
                gemCycle++;

                //生成主宝石弹幕
                SpawnMainGem(source, player, position, velocity, damage, knockback, currentGemType);

                //根据领域等级生成额外的宝石碎片
                int fragmentCount = 2 + HalibutData.GetDomainLayer() / 5;
                SpawnGemFragments(source, player, position, velocity, damage, knockback, fragmentCount, currentGemType);

                //播放音乐化的宝石音效
                PlayMusicalGemSound(position, currentGemType);

                //生成节奏化的召唤特效
                SpawnRhythmicEffect(position, currentGemType);

                SetCooldown();
            }

            return null;
        }

        private void SpawnMainGem(IEntitySource source, Player player, Vector2 position,
            Vector2 velocity, int damage, float knockback, int gemType) {

            //稍微加快速度，增加节奏感
            Vector2 boostedVelocity = velocity * 1.15f;

            Projectile.NewProjectile(
                source,
                position,
                boostedVelocity,
                ModContent.ProjectileType<JewelGemProjectile>(),
                (int)(damage * (0.5f + HalibutData.GetDomainLayer() * 0.15f)),
                knockback * 1.2f,
                player.whoAmI,
                ai0: gemType
            );
        }

        private void SpawnGemFragments(IEntitySource source, Player player, Vector2 position,
            Vector2 velocity, int damage, float knockback, int count, int gemType) {

            for (int i = 0; i < count; i++) {
                float angleOffset = MathHelper.Lerp(-0.35f, 0.35f, i / (float)Math.Max(1, count - 1));
                Vector2 fragmentVel = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(1.0f, 1.25f);

                Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(15f, 15f),
                    fragmentVel,
                    ModContent.ProjectileType<JewelFragmentProjectile>(),
                    (int)(damage * (0.2f + HalibutData.GetDomainLayer() * 0.05f)),
                    knockback * 0.8f,
                    player.whoAmI,
                    ai0: gemType,
                    ai1: i
                );
            }
        }

        ///<summary>
        ///播放音乐化的宝石音效，每种宝石对应不同音高
        ///</summary>
        private void PlayMusicalGemSound(Vector2 position, int gemType) {
            //主音符 - 清脆的钟声
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.8f,
                Pitch = MusicScale[gemType],
                PitchVariance = 0.02f
            }, position);

            //和声 - 柔和的共鸣
            SoundEngine.PlaySound(SoundID.Item28 with {
                Volume = 0.5f,
                Pitch = MusicScale[gemType] + 0.5f, //高八度和声
                PitchVariance = 0.02f
            }, position);

            //节奏打击音 - 增加节奏感
            SoundEngine.PlaySound(SoundID.Item37 with {
                Volume = 0.4f,
                Pitch = 0.6f + gemType * 0.08f
            }, position);

            //每隔一段时间添加重音
            if (gemCycle % 4 == 0) {
                //强调音 - 更响亮
                SoundEngine.PlaySound(SoundID.Item4 with {
                    Volume = 0.7f,
                    Pitch = MusicScale[gemType] - 0.2f
                }, position);
            }

            //每完成一个循环（6种宝石）播放特殊音效
            if (gemCycle % GemTypes == 0) {
                SoundEngine.PlaySound(SoundID.MaxMana with {
                    Volume = 0.6f,
                    Pitch = 0.3f
                }, position);
            }
        }

        ///<summary>
        ///生成节奏化的视觉特效
        ///</summary>
        private void SpawnRhythmicEffect(Vector2 position, int gemType) {
            Color gemColor = GetGemColor(gemType);

            //主脉冲环 - 随节奏扩散
            int pulseCount = 15;
            float pulseIntensity = 1.0f;

            //每4次射击增强一次视觉效果
            if (gemCycle % 4 == 0) {
                pulseCount = 25;
                pulseIntensity = 1.5f;
            }

            //脉冲环形粒子
            for (int i = 0; i < pulseCount; i++) {
                float angle = MathHelper.TwoPi * i / pulseCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 10f) * pulseIntensity;

                Dust sparkle = Dust.NewDustPerfect(
                    position,
                    DustID.GemTopaz + gemType,
                    velocity,
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.5f, 2.5f) * pulseIntensity
                );
                sparkle.noGravity = true;
                sparkle.fadeIn = 1.4f;
            }

            //节奏性闪光粒子
            int sparkleCount = 10;
            if (gemCycle % 2 == 0) {
                sparkleCount = 15; //偶数拍更多粒子
            }

            for (int i = 0; i < sparkleCount; i++) {
                Dust glitter = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(25f, 25f),
                    DustID.Enchanted_Gold,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    Color.Lerp(gemColor, Color.White, 0.6f),
                    Main.rand.NextFloat(1.2f, 2.0f)
                );
                glitter.noGravity = true;
            }

            //每完成一个循环（6种宝石）的华丽爆发
            if (gemCycle % GemTypes == 0) {
                for (int i = 0; i < 30; i++) {
                    float angle = MathHelper.TwoPi * i / 30f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 15f);

                    Dust burst = Dust.NewDustPerfect(
                        position,
                        DustID.RainbowMk2,
                        vel,
                        100,
                        Color.White,
                        Main.rand.NextFloat(2f, 3f)
                    );
                    burst.noGravity = true;
                    burst.fadeIn = 1.8f;
                }
            }
        }

        ///<summary>
        ///获取宝石颜色
        ///</summary>
        public static Color GetGemColor(int gemType) {
            return gemType switch {
                0 => new Color(255, 100, 100),   //红宝石
                1 => new Color(100, 100, 255),   //蓝宝石
                2 => new Color(100, 255, 100),   //绿宝石
                3 => new Color(255, 200, 100),   //黄玉
                4 => new Color(200, 100, 255),   //紫水晶
                5 => new Color(100, 255, 255),   //钻石
                _ => Color.White
            };
        }

        ///<summary>
        ///获取对应宝石的物品ID
        ///</summary>
        public static int GetGemItemID(int gemType) {
            int id = gemType switch {
                0 => ItemID.Ruby,
                1 => ItemID.Sapphire,
                2 => ItemID.Emerald,
                3 => ItemID.Topaz,
                4 => ItemID.Amethyst,
                5 => ItemID.Diamond,
                _ => ItemID.Diamond
            };
            Main.instance.LoadItem(id);
            return id;
        }
    }

    /// <summary>
    /// 主宝石弹幕
    /// </summary>
    internal class JewelGemProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float GemType => ref Projectile.ai[0];
        private ref float Time => ref Projectile.ai[1];

        private float rotationSpeed = 0f;
        private float musicPulse = 0f; //音乐节奏脉动

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 24; //更长的拖尾
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 360;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Time++;

            //节奏性旋转 - 加快旋转速度
            rotationSpeed += 0.025f;
            Projectile.rotation += rotationSpeed;

            //音乐节奏脉动（4拍节奏）
            musicPulse = (float)Math.Sin(Time * 0.3f) * 0.5f + 0.5f;

            //轻微的螺旋运动 - 增加视觉动感
            float spiralWave = (float)Math.Sin(Time * 0.2f) * 0.5f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perpendicular * spiralWave;

            //璀璨照明 - 随音乐节奏脉动
            Color gemColor = FishJewel.GetGemColor((int)GemType);
            float pulse = 0.7f + musicPulse * 0.8f; //更强的脉动
            Lighting.AddLight(Projectile.Center,
                gemColor.R / 255f * pulse * 2.0f,
                gemColor.G / 255f * pulse * 2.0f,
                gemColor.B / 255f * pulse * 2.0f);

            //节奏性粒子生成 - 在"拍点"上生成更多粒子
            bool isBeat = (int)(Time / 15f) != (int)((Time - 1) / 15f); //每15帧一个拍点
            if (Main.rand.NextBool(isBeat ? 1 : 4)) {
                SpawnRhythmicParticles(isBeat);
            }

            //速度衰减 - 稍慢一些保持节奏感
            Projectile.velocity *= 0.997f;
        }

        private void SpawnRhythmicParticles(bool isBeat) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            int particleCount = isBeat ? 3 : 1; //拍点时生成更多粒子

            for (int i = 0; i < particleCount; i++) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.GemTopaz + (int)GemType,
                    -Projectile.velocity * Main.rand.NextFloat(0.3f, 0.6f),
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.5f, 2.2f) * (isBeat ? 1.3f : 1.0f)
                );
                trail.noGravity = true;
                trail.fadeIn = 1.4f;
            }

            //拍点时的额外闪光
            if (isBeat && Main.rand.NextBool(2)) {
                Dust sparkle = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Enchanted_Gold,
                    Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    Color.Lerp(gemColor, Color.White, 0.7f),
                    Main.rand.NextFloat(1.5f, 2.0f)
                );
                sparkle.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            //击中音效 - 音乐化
            float[] hitPitches = new float[] { 0.2f, 0.3f, 0.4f, 0.45f, 0.55f, 0.65f };
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.7f,
                Pitch = hitPitches[(int)GemType]
            }, Projectile.Center);

            //和声
            SoundEngine.PlaySound(SoundID.Item28 with {
                Volume = 0.4f,
                Pitch = hitPitches[(int)GemType] + 0.3f
            }, Projectile.Center);

            //节奏性爆发宝石碎片
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust shard = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    velocity,
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.8f, 3.0f)
                );
                shard.noGravity = true;
                shard.fadeIn = 1.5f;
            }

            //闪光爆发
            for (int i = 0; i < 15; i++) {
                Dust flash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Enchanted_Gold,
                    Main.rand.NextVector2Circular(6f, 6f),
                    100,
                    Color.White,
                    Main.rand.NextFloat(2.0f, 3.0f)
                );
                flash.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            //消失时的华丽爆破
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 7f);
                Dust explosion = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    velocity,
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                explosion.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //获取宝石纹理
            int gemItemID = FishJewel.GetGemItemID((int)GemType);
            Texture2D gemTex = TextureAssets.Item[gemItemID].Value;
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            float fadeAlpha = 1f - Projectile.alpha / 255f;

            //绘制节奏性残影拖尾 - 根据音乐脉动调整透明度
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)Projectile.oldPos.Length;
                float rhythmicAlpha = trailProgress * (0.5f + musicPulse * 0.3f) * fadeAlpha;

                Color trailColor = Color.Lerp(gemColor, Color.White, trailProgress * 0.4f) * rhythmicAlpha;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float rotation = Projectile.oldRot[i];
                float scale = Projectile.scale * (0.6f + trailProgress * 0.4f);

                Main.EntitySpriteDraw(
                    gemTex,
                    drawPos,
                    null,
                    trailColor,
                    rotation,
                    gemTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体宝石
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.8f + musicPulse * 0.4f; //节奏脉动

            //绘制多层光晕效果 - 随音乐脉动
            for (int i = 0; i < 4; i++) {
                float glowScale = Projectile.scale * (1.2f + i * 0.25f + musicPulse * 0.2f) * pulse;
                float glowAlpha = (0.5f - i * 0.1f) * fadeAlpha;

                Color glowColor = Color.Lerp(gemColor, Color.White, i * 0.2f) * glowAlpha;

                Main.EntitySpriteDraw(
                    gemTex,
                    mainDrawPos,
                    null,
                    glowColor,
                    Projectile.rotation + i * 0.15f,
                    gemTex.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体
            Color mainColor = Color.White * fadeAlpha;
            Main.EntitySpriteDraw(
                gemTex,
                mainDrawPos,
                null,
                mainColor,
                Projectile.rotation,
                gemTex.Size() / 2f,
                Projectile.scale * (1.0f + musicPulse * 0.1f),
                SpriteEffects.None,
                0
            );

            //绘制节奏性核心闪光
            Color coreColor = Color.White * pulse * 0.9f * fadeAlpha;
            Main.EntitySpriteDraw(
                gemTex,
                mainDrawPos,
                null,
                coreColor,
                Projectile.rotation + MathHelper.PiOver4,
                gemTex.Size() / 2f,
                Projectile.scale * 0.7f * (0.8f + musicPulse * 0.4f),
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);
            return Color.Lerp(lightColor, gemColor, 0.8f);
        }
    }

    /// <summary>
    /// 宝石碎片弹幕
    /// </summary>
    internal class JewelFragmentProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float GemType => ref Projectile.ai[0];
        private ref float Time => ref Projectile.localAI[0];

        private float rotationSpeed = 0f;
        private float rhythmPhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            rotationSpeed = Main.rand.NextFloat(0.4f, 0.6f) * Main.rand.NextBool().ToDirectionInt();
            rhythmPhase = Main.rand.NextFloat(MathHelper.TwoPi); //随机节奏相位
        }

        public override void AI() {
            Time++;

            //节奏性旋转
            Projectile.rotation += rotationSpeed;

            //节奏性追踪
            float rhythmPulse = (float)Math.Sin(Time * 0.4f + rhythmPhase) * 0.5f + 0.5f;

            if (Time > 15 && Time < 120) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float homingStrength = 0.02f + rhythmPulse * 0.03f; //节奏性追踪强度
                    Projectile.velocity = Vector2.Lerp(
                        Projectile.velocity,
                        toTarget.SafeNormalize(Vector2.Zero) * Projectile.velocity.Length(),
                        homingStrength
                    );
                }
            }

            //节奏性照明
            Color gemColor = FishJewel.GetGemColor((int)GemType);
            float lightPulse = 0.6f + rhythmPulse * 0.6f;
            Lighting.AddLight(Projectile.Center,
                gemColor.R / 255f * lightPulse,
                gemColor.G / 255f * lightPulse,
                gemColor.B / 255f * lightPulse);

            //节奏性粒子效果
            bool isBeat = (int)(Time / 12f) != (int)((Time - 1) / 12f);
            if (Main.rand.NextBool(isBeat ? 2 : 6)) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    -Projectile.velocity * 0.4f,
                    100,
                    gemColor,
                    Main.rand.NextFloat(0.9f, 1.4f) * (isBeat ? 1.4f : 1.0f)
                );
                trail.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            //小型击中音效
            float[] fragmentPitches = new float[] { 0.5f, 0.6f, 0.7f, 0.75f, 0.85f, 0.95f };
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.4f,
                Pitch = fragmentPitches[(int)GemType]
            }, Projectile.Center);

            //小型击中特效
            for (int i = 0; i < 10; i++) {
                Dust shard = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    Main.rand.NextVector2Circular(5f, 5f),
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                shard.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //获取宝石纹理
            int gemItemID = FishJewel.GetGemItemID((int)GemType);
            Texture2D gemTex = TextureAssets.Item[gemItemID].Value;
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            float fadeAlpha = 1f - Projectile.alpha / 255f;
            float rhythmPulse = (float)Math.Sin(Time * 0.4f + rhythmPhase) * 0.5f + 0.5f;

            //绘制节奏性残影拖尾
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = gemColor * progress * (0.4f + rhythmPulse * 0.3f) * fadeAlpha;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float scale = Projectile.scale * 0.6f * (0.5f + progress * 0.5f);

                Main.EntitySpriteDraw(
                    gemTex,
                    drawPos,
                    null,
                    trailColor,
                    Projectile.oldRot[i],
                    gemTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.7f + rhythmPulse * 0.5f;

            //节奏性光晕效果
            for (int i = 0; i < 2; i++) {
                float glowScale = Projectile.scale * (1.1f + i * 0.2f + rhythmPulse * 0.15f) * pulse;
                Color glowColor = Color.Lerp(gemColor, Color.White, i * 0.35f) * (0.35f - i * 0.1f) * fadeAlpha;

                Main.EntitySpriteDraw(
                    gemTex,
                    mainDrawPos,
                    null,
                    glowColor,
                    Projectile.rotation,
                    gemTex.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //主体
            Main.EntitySpriteDraw(
                gemTex,
                mainDrawPos,
                null,
                Color.White * fadeAlpha,
                Projectile.rotation,
                gemTex.Size() / 2f,
                Projectile.scale * 0.75f * (0.9f + rhythmPulse * 0.2f),
                SpriteEffects.None,
                0
            );

            //核心闪光
            Main.EntitySpriteDraw(
                gemTex,
                mainDrawPos,
                null,
                Color.White * pulse * 0.7f * fadeAlpha,
                Projectile.rotation + MathHelper.PiOver4,
                gemTex.Size() / 2f,
                Projectile.scale * 0.5f * (0.8f + rhythmPulse * 0.4f),
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
