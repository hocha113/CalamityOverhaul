using InnoVault.GameContent.BaseEntity;
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
    internal class FishWyverntail : FishSkill
    {
        public override int UnlockFishID => ItemID.Wyverntail;
        public override int DefaultCooldown => 60 * (25 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 35;

        private static bool PlayerHasController(Player player) {
            int type = ModContent.ProjectileType<WhiteWyvernTailController>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI && Main.projectile[i].type == type) {
                    return true;
                }
            }
            return false;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown > 0) {
                return null;
            }

            if (!PlayerHasController(player)) {
                int proj = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<WhiteWyvernTailController>(), 0, 0f, player.whoAmI,
                    ai0: damage, ai1: knockback);
                if (proj >= 0) {
                    SpawnSummonEffect(player.Center);
                }
                SetCooldown();
            }
            return null;
        }

        private static void SpawnSummonEffect(Vector2 position) {
            //召唤云雾特效
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f);
                var cloudParticle = new PRT_CloudParticle(position + Main.rand.NextVector2Circular(20f, 20f),
                    vel, Main.rand.NextFloat(0.8f, 1.5f), new Color(240, 250, 255), 45);
                PRTLoader.AddParticle(cloudParticle);
            }

            //白色火花
            for (int i = 0; i < 22; i++) {
                float angle = MathHelper.TwoPi * i / 22f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                Dust d = Dust.NewDustPerfect(position, DustID.WhiteTorch, vel, 100,
                    new Color(200, 230, 255), Main.rand.NextFloat(1.2f, 1.9f));
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            //银色闪光
            for (int i = 0; i < 12; i++) {
                Dust shard = Dust.NewDustDirect(position - new Vector2(12), 24, 24,
                    DustID.SilverFlame, Scale: Main.rand.NextFloat(1f, 1.6f));
                shard.velocity = Main.rand.NextVector2Circular(4f, 4f);
                shard.noGravity = true;
            }
        }
    }

    internal class WhiteWyvernTailController : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        private static int BaseLifeTime => 60 * (10 + HalibutData.GetDomainLayer());
        private ref float BaseDamage => ref Projectile.ai[0];
        private ref float BaseKnockback => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.tileCollide = false;
            Projectile.timeLeft = BaseLifeTime;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || !FishSkill.GetT<FishWyverntail>().Active(Owner)) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Owner.Center + new Vector2(0, -40);
            Timer++;
            int layer = HalibutData.GetDomainLayer(Owner);
            int interval = Math.Clamp(120 - layer * 8, 35, 120);
            int batch = 1 + layer / 4;

            if (Timer % interval == 0) {
                NPC target = Owner.Center.FindClosestNPC(1400f);
                Vector2 firePos = Projectile.Center + Main.rand.NextVector2Circular(24f, 24f);

                for (int i = 0; i < batch; i++) {
                    Vector2 dir;
                    if (target != null && target.active && target.CanBeChasedBy()) {
                        Vector2 predictive = target.Center + target.velocity * 18f;
                        dir = (predictive - firePos).SafeNormalize(Vector2.UnitY);
                    }
                    else {
                        dir = Main.rand.NextVector2Unit();
                    }

                    float speed = Main.rand.NextFloat(12f, 16f) + layer * 0.8f;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), firePos, dir * speed,
                            ModContent.ProjectileType<MiniWhiteWyvern>(),
                            (int)(BaseDamage * (1.6f + layer * 0.4f)),
                            BaseKnockback * 0.35f,
                            Owner.whoAmI,
                            ai0: target?.whoAmI ?? -1);
                        if (proj >= 0) {
                            Main.projectile[proj].scale = 0.9f + layer * 0.03f;
                        }
                    }
                }

                //发射云雾效果
                SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 0.6f, Pitch = -0.2f }, firePos);
                for (int k = 0; k < 25; k++) {
                    Vector2 v = Main.rand.NextVector2Circular(6f, 6f);
                    var cloudParticle = new PRT_CloudParticle(firePos + Main.rand.NextVector2Circular(15f, 15f),
                        v, Main.rand.NextFloat(0.6f, 1.2f), new Color(230, 245, 255), 35);
                    PRTLoader.AddParticle(cloudParticle);
                }

                for (int k = 0; k < 18; k++) {
                    Vector2 v = Main.rand.NextVector2Circular(5f, 5f);
                    Dust d = Dust.NewDustPerfect(firePos, DustID.WhiteTorch, v, 120,
                        new Color(220, 240, 255), Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.25f, 0.35f, 0.5f);
        }

        public override bool? CanDamage() => false;
        public override bool PreDraw(ref Color lightColor) => false;
    }

    internal class MiniWhiteWyvern : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.WyvernHead;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float SerpentinePhase => ref Projectile.ai[2];

        private NPC target;
        private float desiredRot;
        private float serpAmplitude;
        private float serpFrequency;

        //云雾尾迹粒子管理
        private readonly List<CloudTrailParticle> cloudTrail = new();
        private int cloudSpawnTimer = 0;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = Main.npcFrameCount[NPCID.WyvernHead];
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 50;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60 * 8;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => true;

        public override void AI() {
            StateTimer++;
            SerpentinePhase += 0.15f;
            cloudSpawnTimer++;

            //获取目标
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[(int)TargetIndex].active && Main.npc[(int)TargetIndex].CanBeChasedBy()) {
                target = Main.npc[(int)TargetIndex];
            }
            else {
                target = Projectile.Center.FindClosestNPC(1000f);
                if (target != null) TargetIndex = target.whoAmI;
            }

            //初始化蛇形参数
            if (StateTimer == 1) {
                serpAmplitude = Main.rand.NextFloat(12f, 24f);
                serpFrequency = Main.rand.NextFloat(1.2f, 2f);
            }

            //追踪逻辑
            if (target != null) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float dist = toTarget.Length();
                Vector2 dir = toTarget.SafeNormalize(Vector2.UnitX);

                //蛇形偏移
                Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
                float wave = (float)Math.Sin(SerpentinePhase * serpFrequency) * serpAmplitude *
                    MathHelper.Clamp(dist / 400f, 0.2f, 1f);
                Vector2 desiredVel = dir * MathHelper.Clamp(18f + dist * 0.01f, 12f, 32f) + normal * wave;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.15f);
                desiredRot = Projectile.velocity.ToRotation();

                if (dist < 40f) {
                    serpAmplitude *= 0.85f;
                }
            }
            else {
                Projectile.velocity *= 0.97f;
                desiredRot = Projectile.velocity.ToRotation();
            }

            Projectile.rotation = desiredRot;

            //帧动画
            int frameCount = Main.projFrames[Projectile.type];
            float animSpeed = MathHelper.Clamp(Projectile.velocity.Length() / 16f, 0.3f, 1.4f);
            if (++Projectile.frameCounter >= 6 / animSpeed) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= frameCount) Projectile.frame = 0;
            }

            //飞行云雾尾迹（每3帧生成一次）
            if (cloudSpawnTimer % 3 == 0 && !VaultUtils.isServer) {
                Vector2 trailPos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                trailPos += Main.rand.NextVector2Circular(8f, 8f);
                var cloudParticle = new PRT_CloudParticle(trailPos,
                    -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    Main.rand.NextFloat(0.7f, 1.3f),
                    new Color(235, 245, 255, 180),
                    Main.rand.Next(25, 40));
                PRTLoader.AddParticle(cloudParticle);
            }

            //白色火花粒子
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.WhiteTorch, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f,
                    120, new Color(220, 240, 255), Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }

            //银色闪光（速度快时）
            if (Projectile.velocity.Length() > 20f && Main.rand.NextBool(8)) {
                Dust shimmer = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.SilverFlame, Scale: Main.rand.NextFloat(0.9f, 1.4f));
                shimmer.velocity = -Projectile.velocity * 0.3f;
                shimmer.noGravity = true;
            }

            //淡出
            if (Projectile.timeLeft < 40) {
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, 1f - Projectile.timeLeft / 40f);
            }

            Lighting.AddLight(Projectile.Center, 0.35f, 0.45f, 0.65f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits > 0) {
                return;
            }
            //云雾爆炸特效
            if (!VaultUtils.isServer) {
                SpawnCloudExplosion(Projectile.Center, Projectile.velocity);
            }

            //原版Gore效果
            for (int i = 0; i < 15; i++) {
                int goreType = Main.rand.Next(11, 14); //原版云Gore
                Vector2 goreVel = Main.rand.NextVector2Circular(4f, 4f) + Projectile.velocity * 0.3f;
                Gore gore = Gore.NewGoreDirect(Projectile.GetSource_FromThis(), Projectile.Center,
                    goreVel, goreType, Projectile.scale);
                gore.timeLeft = Main.rand.Next(20, 35);
                gore.scale *= 3;
            }

            //白色粒子爆发
            for (int i = 0; i < 20; i++) {
                Vector2 v = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, v, 100,
                    new Color(255, 255, 255), Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = true;
            }

            //银色冲击波
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 9f);
                Dust shock = Dust.NewDustPerfect(Projectile.Center, DustID.SilverFlame, vel,
                    120, Color.White, Main.rand.NextFloat(1.3f, 1.8f));
                shock.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center); //云爆音效
            Projectile.Explode(180, default, false);
            Projectile.Kill();
        }

        ///<summary>
        ///生成云雾爆炸效果
        ///</summary>
        private static void SpawnCloudExplosion(Vector2 center, Vector2 impactVelocity) {
            //大型云雾爆炸（环形扩散）
            for (int ring = 0; ring < 3; ring++) {
                int count = 12 + ring * 8;
                float baseRadius = 30f + ring * 20f;

                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count;
                    Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(baseRadius * 0.8f, baseRadius * 1.2f);
                    Vector2 velocity = offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 7f) +
                        impactVelocity * 0.2f;

                    var cloudParticle = new PRT_CloudParticle(
                        center + offset * 0.3f,
                        velocity,
                        Main.rand.NextFloat(1f, 2f) + ring * 0.3f,
                        new Color(240, 250, 255, 200 - ring * 40),
                        Main.rand.Next(30, 50)
                    );
                    PRTLoader.AddParticle(cloudParticle);
                }
            }

            //中心浓云
            for (int i = 0; i < 15; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                var cloudParticle = new PRT_CloudParticle(
                    center + Main.rand.NextVector2Circular(10f, 10f),
                    vel + impactVelocity * 0.1f,
                    Main.rand.NextFloat(1.5f, 2.5f),
                    new Color(250, 255, 255, 220),
                    Main.rand.Next(40, 60)
                );
                PRTLoader.AddParticle(cloudParticle);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.WyvernHead);
            Texture2D vaule = TextureAssets.Npc[NPCID.WyvernHead].Value;
            int frameHeight = vaule.Height / Main.npcFrameCount[NPCID.WyvernHead];
            Rectangle source = new Rectangle(0, Projectile.frame * frameHeight, vaule.Width, frameHeight);
            Vector2 origin = source.Size() / 2f;
            float fade = 1f - Projectile.alpha / 255f;
            float drawOffset = MathHelper.PiOver2;

            //绘制云雾拖尾
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - i / (float)Projectile.oldPos.Length;
                Color trailColor = new Color(200, 220, 240) * progress * 0.4f * fade;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float rot = Projectile.oldRot[i] + drawOffset;
                Main.EntitySpriteDraw(vaule, drawPos, source, trailColor, rot, origin,
                    Projectile.scale * (0.9f + progress * 0.1f), SpriteEffects.None, 0);
            }

            //主体
            SpriteEffects spriteEffect = Projectile.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Main.EntitySpriteDraw(vaule, Projectile.Center - Main.screenPosition, source,
                lightColor * fade, Projectile.rotation + drawOffset, origin, Projectile.scale, spriteEffect, 0);

            //辉光层
            Color glow = new Color(255, 255, 255, 0) * 0.4f * fade;
            Main.EntitySpriteDraw(vaule, Projectile.Center - Main.screenPosition, source, glow,
                Projectile.rotation + drawOffset, origin, Projectile.scale * 1.05f, spriteEffect, 0);

            return false;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 255, 255, 200) * (1f - Projectile.alpha / 255f);
    }

    ///<summary>
    ///云雾粒子类
    ///</summary>
    internal class PRT_CloudParticle : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        private Vector2 velocity;
        private float scale;
        private Color color;
        private float rotationSpeed;
        private float currentRotation;

        public PRT_CloudParticle(Vector2 position, Vector2 velocity, float scale, Color color, int lifetime) {
            Position = position;
            this.velocity = velocity;
            this.scale = scale;
            this.color = color;
            Lifetime = lifetime;
            rotationSpeed = Main.rand.NextFloat(-0.08f, 0.08f);
            currentRotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Position += velocity;
            velocity *= 0.96f; //阻力
            scale *= 0.98f; //缩放衰减
            currentRotation += rotationSpeed;

            //透明度渐变
            float lifeProgress = (float)Time / Lifetime;
            float fadeIn = MathHelper.Clamp(lifeProgress * 3f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((1f - lifeProgress) * 2f, 0f, 1f);
            Color = color * fadeIn * fadeOut;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (FishCloud.Fog == null) return false;

            Texture2D fogTex = FishCloud.Fog;
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = fogTex.Size() / 2f;

            spriteBatch.Draw(fogTex, drawPos, null, Color, currentRotation, origin,
                scale * 0.8f, SpriteEffects.None, 0f);

            return false;
        }
    }

    ///<summary>
    ///云雾尾迹粒子
    ///</summary>
    internal class CloudTrailParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public Color Color;
        public int Life;
        public int MaxLife;
        public float Rotation;

        public CloudTrailParticle(Vector2 pos, Vector2 vel, float scale, Color color, int maxLife) {
            Position = pos;
            Velocity = vel;
            Scale = scale;
            Color = color;
            Life = 0;
            MaxLife = maxLife;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.95f;
            Scale *= 0.96f;
            Rotation += 0.05f;
        }

        public bool ShouldRemove() => Life >= MaxLife;
    }
}
