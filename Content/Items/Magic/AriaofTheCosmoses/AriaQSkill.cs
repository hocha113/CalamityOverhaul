using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// Q技能星流环带：单quad星环+6颗星卫结点,结点轮转弹射追踪星屑
    internal class AriaQSkill : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int NodeCount = 6;
        private const int Lifetime = 600;
        private const int SpawnTime = 22;
        private const int FadeTime = 40;
        private const int AttackInterval = 10;
        private const float BaseOrbitR = 170f;

        private float nodePhase;
        private float spinPhase;
        private float visTime;
        private int attackTimer;
        private int nextNode;

        /// <summary>0出场→1稳态→0退场</summary>
        private float lifeEnvelope;
        private float OrbitR => BaseOrbitR * (0.25f + 0.75f * lifeEnvelope)
            * (1f + 0.02f * (float)Math.Sin(visTime * MathHelper.TwoPi * 1.2f));

        private float QuadSide => (BaseOrbitR + 90f) * 2f;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
        }

        private Vector2 NodeWorldPos(int i)
            => Projectile.Center + (nodePhase + MathHelper.TwoPi * i / NodeCount).ToRotationVector2() * OrbitR;

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead
                || player.HeldItem.type != ModContent.ItemType<AriaofTheCosmos>()) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = player.Center;
            visTime += 1f / 60f;
            nodePhase += 0.032f;
            spinPhase += 0.5f / 60f;

            //出场展开/退场收拢包络
            int age = Lifetime - Projectile.timeLeft;
            if (age < SpawnTime) {
                lifeEnvelope = VaultUtils.EaseOutCubic(age / (float)SpawnTime);
                if (age == 1 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item109 with { Volume = 0.85f, Pitch = 0.35f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Volume = 0.7f, Pitch = 0.1f }, Projectile.Center);
                }
            }
            else if (Projectile.timeLeft < FadeTime) {
                lifeEnvelope = VaultUtils.EaseInQuad(Projectile.timeLeft / (float)FadeTime);
            }
            else {
                lifeEnvelope = 1f;
            }

            //结点轮转攻击
            attackTimer++;
            if (attackTimer >= AttackInterval && lifeEnvelope > 0.9f) {
                attackTimer = 0;
                TryNodeAttack();
            }

            //环带碎星飘尘
            if (!VaultUtils.isServer && Projectile.timeLeft % 4 == 0 && lifeEnvelope > 0.3f) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * (OrbitR + Main.rand.NextFloat(-14f, 14f));
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(1f, 2.4f),
                    Color.Lerp(AccretionDisk.ColGold, AccretionDisk.ColHot, Main.rand.NextFloat()) * 0.75f,
                    Main.rand.NextFloat(0.4f, 0.8f))?.Configure(false, Main.rand.Next(10, 18), Main.player[Projectile.owner]);
            }

            float pulse = 0.7f + 0.3f * (float)Math.Sin(visTime * MathHelper.TwoPi * 1.2f);
            Lighting.AddLight(Projectile.Center, AccretionDisk.ColGold.ToVector3() * pulse * lifeEnvelope * 0.8f);
        }

        private void TryNodeAttack() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            NPC target = Projectile.Center.FindClosestNPC(1300f);
            if (target == null) {
                return;
            }

            //轮转结点弹射
            Vector2 nodePos = NodeWorldPos(nextNode);
            nextNode = (nextNode + 1) % NodeCount;

            Vector2 vel = (target.Center - nodePos).SafeNormalize(Vector2.UnitX) * 15f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), nodePos, vel,
                ModContent.ProjectileType<AriaQSkillMiniDisk>(),
                (int)(Projectile.damage * 0.7f), Projectile.knockBack * 0.6f, Projectile.owner,
                target.whoAmI);

            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = 0.75f, MaxInstances = 4 }, nodePos);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(nodePos, vel.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.15f, 0.4f),
                        AccretionDisk.ColHot, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(8, 14));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);

            //结点碎裂成星屑
            for (int i = 0; i < NodeCount; i++) {
                Vector2 nodePos = NodeWorldPos(i);
                for (int j = 0; j < 6; j++) {
                    PRTLoader.NewParticle<PRT_Spark>(nodePos, Main.rand.NextVector2Circular(6f, 6f),
                        Color.Lerp(AccretionDisk.ColGold, AccretionDisk.ColHot, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.6f, 1.1f))?.Configure(false, Main.rand.Next(12, 22));
                }
            }
        }

        //=================== 绘制 ===================

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (VaultUtils.isServer || lifeEnvelope <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.AriaStarRing?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            if (effect == null || noise == null || white == null) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float side = QuadSide;
            Vector2 quadScale = new(side / white.Width, side / white.Height);

            Matrix finalMatrix = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

            effect.Parameters["transformMatrix"]?.SetValue(finalMatrix);
            effect.Parameters["uTime"]?.SetValue(visTime);
            effect.Parameters["uFade"]?.SetValue(lifeEnvelope);
            effect.Parameters["uRingN"]?.SetValue(OrbitR / side);
            effect.Parameters["uRingW"]?.SetValue(17f / side);
            effect.Parameters["uNodePhase"]?.SetValue(nodePhase);
            effect.Parameters["uSpin"]?.SetValue(spinPhase);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["noiseTexture"]?.SetValue(noise);

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Ring"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, drawPos, null, Color.White, 0f, white.Size() * 0.5f, quadScale, SpriteEffects.None, 0);
            sb.End();
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (lifeEnvelope <= 0.02f) {
                return;
            }
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (star == null || glow == null) {
                return;
            }

            //结点星卫本体：星芒+光晕
            for (int i = 0; i < NodeCount; i++) {
                Vector2 pos = NodeWorldPos(i) - Main.screenPosition;
                float flick = 1f + 0.14f * (float)Math.Sin(visTime * MathHelper.TwoPi * 3f + i * 1.7f);
                float readyGlow = i == nextNode ? 1.25f : 1f;

                Main.EntitySpriteDraw(glow, pos, null, AccretionDisk.ColGold * (0.5f * lifeEnvelope), 0f,
                    glow.Size() / 2f, 0.16f * flick * readyGlow, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, pos, null, AccretionDisk.ColHot * (0.95f * lifeEnvelope),
                    visTime * 2.4f + i, star.Size() / 2f, 0.15f * flick * readyGlow, SpriteEffects.None, 0);
            }
        }
    }

    /// Q技能星屑：结点弹射的追踪光矢
    internal class AriaQSkillMiniDisk : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        public ref float TargetNPCIndex => ref Projectile.ai[0];

        private float visTime;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 200;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            visTime += 1f / 60f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //追踪：目标失效则重寻
            NPC target = null;
            if (TargetNPCIndex >= 0 && TargetNPCIndex < Main.maxNPCs) {
                NPC candidate = Main.npc[(int)TargetNPCIndex];
                if (candidate.active && candidate.CanBeChasedBy(Projectile)) {
                    target = candidate;
                }
            }
            if (target == null) {
                target = Projectile.Center.FindClosestNPC(500f);
                if (target != null) {
                    TargetNPCIndex = target.whoAmI;
                }
            }
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 19f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.1f);
            }

            //星尘拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.25f),
                    Color.Lerp(AccretionDisk.ColGold, AccretionDisk.ColHot, Main.rand.NextFloat()) * 0.8f,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(false, Main.rand.Next(8, 14));
            }

            Lighting.AddLight(Projectile.Center, AccretionDisk.ColGold.ToVector3() * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.55f, MaxInstances = 5 }, target.Center);

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(5f, 5f),
                        Color.Lerp(AccretionDisk.ColGold, AccretionDisk.ColHot, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.5f, 1f))?.Configure(false, Main.rand.Next(10, 18));
                }
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                    AccretionDisk.ColGold, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(12, 18), opacity: 1.3f, squishStrenght: 1.8f, hueShift: 0.01f);
            }

            Projectile.damage = (int)(Projectile.damage * 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (star == null || glow == null) {
                return;
            }

            //速度方向拉长的彗星残影
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(glow, pos, null, AccretionDisk.ColGold * (0.25f * k * k), 0f,
                    glow.Size() / 2f, 0.09f * k, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float flick = 1f + 0.15f * (float)Math.Sin(visTime * MathHelper.TwoPi * 5f + Projectile.whoAmI);
            Main.EntitySpriteDraw(glow, drawPos, null, AccretionDisk.ColGold * 0.55f, 0f,
                glow.Size() / 2f, 0.15f * flick, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, AccretionDisk.ColHot * 0.95f,
                Projectile.rotation + MathHelper.PiOver4, star.Size() / 2f,
                new Vector2(0.24f, 0.13f) * flick, SpriteEffects.None, 0);
        }
    }
}
