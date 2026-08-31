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
    /// 【A 档】北极重铸：极地寒晶枪。<br/>
    /// 材质：极渊寒晶凝成的枪锋，白芯青缘，行处凝霜。签名行为：
    /// ①三段连刺，快拍甩出螺旋霜晶（绕轴盘旋、先疾后缓、尽头碎晶）
    /// ②终结拍「立极」——重刺命中处竖起极点寒晶柱，柱顶降下冰锥雨压制驻场
    /// ③驻相枪尖凝霜雾，命中皆挂霜火
    /// </summary>
    internal class GsNorthPole : GsSpearScheme
    {
        public override int TargetItemID => ItemID.NorthPole;

        protected override string GsDescFallback =>
            "Reforged: quick thrusts fling spiraling frost crystals, and the third strike plants a polar pillar" +
            "\nat the wound that rains icicles; the dwell wreathes the tip in freezing mist";

        protected override int HeldProjType => ModContent.ProjectileType<GsNorthPoleHeld>();

        protected override int ComboBeats => 3;
        protected override int ComboResetFrames => 60;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.0f;//霜晶+冰锥驻场吃满机制预算，底伤不加，综合 DPS 落在原版 108%~120%
    }

    /// <summary>
    /// 北极手持突刺：0/1 拍快刺甩螺旋霜晶，2 拍立极重刺（命中植寒晶柱）；
    /// 驻相枪尖霜雾，所有命中挂霜火
    /// </summary>
    internal class GsNorthPoleHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.NorthPole;

        //极地寒晶色板
        internal static readonly Color IceWhite = new(232, 246, 255);
        internal static readonly Color FrostBlue = new(122, 192, 255);
        internal static readonly Color DeepIce = new(46, 86, 170);
        internal static readonly Color CyanCore = new(184, 255, 255);

        private bool IsFinisher => ComboStage >= 2;

        protected override float WindupFrames => IsFinisher ? 6f : 4f;
        protected override float ThrustFrames => IsFinisher ? 7f : 5f;
        protected override float DwellFrames => IsFinisher ? 5f : 4f;
        protected override float RecoverFrames => IsFinisher ? 10f : 8f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => IsFinisher ? 20f : 13f;
        protected override float StabReach => IsFinisher ? 86f : 68f;
        protected override float BladeLength => 96f;
        protected override float CollisionWidth => 32f;
        protected override float TipGreedRadius => 30f;
        protected override float ThrustEasePower => IsFinisher ? 3.4f : 2.8f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsFinisher ? 0.055f : 0.038f;
        protected override int HitboxSize => 54;
        protected override int HitstopFrames => IsFinisher ? 3 : 2;
        protected override float ThrustPitch => IsFinisher ? -0.4f : -0.15f;

        protected override Color EdgeColor => IceWhite;
        protected override Color CoreColor => IsFinisher ? CyanCore : FrostBlue;
        protected override Color ShaftColor => DeepIce with { A = 235 };

        protected override void OnInit() {
            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.25f);
            }
        }

        protected override void OnThrustBurst() {
            //快拍甩出螺旋霜晶（owner 端权威，轨迹相位随生成包过线）
            if (!IsFinisher && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), TipPos, stabUnit * 13f,
                    ModContent.ProjectileType<GsNorthPoleFrostBoltProj>(),
                    (int)(Projectile.damage * 0.45f), Projectile.knockBack * 0.4f, Owner.whoAmI,
                    stabUnit.ToRotation(), Main.rand.NextFloat(MathHelper.TwoPi));
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.45f, Pitch = IsFinisher ? -0.3f : 0.2f }, Owner.Center);
            int count = IsFinisher ? 5 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.5f, 1f));
                Color c = Main.rand.NextBool(3) ? CyanCore : IceWhite;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(4f, 8.5f), c,
                    Main.rand.NextFloat(0.38f, 0.6f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>驻相霜雾：枪尖凝出缓散寒雾</summary>
        protected override void OnTick(int phase) {
            if (VaultUtils.isServer || phase != PhaseDwell) {
                return;
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Smoke>(TipPos + Main.rand.NextVector2Circular(8f, 8f),
                    stabUnit * 0.5f + new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.9f)),
                    FrostBlue * 0.6f, Main.rand.NextFloat(0.4f, 0.7f));
            }
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Sparkle>(TipPos + Main.rand.NextVector2Circular(10f, 10f),
                    Vector2.Zero, IceWhite, Main.rand.NextFloat(0.3f, 0.5f));
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //霜火：所有刺击命中都挂
            target.AddBuff(BuffID.Frostburn, IsFinisher ? 240 : 120);

            //立极：终结拍首个命中处竖起寒晶柱（同场至多两根）
            if (IsFinisher && firstOnTarget && Projectile.numHits <= 1 && Projectile.IsOwnedByLocalPlayer()
                && Owner.ownedProjectileCounts[ModContent.ProjectileType<GsNorthPolePillarProj>()] < 2) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsNorthPolePillarProj>(),
                    (int)(BaseDamage * 0.30f), 1f, Owner.whoAmI);
            }
        }

        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, CyanCore, 0.16f + (IsFinisher ? 0.08f : 0f))
                ?.Configure(10, 0.75f);
            int sparks = IsFinisher ? 9 : 5;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.6) * Main.rand.NextFloat(3f, 8f);
                Color c = Main.rand.NextBool(3) ? CyanCore : IceWhite;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.4f, 0.62f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.25f }, target.Center);
            }
        }

        protected override float ExtraGlowStrength() => IsFinisher ? 0.18f : 0.06f;
    }

    /// <summary>
    /// 螺旋霜晶：绕飞行轴盘旋前进，先疾后缓，行至尽头或命中即碎晶。<br/>
    /// ai[0]=轴向角，ai[1]=盘旋相位（随生成包过线，各端轨迹一致）
    /// </summary>
    internal class GsNorthPoleFrostBoltProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.NorthPole");

        private ref float Timer => ref Projectile.localAI[0];
        private Vector2 Axis => Projectile.ai[0].ToRotationVector2();
        private float Phase => Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 48;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;

            //先疾后缓的轴速 + 绕轴正弦盘旋（相位吃 ai1，各端一致）
            float axisSpeed = MathHelper.Lerp(13f, 7.5f, MathHelper.Clamp(Timer / 32f, 0f, 1f));
            float sway = MathF.Cos(Timer * 0.42f + Phase) * 2.8f;
            Projectile.velocity = Axis * axisSpeed + Axis.RotatedBy(MathHelper.PiOver2) * sway;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsNorthPoleHeld.FrostBlue.ToVector3() * 0.28f);

            if (VaultUtils.isServer) {
                return;
            }
            //盘旋尾迹冰尘
            if (Timer % 3f == 0f) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, -Projectile.velocity * 0.08f,
                    Main.rand.NextBool() ? GsNorthPoleHeld.IceWhite : GsNorthPoleHeld.FrostBlue,
                    Main.rand.NextFloat(0.3f, 0.5f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 90);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //碎晶 + 余痕寒雾（雾比碎片活得久）
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                    Main.rand.NextBool() ? GsNorthPoleHeld.IceWhite : GsNorthPoleHeld.CyanCore,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(10, 16));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)),
                    GsNorthPoleHeld.FrostBlue * 0.5f, Main.rand.NextFloat(0.45f, 0.7f));
            }
        }

        /// <summary>霜晶绘制：白芯短拉丝 + 青缘辉 + 晶点闪（相位吃 whoAmI，仅作抖闪）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (streak == null || glowTex == null || star == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            float twinkle = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.whoAmI * 2.1f);
            Vector2 ssize = streak.Size();

            Main.spriteBatch.Draw(glowTex, drawPos, null, GsNorthPoleHeld.FrostBlue with { A = 0 } * 0.5f, 0f,
                glowTex.Size() / 2f, 0.42f * twinkle, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(streak, drawPos - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 18f, null,
                GsNorthPoleHeld.FrostBlue with { A = 0 } * 0.55f, rot, ssize / 2f,
                new Vector2(56f / ssize.X, 0.16f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(streak, drawPos - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 14f, null,
                GsNorthPoleHeld.IceWhite with { A = 0 } * 0.8f, rot, ssize / 2f,
                new Vector2(40f / ssize.X, 0.09f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(star, drawPos, null, GsNorthPoleHeld.CyanCore with { A = 0 } * (0.85f * twinkle),
                rot, star.Size() / 2f, 0.26f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 极点寒晶柱：立极重刺命中处拔地而起（落点向下寻地），生长 → 降冰锥雨 → 化雾消散。<br/>
    /// 柱体不接触伤害，压制全在冰锥；冰锥由 owner 端派发
    /// </summary>
    internal class GsNorthPolePillarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.NorthPole");

        private const int GrowFrames = 12;
        private const int LifeFrames = 150;
        private const int FadeFrames = 22;
        private const float PillarHeight = 128f;

        private ref float Timer => ref Projectile.localAI[0];
        private bool grounded;

        private float GrowT => MathHelper.Clamp(Timer / GrowFrames, 0f, 1f);
        private float FadeT => MathHelper.Clamp((LifeFrames - Timer) / (float)FadeFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI() {
            //首帧向下寻地扎根（各端同参同算）
            if (!grounded) {
                grounded = true;
                Point tile = Projectile.Center.ToTileCoordinates();
                for (int i = 0; i < 14; i++) {
                    if (WorldGen.SolidTile(tile.X, tile.Y + i)) {
                        Projectile.Center = new Vector2(Projectile.Center.X, (tile.Y + i) * 16f - 4f);
                        break;
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.7f, Pitch = -0.35f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
                }
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            Lighting.AddLight(Projectile.Center - new Vector2(0f, PillarHeight * 0.5f * GrowT),
                GsNorthPoleHeld.FrostBlue.ToVector3() * (0.6f * FadeT));

            //冰锥雨：柱顶两侧交替降下（owner 端派发，随生成包过线）
            if (Timer > GrowFrames && Timer < LifeFrames - FadeFrames && Timer % 22f == 0f
                && Projectile.IsOwnedByLocalPlayer()) {
                Vector2 top = Projectile.Center - new Vector2(0f, PillarHeight);
                for (int i = 0; i < 2; i++) {
                    Vector2 at = top + new Vector2(Main.rand.NextFloat(-70f, 70f), -Main.rand.NextFloat(0f, 18f));
                    Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(8.5f, 11f));
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), at, vel,
                        ModContent.ProjectileType<GsNorthPoleIcicleProj>(),
                        Projectile.damage, 0.8f, Owner.whoAmI);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //柱身风雪环绕
            if (Timer % 4f == 0f && FadeT > 0.3f) {
                Vector2 at = Projectile.Center - new Vector2(0f, Main.rand.NextFloat(0f, PillarHeight * GrowT));
                PRTLoader.NewParticle<PRT_Sparkle>(at + new Vector2(Main.rand.NextFloat(-14f, 14f), 0f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    Main.rand.NextBool() ? GsNorthPoleHeld.IceWhite : GsNorthPoleHeld.FrostBlue,
                    Main.rand.NextFloat(0.3f, 0.55f));
            }
            //谢幕化雾（余痕）
            if (Timer >= LifeFrames - FadeFrames && Timer % 3f == 0f) {
                Vector2 at = Projectile.Center - new Vector2(0f, Main.rand.NextFloat(0f, PillarHeight));
                PRTLoader.NewParticle<PRT_Smoke>(at, new Vector2(0f, -0.5f),
                    GsNorthPoleHeld.FrostBlue * 0.45f, Main.rand.NextFloat(0.5f, 0.8f));
            }
        }

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>寒晶柱自绘：三束竖向晶簇 + 白芯 + 基座光池 + 顶端环绕晶点（全定值/时间相位）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (streak == null || glowTex == null || star == null) {
                return false;
            }
            float alpha = FadeT * (0.35f + GrowT * 0.65f);
            if (alpha <= 0.02f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float height = PillarHeight * (0.2f + 0.8f * GrowT);
            Vector2 ssize = streak.Size();
            float seed = Projectile.whoAmI * 1.7f;

            //基座光池
            Main.spriteBatch.Draw(glowTex, basePos, null, GsNorthPoleHeld.FrostBlue with { A = 0 } * (0.5f * alpha),
                0f, glowTex.Size() / 2f, new Vector2(1.3f, 0.45f), SpriteEffects.None, 0f);

            //三束竖向晶簇（主柱 + 两根斜倚的短柱），端点微呼吸
            for (int i = -1; i <= 1; i++) {
                float lean = i * 0.14f;
                float segH = height * (i == 0 ? 1f : 0.62f);
                float breathe = 1f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + seed + i * 2f);
                Vector2 dirUp = new Vector2(MathF.Sin(lean), -MathF.Cos(lean));
                Vector2 mid = basePos + dirUp * (segH * 0.5f * breathe) + new Vector2(i * 10f, 0f);
                float rotUp = dirUp.ToRotation();
                Main.spriteBatch.Draw(streak, mid, null,
                    GsNorthPoleHeld.FrostBlue with { A = 0 } * (alpha * (i == 0 ? 0.8f : 0.5f)),
                    rotUp, ssize / 2f, new Vector2(segH * breathe / ssize.X, i == 0 ? 0.24f : 0.15f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(streak, mid, null,
                    GsNorthPoleHeld.IceWhite with { A = 0 } * (alpha * (i == 0 ? 0.9f : 0.55f)),
                    rotUp, ssize / 2f, new Vector2(segH * breathe / ssize.X * 0.9f, i == 0 ? 0.10f : 0.06f), SpriteEffects.None, 0f);
            }

            //顶端环绕晶点（角度吃全局时间 + whoAmI 种子）
            Vector2 top = basePos - new Vector2(0f, height);
            for (int i = 0; i < 3; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 2.4f + seed + i * MathHelper.TwoPi / 3f;
                Vector2 orbit = top + ang.ToRotationVector2() * new Vector2(22f, 9f);
                Main.spriteBatch.Draw(star, orbit, null, GsNorthPoleHeld.CyanCore with { A = 0 } * (0.7f * alpha),
                    ang, star.Size() / 2f, 0.2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>极点冰锥：自柱顶坠落的寒晶碎锥，触地即碎，命中挂霜火</summary>
    internal class GsNorthPoleIcicleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.NorthPole");

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 13f);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsNorthPoleHeld.FrostBlue.ToVector3() * 0.18f);

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, -Projectile.velocity * 0.05f,
                    GsNorthPoleHeld.IceWhite, Main.rand.NextFloat(0.25f, 0.4f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 120);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.25f, Pitch = 0.5f }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsNorthPoleHeld.IceWhite : GsNorthPoleHeld.FrostBlue,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        /// <summary>冰锥自绘：短白芯拉丝 + 青辉点（无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (streak == null || glowTex == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 ssize = streak.Size();
            Main.spriteBatch.Draw(glowTex, drawPos, null, GsNorthPoleHeld.FrostBlue with { A = 0 } * 0.4f, 0f,
                glowTex.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(streak, drawPos, null, GsNorthPoleHeld.IceWhite with { A = 0 } * 0.85f,
                Projectile.rotation, ssize / 2f, new Vector2(30f / ssize.X, 0.08f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
