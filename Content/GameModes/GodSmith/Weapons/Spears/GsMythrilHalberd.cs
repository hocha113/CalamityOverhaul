using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 秘银戟重铸：秘银月刃。<br/>
    /// 材质：翠绿秘银戟刃。签名行为：①两拍连刺——轻拍快刺、重拍深刺几何差异化
    /// ②重拍刺出的爆发帧放出一道短程秘银月牙刃，飞约 220 像素消散、可穿透两个目标
    /// ③重拍命中月鸣泛音，轻拍是干净的秘银脆响
    /// </summary>
    internal class GsMythrilHalberd : GsSpearScheme
    {
        public override int TargetItemID => ItemID.MythrilHalberd;

        protected override string GsDescFallback =>
            "Reforged: two-beat halberd work, a quick jab then a deep heavy thrust;\nthe heavy beat looses a short-ranged mythril crescent that pierces twice";
        protected override int HeldProjType => ModContent.ProjectileType<GsMythrilHalberdHeld>();

        protected override int ComboBeats => 2;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;//月牙刃吃掉大半机制预算，综合 DPS 落在原版 106%~118%
    }

    /// <summary>
    /// 秘银戟手持突刺。ai[0]=拍号 0 轻快刺 / 1 重深刺；
    /// 重拍爆发帧放 GsMythrilHalberdWaveProj（60% 伤害，穿透 2）
    /// </summary>
    internal class GsMythrilHalberdHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.MythrilHalberd;

        private bool IsHeavy => ComboStage == 1;

        //重拍更沉更深
        protected override float WindupFrames => IsHeavy ? 6f : 4f;
        protected override float ThrustFrames => IsHeavy ? 6f : 5f;
        protected override float DwellFrames => IsHeavy ? 4f : 3f;
        protected override float RecoverFrames => IsHeavy ? 9f : 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => IsHeavy ? 18f : 13f;
        protected override float StabReach => IsHeavy ? 72f : 58f;
        protected override float BladeLength => 92f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override float ThrustEasePower => IsHeavy ? 3.2f : 2.7f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => IsHeavy ? 0.05f : 0.035f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => IsHeavy ? 3 : 2;
        protected override float ThrustPitch => IsHeavy ? -0.28f : -0.10f;

        protected override void OnInit() {
            if (IsHeavy) {
                Projectile.damage = (int)(Projectile.damage * 1.15f);
            }
        }

        protected override void OnThrustBurst() {
            //重拍爆发帧放月牙刃（owner 端生成，随生成包过线）
            if (IsHeavy && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), TipPos, stabUnit * 11f,
                    ModContent.ProjectileType<GsMythrilHalberdWaveProj>(),
                    (int)(BaseDamage * 0.6f), Projectile.knockBack * 0.4f, Owner.whoAmI);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            if (IsHeavy) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.25f }, Owner.Center);
            }
        }

        /// <summary>命中反馈：秘银脆响；重拍升级为月鸣泛音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.35f, Pitch = IsHeavy ? -0.1f : 0.3f, MaxInstances = 3 }, target.Center);
            if (IsHeavy) {
                SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 2 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 秘银月牙刃：重拍刺出时自戟尖放出的短程刃气，飞约 220 像素消散，穿透 2。原版泰拉光束贴图默认绘制
    /// </summary>
    internal class GsMythrilHalberdWaveProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TerraBeam;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MythrilHalberd");

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;//穿透 2：至多命中两个目标
            Projectile.timeLeft = 20;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            //原版光束贴图竖向，刃口朝飞行向需补 π/2
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            //末段骤减收势（不匀速直飞）
            if (Life > 12f) {
                Projectile.velocity *= 0.88f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
        }
    }
}
