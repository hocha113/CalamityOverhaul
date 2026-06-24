using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
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
    internal class FishofCthulu : FishSkill
    {
        public override int UnlockFishID => ItemID.TheFishofCthulu;
        public override int DefaultCooldown => 60 * 11 - HalibutData.GetDomainLayer() / 2;
        public override int ResearchDuration => 60 * 25;

        private int EyesPerShot => 1 + HalibutData.GetDomainLayer() / 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown > 0) {
                return null;
            }

            for (int i = 0; i < EyesPerShot; i++) {
                float angleOffset = MathHelper.Lerp(-0.4f, 0.4f, i / (float)Math.Max(1, EyesPerShot - 1));
                Vector2 eyeVelocity = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(0.9f, 1.1f);

                Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(30f, 30f),
                    eyeVelocity,
                    ModContent.ProjectileType<CthulhuEye>(),
                    (int)(damage * (1.6f + HalibutData.GetDomainLayer() * 0.4f)),
                    knockback * 0.6f,
                    player.whoAmI,
                    ai0: i
                );
            }

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 3 }, position);
            SpawnSummonEffect(position);
            SetCooldown();

            return null;
        }

        private static void SpawnSummonEffect(Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 from = position + angle.ToRotationVector2() * Main.rand.NextFloat(40f, 80f);
                PRTLoader.NewParticle<PRT_Light>(from, (position - from) * 0.07f
                    , CthulhuEye.BloodRed, Main.rand.NextFloat(0.5f, 0.8f)).Configure(24, hueShift: 0.004f);
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(position + Main.rand.NextVector2Circular(20f, 20f)
                    , Main.rand.NextVector2Circular(3f, 3f), new Color(40, 12, 30), Main.rand.NextFloat(0.8f, 1.2f))
                    .Configure(26, 0.7f, 0.05f);
            }
        }
    }

    /// <summary>
    /// 克苏鲁之眼：以"长蓄势 + 瞬时冲刺 + 硬刹车"演出捕食感。冲刺前向后拉满、瞳孔放大、
    /// 影焰汇聚并打出瞄准预告线，随后影焰着色器拖尾贯穿目标。
    /// </summary>
    internal class CthulhuEye : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.EyeofCthulhu;

        private ref float EyeID => ref Projectile.ai[0];
        private ref float AIState => ref Projectile.ai[1];
        private ref float AITimer => ref Projectile.ai[2];

        private int targetNPC = -1;

        //环绕
        private float orbitAngle;
        private float orbitRadius;
        private float randOrbitRadius;
        private bool isOrbiting;
        private int orbitDuration;

        //冲刺
        private bool isDashing;
        private Vector2 dashDirection;
        private float dashSpeed;
        private int dashCooldown;
        private int totalDashes;
        private float dashWindCharge;   //蓄力 0-1（瞳孔/影焰强度）

        //朝向
        private float desiredRotation;
        private float rotationSpeed = 0.2f;
        private float pupilRotation;
        private float pupilDilate = 1f;

        //蓄力/动画
        private int noActionTimer;
        private float frameTransition;
        private int targetMinFrame;
        private float glow;
        private float trailOpacity;
        private Trail dashTrail;
        private readonly List<FishSkillVFX.ShockRing> rings = new();

        private const int MaxNoActionTime = 180;
        private const int MinOrbitTime = 60;
        private const int MaxOrbitTime = 150;
        private const float TransitionSpeed = 0.15f;
        private const int PreDashTime = 16;
        private const int PreDashFreeze = 5;   //冲刺前的"寂静"定格
        private const int PostDashTime = 20;

        public static readonly Color BloodRed = new(205, 45, 45);
        public static readonly Color ShadowGlow = new(120, 35, 130);

        private enum EyeState
        {
            Seeking,
            Orbiting,
            PreDash,
            Dashing,
            PostDash,
            Returning
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;

            orbitAngle = EyeID * MathHelper.TwoPi / 4f;
            randOrbitRadius = Main.rand.NextFloat(-20f, 20f);
        }

        public override bool? CanDamage() => AIState != (int)EyeState.Orbiting && AIState != (int)EyeState.Seeking;

        public override void AI() {
            AITimer++;
            noActionTimer++;
            if (dashCooldown > 0) {
                dashCooldown--;
            }

            EyeState currentState = (EyeState)AIState;
            switch (currentState) {
                case EyeState.Seeking:
                    targetMinFrame = 0;
                    SeekingAI();
                    break;
                case EyeState.Orbiting:
                    targetMinFrame = 0;
                    OrbitingAI();
                    break;
                case EyeState.PreDash:
                    targetMinFrame = 2;
                    PreDashAI();
                    break;
                case EyeState.Dashing:
                    targetMinFrame = 2;
                    DashingAI();
                    break;
                case EyeState.PostDash:
                    targetMinFrame = 2;
                    PostDashAI();
                    break;
                case EyeState.Returning:
                    targetMinFrame = 0;
                    ReturningAI();
                    break;
            }

            if (noActionTimer > MaxNoActionTime && currentState == EyeState.Orbiting
                && targetNPC >= 0 && Main.npc[targetNPC].active) {
                StartDash(Main.npc[targetNPC], true);
            }

            UpdateRotation();
            UpdatePupilRotation();
            UpdateFrameTransition();

            glow = MathHelper.Lerp(glow, isDashing ? 1.8f : (isOrbiting ? 0.7f : 0.4f), 0.2f);
            trailOpacity = MathHelper.Lerp(trailOpacity, isDashing ? 1f : 0f, isDashing ? 0.5f : 0.18f);

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Update();
                if (rings[i].Dead) {
                    rings.RemoveAt(i);
                }
            }

            if (!Main.dedServ && Main.rand.NextBool(isDashing ? 1 : 5)) {
                SpawnTrailParticle();
            }

            Lighting.AddLight(Projectile.Center, BloodRed.ToVector3() * glow * 0.6f);

            if (Projectile.timeLeft < 30) {
                Projectile.alpha = (int)((1f - Projectile.timeLeft / 30f) * 255);
            }
        }

        private void UpdateFrameTransition() {
            float targetTransition = targetMinFrame / 2f;
            frameTransition = MathHelper.Lerp(frameTransition, targetTransition, TransitionSpeed);
            int actualMinFrame = (int)Math.Round(frameTransition * 2);
            VaultUtils.ClockFrame(ref Projectile.frame, 5, actualMinFrame + 2, actualMinFrame);
        }

        private void SeekingAI() {
            if (targetNPC == -1 || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy()) {
                NPC npc = Projectile.Center.FindClosestNPC(1000f);
                if (npc != null && npc.CanBeChasedBy()) {
                    targetNPC = npc.whoAmI;
                }
            }

            if (targetNPC != -1) {
                AIState = (float)EyeState.Orbiting;
                AITimer = 0;
                orbitDuration = 0;
                isOrbiting = true;
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            }
            else {
                Projectile.velocity *= 0.98f;
                if (Projectile.velocity.LengthSquared() > 1f) {
                    desiredRotation = Projectile.velocity.ToRotation();
                }
            }
        }

        private void OrbitingAI() {
            if (targetNPC < 0 || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy()) {
                AIState = (float)EyeState.Seeking;
                targetNPC = -1;
                isOrbiting = false;
                return;
            }

            NPC target = Main.npc[targetNPC];
            orbitDuration++;

            float orbitSpeed = 0.08f + HalibutData.GetDomainLayer() * 0.01f;
            orbitAngle += orbitSpeed;
            orbitRadius = target.width / 2f + 40f + randOrbitRadius;
            Vector2 idealPosition = target.Center + orbitAngle.ToRotationVector2() * orbitRadius;

            Vector2 toIdeal = idealPosition - Projectile.Center;
            float distance = toIdeal.Length();
            if (distance > 20f) {
                float targetSpeed = Math.Min(distance * 0.2f, 16f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toIdeal.SafeNormalize(Vector2.Zero) * targetSpeed, 0.2f);
            }
            else {
                Projectile.velocity *= 0.95f;
            }

            desiredRotation = (target.Center - Projectile.Center).ToRotation();
            pupilDilate = MathHelper.Lerp(pupilDilate, 1f, 0.1f);

            if (ShouldDash(target)) {
                StartDash(target);
            }
        }

        private void PreDashAI() {
            AITimer++;
            bool freeze = AITimer >= PreDashTime - PreDashFreeze;

            //向后拉满（反向蓄势）；冻结段彻底定住，制造"寂静"
            if (!freeze) {
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, -dashDirection * 6f, 0.25f);
            }
            else {
                Projectile.velocity *= 0.55f;
            }

            desiredRotation = dashDirection.ToRotation();
            dashWindCharge = MathHelper.Clamp(AITimer / (float)PreDashTime, 0f, 1f);
            pupilDilate = MathHelper.Lerp(pupilDilate, 1.7f, 0.25f);//瞳孔放大锁定

            //影焰汇聚 + 上升音高
            if (!freeze && !Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2Circular(46f, 46f);
                PRTLoader.NewParticle<PRT_Light>(from, (Projectile.Center - from) * 0.12f, ShadowGlow, 0.5f)
                    .Configure(16, hueShift: 0.01f);
            }
            if (AITimer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            }

            if (AITimer >= PreDashTime) {
                AIState = (float)EyeState.Dashing;
                AITimer = 0;
                isDashing = true;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.4f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.7f, Pitch = 0.7f }, Projectile.Center);
            }
        }

        private bool ShouldDash(NPC target) {
            if (dashCooldown > 0 || AITimer < 20) {
                return false;
            }
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);
            if (distanceToTarget < 80f || distanceToTarget > 450f) {
                return false;
            }
            return Main.rand.NextFloat() < CalculateDashChance(target, distanceToTarget);
        }

        private float CalculateDashChance(NPC target, float distanceToTarget) {
            float baseChance = 0.02f;
            if (orbitDuration > MinOrbitTime) {
                baseChance += Math.Min((orbitDuration - MinOrbitTime) / 90f, 0.5f);
            }
            if (orbitDuration > MaxOrbitTime) {
                return 1f;
            }
            if (distanceToTarget > 150f && distanceToTarget < 300f) {
                baseChance += 0.15f;
            }
            float targetSpeed = target.velocity.Length();
            if (targetSpeed > 5f) {
                baseChance += Math.Min(targetSpeed / 50f, 0.1f);
            }
            if (totalDashes < 3) {
                baseChance += 0.05f;
            }
            baseChance += HalibutData.GetDomainLayer() * 0.01f;
            Vector2 toTarget = target.Center - Projectile.Center;
            if (Vector2.Dot(toTarget.SafeNormalize(Vector2.Zero), target.velocity.SafeNormalize(Vector2.Zero)) > 0.5f) {
                baseChance += 0.1f;
            }
            return Math.Clamp(baseChance, 0f, 1f);
        }

        private void StartDash(NPC target, bool forced = false) {
            AIState = (float)EyeState.PreDash;
            AITimer = 0;
            totalDashes++;
            noActionTimer = 0;
            dashWindCharge = 0f;

            float predictionFactor = forced ? 25f : 20f;
            Vector2 predictedPos = target.Center + target.velocity * predictionFactor;
            dashDirection = (predictedPos - Projectile.Center).SafeNormalize(Vector2.Zero);
            dashSpeed = (forced ? 27f : 23f) + HalibutData.GetDomainLayer() * 2f;
            desiredRotation = dashDirection.ToRotation();
            dashCooldown = (forced ? 110 : 90) - HalibutData.GetDomainLayer() * 6;
            orbitDuration = 0;
        }

        private void DashingAI() {
            AITimer++;

            if (AITimer < 30) {
                if (AITimer < 8) {
                    dashSpeed *= 1.1f;//瞬时拉满
                }
                else if (AITimer < 18) {
                    dashSpeed *= 0.99f;
                }
                else {
                    dashSpeed *= 0.9f;//减速尾段
                }

                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dashDirection * dashSpeed, 0.35f);
                desiredRotation = dashDirection.ToRotation();
                pupilDilate = MathHelper.Lerp(pupilDilate, 1.4f, 0.2f);

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.2f
                        , Color.Lerp(BloodRed, ShadowGlow, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.2f)).Configure(false, 14);
                }
            }
            else {
                AIState = (float)EyeState.PostDash;
                AITimer = 0;
                isDashing = false;
            }
        }

        private void PostDashAI() {
            AITimer++;
            Projectile.velocity *= 0.88f;//硬刹车
            pupilDilate = MathHelper.Lerp(pupilDilate, 1f, 0.12f);
            if (AITimer >= PostDashTime) {
                AIState = (float)EyeState.Returning;
                AITimer = 0;
            }
        }

        private void ReturningAI() {
            if (targetNPC < 0 || !Main.npc[targetNPC].active || !Main.npc[targetNPC].CanBeChasedBy()) {
                AIState = (float)EyeState.Seeking;
                targetNPC = -1;
                return;
            }

            NPC target = Main.npc[targetNPC];
            orbitRadius = target.width / 2f + 40f + randOrbitRadius;
            Vector2 orbitPosition = target.Center + orbitAngle.ToRotationVector2() * orbitRadius;
            Vector2 toOrbit = orbitPosition - Projectile.Center;
            float distanceToOrbit = toOrbit.Length();

            float returnSpeed = distanceToOrbit > 200f ? Math.Min(distanceToOrbit * 0.15f, 18f)
                : distanceToOrbit > 80f ? Math.Min(distanceToOrbit * 0.12f, 12f)
                : Math.Min(distanceToOrbit * 0.1f, 8f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toOrbit.SafeNormalize(Vector2.Zero) * returnSpeed, 0.15f);
            desiredRotation = (target.Center - Projectile.Center).ToRotation();

            if ((distanceToOrbit < orbitRadius * 1.2f && Projectile.velocity.Length() < 10f) || AITimer > 120) {
                AIState = (float)EyeState.Orbiting;
                AITimer = 0;
                orbitDuration = 0;
                isOrbiting = true;
                noActionTimer = 0;
            }
        }

        private void UpdateRotation() {
            float angleDiff = MathHelper.WrapAngle(desiredRotation - Projectile.rotation);
            float currentRotSpeed = isDashing ? 0.4f : (isOrbiting ? 0.15f : rotationSpeed);
            Projectile.rotation = MathHelper.WrapAngle(Projectile.rotation + angleDiff * currentRotSpeed);
        }

        private void UpdatePupilRotation() {
            Vector2 look = targetNPC >= 0 && Main.npc[targetNPC].active
                ? Main.npc[targetNPC].Center - Projectile.Center
                : Main.MouseWorld - Projectile.Center;
            pupilRotation = look.ToRotation();
        }

        private void SpawnTrailParticle() {
            Color c = Color.Lerp(BloodRed, ShadowGlow, Main.rand.NextFloat(0.4f));
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                , -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.3f), c, Main.rand.NextFloat(0.35f, 0.6f))
                .Configure(16, hueShift: 0.006f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            noActionTimer = 0;
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.2f }, Projectile.Center);

            bool wasDash = isDashing || AIState == (float)EyeState.PreDash || AIState == (float)EyeState.PostDash;
            if (wasDash) {
                target.AddBuff(BuffID.ShadowFlame, 180);
                FishSkillVFX.Punch(Owner, 4f);
                if (!Main.dedServ) {
                    rings.Add(new FishSkillVFX.ShockRing(target.Center, 90f, 9f, BloodRed, 1f, 18, 30));
                }
            }

            if (!Main.dedServ) {
                for (int i = 0; i < 12; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center, Main.rand.NextVector2Circular(7f, 7f)
                        , Color.Lerp(BloodRed, ShadowGlow, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.3f)).Configure(true, 22);
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(target.Center, new Vector2(0, -Main.rand.NextFloat(1f, 3f))
                        , new Color(45, 14, 34), Main.rand.NextFloat(0.9f, 1.4f)).Configure(26, 0.7f, 0.05f);
                }
            }

            if (isDashing) {
                AIState = (float)EyeState.PostDash;
                isDashing = false;
                AITimer = 0;
            }
            else if (AIState == (float)EyeState.PreDash) {
                AIState = (float)EyeState.Returning;
                AITimer = 0;
            }
        }

        private Player Owner => Main.player[Projectile.owner];

        private bool TrailVisible => trailOpacity > 0.05f;

        public float TrailWidth(float c) => MathHelper.Lerp(34f, 4f, c) * trailOpacity;

        public Color TrailColor(Vector2 _) => Color.White * trailOpacity;

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.EyeofCthulhu);
            Texture2D texture = TextureAssets.Npc[NPCID.EyeofCthulhu].Value;
            int frameHeight = texture.Height / Main.npcFrameCount[NPCID.EyeofCthulhu];
            Rectangle src = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = src.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = 1f - Projectile.alpha / 255f;
            float rot = Projectile.rotation - MathHelper.PiOver2;
            float baseScale = Projectile.scale * 0.6f;

            //冲刺瞄准预告线（蓄力越满越亮）
            if (dashWindCharge > 0.05f && (EyeState)AIState == EyeState.PreDash) {
                Texture2D line = CWRAsset.LightShot.Value;
                float telAlpha = dashWindCharge * dashWindCharge * fade;
                Main.spriteBatch.Draw(line, drawPos, null, BloodRed with { A = 0 } * (telAlpha * 0.7f)
                    , dashDirection.ToRotation(), new Vector2(0f, line.Height / 2f)
                    , new Vector2(2.4f * dashWindCharge, 0.5f), SpriteEffects.None, 0f);
            }

            //外发光
            Color glowColor = Color.Lerp(BloodRed, Color.White, MathHelper.Clamp(glow - 1f, 0f, 1f)) with { A = 0 };
            Main.spriteBatch.Draw(texture, drawPos, src, glowColor * (glow * 0.45f * fade), rot, origin, baseScale * 1.18f, SpriteEffects.None, 0f);

            //本体
            Main.spriteBatch.Draw(texture, drawPos, src, lightColor * fade, rot, origin, baseScale, SpriteEffects.None, 0f);

            //瞳孔（追踪 + 蓄力放大）
            DrawPupil(drawPos, fade, baseScale);

            //影焰着色器拖尾 + 命中冲击环
            bool drawTrail = TrailVisible && BuildTrail();
            bool drawRings = rings.Count > 0;
            if (drawTrail || drawRings) {
                Main.spriteBatch.End();
                if (drawTrail) {
                    Effect gradient = EffectLoader.GradientTrail?.Value;
                    if (gradient != null) {
                        FishSkillVFX.ApplyGradientTrail(gradient, CWRAsset.BloodRed_Bar.Value, CWRAsset.LightShot.Value, 0.1f);
                        Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                        dashTrail.DrawTrail(gradient);
                        Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
                    }
                }
                if (drawRings) {
                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp
                        , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Texture2D ringTex = CWRAsset.Placeholder_White.Value;
                    foreach (FishSkillVFX.ShockRing r in rings) {
                        r.Draw(ringTex);
                    }
                    Main.spriteBatch.End();
                }
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        private void DrawPupil(Vector2 drawPos, float fade, float baseScale) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 offset = pupilRotation.ToRotationVector2() * 7f * pupilDilate;
            Vector2 pupilPos = drawPos + offset;

            //血色虹膜光
            Main.spriteBatch.Draw(glow, pupilPos, null, BloodRed with { A = 0 } * (0.6f * fade)
                , 0f, glow.Size() / 2f, baseScale * 0.5f * pupilDilate, SpriteEffects.None, 0f);
            //暗色瞳孔（用乘色软光收一个深核）
            Main.spriteBatch.Draw(glow, pupilPos, null, new Color(20, 0, 10) * (0.9f * fade)
                , 0f, glow.Size() / 2f, baseScale * 0.32f * pupilDilate, SpriteEffects.None, 0f);
            //高光点
            Main.spriteBatch.Draw(glow, pupilPos - offset * 0.3f, null, Color.White with { A = 0 } * (0.5f * fade)
                , 0f, glow.Size() / 2f, baseScale * 0.12f, SpriteEffects.None, 0f);
        }

        private bool BuildTrail() {
            if (Main.dedServ || Projectile.oldPos == null || Projectile.oldPos.Length < 4) {
                return false;
            }
            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                Vector2 old = Projectile.oldPos[i];
                positions[i] = old == Vector2.Zero ? Projectile.Center : old + Projectile.Size * 0.5f;
            }
            dashTrail ??= new Trail(positions, TrailWidth, TrailColor);
            dashTrail.TrailPositions = positions;
            return true;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(255, 200, 200, 200) * (1f - Projectile.alpha / 255f);
    }
}
