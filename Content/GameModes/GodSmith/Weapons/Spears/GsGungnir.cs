using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【A 档】贡格尼尔重铸：奥丁的誓约之枪。<br/>
    /// 材质：神鎏金圣枪，金辉裹白芯，行处遗落符文光尘。签名行为：
    /// ①左键神威三段刺，终结拍金环迸响 ②右键掷出誓约之枪——出手即锁定瞄准线上的猎物，
    /// 枪化贯穿光柱弯折命中，从不落空 ③光柱穿身留符文余痕，终点绽金环后光归持者
    /// </summary>
    internal class GsGungnir : GsSpearScheme
    {
        public override int TargetItemID => ItemID.Gungnir;

        protected override string GsDescFallback =>
            "Reforged: a stately three-beat thrust crowned by a golden finisher;" +
            "\nright click hurls the oath-lance as a piercing bolt of light that never misses its sworn prey";

        protected override int HeldProjType => ModContent.ProjectileType<GsGungnirHeld>();

        protected override int ComboBeats => 3;
        protected override int ComboResetFrames => 60;

        /// <summary>掷枪冷却（真实帧），只在 myPlayer 路径消费</summary>
        private int throwCooldown;

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            //掷枪演出或突刺在场都算冷却
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GsGungnirThrowHeld>()] > 0
                || player.ownedProjectileCounts[HeldProjType] > 0) {
                return false;
            }
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer && throwCooldown <= 0
                    && player.ownedProjectileCounts[ModContent.ProjectileType<GsGungnirBoltProj>()] == 0) {
                    throwCooldown = 110;
                    Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                        ModContent.ProjectileType<GsGungnirThrowHeld>(),
                        player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
                }
                return false;
            }
            return base.GsCanUseItem(item, player);
        }

        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player);
            if (player.whoAmI == Main.myPlayer && throwCooldown > 0) {
                throwCooldown--;
            }
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//掷枪光柱与终结金环吃掉预算，底伤只碰边，综合 DPS 落在原版 105%~120%
    }

    /// <summary>
    /// 贡格尼尔左键手持突刺：神威三段。0/1 拍中线快刺带金辉，2 拍神威重刺——
    /// 更深更沉，爆发帧迸金环，命中金白神辉爆
    /// </summary>
    internal class GsGungnirHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Gungnir;

        //神鎏金色板
        internal static readonly Color GoldEdge = new(255, 226, 140);
        internal static readonly Color GoldMain = new(226, 176, 74);
        internal static readonly Color HolyWhite = new(255, 250, 232);
        internal static readonly Color RuneAmber = new(255, 190, 96);

        private bool IsFinisher => ComboStage >= 2;

        protected override float WindupFrames => IsFinisher ? 7f : 5f;
        protected override float ThrustFrames => IsFinisher ? 7f : 5f;
        protected override float DwellFrames => IsFinisher ? 5f : 3f;
        protected override float RecoverFrames => IsFinisher ? 10f : 8f;
        protected override float RestHoldout => 14f;
        protected override float PullbackDist => IsFinisher ? 22f : 14f;
        protected override float StabReach => IsFinisher ? 92f : 74f;
        protected override float BladeLength => 100f;
        protected override float CollisionWidth => 32f;
        protected override float TipGreedRadius => 32f;
        protected override float ThrustEasePower => IsFinisher ? 3.5f : 2.8f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsFinisher ? 0.06f : 0.04f;
        protected override int HitboxSize => 56;
        protected override int HitstopFrames => IsFinisher ? 3 : 2;
        protected override float ThrustPitch => IsFinisher ? -0.35f : -0.18f;

        protected override Color EdgeColor => GoldEdge;
        protected override Color CoreColor => IsFinisher ? HolyWhite : GoldMain;

        protected override void OnInit() {
            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.30f);
            }
        }

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = ThrustPitch }, Owner.Center);
            if (IsFinisher) {
                //神威重刺：金环自枪尖荡开 + 低鸣
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.35f }, Owner.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(TipPos, stabUnit * 2f, GoldEdge, 0.6f)
                    ?.Configure(0.10f, 0.55f, 14);
            }
            int count = IsFinisher ? 5 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.45f, 1f));
                Color c = Main.rand.NextBool(3) ? HolyWhite : GoldEdge;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(4f, 9f), c,
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>驻相金辉不散：终结拍驻相刃身升温可见</summary>
        protected override float ExtraGlowStrength()
            => IsFinisher && CurrentPhase is PhaseDwell or PhaseThrust ? 0.25f : 0.08f;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!IsFinisher || !firstOnTarget || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, stabUnit, 4f, 5.5f, 8, 520f, FullName));
            }
        }

        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, HolyWhite, 0.16f + (IsFinisher ? 0.10f : 0f))
                ?.Configure(10, 0.8f);
            int sparks = IsFinisher ? 10 : 6;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3.5f, 8.5f);
                Color c = Main.rand.NextBool(3) ? HolyWhite : GoldEdge;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.4f, 0.68f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            if (IsFinisher) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, RuneAmber, 0.5f)
                    ?.Configure(0.12f, 0.62f, 15);
            }
        }
    }

    /// <summary>
    /// 誓约掷枪演出：举枪过肩后引蓄势（金光收束、臂姿后仰）→ 松手瞬间掷出光柱之枪
    /// （后坐半步 + 金环荡开）→ 挥臂收势。掷出后枪身不再绘制，光柱弹幕接管
    /// </summary>
    internal class GsGungnirThrowHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.Gungnir");

        private const float RaiseFrames = 10f;
        private const float FollowFrames = 8f;

        private Vector2 aimUnit;
        private int facing = 1;
        private float speedMul = 1f;
        private float elapsed;
        private bool loosed;

        private float RaiseT => MathHelper.Clamp(elapsed / RaiseFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override void Initialize() {
            aimUnit = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            facing = MathF.Abs(aimUnit.X) < 0.05f ? Owner.direction : Math.Sign(aimUnit.X);
            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }
        }

        public override void AI() {
            if (Item.type != ItemID.Gungnir || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 90;

            //举枪期金光向枪身收束
            if (elapsed < RaiseFrames) {
                if (!VaultUtils.isServer && Main.rand.NextFloat() < 0.5f) {
                    Vector2 lanceAt = Hand() + LanceDir() * Main.rand.NextFloat(20f, 70f);
                    Vector2 from = lanceAt + Main.rand.NextVector2Unit() * Main.rand.NextFloat(30f, 56f);
                    PRTLoader.NewParticle<PRT_Light>(from, (lanceAt - from) * 0.16f,
                        Main.rand.NextBool(3) ? GsGungnirHeld.HolyWhite : GsGungnirHeld.GoldEdge,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(8, 12), 0.55f, 1.3f);
                }
            }
            else if (!loosed) {
                loosed = true;
                Loose();
            }

            UpdatePose();
            elapsed += speedMul;

            if (elapsed >= RaiseFrames + FollowFrames && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.Kill();
            }
        }

        /// <summary>松手：锁定瞄准线上最近的猎物，掷出光柱之枪（owner 端权威）</summary>
        private void Loose() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int targetWho = PickSwornPrey();
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Hand() + aimUnit * 30f, aimUnit * 16f,
                    ModContent.ProjectileType<GsGungnirBoltProj>(),
                    (int)(Projectile.damage * 2.0f), Projectile.knockBack * 1.5f, Owner.whoAmI, targetWho);
                //施力者后坐半步
                if (!Owner.mount.Active) {
                    Owner.velocity -= aimUnit * 2.6f;
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.8f, Pitch = 0.15f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.2f }, Owner.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Hand() + aimUnit * 34f, aimUnit * 3f,
                GsGungnirHeld.GoldEdge, 0.7f)?.Configure(0.10f, 0.7f, 15);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Owner.Center, aimUnit, 5f, 6f, 9, 600f, FullName));
            }
        }

        /// <summary>誓约锁定：瞄准射线附近最贴线的敌人（前向 1100px 内）</summary>
        private int PickSwornPrey() {
            Vector2 origin = Hand();
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal) {
                    continue;
                }
                Vector2 to = npc.Center - origin;
                float forward = Vector2.Dot(to, aimUnit);
                if (forward < 0f || forward > 1100f) {
                    continue;
                }
                float offLine = MathF.Abs(Vector2.Dot(to, aimUnit.RotatedBy(MathHelper.PiOver2)));
                if (offLine > 320f) {
                    continue;
                }
                float score = offLine * 2f + forward * 0.25f;
                if (score < bestScore) {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private Vector2 Hand() => Owner.GetPlayerStabilityCenter();

        /// <summary>举枪期的枪身指向：从瞄准向后仰过肩，松手后不再有枪</summary>
        private Vector2 LanceDir() {
            float cock = MathF.Sin(RaiseT * MathHelper.PiOver2) * 0.85f;
            return aimUnit.RotatedBy(-cock * facing);
        }

        private void UpdatePose() {
            Owner.ChangeDir(facing);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (aimUnit * Owner.direction).ToRotation();
            Projectile.Center = Hand();
            Projectile.rotation = aimUnit.ToRotation();

            //臂姿：举枪后引 → 松手前挥
            float armRot;
            if (elapsed < RaiseFrames) {
                armRot = LanceDir().ToRotation() - MathHelper.PiOver2;
            }
            else {
                float t = MathHelper.Clamp((elapsed - RaiseFrames) / FollowFrames, 0f, 1f);
                float swing = MathHelper.Lerp(-0.85f, 0.35f, 1f - MathF.Pow(1f - t, 3f));
                armRot = aimUnit.RotatedBy(swing * facing * -1f).ToRotation() - MathHelper.PiOver2;
            }
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRot - facing * 0.35f);
        }

        /// <summary>只在举枪期绘制枪身（松手后光柱接管）+ 收束金辉</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (elapsed >= RaiseFrames) {
                return false;
            }
            Main.instance.LoadItem(ItemID.Gungnir);
            Texture2D tex = TextureAssets.Item[ItemID.Gungnir].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 dir = LanceDir();
            float scale = 100f / MathF.Max(tex.Size().Length() * 0.9f, 1f);
            float rot = dir.ToRotation() + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (facing < 0) {
                rot += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }
            Vector2 drawPos = Hand() + dir * 50f - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, null, lightColor, rot, origin, scale, effect, 0f);
            //蓄势金辉随举枪升温
            Color glow = GsGungnirHeld.GoldEdge with { A = 0 } * (0.15f + RaiseT * 0.4f);
            Main.spriteBatch.Draw(tex, drawPos, null, glow, rot, origin, scale * 1.05f, effect, 0f);
            return false;
        }
    }

    /// <summary>
    /// 誓约光柱：掷出的贡格尼尔化身。出手即加速（非匀速），朝锁定猎物弯折直至穿身——从不落空；
    /// 穿身不停，沿途遗落符文光尘（余痕），行程尽头绽金环化光。<br/>
    /// ai[0]=锁定目标 whoAmI（-1 无目标，随生成包过线）
    /// </summary>
    internal class GsGungnirBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.Gungnir");

        private int TargetWho => (int)Projectile.ai[0];
        private ref float Timer => ref Projectile.localAI[0];
        private bool passed;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = false;//神掷无视地形，必中的代价由射程与冷却付
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 70;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;

            //出手加速：初速 16 → 十帧内推到 44（飞行相不匀速）
            float speed = Projectile.velocity.Length();
            if (Timer <= 10f && speed < 44f) {
                speed = MathF.Min(44f, speed + 3.0f);
            }

            //誓约弯折：越过猎物之前持续朝其修正弹道
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            if (!passed && TargetWho >= 0 && TargetWho < Main.maxNPCs) {
                NPC prey = Main.npc[TargetWho];
                if (prey.active && !prey.friendly) {
                    Vector2 to = prey.Center - Projectile.Center;
                    if (Vector2.Dot(to, dir) <= 0f) {
                        passed = true;//已越过，直行到底
                    }
                    else {
                        Vector2 want = to.SafeNormalize(dir);
                        dir = Vector2.Lerp(dir, want, 0.22f).SafeNormalize(dir);
                    }
                }
                else {
                    passed = true;
                }
            }
            Projectile.velocity = dir * speed;
            Projectile.rotation = dir.ToRotation();

            Lighting.AddLight(Projectile.Center, GsGungnirHeld.GoldEdge.ToVector3() * 0.6f);

            if (VaultUtils.isServer) {
                return;
            }
            //符文余痕：沿途遗落缓散金尘（寿命长于弹体路过的瞬间）
            if (Timer % 2f == 0f) {
                Vector2 drift = new(0f, -Main.rand.NextFloat(0.2f, 0.7f));
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    drift, Main.rand.NextBool(3) ? GsGungnirHeld.HolyWhite : GsGungnirHeld.RuneAmber,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(22, 32), 0.55f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.whoAmI == TargetWho) {
                //誓约之的：对锁定猎物必定暴击
                modifiers.SetCrit();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = dir.RotatedByRandom(0.7) * Main.rand.NextFloat(4f, 10f);
                Color c = Main.rand.NextBool(3) ? GsGungnirHeld.HolyWhite : GsGungnirHeld.GoldEdge;
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            if (Projectile.numHits <= 1) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero,
                    GsGungnirHeld.GoldEdge, 0.55f)?.Configure(0.12f, 0.66f, 14);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //终点：金环绽开，光归持者
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.35f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                GsGungnirHeld.RuneAmber, 0.6f)?.Configure(0.10f, 0.8f, 16);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    GsGungnirHeld.GoldEdge, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 26), 0.6f);
            }
            if (Main.myPlayer == Projectile.owner) {
                Player owner = Main.player[Projectile.owner];
                if (owner.active) {
                    for (int i = 0; i < 2; i++) {
                        PRTLoader.NewParticle<PRT_Light>(owner.Center + Main.rand.NextVector2Circular(14f, 14f),
                            new Vector2(0f, -0.6f), GsGungnirHeld.HolyWhite, 0.4f)?.Configure(14, 0.6f);
                    }
                }
            }
        }

        /// <summary>光柱绘制：金缘白芯双层拉丝 + 枪形本体 + 位移残影 + 尖端星芒（无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D star = CWRAsset.StarFlare01?.Value;
            Main.instance.LoadItem(ItemID.Gungnir);
            Texture2D lance = TextureAssets.Item[ItemID.Gungnir].Value;
            if (streak == null || star == null) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float rot = dir.ToRotation();
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / 44f, 0.2f, 1f);
            Vector2 streakSize = streak.Size();

            //位移残影（枪形，越远越淡）
            Vector2 lanceOrigin = lance.Size() / 2f;
            float lanceScale = 96f / MathF.Max(lance.Size().Length() * 0.9f, 1f);
            float lanceRot = rot + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (dir.X < 0f) {
                lanceRot = rot + MathHelper.PiOver4 + MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }
            for (int i = 3; i >= 1; i--) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 gPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color ghost = GsGungnirHeld.GoldMain with { A = 0 } * (0.28f - i * 0.07f);
                Main.spriteBatch.Draw(lance, gPos, null, ghost, lanceRot, lanceOrigin, lanceScale, effect, 0f);
            }

            //光柱双层拉丝：金缘宽层 + 白芯窄层（长度随速）
            float len = 130f + 130f * speedT;
            Color gold = GsGungnirHeld.GoldEdge with { A = 0 } * (0.55f * speedT + 0.25f);
            Main.spriteBatch.Draw(streak, drawPos - dir * (len * 0.35f), null, gold, rot, streakSize / 2f,
                new Vector2(len / streakSize.X, 0.30f), SpriteEffects.None, 0f);
            Color core = GsGungnirHeld.HolyWhite with { A = 0 } * (0.7f * speedT + 0.2f);
            Main.spriteBatch.Draw(streak, drawPos - dir * (len * 0.32f), null, core, rot, streakSize / 2f,
                new Vector2(len / streakSize.X * 0.85f, 0.14f), SpriteEffects.None, 0f);

            //枪形本体（光化的神枪仍有形）
            Main.spriteBatch.Draw(lance, drawPos, null, GsGungnirHeld.HolyWhite with { A = 0 } * 0.9f,
                lanceRot, lanceOrigin, lanceScale, effect, 0f);

            //尖端星芒（相位吃 whoAmI 种子）
            float twinkle = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.whoAmI * 1.3f);
            Vector2 tipAt = drawPos + dir * 52f;
            Main.spriteBatch.Draw(star, tipAt, null, GsGungnirHeld.HolyWhite with { A = 0 } * (0.75f * twinkle),
                rot, star.Size() / 2f, 0.38f * twinkle, SpriteEffects.None, 0f);
            return false;
        }
    }
}
