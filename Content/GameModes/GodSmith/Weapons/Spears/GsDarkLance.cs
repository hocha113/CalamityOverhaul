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
    /// 【长矛首把·垂直切片】黑暗长枪重铸：噬影蓄力长刺。<br/>
    /// 材质：黑曜枪杆上缠绕的噬影黑焰。签名行为：①按住蓄力，黑焰焰须向枪身收束、
    /// 枪尖压低蓄势 ②放刺距离与伤害随蓄力增长，满蓄驻相延长、黑焰驻烧
    /// ③满蓄命中从伤口迸出三团噬影火，追咬近旁猎物并挂暗影焰
    /// </summary>
    internal class GsDarkLance : GsSpearScheme
    {
        public override int TargetItemID => ItemID.DarkLance;

        protected override string GsDescFallback =>
            "Reforged: hold to feed the lance with devouring shadowflame, release to strike farther and harder;" +
            "\na fully charged hit bursts three shadow embers from the wound";

        protected override int HeldProjType => ModContent.ProjectileType<GsDarkLanceHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;//蓄力收益走机制端，底伤小补，综合 DPS 落在原版 105%~120%
    }

    /// <summary>
    /// 黑暗长枪手持突刺：蓄力驻留在蓄势末，黑焰收束可见；
    /// 放刺深度 ×1~1.6、伤害 ×1~1.85 随蓄力，满蓄（≥80%）命中迸噬影火团
    /// </summary>
    internal class GsDarkLanceHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.DarkLance;

        //噬影黑焰色板
        internal static readonly Color VoidDeep = new(30, 18, 46);
        internal static readonly Color ShadowPurple = new(126, 66, 198);
        internal static readonly Color ShadowHot = new(198, 122, 255);
        internal static readonly Color PaleEdge = new(224, 210, 252);

        protected override float WindupFrames => 5f;
        protected override float ThrustFrames => 5f + ChargeT * 2f;
        protected override float DwellFrames => 3f + ChargeT * 3f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => 18f;
        protected override float StabReach => 72f;
        protected override float BladeLength => 92f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 30f;
        protected override float ThrustEasePower => 6f + ChargeT * 2f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.045f;
        protected override int HitboxSize => 56;
        protected override int HitstopFrames => FullyCharged ? 3 : 2;
        protected override float ThrustPitch => -0.25f;

        protected override Color EdgeColor => PaleEdge;
        protected override Color CoreColor => ShadowPurple;

        /// <summary>最大蓄力约半秒，长按即满</summary>
        protected override float MaxChargeFrames => 32f;

        private bool FullyCharged => ChargeT >= 0.8f;
        private bool fullChargeCuePlayed;

        /// <summary>蓄力期黑焰收束：焰须自四周扑向枪身，满蓄时机一声低鸣</summary>
        protected override void OnChargingTick() {
            if (!fullChargeCuePlayed && ChargeT >= 1f) {
                fullChargeCuePlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.5f }, Owner.Center);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            //焰须收束（吸入向粒子，密度随蓄力升）
            if (Main.rand.NextFloat() < 0.35f + ChargeT * 0.4f) {
                Vector2 shaftAt = Hand + stabUnit * Main.rand.NextFloat(10f, holdout + BladeLength * 0.8f);
                Vector2 from = shaftAt + Main.rand.NextVector2Unit() * Main.rand.NextFloat(36f, 64f);
                Color c = Main.rand.NextBool(3) ? ShadowHot : ShadowPurple;
                PRTLoader.NewParticle<PRT_Light>(from, (shaftAt - from) * 0.14f, c,
                    Main.rand.NextFloat(0.35f, 0.65f))?.Configure(Main.rand.Next(8, 13), 0.5f, 1.3f);
            }
        }

        /// <summary>放刺瞬间：几何与伤害按蓄力结算</summary>
        protected override void OnChargeRelease() {
            reachChargeMul = 1f + ChargeT * 0.6f;
            Projectile.damage = (int)(BaseDamage * (1f + ChargeT * 0.85f));
        }

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            if (FullyCharged) {
                SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.45f, Pitch = -0.2f }, Owner.Center);
            }
            //爆发帧黑焰沿枪杆向后拉丝
            int count = 3 + (int)(ChargeT * 4f);
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.4f, 1f));
                Color c = Main.rand.NextBool(3) ? ShadowHot : ShadowPurple;
                PRTLoader.NewParticle<PRT_Spark>(at, -stabUnit * Main.rand.NextFloat(2.5f, 6f), c,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>驻相黑焰驻烧：满蓄驻相枪尖持续吐焰</summary>
        protected override void OnTick(int phase) {
            if (VaultUtils.isServer || phase != PhaseDwell || !FullyCharged) {
                return;
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(TipPos + Main.rand.NextVector2Circular(6f, 6f),
                    stabUnit.RotatedByRandom(0.5) * Main.rand.NextFloat(0.8f, 2.2f),
                    Main.rand.NextBool(3) ? ShadowHot : ShadowPurple,
                    Main.rand.NextFloat(0.4f, 0.8f))?.Configure(Main.rand.Next(10, 16), 0.45f, 1.4f);
            }
        }

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (FullyCharged) {
                modifiers.Knockback *= 1.4f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //影燃：命中挂暗影焰，蓄力越足烧得越久
            target.AddBuff(BuffID.ShadowFlame, 120 + (int)(ChargeT * 180f));

            //满蓄首个命中：伤口迸出三团噬影火（owner 端生成，随生成包过线）
            if (FullyCharged && firstOnTarget && Projectile.numHits <= 1 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = stabUnit.RotatedBy((i - 1) * 0.55f) * 7.5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, vel,
                        ModContent.ProjectileType<GsDarkLanceEmberProj>(),
                        (int)(BaseDamage * 0.35f), Projectile.knockBack * 0.3f, Owner.whoAmI);
                }
            }
        }

        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, ShadowHot, 0.18f + ChargeT * 0.10f)
                ?.Configure(10, 0.8f);
            int sparks = 5 + (int)(ChargeT * 5f);
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.6) * Main.rand.NextFloat(3f, 7f + ChargeT * 4f);
                Color c = Main.rand.NextBool(3) ? PaleEdge : ShadowPurple;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.4f, Pitch = 0.2f }, target.Center);
            }
        }

        /// <summary>蓄力可视化：黑焰壳缠绕枪身（暗层用真 alpha 的 Extra_98，加色层缀热紫）</summary>
        protected override void DrawUnderBlade(SpriteBatch sb) {
            if (ChargeT <= 0.03f || CurrentPhase != PhaseWindup) {
                return;
            }
            Texture2D dark = CWRAsset.Extra_98?.Value;
            if (dark == null) {
                return;
            }
            //黑焰壳沿枪身三段布点（定值布点，无随机）
            for (int i = 0; i < 3; i++) {
                float along = 14f + i * (BladeLength * 0.32f);
                Vector2 at = Hand + stabUnit * (holdout + along) - Main.screenPosition;
                float wob = MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 2.1f) * 0.06f;
                float sc = (0.24f + i * 0.05f + wob) * ChargeT;
                sb.Draw(dark, at, null, VoidDeep * (0.55f * ChargeT), 0f, dark.Size() / 2f, sc, SpriteEffects.None, 0f);
                sb.Draw(dark, at, null, ShadowPurple with { A = 0 } * (0.35f * ChargeT), 0f, dark.Size() / 2f, sc * 0.7f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 噬影火团：满蓄刺击从伤口迸出，短暂直飞后追咬最近猎物，命中挂暗影焰。<br/>
    /// 自绘三层：真 alpha 暗核 + 热紫加色晕 + 苍白芯点；轨迹摆动用 whoAmI 种子（绘制无随机）
    /// </summary>
    internal class GsDarkLanceEmberProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.DarkLance");

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;

            //短暂直飞后追咬最近猎物；掉头不掉速
            if (Timer > 10f) {
                NPC target = Projectile.Center.FindClosestNPC(520f);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1.02f, 0.12f);
                }
            }
            float speed = Projectile.velocity.Length();
            if (speed < 6f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 6f;
            }

            Lighting.AddLight(Projectile.Center, GsDarkLanceHeld.ShadowPurple.ToVector3() * 0.32f);

            if (VaultUtils.isServer) {
                return;
            }
            //焰尾：暗紫拉丝随飞
            if (Timer % 2f == 0f) {
                Color c = Main.rand.NextBool(3) ? GsDarkLanceHeld.ShadowHot : GsDarkLanceHeld.ShadowPurple;
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.12f, c,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(9, 14), 0.5f, 1.5f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.ShadowFlame, 180);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f);
                Color c = Main.rand.NextBool() ? GsDarkLanceHeld.ShadowPurple : GsDarkLanceHeld.VoidDeep;
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>三层自绘：真 alpha 暗核压底、热紫晕、苍白芯；摆动相位吃 whoAmI 种子</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D dark = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (dark == null || glow == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float seed = Projectile.whoAmI * 1.37f;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + seed);

            //暗核（真 alpha 才能压暗）
            Main.spriteBatch.Draw(dark, drawPos, null, GsDarkLanceHeld.VoidDeep * 0.7f, seed,
                dark.Size() / 2f, 0.16f * pulse, SpriteEffects.None, 0f);
            //热紫晕（加色 A=0）
            Main.spriteBatch.Draw(glow, drawPos, null, GsDarkLanceHeld.ShadowPurple with { A = 0 } * 0.75f, 0f,
                glow.Size() / 2f, 0.55f * pulse, SpriteEffects.None, 0f);
            //苍白芯点
            Main.spriteBatch.Draw(glow, drawPos, null, GsDarkLanceHeld.PaleEdge with { A = 0 } * 0.55f, 0f,
                glow.Size() / 2f, 0.22f * pulse, SpriteEffects.None, 0f);
            return false;
        }
    }
}
