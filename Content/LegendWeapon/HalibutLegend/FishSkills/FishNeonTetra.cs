using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 霓虹脂鲤技能，行走时周期性出现发光鱼照明并伤害敌人
    /// </summary>
    internal class FishNeonTetra : FishSkill
    {
        public override int UnlockFishID => ItemID.NeonTetra;
        public override int DefaultCooldown => 10;
        public override int ResearchDuration => 60 * 12;

        private int walkTimer = 0;
        private const int WalkInterval = 18; //每18帧生成一次
        private Vector2 lastPlayerPosition = Vector2.Zero;

        public void UpdatePlayer(Player player) {
            if (Cooldown > 0) {
                return;
            }
            //检测玩家是否在移动
            float moveDistance = Vector2.Distance(player.Center, lastPlayerPosition);

            if (moveDistance > 1f) { //玩家正在移动
                walkTimer++;

                if (walkTimer >= WalkInterval) {
                    walkTimer = 0;
                    SetCooldown();
                    SpawnNeonTetra(player);
                }
            }
            else {
                //不移动时重置计时
                walkTimer = 0;
            }

            lastPlayerPosition = player.Center;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (Active(player)) {
                UpdatePlayer(player);
            }

            return base.UpdateCooldown(halibutPlayer, player);
        }

        private void SpawnNeonTetra(Player player) {
            if (Main.myPlayer != player.whoAmI) return;

            //在玩家周围随机位置生成霓虹脂鲤
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(40f, 80f);
            Vector2 spawnPos = player.Center + angle.ToRotationVector2() * distance;
            ShootState shootState = player.GetShootState();
            int neonProj = Projectile.NewProjectile(
                shootState.Source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<NeonTetraLightProjectile>(),
                (int)(shootState.WeaponDamage * (0.3f + HalibutData.GetDomainLayer() * 0.1f)),
                2f,
                player.whoAmI
            );

            if (neonProj >= 0) {
                Main.projectile[neonProj].netUpdate = true;
            }
        }
    }

    /// <summary>
    /// 霓虹脂鲤发光弹幕
    /// </summary>
    internal class NeonTetraLightProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.NeonTetra;

        private ref float Timer => ref Projectile.ai[0];
        private ref float PhaseOffset => ref Projectile.ai[1];

        private float glowIntensity = 0f;
        private float pulsePhase = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 10;

        //光照参数
        private const float LightRadius = 200f;
        private const int LifeTime = 120;
        private const float DamageRadius = 150f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 224;
            Projectile.height = 224;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;

            PhaseOffset = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            Timer++;
            pulsePhase += 0.15f;

            //淡入淡出效果
            float lifeProgress = Timer / LifeTime;
            if (lifeProgress < 0.2f) {
                //淡入
                float fadeIn = lifeProgress / 0.2f;
                Projectile.alpha = (int)(255 * (1f - fadeIn));
                glowIntensity = fadeIn;
            }
            else if (lifeProgress > 0.7f) {
                //淡出
                float fadeOut = (lifeProgress - 0.7f) / 0.3f;
                Projectile.alpha = (int)(255 * fadeOut);
                glowIntensity = 1f - fadeOut;
            }
            else {
                Projectile.alpha = 0;
                glowIntensity = 1f;
            }

            //轻微漂浮运动
            float floatX = (float)Math.Sin(pulsePhase * 0.8f + PhaseOffset) * 1.5f;
            float floatY = (float)Math.Cos(pulsePhase * 0.6f + PhaseOffset) * 1f;
            Projectile.velocity = new Vector2(floatX, floatY);

            //旋转
            Projectile.rotation += 0.05f;

            //青色光照
            float lightIntensity = glowIntensity * (0.8f + (float)Math.Sin(pulsePhase) * 0.2f);
            Lighting.AddLight(Projectile.Center, 0.2f * lightIntensity, 0.8f * lightIntensity, 1f * lightIntensity);

            //更新拖尾
            UpdateTrail();

            //生成青色粒子
            if (Main.rand.NextBool(5) && glowIntensity > 0.5f) {
                SpawnNeonParticle();
            }

            //照明和伤害范围内的敌人
            if (Timer % 15 == 0) {
                ApplyLightingEffect();
            }
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private void SpawnNeonParticle() {
            Color neonColor = new Color(50, 200, 255);

            BasePRT neon = new PRT_Light(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                Main.rand.NextVector2Circular(2f, 2f),
                0.6f,
                neonColor,
                20,
                1f,
                hueShift: 0.01f
            );
            PRTLoader.AddParticle(neon);
        }

        private void ApplyLightingEffect() {
            //对范围内的敌人造成照明效果（显示位置）和伤害
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;

                float distance = Vector2.Distance(Projectile.Center, npc.Center);

                if (distance < LightRadius) {
                    //照明效果显示敌人位置
                    Lighting.AddLight(npc.Center, 0.3f, 1f, 1.2f);

                    //在敌人头顶生成照明标记
                    if (Main.rand.NextBool(3)) {
                        SpawnLightMark(npc);
                    }
                }
            }
        }

        private void SpawnLightMark(NPC npc) {
            Color markColor = new Color(100, 220, 255);

            Dust mark = Dust.NewDustPerfect(
                npc.Center + new Vector2(0, -npc.height / 2 - 10),
                DustID.BlueCrystalShard,
                Vector2.Zero,
                0,
                markColor,
                1.2f
            );
            mark.noGravity = true;
            mark.fadeIn = 1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中时产生青色爆发
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Color neonColor = new Color(50, 200, 255);

                BasePRT neon = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.7f,
                    neonColor,
                    25,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(neon);
            }

            //青色水花
            for (int i = 0; i < 5; i++) {
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    new Color(100, 220, 255),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                splash.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //消散效果
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Color neonColor = new Color(50, 200, 255);

                BasePRT fade = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    Main.rand.NextFloat(0.5f, 0.8f),
                    neonColor,
                    30,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(fade);
            }

            //青色水花
            for (int i = 0; i < 8; i++) {
                Dust splash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    new Color(100, 220, 255),
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                splash.noGravity = Main.rand.NextBool();
            }

            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.3f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.NeonTetra].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;

            //绘制青色拖尾
            DrawNeonTrail(sb, fishTex, origin, alpha);

            //绘制青色光晕
            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.3f + i * 0.2f);
                float glowAlpha = glowIntensity * (1f - i * 0.3f) * 0.5f * alpha;
                Color glowColor = new Color(50, 200, 255, 0);

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

            //主体绘制 - 青色调
            Color mainColor = Color.Lerp(
                lightColor,
                new Color(100, 220, 255),
                glowIntensity * 0.7f
            );

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor * alpha,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //脉冲光效
            float pulseIntensity = 0.5f + (float)Math.Sin(pulsePhase) * 0.3f;
            sb.Draw(
                fishTex,
                drawPos,
                null,
                new Color(150, 230, 255, 0) * pulseIntensity * glowIntensity * alpha,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale * 1.1f,
                SpriteEffects.None,
                0
            );

            //白色核心高光
            sb.Draw(
                fishTex,
                drawPos,
                null,
                Color.White * glowIntensity * 0.6f * alpha,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale * 0.8f,
                SpriteEffects.None,
                0
            );

            return false;
        }

        private void DrawNeonTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, float alpha) {
            if (trailPositions.Count < 2) return;

            for (int i = 1; i < trailPositions.Count; i++) {
                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * alpha * 0.6f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.5f, 1f, progress);

                Color trailColor = Color.Lerp(
                    new Color(30, 150, 200),
                    new Color(100, 220, 255),
                    progress
                ) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    Projectile.rotation - i * 0.1f + MathHelper.PiOver4,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
}
