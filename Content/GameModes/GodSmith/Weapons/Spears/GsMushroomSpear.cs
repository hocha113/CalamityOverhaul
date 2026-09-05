using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 蘑菇矛重铸：菌雷。<br/>
    /// 材质：发光菌木矛。签名行为：①两拍连刺——轻拍快刺、重拍深刺；重拍命中在目标处
    /// 种下一枚发光菌雷 ②菌雷驻场约 0.8 秒后孢爆——
    /// 小范围 60% 伤害，并给持矛者回 2 点生命微疗 ③命中反馈是软木闷响
    /// </summary>
    internal class GsMushroomSpear : GsSpearScheme
    {
        public override int TargetItemID => ItemID.MushroomSpear;

        protected override string GsDescFallback =>
            "Reforged: the heavy second beat plants a glowing spore mine in the wound;\nit bursts moments later, harming foes around and feeding 2 life back to the wielder";
        protected override int HeldProjType => ModContent.ProjectileType<GsMushroomSpearHeld>();

        protected override int ComboBeats => 2;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;//菌雷延迟爆 + 微疗吃机制预算，综合 DPS 落在原版 106%~118%
    }

    /// <summary>
    /// 蘑菇矛手持突刺。ai[0]=拍号 0 轻快刺 / 1 重深刺；
    /// 重拍首个命中在目标处种菌雷（owner 端生成，驻场后爆 60% 伤害）
    /// </summary>
    internal class GsMushroomSpearHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.MushroomSpear;

        private bool IsHeavy => ComboStage == 1;

        protected override float WindupFrames => IsHeavy ? 5f : 4f;
        protected override float ThrustFrames => IsHeavy ? 6f : 5f;
        protected override float DwellFrames => IsHeavy ? 4f : 3f;
        protected override float RecoverFrames => IsHeavy ? 9f : 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => IsHeavy ? 16f : 13f;
        protected override float StabReach => IsHeavy ? 68f : 56f;
        protected override float BladeLength => 90f;
        protected override float CollisionWidth => 29f;
        protected override float TipGreedRadius => 26f;
        protected override float ThrustEasePower => IsHeavy ? 3.1f : 2.6f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsHeavy ? 0.045f : 0.034f;
        protected override int HitboxSize => 50;
        protected override int HitstopFrames => IsHeavy ? 3 : 2;
        protected override float ThrustPitch => IsHeavy ? -0.30f : -0.12f;

        protected override void OnInit() {
            if (IsHeavy) {
                Projectile.damage = (int)(Projectile.damage * 1.12f);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //命中反馈：软木闷响
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.45f, Pitch = -0.5f, MaxInstances = 3 }, target.Center);
            }
            //重拍首个命中种菌雷（owner 端生成，随生成包过线）
            if (!IsHeavy || !firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsMushroomSpearMineProj>(),
                (int)(BaseDamage * 0.6f), Projectile.knockBack * 0.3f, Owner.whoAmI);
        }
    }

    /// <summary>
    /// 发光菌雷：重拍种在目标处，驻场约 0.8 秒后孢爆——
    /// 小范围伤害窗 5 帧，爆点给持矛者回 2 点生命。<br/>
    /// 原版蘑菇矛孢子贴图默认绘制；驻场期无伤害，只有爆窗结算
    /// </summary>
    internal class GsMushroomSpearMineProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Mushroom;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MushroomSpear");

        /// <summary>驻场帧数（真实帧）；到点转入爆窗</summary>
        private const int ArmFrames = 48;
        /// <summary>爆窗帧数：Resize 后的伤害判定窗</summary>
        private const int BurstFrames = 5;

        private ref float Life => ref Projectile.localAI[0];
        private bool Bursting => Life > ArmFrames;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ArmFrames + BurstFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>驻场期不结算，只有爆窗有伤害</summary>
        public override bool? CanDamage() => Bursting ? null : false;

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            if (Life == ArmFrames + 1) {
                Detonate();
            }
        }

        /// <summary>孢爆：伤害窗展开 + 微疗 + 爆响</summary>
        private void Detonate() {
            //爆窗判定范围（一次性展开，配合 CanDamage 的爆窗开关）
            Projectile.Resize(120, 120);

            //微疗：菌雷把一口养分还给持矛者（owner 端本地结算，Heal 自带同步）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Main.player[Projectile.owner].Heal(2);
            }

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.4f, Pitch = 0.45f }, Projectile.Center);
        }
    }
}
