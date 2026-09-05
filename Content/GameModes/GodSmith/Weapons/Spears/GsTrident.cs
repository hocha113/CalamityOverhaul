using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
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
    /// 穿刺飞行短程后消散 ②身处水中或雨天时潮汐涨势，水束更快更远
    /// ③命中带浪声，与铁器命中截然不同
    /// </summary>
    internal class GsTrident : GsSpearScheme
    {
        public override int TargetItemID => ItemID.Trident;

        protected override string GsDescFallback =>
            "Reforged: every strike bursts three jets of seawater from the wound;\nwhile wet or in the rain the tide rises, jets fly faster and farther";
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

        /// <summary>潮汐涨势：身处水中或雨天（owner 端读取，水束参数随生成同步）</summary>
        private bool TideRising => Owner.wet || Owner.ZoneRain;

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = ThrustPitch }, Owner.Center);
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            Vector2 from = Vector2.Lerp(TipPos, target.Center, 0.5f);
            //命中反馈：浪声
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.65f, Pitch = 0.15f, MaxInstances = 3 }, from);
            }
            //三叉分水：每刺首个命中迸三股扇形水束（owner 端生成，速度随生成包过线）
            if (!firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            bool rising = TideRising;
            float speed = rising ? 12.5f : 9.5f;
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = stabUnit.RotatedBy(i * 0.30f) * speed;
                //ai1 记被叉住的目标：水束只溅向别人，不给单体白送三段
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                    ModContent.ProjectileType<GsTridentJetProj>(),
                    (int)(BaseDamage * 0.30f), Projectile.knockBack * 0.25f, Owner.whoAmI,
                    rising ? 1f : 0f, target.whoAmI);
            }
        }
    }

    /// <summary>
    /// 分水水束：命中迸出的扇形小穿刺水刺，短程直飞后消散。原版水流贴图默认绘制。
    /// ai[0]=潮汐涨势旗（1 = 射程延长），ai[1]=被叉住的目标（水束不回头打它）
    /// </summary>
    internal class GsTridentJetProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WaterStream;
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
            //原版水流贴图竖向，对齐飞行向需补 π/2
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.3f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
        }
    }
}
