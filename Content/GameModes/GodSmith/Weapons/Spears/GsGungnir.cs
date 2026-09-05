using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【A 档】贡格尼尔重铸：奥丁的誓约之枪。<br/>
    /// 材质：神鎏金圣枪。签名行为：左键神威三段刺，终结拍更深更沉，命中金属重音 + 震屏
    /// </summary>
    internal class GsGungnir : GsSpearScheme
    {
        public override int TargetItemID => ItemID.Gungnir;

        protected override string GsDescFallback =>
            "Reforged: a stately three-beat thrust crowned by a golden finisher";
        protected override int HeldProjType => ModContent.ProjectileType<GsGungnirHeld>();

        protected override int ComboBeats => 3;
        protected override int ComboResetFrames => 60;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//终结拍吃掉预算，底伤只碰边，综合 DPS 落在原版 105%~120%
    }

    /// <summary>
    /// 贡格尼尔左键手持突刺：神威三段。0/1 拍中线快刺，2 拍神威重刺——
    /// 更深更沉，命中金属重音 + 震屏
    /// </summary>
    internal class GsGungnirHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Gungnir;

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
                //神威重刺：低鸣
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.35f }, Owner.Center);
            }
        }

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
    }
}
