using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    internal class MuraSlashDefault : BaseHeldProj
    {
        #region Data
        private const float SamScaleMultiplier = 1.65f;
        private const float NormalOffsetDistance = 80f;
        private const float NormalCollisionLength = 220f;
        private const float NormalCollisionWidth = 250f;
        private const float Slash3CollisionLength = 300f;
        private const float Slash3CollisionWidth = 270f;
        private const int HitboxInflateSize = 60;
        private const int FrameUpdateInterval = 3;
        private const int DefaultHitCooldown = 8;
        public override string Texture => CWRConstant.Cay_Proj_Melee + "MurasamaSlash";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName(MurasamaOverride.ID);
        public ref int HitCooldown => ref Owner.GetMurasamaHitCooldown();
        public bool onspan;
        public bool CanAttemptDead;
        public bool Slashing = false;
        public bool Slash1 => Projectile.frame == 10;
        public bool Slash2 => Projectile.frame == 0;
        public bool Slash3 => Projectile.frame == 6;
        #endregion

        #region 初始化
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 14;
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 216;
            Projectile.height = 216;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 6;
            Projectile.frameCounter = 0;
            Projectile.alpha = 255;
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.frameCounter <= 1) {
                return false;
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame(verticalFrames: Main.projFrames[Type], frameY: Projectile.frame);
            Vector2 origin = frame.Size() * 0.5f;
            SpriteEffects spriteEffects = Projectile.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Color drawColor = MurasamaOverride.NameIsVergil(Owner) ? Color.Blue : Color.White;

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + Projectile.velocity * 0.3f + new Vector2(0, -32).RotatedBy(Projectile.rotation),
                frame, drawColor, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);

            return false;
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(100, 0, 0, 0);
        }
        #endregion

        #region AI部分
        public override void AI() {
            Projectile.SetProjtimesPierced(0);

            if (!onspan) {
                InitializeProjectile();
            }

            UpdateSlashState();
            UpdateDamageModifiers();
            UpdateAnimation();
            UpdateLighting();
            HandlePlayerInput();
            UpdateRotationAndPosition();
        }

        private void InitializeProjectile() {
            Projectile.scale = MurasamaOverride.NameIsSam(Owner) ? SamScaleMultiplier : MurasamaOverride.GetOnScale(Item);
            Projectile.scale *= GetMuraSizeInMeleeSengs(Owner);
            Projectile.frame = Main.zenithWorld ? 6 : 10;
            Projectile.alpha = 0;
            onspan = true;
        }

        private void UpdateSlashState() {
            if (Slash2) {
                PlaySlashSound(-0.1f, 0.2f);
                SetSlashingState();
            }
            else if (Slash3) {
                PlayBigSlashSound();
                SetSlashingState();
            }
            else if (Slash1) {
                PlaySlashSound(-0.05f, 0.2f);
                SetSlashingState();
            }
            else {
                CanAttemptDead = false;
                Slashing = false;
            }
        }

        private void SetSlashingState() {
            if (HitCooldown == 0) {
                Slashing = true;
            }
            CanAttemptDead = true;
            Projectile.numHits = 0;
        }

        private void UpdateDamageModifiers() {
            if (Projectile.frame == 5 && Projectile.frameCounter % FrameUpdateInterval == 0) {
                Projectile.damage = Projectile.damage * 2;
            }
            if (Projectile.frame == 7 && Projectile.frameCounter % FrameUpdateInterval == 0) {
                Projectile.damage = (int)(Projectile.damage * 0.5f);
            }
        }

        private void UpdateAnimation() {
            Projectile.frameCounter++;
            if (Projectile.frameCounter % FrameUpdateInterval == 0) {
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        private void UpdateLighting() {
            Vector2 origin = Projectile.Center + Projectile.velocity * 3f;
            float lightIntensity = Slashing ? 3.5f : 2f;
            Lighting.AddLight(origin, Color.Red.ToVector3() * lightIntensity);
        }

        private void HandlePlayerInput() {
            Vector2 playerRotatedPoint = Owner.RotatedRelativePoint(Owner.MountedCenter, true);

            if (Projectile.IsOwnedByLocalPlayer()) {
                bool canChannel = Owner.channel && !Owner.noItems && !Owner.CCed
                    && Owner.ownedProjectileCounts[ModContent.ProjectileType<MuraTriggerDash>()] <= 0;

                if (canChannel) {
                    HandleChannelMovement(Owner, playerRotatedPoint);
                }
                else {
                    if (CanAttemptDead || Main.zenithWorld) {
                        HitCooldown = Main.zenithWorld ? 0 : DefaultHitCooldown;
                        Projectile.Kill();
                    }
                }
            }
        }

        private void UpdateRotationAndPosition() {
            float velocityAngle = Projectile.velocity.ToRotation();

            if (Slashing || Slash1) {
                Projectile.rotation = velocityAngle + (Projectile.direction == -1).ToInt() * MathHelper.Pi;
            }

            Projectile.direction = (Math.Cos(velocityAngle) > 0).ToDirectionInt();

            float offset = NormalOffsetDistance * Projectile.scale;
            Vector2 playerRotatedPoint = Owner.RotatedRelativePoint(Owner.MountedCenter, true);
            Projectile.Center = playerRotatedPoint + velocityAngle.ToRotationVector2() * offset;

            Owner.ChangeDir(Projectile.direction);
            Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        public void HandleChannelMovement(Player player, Vector2 playerRotatedPoint) {
            float speed = 1f;
            if (player.GetItem().shoot == Projectile.type) {
                speed = player.GetItem().shootSpeed * Projectile.scale;
            }
            Vector2 newVelocity = ToMouse.SafeNormalize(Vector2.UnitX * player.direction) * speed;

            if (Slashing) {
                if (Projectile.velocity.X != newVelocity.X || Projectile.velocity.Y != newVelocity.Y) {
                    Projectile.netUpdate = true;
                }
                Projectile.velocity = newVelocity;
            }
        }

        public static float GetMuraSizeInMeleeSengs(Player player) {
            Item mura = player.GetItem();
            if (mura.type == MurasamaOverride.ID) {
                return MathHelper.Clamp(player.GetAdjustedItemScale(mura), 0.5f, 1.5f);
            }
            return 1f;
        }
        #endregion

        #region 音效
        private void PlaySlashSound(float pitch, float volume) {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/MurasamaSwing".GetSound() with { Pitch = pitch, Volume = volume }, Projectile.Center);
        }

        private void PlayBigSlashSound() {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/MurasamaBigSwing".GetSound() with { Pitch = 0f, Volume = 0.25f }, Projectile.Center);
        }

        private void PlayHitSound(NPC target) {
            float pitch = Slash2 ? -0.1f : Slash3 ? 0.1f : Slash1 ? -0.15f : 0;

            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/MurasamaHitOrganic".GetSound() with { Pitch = pitch, Volume = 0.45f }, Projectile.Center);
            }
            else {
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/MurasamaHitInorganic".GetSound() with { Pitch = pitch, Volume = 0.55f }, Projectile.Center);
            }
        }
        #endregion

        #region 碰撞伤害
        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            if (Slash3) {
                hitbox.Inflate(HitboxInflateSize, HitboxInflateSize);
            }
        }

        public override bool? CanDamage() {
            return Slashing == false ? false : null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0;
            Vector2 unitOffset = new Vector2(0, -32).RotatedBy(Projectile.rotation);
            Vector2 orig = Owner.GetPlayerStabilityCenter() + unitOffset;

            float collisionLength = Slash3 ? Slash3CollisionLength : NormalCollisionLength;
            float collisionWidth = Slash3 ? Slash3CollisionWidth : NormalCollisionWidth;

            Vector2 endPos = orig + ToMouse.UnitVector() * (collisionLength * Projectile.scale);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                orig, endPos, collisionWidth * Projectile.scale, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            ApplyBaseDamageModifiers(target, ref modifiers);
            ApplyBossDamageModifiers(target, ref modifiers);
            ApplyWormDamageModifiers(target, ref modifiers);

            //无视防御
            modifiers.DefenseEffectiveness *= 0f;
        }

        private void ApplyBaseDamageModifiers(NPC target, ref NPC.HitModifiers modifiers) {
            //Boss存活时对非Boss单位造成2倍伤害
            if (CWRWorld.HasBoss && !target.boss) {
                modifiers.FinalDamage *= 2f;
            }

            //对蠕虫只造成50%伤害
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        private void ApplyBossDamageModifiers(NPC target, ref NPC.HitModifiers modifiers) {
            //对克苏鲁之眼造成1.5倍伤害
            if (target.type == NPCID.EyeofCthulhu) {
                modifiers.FinalDamage *= 1.5f;
            }

            //对红宝石、蓝宝石仅造成75%伤害
            if (target.type == CWRID.NPC_KingSlimeJewelRuby || target.type == CWRID.NPC_KingSlimeJewelSapphire) {
                modifiers.FinalDamage *= 0.75f;
            }

            //对飞眼怪仅造成25%伤害
            if (target.type == NPCID.Creeper) {
                modifiers.FinalDamage *= 0.25f;
            }

            //对血肉蠕虫仅造成66%伤害
            if (CWRLoad.targetNpcTypes4.Contains(target.type) || CWRLoad.targetNpcTypes5.Contains(target.type) || CWRLoad.targetNpcTypes17.Contains(target.type)) {
                modifiers.FinalDamage *= 0.66f;
            }

            //对骷髅王之手仅造成50%伤害
            if (target.type == NPCID.SkeletronHand) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对史神护卫仅造成50%伤害
            if (target.type == CWRID.NPC_EbonianPaladin || target.type == CWRID.NPC_CrimulanPaladin) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对史神小护卫仅造成25%伤害
            if (target.type == CWRID.NPC_SplitEbonianPaladin || target.type == CWRID.NPC_SplitCrimulanPaladin) {
                modifiers.FinalDamage *= 0.25f;
            }

            //对肉山造成1.5倍伤害
            if (target.type == NPCID.WallofFlesh) {
                modifiers.FinalDamage *= 1.5f;
            }

            //对肉山眼仅造成50%伤害
            if (target.type == NPCID.WallofFleshEye) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对双子魔眼造成1.5倍伤害
            if (target.type == NPCID.Retinazer || target.type == NPCID.Spazmatism) {
                modifiers.FinalDamage *= 1.5f;
            }

            //对灾眼兄弟仅造成50%伤害
            if (target.type == CWRID.NPC_Cataclysm || target.type == CWRID.NPC_Catastrophe) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对石巨人之拳仅造成50%伤害
            if (target.type == NPCID.GolemFistLeft || target.type == NPCID.GolemFistRight) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        private void ApplyWormDamageModifiers(NPC target, ref NPC.HitModifiers modifiers) {
            //对黄沙恶虫造成1.32倍伤害
            if (CWRLoad.targetNpcTypes9.Contains(target.type)) {
                modifiers.FinalDamage *= 1.32f;
            }

            //对渊海灾虫仅造成30%伤害
            if (CWRLoad.targetNpcTypes11.Contains(target.type)) {
                modifiers.FinalDamage *= 0.3f;
            }

            //对渊海灾虫体节仅造成10%伤害
            if (target.type == CWRID.NPC_AquaticScourgeBodyAlt) {
                modifiers.FinalDamage *= 0.1f;
            }

            //对毁灭魔像飞出的头仅造成50%伤害
            if (target.type == CWRID.NPC_RavagerHead2) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对毁灭魔像身体部位造成50%伤害
            if (target.type == CWRID.NPC_RavagerClawLeft || target.type == CWRID.NPC_RavagerClawRight || target.type == CWRID.NPC_RavagerHead
                || target.type == CWRID.NPC_RavagerLegLeft || target.type == CWRID.NPC_RavagerLegRight) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对暗能量仅造成50%伤害
            if (target.type == CWRID.NPC_DarkEnergy) {
                modifiers.FinalDamage *= 0.5f;
            }

            //对幽花复制体仅造成75%伤害
            if (target.type == CWRID.NPC_PolterghastHook) {
                modifiers.FinalDamage *= 0.75f;
            }

            //对塔纳托斯体节造成1.32倍伤害
            if (target.type == CWRID.NPC_ThanatosBody1 || target.type == CWRID.NPC_ThanatosBody2 || target.type == CWRID.NPC_ThanatosTail) {
                modifiers.FinalDamage *= 1.32f;
            }

            //对塔纳托斯头造成5.7倍伤害
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 5.7f;
            }

            //对神明吞噬者头尾、风编尾造成2倍伤害
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail
                || target.type == CWRID.NPC_StormWeaverTail) {
                modifiers.FinalDamage *= 2f;
            }

            //对星流双子造成1.66倍伤害
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.66f;
            }

            //对终灾造成1.66倍伤害
            if (target.type == CWRID.NPC_SupremeCalamitas) {
                modifiers.FinalDamage *= 1.66f;
            }
        }
        #endregion

        #region 打击效果
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            UpdateRisingDragonCharge();
            PlayHitSound(target);
            if (Projectile.numHits % 2 == 0) {
                SpawnHitParticles(target);
            }
            SpawnHitSparks(target);
            SpawnHitDust(target);
        }

        private void UpdateRisingDragonCharge() {
            if (Projectile.numHits == 0) {
                Owner.CWR().RisingDragonCharged += (int)(MurasamaOverride.GetOnRDCD(Item) / 10f);
                if (Owner.CWR().RisingDragonCharged > MurasamaOverride.GetOnRDCD(Item)) {
                    Owner.CWR().RisingDragonCharged = MurasamaOverride.GetOnRDCD(Item);
                }

                int type = ModContent.ProjectileType<MurasamaHeld>();
                foreach (var p in Main.projectile) {
                    if (p.type == type && p.ModProjectile is MurasamaHeld murasamaHeldProj) {
                        murasamaHeldProj.noAttenuationTime = 180;
                    }
                }
            }
        }

        private void SpawnHitParticles(NPC target) {
            for (int i = 0; i < 3; i++) {
                Color impactColor = Slash3 ? (Main.rand.NextBool(3) ? Color.LightCoral : Color.White) : (Main.rand.NextBool(4) ? Color.LightCoral : Color.Crimson);
                float impactParticleScale = Main.rand.NextFloat(0.6f, 0.85f);
                Vector2 particlePosition = target.Center + Main.rand.NextVector2Circular(target.width * 0.75f, target.height * 0.75f);

                if (Slash3) {
                    PRT_Sparkle impactParticle2 = new(particlePosition, Vector2.Zero, Color.White, Color.Red, impactParticleScale * 1.2f, 8, 0, 2.5f);
                    PRTLoader.AddParticle(impactParticle2);
                }

                PRT_Sparkle impactParticle = new(particlePosition, Vector2.Zero, impactColor, Color.Red, impactParticleScale, 8, 0, 1.5f);
                PRTLoader.AddParticle(impactParticle);
            }
        }

        private void SpawnHitSparks(NPC target) {
            float sparkCount = MathHelper.Clamp(Slash3 ? 18 - Projectile.numHits * 3 : 5 - Projectile.numHits * 2, 0, 18);
            float rotationOffset = Slash2 ? -0.45f * Owner.direction : Slash3 ? 0 : Slash1 ? 0.45f * Owner.direction : 0;

            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity = Projectile.velocity.RotatedBy(rotationOffset).RotatedByRandom(0.35f) * Main.rand.NextFloat(0.5f, 1.8f);
                int sparkLifetime = Main.rand.Next(23, 35);
                float sparkScale = Main.rand.NextFloat(0.95f, 1.8f);
                Color sparkColor = Slash3 ? (Main.rand.NextBool(3) ? Color.Red : Color.IndianRed) : (Main.rand.NextBool() ? Color.Red : Color.Firebrick);
                Vector2 sparkPosition = target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f) + Projectile.velocity * 1.2f;

                if (Main.rand.NextBool()) {
                    PRT_SparkAlpha spark = new(sparkPosition, sparkVelocity * (Slash3 ? 1f : 0.65f), false, (int)(sparkLifetime * (Slash3 ? 1.2f : 1f)), sparkScale * (Slash3 ? 1.4f : 1f), sparkColor);
                    PRTLoader.AddParticle(spark);
                }
                else {
                    PRT_Line spark = new(sparkPosition, sparkVelocity * (Slash3 ? 1f : 0.65f), false, (int)(sparkLifetime * (Slash3 ? 1.2f : 1f)), sparkScale * (Slash3 ? 0.86f : 0.6f), Main.rand.NextBool() ? Color.Red : Color.Firebrick);
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        private void SpawnHitDust(NPC target) {
            float dustCount = MathHelper.Clamp(Slash3 ? 25 - Projectile.numHits * 3 : 12 - Projectile.numHits * 2, 0, 25);
            float rotationOffset = Slash2 ? -0.45f * Owner.direction : Slash3 ? 0 : Slash1 ? 0.45f * Owner.direction : 0;

            for (int i = 0; i <= dustCount; i++) {
                int dustID = Main.rand.NextBool(3) ? 182 : (Main.rand.NextBool() ? (Slash3 ? 309 : 296) : 90);
                Vector2 dustPosition = target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f);
                Vector2 dustVelocity = Projectile.velocity.RotatedBy(rotationOffset).RotatedByRandom(0.55f) * Main.rand.NextFloat(0.3f, 1.1f);

                Dust dust = Dust.NewDustPerfect(dustPosition, dustID, dustVelocity);
                dust.scale = Main.rand.NextFloat(0.9f, 2.4f);
                dust.noGravity = true;
            }
        }
        #endregion
    }
}
