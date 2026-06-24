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
    /// <summary>公主鱼技能：召唤优雅的彩虹咏唱者，蓄力后扇射追踪魔弹</summary>
    internal class FishPrincess : FishSkill
    {
        public override int UnlockFishID => ItemID.PrincessFish;
        public override int DefaultCooldown => 50 - HalibutData.GetDomainLayer() * 2;
        public override int ResearchDuration => 60 * 22;

        private static readonly List<int> ActivePrincessFish = new();
        private static int MaxPrincessFish => 3 + HalibutData.GetDomainLayer() / 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFish();

                if (ActivePrincessFish.Count < MaxPrincessFish) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(200f, 300f);
                    Vector2 spawnPos = player.Center + angle.ToRotationVector2() * distance;

                    int fishProj = Projectile.NewProjectile(
                        source,
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<PrincessFishMinion>(),
                        (int)(damage * (0.2f + HalibutData.GetDomainLayer() * 0.05f)),
                        knockback * 1.5f,
                        player.whoAmI,
                        ai2: ActivePrincessFish.Count
                    );

                    if (fishProj >= 0 && fishProj < Main.maxProjectiles) {
                        ActivePrincessFish.Add(fishProj);
                        SpawnSummonEffect(spawnPos);
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.4f }, spawnPos);
                        SoundEngine.PlaySound(SoundID.Item82 with { Volume = 0.5f, Pitch = 0.3f }, spawnPos);
                    }
                }
            }

            return null;
        }

        private static void CleanupInactiveFish() {
            ActivePrincessFish.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<PrincessFishMinion>();
            });
        }

        private static void SpawnSummonEffect(Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                Color c = Main.hslToRgb((i / 24f + Main.GlobalTimeWrappedHourly * 0.3f) % 1f, 1f, 0.65f);
                PRTLoader.NewParticle<PRT_Light>(position, angle.ToRotationVector2() * Main.rand.NextFloat(4f, 9f), c, 0.7f)
                    .Configure(28, hueShift: 0.02f);
            }
            for (int i = 0; i < 14; i++) {
                Color c = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f);
                PRTLoader.NewParticle<PRT_Spark>(position, Main.rand.NextVector2Circular(8f, 8f), c, Main.rand.NextFloat(0.7f, 1.1f))
                    .Configure(false, Main.rand.Next(18, 28));
            }
        }
    }

    /// <summary>
    /// 公主鱼咏唱者：优雅环绕玩家，彩虹飘带（顶点绘制，逐顶点变色）尾随。
    /// 攻击采用"聚能蓄力 → 扇射 → 后坐回收"的演出节奏。
    /// </summary>
    internal class PrincessFishMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.PrincessFish;

        private enum FishState
        {
            Spawning,
            Following,
            Targeting,
            Attacking
        }

        private FishState State {
            get => (FishState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }
        private ref float AttackCooldown => ref Projectile.ai[1];
        private ref float FishIndex => ref Projectile.ai[2];
        private ref float StateTimer => ref Projectile.localAI[0];

        private int targetNPCID = -1;
        private Vector2 idleOffset;
        private float orbitAngle;
        private float floatPhase;
        private Vector2 castFocus;     //蓄力聚能点
        private Vector2 castRecoilVel; //咏唱后坐

        private float glowIntensity;
        private float rainbowHue;
        private float chargeUp;        //攻击蓄力 0-1
        private readonly List<Vector2> trail = new();
        private const int MaxTrailLength = 22;

        private const float SearchRange = 1400f;
        private const int AttackInterval = 90;
        private const int SpawningDuration = 20;
        private const int CastWindUp = 16;
        private const int CastTotal = 42;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            floatPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead || !FishSkill.GetT<FishPrincess>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            StateTimer++;

            switch (State) {
                case FishState.Spawning:
                    SpawningAI();
                    break;
                case FishState.Following:
                    FollowingAI(owner);
                    break;
                case FishState.Targeting:
                    TargetingAI(owner);
                    break;
                case FishState.Attacking:
                    AttackingAI(owner);
                    break;
            }

            //咏唱后坐弹簧
            Projectile.Center += castRecoilVel;
            castRecoilVel *= 0.82f;

            UpdateTrail();
            rainbowHue = (rainbowHue + 0.01f) % 1f;
            Lighting.AddLight(Projectile.Center, Main.hslToRgb(rainbowHue, 1f, 0.6f).ToVector3() * (0.6f + glowIntensity * 0.4f));

            if (!Main.dedServ && Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , Projectile.velocity * -0.2f + Main.rand.NextVector2Circular(1f, 1f)
                    , Main.hslToRgb(rainbowHue, 1f, 0.6f), 0.4f).Configure(18, hueShift: 0.03f);
            }

            if (AttackCooldown > 0) {
                AttackCooldown--;
            }
        }

        private void SpawningAI() {
            float p = StateTimer / SpawningDuration;
            Projectile.alpha = (int)((1f - p) * 255f);
            Projectile.scale = VaultUtils.EaseOutBack(MathHelper.Clamp(p, 0f, 1f));
            Projectile.velocity.Y = -2f * (1f - p);
            Projectile.velocity.X *= 0.9f;
            glowIntensity = p;

            if (StateTimer >= SpawningDuration) {
                State = FishState.Following;
                StateTimer = 0;
                Projectile.alpha = 0;
                Projectile.scale = 1f;
            }
        }

        private void FollowingAI(Player owner) {
            UpdateIdleOffset();
            orbitAngle += 0.02f;
            //利萨如曲线环绕，比正圆更优雅
            Vector2 orbitPos = owner.Center + new Vector2(
                (float)Math.Cos(orbitAngle + FishIndex) * 160f,
                (float)Math.Sin((orbitAngle + FishIndex * 0.7f) * 1.3f) * 96f) + idleOffset;

            Vector2 toTarget = orbitPos - Projectile.Center;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 0.08f, 0.2f);
            FaceVelocity(0.15f);

            glowIntensity = 0.6f + (float)Math.Sin(StateTimer * 0.1f) * 0.2f;
            chargeUp = MathHelper.Lerp(chargeUp, 0f, 0.2f);

            if (AttackCooldown <= 0) {
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = FishState.Targeting;
                    StateTimer = 0;
                }
            }
        }

        private void TargetingAI(Player owner) {
            if (!IsTargetValid()) {
                State = FishState.Following;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];
            Vector2 attackPos = target.Center + new Vector2(0, -210f);
            Vector2 toAttackPos = attackPos - Projectile.Center;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toAttackPos.SafeNormalize(Vector2.Zero) * 15f, 0.15f);

            FaceTarget(target, 0.2f);
            glowIntensity = 0.8f + (float)Math.Sin(StateTimer * 0.3f) * 0.2f;

            if (Vector2.Distance(Projectile.Center, attackPos) < 100f && StateTimer > 22) {
                State = FishState.Attacking;
                StateTimer = 0;
            }
        }

        private void AttackingAI(Player owner) {
            if (!IsTargetValid()) {
                State = FishState.Following;
                StateTimer = 0;
                AttackCooldown = AttackInterval;
                chargeUp = 0f;
                return;
            }

            NPC target = Main.npc[targetNPCID];
            Projectile.velocity *= 0.85f;
            FaceTarget(target, 0.25f);

            if (StateTimer <= CastWindUp) {
                //蓄力：在身前聚出能量焦点，光辉收紧
                chargeUp = StateTimer / CastWindUp;
                Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                castFocus = Projectile.Center + aim * 26f;
                glowIntensity = 1f + chargeUp * 0.8f;

                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 from = castFocus + Main.rand.NextVector2Circular(40f, 40f);
                    PRTLoader.NewParticle<PRT_Light>(from, (castFocus - from) * 0.14f
                        , Main.hslToRgb((rainbowHue + Main.rand.NextFloat(0.3f)) % 1f, 1f, 0.7f), 0.5f).Configure(14, hueShift: 0.03f);
                }
            }
            else if (StateTimer == CastWindUp + 1) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    LaunchMagicAttack(target);
                }
                //后坐 + 释放
                Vector2 aim = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                castRecoilVel = -aim * 9f;
                chargeUp = 0f;
                SpawnAttackEffect(aim);
            }
            else {
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.8f, 0.15f);
            }

            if (StateTimer >= CastTotal) {
                State = FishState.Following;
                StateTimer = 0;
                AttackCooldown = AttackInterval - HalibutData.GetDomainLayer() * 8;
            }
        }

        private void LaunchMagicAttack(NPC target) {
            int projectileCount = 3 + HalibutData.GetDomainLayer() / 4;
            Vector2 targetPos = target.Center + target.velocity * 20f;
            Vector2 baseDir = (targetPos - Projectile.Center).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < projectileCount; i++) {
                float spreadAngle = projectileCount > 1 ? MathHelper.Lerp(-0.32f, 0.32f, i / (float)(projectileCount - 1)) : 0f;
                Vector2 velocity = baseDir.RotatedBy(spreadAngle) * 18f;
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                    , ModContent.ProjectileType<PrincessMagicOrb>(), Projectile.damage, Projectile.knockBack
                    , Projectile.owner, ai0: i / (float)projectileCount);
                if (proj >= 0) {
                    Main.projectile[proj].netUpdate = true;
                }
            }

            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item43 with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
        }

        private void FaceVelocity(float lerp) {
            if (Projectile.velocity.LengthSquared() > 0.5f) {
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, Projectile.velocity.ToRotation(), lerp);
            }
        }

        private void FaceTarget(NPC target, float lerp) {
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, (target.Center - Projectile.Center).ToRotation(), lerp);
        }

        private void UpdateIdleOffset() {
            idleOffset.X = (float)Math.Sin(floatPhase * 0.8f) * 30f;
            idleOffset.Y = (float)Math.Cos(floatPhase * 0.6f) * 20f;
            floatPhase += 0.05f;
        }

        private void UpdateTrail() {
            trail.Insert(0, Projectile.Center);
            if (trail.Count > MaxTrailLength) {
                trail.RemoveAt(trail.Count - 1);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) {
                return false;
            }
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        private void SpawnAttackEffect(Vector2 aim) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 22; i++) {
                float a = MathHelper.Lerp(-0.9f, 0.9f, i / 21f);
                Color c = Main.hslToRgb((i / 22f + rainbowHue) % 1f, 1f, 0.6f);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, aim.RotatedBy(a) * Main.rand.NextFloat(5f, 10f), c, 0.7f)
                    .Configure(26, hueShift: 0.02f);
            }
            for (int i = 0; i < 10; i++) {
                Color c = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, aim.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(6f, 12f)
                    , c, Main.rand.NextFloat(0.7f, 1.1f)).Configure(false, 22);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 26; i++) {
                Color c = Main.hslToRgb((i / 26f + rainbowHue) % 1f, 1f, 0.6f);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(8f, 8f), c, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(28, hueShift: 0.02f);
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
        }

        public float TrailWidth(float t) => MathHelper.Lerp(26f, 2f, t) * Projectile.scale * (0.7f + glowIntensity * 0.3f);

        public Color TrailColor(float t) {
            Color c = Main.hslToRgb((rainbowHue + t * 0.55f) % 1f, 1f, 0.6f);
            return c * (1f - t) * 0.85f * ((255f - Projectile.alpha) / 255f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fishTex = TextureAssets.Item[ItemID.PrincessFish].Value;
            Vector2 origin = fishTex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fade = (255f - Projectile.alpha) / 255f;
            float rot = Projectile.rotation + MathHelper.PiOver4;

            //聚能焦点（蓄力时）
            if (chargeUp > 0.02f) {
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                Vector2 fp = castFocus - Main.screenPosition;
                Color fc = Main.hslToRgb(rainbowHue, 1f, 0.75f) with { A = 0 };
                float fs = chargeUp * (0.6f + (float)Math.Sin(StateTimer * 0.6f) * 0.1f);
                Main.spriteBatch.Draw(glowTex, fp, null, fc * (chargeUp * fade), 0f, glowTex.Size() / 2f, fs, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(glowTex, fp, null, Color.White with { A = 0 } * (chargeUp * 0.8f * fade), 0f, glowTex.Size() / 2f, fs * 0.5f, SpriteEffects.None, 0f);
            }

            //彩虹辉光层
            for (int i = 0; i < 3; i++) {
                Color gc = Main.hslToRgb((rainbowHue + i * 0.1f) % 1f, 1f, 0.6f) with { A = 0 };
                Main.spriteBatch.Draw(fishTex, drawPos, null, gc * (glowIntensity * (0.5f - i * 0.13f) * fade)
                    , rot, origin, Projectile.scale * (1.2f + i * 0.15f), SpriteEffects.None, 0f);
            }

            //本体（融入彩虹色调）
            Color body = Color.Lerp(lightColor, Main.hslToRgb(rainbowHue, 0.8f, 0.7f), glowIntensity * 0.55f);
            Main.spriteBatch.Draw(fishTex, drawPos, null, body * fade, rot, origin, Projectile.scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(fishTex, drawPos, null, Color.White with { A = 0 } * (glowIntensity * 0.4f * fade), rot, origin, Projectile.scale * 0.95f, SpriteEffects.None, 0f);

            //彩虹飘带（顶点绘制）
            if (trail.Count >= 2) {
                Vector2[] pts = new Vector2[trail.Count];
                for (int i = 0; i < trail.Count; i++) {
                    pts[i] = trail[i] - Main.screenPosition;
                }

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                FishSkillVFX.DrawRibbon(CWRAsset.LightShot.Value, pts, TrailWidth, TrailColor);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }
    }

    /// <summary>公主鱼的彩虹魔弹：彩虹飘带尾 + 追踪 + 弹跳，命中绽放</summary>
    internal class PrincessMagicOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float ColorOffset => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private float orbRotation;
        private float pulsePhase;
        private readonly List<FishSkillVFX.ShockRing> rings = new();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private float Hue => (ColorOffset + Main.GlobalTimeWrappedHourly * 0.5f) % 1f;

        public override void AI() {
            Timer++;
            pulsePhase += 0.15f;
            orbRotation += 0.2f;

            if (Timer > 15) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Projectile.velocity += (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 0.25f;
                    if (Projectile.velocity.Length() > 20f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    }
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Main.hslToRgb(Hue, 1f, 0.6f).ToVector3() * 1.2f);

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Update();
                if (rings[i].Dead) {
                    rings.RemoveAt(i);
                }
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(1f, 1f), Main.hslToRgb(Hue, 1f, 0.65f)
                    , Main.rand.NextFloat(0.6f, 1f)).Configure(false, 14);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            rings.Add(new FishSkillVFX.ShockRing(Projectile.Center, 70f, 8f, Main.hslToRgb(Hue, 1f, 0.65f), 1f, 16, 30));
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Color c = Main.hslToRgb((Hue + i * 0.05f) % 1f, 1f, 0.6f);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, angle.ToRotationVector2() * 6f, c, 0.7f).Configure(24, hueShift: 0.02f);
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            }

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 18; i++) {
                Color c = Main.hslToRgb((Hue + i * 0.03f) % 1f, 1f, 0.6f);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(8f, 8f), c, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(28, hueShift: 0.02f);
            }
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
        }

        public float TrailWidth(float t) => MathHelper.Lerp(20f, 2f, t);

        public Color TrailColor(float t) {
            Color c = Main.hslToRgb((Hue + t * 0.4f) % 1f, 1f, 0.6f);
            return c * (1f - t) * 0.8f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            Color orbColor = Main.hslToRgb(Hue, 1f, 0.7f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + (float)Math.Sin(pulsePhase) * 0.15f;

            //外层光晕（A=0 加色）
            for (int i = 0; i < 3; i++) {
                Color gc = Main.hslToRgb((Hue + i * 0.05f) % 1f, 1f, 0.6f) with { A = 0 };
                Main.spriteBatch.Draw(glowTex, drawPos, null, gc * ((1f - i * 0.3f) * 0.5f), orbRotation
                    , glowTex.Size() / 2f, pulse * (0.5f + i * 0.2f), SpriteEffects.None, 0f);
            }
            Main.spriteBatch.Draw(glowTex, drawPos, null, orbColor with { A = 0 }, orbRotation, glowTex.Size() / 2f, pulse * 0.35f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glowTex, drawPos, null, Color.White with { A = 0 } * 0.85f, orbRotation, glowTex.Size() / 2f, pulse * 0.2f, SpriteEffects.None, 0f);

            //彩虹飘带（顶点绘制） + 命中环
            bool drawTrail = BuildTrailPoints(out Vector2[] pts);
            bool drawRings = rings.Count > 0;
            if (drawTrail || drawRings) {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                if (drawTrail) {
                    FishSkillVFX.DrawRibbon(CWRAsset.LightShot.Value, pts, TrailWidth, TrailColor);
                }
                if (drawRings) {
                    Texture2D ringTex = CWRAsset.Placeholder_White.Value;
                    foreach (FishSkillVFX.ShockRing r in rings) {
                        r.Draw(ringTex);
                    }
                }
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        private bool BuildTrailPoints(out Vector2[] pts) {
            pts = null;
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 4) {
                return false;
            }
            List<Vector2> list = new(Projectile.oldPos.Length);
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                list.Add(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition);
            }
            if (list.Count < 2) {
                return false;
            }
            pts = list.ToArray();
            return true;
        }
    }
}
