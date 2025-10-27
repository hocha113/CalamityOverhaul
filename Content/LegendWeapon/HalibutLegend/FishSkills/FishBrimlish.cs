using CalamityMod;
using CalamityMod.Items.Fishing.BrimstoneCragCatches;
using CalamityMod.Dusts;
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
    /// ��ǻ��㼼�ܣ�����ʱ�ٻ���ǻ���������������
    /// </summary>
    internal class FishBrimlish : FishSkill
    {
        public override int UnlockFishID => ModContent.ItemType<Brimlish>();
        public override int DefaultCooldown => 20 - HalibutData.GetDomainLayer();
        public override int ResearchDuration => 60 * 14;

        private int shootCounter = 0;
        private const int ShootInterval = 8; //ÿ8�ο��𴥷�һ��

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            shootCounter++;

            if (shootCounter >= ShootInterval && Cooldown <= 0) {
                shootCounter = 0;
                SetCooldown();

                //���������ٻ���ǻ���
                SpawnBrimfishSpitter(player, source, damage, knockback);
            }

            return null;
        }

        private void SpawnBrimfishSpitter(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //����Һ�����
            Vector2 behindPlayer = player.Center - new Vector2(player.direction * 120f, 60f);

            int brimfishProj = Projectile.NewProjectile(
                source,
                behindPlayer,
                Vector2.Zero,
                ModContent.ProjectileType<BrimfishSpitterProjectile>(),
                (int)(damage * (0.8f + HalibutData.GetDomainLayer() * 0.2f)),
                knockback,
                player.whoAmI
            );

            if (brimfishProj >= 0) {
                Main.projectile[brimfishProj].netUpdate = true;
            }

            //��ǻ��ٻ���Ч
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, behindPlayer);
        }
    }

    /// <summary>
    /// ��ǻ�����������Ļ
    /// </summary>
    internal class BrimfishSpitterProjectile : ModProjectile
    {
        public override string Texture => "CalamityMod/Items/Fishing/BrimstoneCragCatches/Brimlish";

        private enum FishState
        {
            Appearing,   //����
            Charging,    //����
            Spitting,    //����
            Fading       //��ʧ
        }

        private ref float StateRaw => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeProgress => ref Projectile.localAI[0];

        private FishState State {
            get => (FishState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private float glowIntensity = 0f;
        private float pulsePhase = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 12;

        //״̬����ʱ��
        private const int AppearDuration = 15;
        private const int ChargeDuration = 25;
        private const int SpitDuration = 40;
        private const int FadeDuration = 20;

        //��������
        private const float SearchRange = 1200f;
        private const int FlameCount = 12; //�����������

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = AppearDuration + ChargeDuration + SpitDuration + FadeDuration + 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => false; //�㱾������˺���ֻ�л�������˺�

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            pulsePhase += 0.2f;

            //״̬��
            switch (State) {
                case FishState.Appearing:
                    AppearingBehavior(owner);
                    break;
                case FishState.Charging:
                    ChargingBehavior(owner);
                    break;
                case FishState.Spitting:
                    SpittingBehavior(owner);
                    break;
                case FishState.Fading:
                    FadingBehavior();
                    break;
            }

            //������β
            UpdateTrail();

            //��ǻ𻷾�����
            float pulse = (float)Math.Sin(pulsePhase) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.8f * pulse * glowIntensity, 0.2f * pulse * glowIntensity, 0.1f * pulse * glowIntensity);

            //��ǻ𻷾�����
            if (glowIntensity > 0.3f && Main.rand.NextBool(4)) {
                SpawnBrimstoneAmbient();
            }

            //��ת����Ŀ��
            if (State == FishState.Charging || State == FishState.Spitting) {
                if (IsTargetValid()) {
                    NPC target = Main.npc[targetNPCID];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.rotation = MathHelper.Lerp(
                        Projectile.rotation,
                        toTarget.ToRotation() + MathHelper.PiOver4,
                        0.15f
                    );
                }
            }
        }

        private void AppearingBehavior(Player owner) {
            float progress = StateTimer / AppearDuration;

            //����
            Projectile.alpha = (int)(255 * (1f - progress));
            glowIntensity = progress;
            Projectile.scale = progress;

            //��΢Ư��
            float floatY = (float)Math.Sin(pulsePhase * 0.8f) * 2f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.1f);

            //����ʱ����Ч��
            if (Main.rand.NextBool(3)) {
                SpawnAppearDust();
            }

            if (StateTimer >= AppearDuration) {
                State = FishState.Charging;
                StateTimer = 0;

                //����Ŀ��
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                }
            }
        }

        private void ChargingBehavior(Player owner) {
            float progress = StateTimer / ChargeDuration;
            ChargeProgress = progress;

            //����ʱ����ǿ������
            glowIntensity = 0.6f + progress * 0.4f;

            //��΢Ư��
            float floatY = (float)Math.Sin(pulsePhase * 1.2f) * 3f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.1f);

            //�������ſ�Ч����ͨ������ģ�⣩
            Projectile.scale = 1f + progress * 0.3f;

            //����ʱ����������ǻ�����
            if (Main.rand.NextBool(2)) {
                SpawnChargeDust();
            }

            //������Ч
            if (StateTimer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with {
                    Volume = 0.3f * progress,
                    Pitch = -0.5f + progress * 0.3f
                }, Projectile.Center);
            }

            if (StateTimer >= ChargeDuration) {
                State = FishState.Spitting;
                StateTimer = 0;

                //��ʼ����
                SpitBrimstoneFlames(owner);

                //������Ч
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.9f,
                    Pitch = -0.3f
                }, Projectile.Center);
            }
        }

        private void SpittingBehavior(Player owner) {
            float progress = StateTimer / SpitDuration;

            //����ʱ����ǿ�ҷ���
            glowIntensity = 1f - progress * 0.3f;

            //����ʱ������Ч��
            if (IsTargetValid()) {
                NPC target = Main.npc[targetNPCID];
                Vector2 toTarget = target.Center - Projectile.Center;
                Vector2 recoil = -toTarget.SafeNormalize(Vector2.Zero) * (1f - progress) * 2f;
                Projectile.Center += recoil * 0.1f;
            }

            //����Ư��
            float floatY = (float)Math.Sin(pulsePhase) * 2f;
            Projectile.Center = Projectile.Center + new Vector2(0, floatY * 0.05f);

            //����ʱ�������ɻ�������
            if (Main.rand.NextBool(2)) {
                SpawnSpitEffect();
            }

            if (StateTimer >= SpitDuration) {
                State = FishState.Fading;
                StateTimer = 0;
            }
        }

        private void FadingBehavior() {
            float progress = StateTimer / FadeDuration;

            //����
            Projectile.alpha = (int)(255 * progress);
            glowIntensity = 1f - progress;
            Projectile.scale = 1f - progress * 0.5f;

            //�����³�
            Projectile.velocity.Y += 0.2f;

            if (StateTimer >= FadeDuration) {
                Projectile.Kill();
            }
        }

        private void SpitBrimstoneFlames(Player owner) {
            if (!IsTargetValid() || Main.myPlayer != Projectile.owner) return;

            NPC target = Main.npc[targetNPCID];

            //������λ������
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 20f;
            Vector2 toTarget = (target.Center - mouthPos).SafeNormalize(Vector2.Zero);

            //�������λ���
            for (int i = 0; i < FlameCount; i++) {
                float spreadAngle = MathHelper.Lerp(-0.5f, 0.5f, i / (float)(FlameCount - 1));
                Vector2 velocity = toTarget.RotatedBy(spreadAngle) * Main.rand.NextFloat(12f, 18f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    mouthPos,
                    velocity,
                    ModContent.ProjectileType<BrimstoneFlameProjectile>(),
                    Projectile.damage,
                    2f,
                    Projectile.owner
                );
                Main.projectile[proj].friendly = true;
            }

            //���䱬����Ч
            for (int i = 0; i < 40; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.8f) * Main.rand.NextFloat(8f, 20f);
                Dust brimstone = Dust.NewDustPerfect(
                    mouthPos,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.6f;
            }

            //������ı���
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.6f) * Main.rand.NextFloat(6f, 16f);
                Dust fire = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.Torch,
                    velocity,
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                fire.noGravity = true;
            }
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        //��Ч����
        private void SpawnBrimstoneAmbient() {
            Dust brimstone = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                (int)CalamityDusts.Brimstone,
                Main.rand.NextVector2Circular(1.5f, 1.5f),
                0,
                default,
                Main.rand.NextFloat(1f, 1.5f)
            );
            brimstone.noGravity = true;
        }

        private void SpawnAppearDust() {
            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2f)
                );
                brimstone.noGravity = true;
            }
        }

        private void SpawnChargeDust() {
            //����λ��
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 15f;

            for (int i = 0; i < 3; i++) {
                Vector2 velocity = -Projectile.rotation.ToRotationVector2().RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 5f);
                Dust brimstone = Dust.NewDustPerfect(
                    mouthPos + Main.rand.NextVector2Circular(10f, 10f),
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.5f;
            }

            //��ɫ�������
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.Torch,
                    -Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(1f, 3f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }
        }

        private void SpawnSpitEffect() {
            Vector2 mouthPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 20f;

            for (int i = 0; i < 2; i++) {
                Vector2 velocity = Projectile.rotation.ToRotationVector2().RotatedByRandom(0.4f) * Main.rand.NextFloat(6f, 12f);
                Dust brimstone = Dust.NewDustPerfect(
                    mouthPos,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.4f;
            }

            //����
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustPerfect(
                    mouthPos,
                    DustID.Torch,
                    Projectile.rotation.ToRotationVector2() * Main.rand.NextFloat(4f, 8f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2f, 3f)
                );
                fire.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //��ɢЧ��
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
            }

            //�������
            for (int i = 0; i < 15; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(5f, 5f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.5f,
                Pitch = -0.5f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            float drawRot = Projectile.rotation;
            float alpha = (255f - Projectile.alpha) / 255f;

            //������ǻ���β
            DrawBrimstoneTrail(sb, fishTex, origin, alpha);

            //��ǻ𷢹��
            if (glowIntensity > 0.5f) {
                for (int i = 0; i < 4; i++) {
                    float glowScale = Projectile.scale * (1.2f + i * 0.15f);
                    float glowAlpha = (glowIntensity - 0.5f) * (1f - i * 0.25f) * 0.7f * alpha;

                    sb.Draw(
                        fishTex,
                        drawPos,
                        null,
                        new Color(255, 100, 50, 0) * glowAlpha,
                        drawRot,
                        origin,
                        glowScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //�����Թ�Ч��
            if (State == FishState.Charging) {
                float chargeGlow = ChargeProgress;
                for (int i = 0; i < 3; i++) {
                    float chargeScale = Projectile.scale * (1.3f + i * 0.2f);
                    float chargeAlpha = chargeGlow * (1f - i * 0.3f) * 0.6f * alpha;

                    sb.Draw(
                        fishTex,
                        drawPos,
                        null,
                        new Color(255, 80, 40, 0) * chargeAlpha,
                        drawRot,
                        origin,
                        chargeScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //������� - ��ǻ���ɫ��
            Color mainColor = Color.Lerp(
                lightColor,
                new Color(255, 120, 60),
                glowIntensity * 0.6f
            );

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor * alpha,
                drawRot,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //������ǻ�Ч��
            float pulseIntensity = 0.5f + (float)Math.Sin(pulsePhase) * 0.3f;
            sb.Draw(
                fishTex,
                drawPos,
                null,
                new Color(255, 140, 70, 0) * pulseIntensity * glowIntensity * alpha,
                drawRot,
                origin,
                Projectile.scale * 1.1f,
                SpriteEffects.None,
                0
            );

            //���Ⱥ���
            if (glowIntensity > 0.7f) {
                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    Color.White * glowIntensity * 0.5f * alpha,
                    drawRot,
                    origin,
                    Projectile.scale * 0.8f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        private void DrawBrimstoneTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, float alpha) {
            if (trailPositions.Count < 2) return;

            for (int i = 1; i < trailPositions.Count; i++) {
                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * alpha * 0.6f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.6f, 1f, progress);

                //��ǻ𽥱�ɫ
                Color trailColor = Color.Lerp(
                    new Color(200, 60, 30),
                    new Color(255, 140, 70),
                    progress
                ) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    Projectile.rotation - i * 0.08f,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }

    /// <summary>
    /// ��ǻ��浯Ļ
    /// </summary>
    internal class BrimstoneFlameProjectile : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float Timer => ref Projectile.ai[0];
        private float rotationSpeed = 0f;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;

            rotationSpeed = Main.rand.NextFloat(-0.3f, 0.3f);
        }

        public override void AI() {
            Timer++;

            //����
            Projectile.velocity *= 0.98f;

            //��΢׷������ĵ���
            if (Timer % 15 == 0 && Timer < 60) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.8f;

                    if (Projectile.velocity.Length() > 20f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    }
                }
            }

            //��ת
            Projectile.rotation += rotationSpeed;

            //��ǻ����
            Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.1f);

            //��ǻ����ӹ켣
            if (Main.rand.NextBool(2)) {
                Dust brimstone = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    (int)CalamityDusts.Brimstone,
                    0, 0, 0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                brimstone.velocity = -Projectile.velocity * 0.3f;
                brimstone.noGravity = true;
            }

            //����β��
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Torch,
                    0, 0, 0,
                    Color.Red,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                fire.velocity = -Projectile.velocity * 0.2f;
                fire.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //��ǻ���б���
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                brimstone.noGravity = true;
            }

            //���汬��
            for (int i = 0; i < 8; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(5f, 5f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //��ɢ����
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                brimstone.noGravity = true;
            }

            //����
            for (int i = 0; i < 10; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(6f, 6f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //�򵥵ķ����������
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //�����ǻ�Թ�
            for (int i = 0; i < 3; i++) {
                float scale = 0.4f + i * 0.15f;
                float alpha = (1f - i * 0.3f) * 0.8f;

                Main.spriteBatch.Draw(
                    glowTex,
                    drawPos,
                    null,
                    new Color(255, 100, 50, 0) * alpha,
                    Projectile.rotation,
                    glowTex.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0
                );
            }

            //��������
            Main.spriteBatch.Draw(
                glowTex,
                drawPos,
                null,
                Color.White with { A = 0 } * 0.9f,
                Projectile.rotation,
                glowTex.Size() / 2f,
                0.25f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
