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
        public override int DefaultCooldown => 75 - HalibutData.GetDomainLayer() * 5;
        public override int ResearchDuration => 60 * 18;

        private int gemCycle = 0; //宝石循环计数
        private const int GemTypes = 6; //6种宝石类型

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                //循环切换宝石类型
                int currentGemType = gemCycle % GemTypes;
                gemCycle++;

                //生成主宝石弹幕
                SpawnMainGem(source, player, position, velocity, damage, knockback, currentGemType);

                //根据领域等级生成额外的宝石碎片
                int fragmentCount = 2 + HalibutData.GetDomainLayer() / 2;
                SpawnGemFragments(source, player, position, velocity, damage, knockback, fragmentCount, currentGemType);

                //播放华丽的宝石音效
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.7f,
                    Pitch = 0.3f + currentGemType * 0.1f
                }, position);

                //生成宝石召唤特效
                SpawnSummonEffect(position, currentGemType);

                SetCooldown();
            }

            return null;
        }

        private void SpawnMainGem(IEntitySource source, Player player, Vector2 position, 
            Vector2 velocity, int damage, float knockback, int gemType) {
            
            Projectile.NewProjectile(
                source,
                position,
                velocity,
                ModContent.ProjectileType<JewelGemProjectile>(),
                (int)(damage * (2.2f + HalibutData.GetDomainLayer() * 0.45f)),
                knockback * 1.2f,
                player.whoAmI,
                ai0: gemType
            );
        }

        private void SpawnGemFragments(IEntitySource source, Player player, Vector2 position,
            Vector2 velocity, int damage, float knockback, int count, int gemType) {
            
            for (int i = 0; i < count; i++) {
                float angleOffset = MathHelper.Lerp(-0.35f, 0.35f, i / (float)Math.Max(1, count - 1));
                Vector2 fragmentVel = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(0.85f, 1.15f);

                Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(20f, 20f),
                    fragmentVel,
                    ModContent.ProjectileType<JewelFragmentProjectile>(),
                    (int)(damage * (1.3f + HalibutData.GetDomainLayer() * 0.25f)),
                    knockback * 0.8f,
                    player.whoAmI,
                    ai0: gemType,
                    ai1: i
                );
            }
        }

        private void SpawnSummonEffect(Vector2 position, int gemType) {
            Color gemColor = GetGemColor(gemType);

            //璀璨的环形粒子
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust sparkle = Dust.NewDustPerfect(
                    position,
                    DustID.GemTopaz + gemType,
                    velocity,
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                sparkle.noGravity = true;
                sparkle.fadeIn = 1.4f;
            }

            //额外的闪光粒子
            for (int i = 0; i < 15; i++) {
                Dust glitter = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(30f, 30f),
                    DustID.Enchanted_Gold,
                    Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    Color.Lerp(gemColor, Color.White, 0.5f),
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                glitter.noGravity = true;
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

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
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

            //华丽的旋转
            rotationSpeed += 0.015f;
            Projectile.rotation += rotationSpeed;

            //轻微的波动运动
            float wave = (float)Math.Sin(Time * 0.15f) * 0.3f;
            Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perpendicular * wave;

            //璀璨照明
            Color gemColor = FishJewel.GetGemColor((int)GemType);
            float pulse = (float)Math.Sin(Time * 0.3f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 
                gemColor.R / 255f * pulse * 1.5f,
                gemColor.G / 255f * pulse * 1.5f,
                gemColor.B / 255f * pulse * 1.5f);

            //生成华丽粒子
            if (Main.rand.NextBool(3)) {
                SpawnGemParticles();
            }

            //速度衰减
            Projectile.velocity *= 0.995f;
        }

        private void SpawnGemParticles() {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.GemTopaz + (int)GemType,
                -Projectile.velocity * Main.rand.NextFloat(0.2f, 0.5f),
                100,
                gemColor,
                Main.rand.NextFloat(1.2f, 1.8f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.2f;

            //闪光粒子
            if (Main.rand.NextBool(4)) {
                Dust sparkle = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Enchanted_Gold,
                    Main.rand.NextVector2Circular(2f, 2f),
                    100,
                    Color.Lerp(gemColor, Color.White, 0.6f),
                    Main.rand.NextFloat(1f, 1.5f)
                );
                sparkle.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            //华丽的击中特效
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.6f,
                Pitch = 0.4f
            }, Projectile.Center);

            //爆发宝石碎片
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(7f, 7f);
                Dust shard = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    velocity,
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                shard.noGravity = true;
                shard.fadeIn = 1.3f;
            }

            //闪光爆发
            for (int i = 0; i < 12; i++) {
                Dust flash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Enchanted_Gold,
                    Main.rand.NextVector2Circular(5f, 5f),
                    100,
                    Color.White,
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                flash.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            //消失时的华丽爆破
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust explosion = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    velocity,
                    100,
                    gemColor,
                    Main.rand.NextFloat(1.5f, 2.2f)
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

            //绘制残影拖尾
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)Projectile.oldPos.Length;
                float trailAlpha = trailProgress * 0.6f * fadeAlpha;

                Color trailColor = Color.Lerp(gemColor, Color.White, trailProgress * 0.3f) * trailAlpha;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float rotation = Projectile.oldRot[i];
                float scale = Projectile.scale * (0.7f + trailProgress * 0.3f);

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
            float pulse = (float)Math.Sin(Time * 0.3f) * 0.15f + 0.85f;

            //绘制多层光晕效果
            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.3f + i * 0.2f) * pulse;
                float glowAlpha = (0.4f - i * 0.12f) * fadeAlpha;

                Color glowColor = Color.Lerp(gemColor, Color.White, i * 0.25f) * glowAlpha;

                Main.EntitySpriteDraw(
                    gemTex,
                    mainDrawPos,
                    null,
                    glowColor,
                    Projectile.rotation + i * 0.1f,
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
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //绘制核心闪光
            Color coreColor = Color.White * pulse * 0.7f * fadeAlpha;
            Main.EntitySpriteDraw(
                gemTex,
                mainDrawPos,
                null,
                coreColor,
                Projectile.rotation + MathHelper.PiOver4,
                gemTex.Size() / 2f,
                Projectile.scale * 0.6f,
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);
            return Color.Lerp(lightColor, gemColor, 0.7f);
        }
    }

    ///<summary>
    ///宝石碎片弹幕 - 小型快速宝石
    ///</summary>
    internal class JewelFragmentProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float GemType => ref Projectile.ai[0];
        private ref float Time => ref Projectile.localAI[0];

        private float rotationSpeed = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            rotationSpeed = Main.rand.NextFloat(0.3f, 0.5f) * Main.rand.NextBool().ToDirectionInt();
        }

        public override void AI() {
            Time++;

            //快速旋转
            Projectile.rotation += rotationSpeed;

            //轻微追踪
            if (Time > 15 && Time < 120) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity = Vector2.Lerp(
                        Projectile.velocity,
                        toTarget.SafeNormalize(Vector2.Zero) * Projectile.velocity.Length(),
                        0.03f
                    );
                }
            }

            //照明
            Color gemColor = FishJewel.GetGemColor((int)GemType);
            Lighting.AddLight(Projectile.Center,
                gemColor.R / 255f * 0.8f,
                gemColor.G / 255f * 0.8f,
                gemColor.B / 255f * 0.8f);

            //粒子效果
            if (Main.rand.NextBool(5)) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    -Projectile.velocity * 0.3f,
                    100,
                    gemColor,
                    Main.rand.NextFloat(0.8f, 1.2f)
                );
                trail.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Color gemColor = FishJewel.GetGemColor((int)GemType);

            //小型击中特效
            for (int i = 0; i < 8; i++) {
                Dust shard = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.GemTopaz + (int)GemType,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    gemColor,
                    Main.rand.NextFloat(1f, 1.5f)
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

            //绘制残影拖尾
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = gemColor * progress * 0.5f * fadeAlpha;

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
            float pulse = (float)Math.Sin(Time * 0.5f) * 0.2f + 0.8f;

            //光晕效果
            for (int i = 0; i < 2; i++) {
                float glowScale = Projectile.scale * (1.2f + i * 0.15f) * pulse;
                Color glowColor = Color.Lerp(gemColor, Color.White, i * 0.3f) * (0.3f - i * 0.1f) * fadeAlpha;

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
                Projectile.scale * 0.8f,
                SpriteEffects.None,
                0
            );

            //核心闪光
            Main.EntitySpriteDraw(
                gemTex,
                mainDrawPos,
                null,
                Color.White * pulse * 0.6f * fadeAlpha,
                Projectile.rotation + MathHelper.PiOver4,
                gemTex.Size() / 2f,
                Projectile.scale * 0.5f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
