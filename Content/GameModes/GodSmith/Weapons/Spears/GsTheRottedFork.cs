using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛】腐叉重铸：腐脓挤压。<br/>
    /// 材质：猩红血肉拧成的三股叉，齿间滴脓。签名行为：①两拍连刺——轻拍快叉、
    /// 重拍慢压更深 ②重拍命中从伤口挤出一枚腐脓弹，抛物线坠地或撞怪后
    /// 摊成小腐蚀池持续灼蚀 ③命中黏腻湿响与脓液飞溅，血肉气息浓重
    /// </summary>
    internal class GsTheRottedFork : GsSpearScheme
    {
        public override int TargetItemID => ItemID.TheRottedFork;

        protected override string GsDescFallback =>
            "Reforged: two-beat strikes, a quick jab then a heavy squeeze;" +
            "\nthe heavy beat squeezes a glob of gore from the wound that splats into a corroding pool";

        protected override int HeldProjType => ModContent.ProjectileType<GsTheRottedForkHeld>();

        protected override int ComboBeats => 2;

        //腐脓弹+腐蚀池吃掉大半预算，底伤小补，综合 DPS 落在原版 105%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;
    }

    /// <summary>
    /// 腐叉手持突刺。ai[0]=拍号 0 轻拍快叉 / 1 重拍慢压；
    /// 重拍命中挤出腐脓弹（20% 伤害，落点留腐蚀池）
    /// </summary>
    internal class GsTheRottedForkHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.TheRottedFork;

        //猩红血肉色板
        internal static readonly Color PusBright = new(238, 142, 148);  //脓液亮粉
        internal static readonly Color FleshRed = new(190, 58, 64);     //血肉红
        internal static readonly Color IchorPurple = new(134, 48, 96);  //腐紫
        internal static readonly Color GoreDark = new(66, 20, 30);      //腐肉暗底

        private bool IsHeavyBeat => ComboStage >= 1;

        //轻拍快叉，重拍慢压更深；重拍收势特意拉长（对齐原版 31 帧节奏，腐脓弹才有预算）
        protected override float WindupFrames => IsHeavyBeat ? 6f : 5f;
        protected override float ThrustFrames => IsHeavyBeat ? 6f : 5f;
        protected override float DwellFrames => IsHeavyBeat ? 4f : 3f;
        protected override float RecoverFrames => IsHeavyBeat ? 12f : 10f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => IsHeavyBeat ? 18f : 12f;
        protected override float StabReach => IsHeavyBeat ? 72f : 56f;
        protected override float BladeLength => 86f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override float ThrustEasePower => IsHeavyBeat ? 3.2f : 2.6f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsHeavyBeat ? 0.055f : 0.032f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => IsHeavyBeat ? 3 : 2;
        protected override float ThrustPitch => IsHeavyBeat ? -0.38f : -0.20f;

        protected override Color EdgeColor => PusBright;
        protected override Color CoreColor => IsHeavyBeat ? IchorPurple : FleshRed;
        protected override Color ShaftColor => GoreDark with { A = 235 };

        protected override void OnInit() {
            //重拍慢压：伤害上浮补慢拍节奏
            if (IsHeavyBeat) {
                Projectile.damage = (int)(Projectile.damage * 1.20f);
            }
        }

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = ThrustPitch }, Owner.Center);
            if (IsHeavyBeat) {
                //重拍带一声黏腻湿响
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.35f }, Owner.Center);
            }
            //爆发帧脓滴沿叉齿甩出
            int count = IsHeavyBeat ? 4 : 2;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.55f, 1f));
                Color c = Main.rand.NextBool(3) ? PusBright : FleshRed;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(3.5f, 7f), c,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>重拍血肉辉光更沉</summary>
        protected override float ExtraGlowStrength() => IsHeavyBeat ? 0.12f : 0f;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //腐脓挤压：重拍首个命中从伤口挤出一枚腐脓弹（owner 端生成，随生成包过线）
            if (!IsHeavyBeat || !firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 from = Vector2.Lerp(TipPos, target.Center, 0.5f);
            Vector2 vel = stabUnit * 5f;
            vel.Y -= 3.5f;//挤出后小抛物线
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                ModContent.ProjectileType<GsRottedForkGlobProj>(),
                (int)(BaseDamage * 0.20f), Projectile.knockBack * 0.3f, Owner.whoAmI);
        }

        /// <summary>命中反馈：黏腻湿响 + 脓液飞溅 + 浓重血尘（重拍加量）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.45f, Pitch = IsHeavyBeat ? -0.5f : -0.2f, MaxInstances = 3 }, pos);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero,
                IsHeavyBeat ? IchorPurple : FleshRed, 0.17f)?.Configure(9, 0.7f);
            int sparks = IsHeavyBeat ? 8 : 5;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.7) * Main.rand.NextFloat(2.5f, 6.5f);
                Color c = Main.rand.NextBool(3) ? PusBright : FleshRed;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.38f, 0.6f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            int bloods = IsHeavyBeat ? 5 : 3;
            for (int i = 0; i < bloods; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(1f) * Main.rand.NextFloat(1.5f, 4.5f), 80, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }

        /// <summary>重拍蓄势自绘：齿间两团脓液鼓胀（真 alpha 暗核 + 腐紫加色晕，无随机）</summary>
        protected override void DrawUnderBlade(SpriteBatch sb) {
            if (!IsHeavyBeat || CurrentPhase != PhaseWindup) {
                return;
            }
            Texture2D dark = CWRAsset.Extra_98?.Value;
            if (dark == null) {
                return;
            }
            float windT = MathHelper.Clamp(Elapsed / WindupFrames, 0f, 1f);
            for (int i = 0; i < 2; i++) {
                Vector2 at = Hand + stabUnit * (holdout + BladeLength * (0.62f + i * 0.24f)) - Main.screenPosition;
                float wob = MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + i * 2.7f + Projectile.whoAmI) * 0.05f;
                float sc = (0.14f + i * 0.04f + wob) * windT;
                sb.Draw(dark, at, null, GoreDark * (0.6f * windT), 0f, dark.Size() / 2f, sc, SpriteEffects.None, 0f);
                sb.Draw(dark, at, null, IchorPurple with { A = 0 } * (0.4f * windT), 0f, dark.Size() / 2f, sc * 0.7f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 腐脓弹：重拍从伤口挤出，抛物线受重力，坠地或撞怪即摊成腐蚀池。<br/>
    /// 自绘：Extra_98 真 alpha 暗核 + 红紫加色晕，鼓动相位吃 whoAmI 种子（绘制无随机）
    /// </summary>
    internal class GsRottedForkGlobProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TheRottedFork");

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            //抛物线受重力
            Projectile.velocity.Y += 0.32f;
            if (Projectile.velocity.Y > 12f) {
                Projectile.velocity.Y = 12f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.04f;

            Lighting.AddLight(Projectile.Center, GsTheRottedForkHeld.FleshRed.ToVector3() * 0.2f);

            if (VaultUtils.isServer) {
                return;
            }
            //拖脓滴
            if (Projectile.timeLeft % 3 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    -Projectile.velocity * 0.08f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //落地或命中：摊成腐蚀池（owner 端生成，随生成包过线）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsRottedForkPoolProj>(),
                    (int)(Projectile.damage * 0.6f), 0f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.5f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-3.5f, -0.5f));
                Color c = Main.rand.NextBool() ? GsTheRottedForkHeld.FleshRed : GsTheRottedForkHeld.PusBright;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>腐脓弹自绘：真 alpha 暗核压底 + 红紫加色晕 + 亮粉芯，鼓动吃 whoAmI 种子</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D dark = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (dark == null || glow == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.whoAmI * 1.37f;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + seed);

            Main.spriteBatch.Draw(dark, drawPos, null, GsTheRottedForkHeld.GoreDark * 0.75f, seed,
                dark.Size() / 2f, 0.15f * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, drawPos, null, GsTheRottedForkHeld.IchorPurple with { A = 0 } * 0.7f, 0f,
                glow.Size() / 2f, 0.45f * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, drawPos, null, GsTheRottedForkHeld.PusBright with { A = 0 } * 0.45f, 0f,
                glow.Size() / 2f, 0.2f * pulse, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 腐蚀池：腐脓弹落点摊开的小片脓沼，~1.2 秒内持续低伤判定。<br/>
    /// 自绘：真 alpha 暗沼压底 + 腐紫脉动晕，偶发上浮脓泡（AI 里生成，绘制无随机）
    /// </summary>
    internal class GsRottedForkPoolProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TheRottedFork");

        private const int LifeFrames = 72;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;//~1.2s 内至多灼蚀 4 跳
            Projectile.knockBack = 0f;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Lighting.AddLight(Projectile.Center,
                GsTheRottedForkHeld.IchorPurple.ToVector3() * (0.25f * (Projectile.timeLeft / (float)LifeFrames)));

            if (VaultUtils.isServer) {
                return;
            }
            //偶发上浮脓泡
            if (Main.rand.NextBool(6)) {
                Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-24f, 24f), 4f);
                Color c = Main.rand.NextBool(3) ? GsTheRottedForkHeld.PusBright : GsTheRottedForkHeld.FleshRed;
                PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)), c,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(10, 16), 0.5f);
            }
        }

        /// <summary>脓沼自绘：横摊的真 alpha 暗沼 + 腐紫脉动晕（whoAmI 种子，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D dark = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (dark == null || glow == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float lifeT = Projectile.timeLeft / (float)LifeFrames;
            //头几帧摊开，尾段收干
            float spread = Math.Min(1f, (LifeFrames - Projectile.timeLeft) / 6f) * (0.4f + 0.6f * lifeT);
            float seed = Projectile.whoAmI * 1.37f;
            float pulse = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + seed);

            Main.spriteBatch.Draw(dark, drawPos, null, GsTheRottedForkHeld.GoreDark * (0.7f * spread), 0f,
                dark.Size() / 2f, new Vector2(0.5f, 0.13f) * pulse * spread, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, drawPos, null, GsTheRottedForkHeld.IchorPurple with { A = 0 } * (0.5f * spread), 0f,
                glow.Size() / 2f, new Vector2(1.0f, 0.3f) * pulse * spread, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, drawPos, null, GsTheRottedForkHeld.FleshRed with { A = 0 } * (0.35f * spread * pulse), 0f,
                glow.Size() / 2f, new Vector2(0.6f, 0.18f) * spread, SpriteEffects.None, 0f);
            return false;
        }
    }
}
