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
    /// 【长矛】三叉戟重铸：三叉分水。<br/>
    /// 材质：海神青铜三叉，戟身覆潮痕。签名行为：①命中从伤口迸出三股扇形水束，
    /// 穿刺飞行短程后化作水花消散 ②身处水中或雨天时潮汐涨势，水束更快更远、戟尖泛潮光
    /// ③命中带水花与浪声，与铁器命中截然不同
    /// </summary>
    internal class GsTrident : GsSpearScheme
    {
        public override int TargetItemID => ItemID.Trident;

        protected override string GsDescFallback =>
            "Reforged: every strike bursts three jets of seawater from the wound;" +
            "\nwhile wet or in the rain the tide rises, jets fly faster and farther";

        protected override int HeldProjType => ModContent.ProjectileType<GsTridentHeld>();

        //三股水束吃掉大半预算，底伤小补，综合 DPS 落在原版 105%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;
    }

    /// <summary>
    /// 三叉戟手持突刺：一记沉稳的分水刺；
    /// 每刺首个命中迸出三股扇形水束（各 30% 伤害），水中/雨天水束增强
    /// </summary>
    internal class GsTridentHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Trident;

        //波塞冬蓝绿色板
        internal static readonly Color SeaFoam = new(178, 244, 232);   //浪沫亮
        internal static readonly Color TideTeal = new(56, 182, 190);   //潮汐青
        internal static readonly Color DeepSea = new(22, 74, 112);     //深海底色
        internal static readonly Color PearlWhite = new(238, 248, 250);//珠白芯

        //收势拉长对齐原版 31 帧节奏，水束的加成才有预算
        protected override float WindupFrames => 6f;
        protected override float ThrustFrames => 6f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 11f;
        protected override float RestHoldout => 13f;
        protected override float PullbackDist => 15f;
        protected override float StabReach => 62f;
        protected override float BladeLength => 88f;
        protected override float CollisionWidth => 32f;
        protected override float TipGreedRadius => 28f;
        protected override float ThrustEasePower => 2.8f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.04f;
        protected override int HitboxSize => 54;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.22f;

        protected override Color EdgeColor => SeaFoam;
        protected override Color CoreColor => TideTeal;
        protected override Color ShaftColor => DeepSea with { A = 235 };

        /// <summary>潮汐涨势：身处水中或雨天（owner 端读取，水束参数随生成同步）</summary>
        private bool TideRising => Owner.wet || Owner.ZoneRain;

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = ThrustPitch }, Owner.Center);
            //爆发帧浪沫沿戟身洒出
            for (int i = 0; i < 3; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.5f, 1f));
                Color c = Main.rand.NextBool(3) ? PearlWhite : TideTeal;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(3.5f, 7f), c,
                    Main.rand.NextFloat(0.32f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>潮汐涨势可视化：戟尖泛潮光</summary>
        protected override float ExtraGlowStrength() => TideRising ? 0.16f : 0f;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //三叉分水：每刺首个命中迸三股扇形水束（owner 端生成，速度随生成包过线）
            if (!firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            bool rising = TideRising;
            float speed = rising ? 12.5f : 9.5f;
            Vector2 from = Vector2.Lerp(TipPos, target.Center, 0.5f);
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = stabUnit.RotatedBy(i * 0.30f) * speed;
                //ai1 记被叉住的目标：水束只溅向别人，不给单体白送三段
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                    ModContent.ProjectileType<GsTridentJetProj>(),
                    (int)(BaseDamage * 0.30f), Projectile.knockBack * 0.25f, Owner.whoAmI,
                    rising ? 1f : 0f, target.whoAmI);
            }
        }

        /// <summary>命中反馈：水花 + 浪声，潮汐涨势时更盛</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.65f, Pitch = 0.15f, MaxInstances = 3 }, pos);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, SeaFoam, 0.18f)?.Configure(9, 0.75f);
            int sparks = TideRising ? 8 : 5;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.65) * Main.rand.NextFloat(3f, 7f);
                Color c = Main.rand.NextBool(3) ? PearlWhite : TideTeal;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.36f, 0.58f))
                    ?.Configure(true, Main.rand.Next(12, 18));
            }
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Water,
                    stabUnit.RotatedByRandom(0.9) * Main.rand.NextFloat(2f, 5f), 60, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }

        /// <summary>潮痕：涨势时沿戟身缀两枚潮光点（定值布点，无随机）</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            if (!TideRising || FanFade <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                Vector2 at = Hand + stabUnit * (holdout + BladeLength * (0.45f + i * 0.35f)) - Main.screenPosition;
                float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + i * 2.4f + Projectile.whoAmI);
                Color c = TideTeal with { A = 0 } * (0.38f * FanFade * pulse);
                sb.Draw(glow, at, null, c, 0f, glow.Size() / 2f, 0.30f * pulse, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 分水水束：命中迸出的扇形小穿刺水刺，短程直飞后化水花消散。
    /// ai[0]=潮汐涨势旗（1 = 射程延长），ai[1]=被叉住的目标（水束不回头打它）。<br/>
    /// 自绘：LightShot 拉丝染海蓝双层 + 珠白芯；随飞洒水尘
    /// </summary>
    internal class GsTridentJetProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.Trident");

        private ref float Timer => ref Projectile.localAI[0];
        private bool TideRising => Projectile.ai[0] > 0f;

        /// <summary>水束是溅射伤害，不回头打被叉住的目标</summary>
        public override bool? CanHitNPC(NPC target)
            => target.whoAmI == (int)Projectile.ai[1] ? false : null;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 15;//速度 ~9.5 时飞约 140px 消散
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Timer == 0f && TideRising) {
                Projectile.timeLeft = 18;//涨势：更快 + 更远
            }
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsTridentHeld.TideTeal.ToVector3() * 0.25f);

            if (VaultUtils.isServer) {
                return;
            }
            //随飞洒水尘
            if (Timer % 2f == 0f) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f), 80, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //化作水花消散
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f), 60, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsTridentHeld.SeaFoam, 0.14f)?.Configure(7, 0.6f);
        }

        /// <summary>水束自绘：深海压底 + 潮汐青拉丝 + 珠白芯（加色 A=0，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            Vector2 texSize = streak.Size();
            //出生与临终各留两帧淡入淡出
            float lifeFade = Math.Min(1f, Math.Min(Timer / 2f, Projectile.timeLeft / 3f));
            float len = (TideRising ? 46f : 38f) * lifeFade;
            float sway = 1f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.whoAmI * 1.7f);

            Color deep = GsTridentHeld.DeepSea with { A = 0 } * (0.55f * lifeFade);
            Main.spriteBatch.Draw(streak, drawPos, null, deep, rot, texSize / 2f,
                new Vector2(len / texSize.X, 0.14f * sway), SpriteEffects.None, 0f);
            Color teal = GsTridentHeld.TideTeal with { A = 0 } * (0.8f * lifeFade);
            Main.spriteBatch.Draw(streak, drawPos, null, teal, rot, texSize / 2f,
                new Vector2(len / texSize.X * 0.85f, 0.09f * sway), SpriteEffects.None, 0f);
            Color core = GsTridentHeld.PearlWhite with { A = 0 } * (0.6f * lifeFade);
            Main.spriteBatch.Draw(streak, drawPos, null, core, rot, texSize / 2f,
                new Vector2(len / texSize.X * 0.6f, 0.045f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
