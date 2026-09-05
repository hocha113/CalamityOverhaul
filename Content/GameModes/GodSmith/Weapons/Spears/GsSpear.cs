using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛】铁头木杆矛重铸：操典渐进三段刺。<br/>
    /// 材质：白蜡木杆磨亮铁尖。签名行为：①三拍操典——一拍练手、二拍加深、三拍老兵刺，
    /// 逐拍更深更快（reach 与刺出锐度递增）
    /// ②第三拍沿出手向小前压半步，命中金属重音 + 小震屏
    /// </summary>
    internal class GsSpear : GsSpearScheme
    {
        public override int TargetItemID => ItemID.Spear;

        protected override string GsDescFallback =>
            "Reforged: drill-manual thrusts in three beats, each one deeper and faster than the last;\nthe third beat steps into the strike like a veteran";
        protected override int HeldProjType => ModContent.ProjectileType<GsSpearHeld>();

        protected override int ComboBeats => 3;

        //公认弱势：最早期白板长矛，签名只是操典参数与体术；周期快于原版 31f，频率折算后 1.15 即贴弱势 135% 上限
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;
    }

    /// <summary>
    /// 铁头矛手持突刺。ai[0]=拍号 0 新兵刺 / 1 老练刺 / 2 老兵刺：
    /// 逐拍回拉更深、刺出更快更远，第三拍前压半步且伤害上浮
    /// </summary>
    internal class GsSpearHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Spear;

        private int Beat => Math.Clamp(ComboStage, 0, 2);
        private bool IsVeteranBeat => Beat >= 2;

        //操典渐进：逐拍更短促（新兵慢工整，老兵快狠准），行程与锐度递增。
        //前两拍收势特意拉长（对齐原版 31 帧节奏），把「快」留给老兵拍，全程不超原版均速太多
        protected override float WindupFrames => 6f - Beat;
        protected override float ThrustFrames => 6f - Beat * 0.5f;
        protected override float DwellFrames => 3f + Beat;
        protected override float RecoverFrames => 14f - Beat * 2f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => 12f + Beat * 3f;
        protected override float StabReach => 55f + Beat * 11f;
        protected override float BladeLength => 84f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 26f;
        protected override float ThrustEasePower => 2.5f + Beat * 0.25f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.035f + Beat * 0.012f;
        protected override int HitboxSize => 50;
        protected override int HitstopFrames => IsVeteranBeat ? 3 : 2;
        protected override float ThrustPitch => -0.30f + Beat * 0.08f;

        protected override void OnInit() {
            //老兵刺伤害只浮一成（底伤已按弱势补足，老兵拍又快又深，机制端只留零头）
            if (IsVeteranBeat) {
                Projectile.damage = (int)(Projectile.damage * 1.10f);
            }
        }

        protected override void OnThrustBurst() {
            //老兵刺前压：爆发首帧沿出手向踏半步（owner 端权威，位置随原版同步）
            if (IsVeteranBeat && Owner.whoAmI == Main.myPlayer && !Owner.mount.Active) {
                Owner.velocity.X += facingDir * 2.8f;
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.75f, Pitch = ThrustPitch }, Owner.Center);
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!IsVeteranBeat || !firstOnTarget) {
                return;
            }
            //老兵刺命中的升级反馈：金属重音 + 小震屏
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.55f, Pitch = 0.1f }, target.Center);
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        target.Center, stabUnit, 3.2f, 5f, 7, 480f, FullName));
                }
            }
        }
    }
}
