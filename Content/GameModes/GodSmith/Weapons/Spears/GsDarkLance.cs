using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛首把·垂直切片】黑暗长枪重铸：噬影蓄力长刺。<br/>
    /// 材质：黑曜枪杆上缠绕的噬影黑焰。签名行为：①按住蓄力，枪尖压低蓄势
    /// ②放刺距离与伤害随蓄力增长，满蓄驻相延长
    /// ③满蓄命中从伤口迸出三团噬影火，追咬近旁猎物并挂暗影焰
    /// </summary>
    internal class GsDarkLance : GsSpearScheme
    {
        public override int TargetItemID => ItemID.DarkLance;

        protected override string GsDescFallback =>
            "Reforged: hold to feed the lance with devouring shadowflame, release to strike farther and harder;\na fully charged hit bursts three shadow embers from the wound";
        protected override int HeldProjType => ModContent.ProjectileType<GsDarkLanceHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;//蓄力收益走机制端，底伤小补，综合 DPS 落在原版 105%~120%
    }

    /// <summary>
    /// 黑暗长枪手持突刺：蓄力驻留在蓄势末；
    /// 放刺深度 ×1~1.6、伤害 ×1~1.85 随蓄力，满蓄（≥80%）命中迸噬影火团
    /// </summary>
    internal class GsDarkLanceHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.DarkLance;

        protected override float WindupFrames => 5f;
        /// <summary>满蓄 reach ×1.6：刺出帧数随行程等比放大（6→9），驻相收敛到 4 帧（桥接后驻相才是卖点，悬空不是）</summary>
        protected override float ThrustFrames => 6f + ChargeT * 3f;
        protected override float DwellFrames => 3f + ChargeT * 1f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => 18f;
        protected override float StabReach => 72f;
        protected override float BladeLength => 92f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 30f;
        protected override float ThrustEasePower => 3f + ChargeT * 0.5f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.045f;
        protected override int HitboxSize => 56;
        protected override int HitstopFrames => FullyCharged ? 3 : 2;
        protected override float ThrustPitch => -0.25f;

        /// <summary>最大蓄力约半秒，长按即满</summary>
        protected override float MaxChargeFrames => 32f;

        private bool FullyCharged => ChargeT >= 0.8f;
        private bool fullChargeCuePlayed;

        /// <summary>蓄力期：满蓄时机一声低鸣</summary>
        protected override void OnChargingTick() {
            if (!fullChargeCuePlayed && ChargeT >= 1f) {
                fullChargeCuePlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.5f }, Owner.Center);
                }
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

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.4f, Pitch = 0.2f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 噬影火团：满蓄刺击从伤口迸出，短暂直飞后追咬最近猎物，命中挂暗影焰。原版暗影焰贴图默认绘制
    /// </summary>
    internal class GsDarkLanceEmberProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowFlame;
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
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.ShadowFlame, 180);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
        }
    }
}
