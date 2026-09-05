using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 直尺重铸「丈量惩戒」。<br/>
    /// 材质：教务处硬木直尺，红漆刻度。签名行为：①刺线最后两成是 sweet spot——用尺头丈量到位
    /// 伤害 ×1.6，附升级音 ②量歪了（非尺头命中）只有 ×0.85
    /// </summary>
    internal class GsRuler : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.Ruler;

        protected override string GsDescFallback =>
            "Reforged: discipline demands exact measurement, land the very tip for a 1.6x graded strike;\nsloppy hits closer to the hand are marked down to 0.85x";
        protected override int HeldProjType => ModContent.ProjectileType<GsRulerHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.30f;//joke 弱势武器（原版是 noMelee 的量地工具），sweet spot 期望约 ×1.0，按弱势条款补三成底伤
    }

    /// <summary>
    /// 直尺手持突刺：轻而长的丈量刺（StabReach 全族最远、刺线最细）。
    /// sweet spot 判定：target 碰撞箱到 TipPos 的距离 ≤ 刺线全长两成
    /// </summary>
    internal class GsRulerHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Ruler;

        protected override float WindupFrames => 2f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 5f;
        protected override float PullbackDist => 7f;
        protected override float StabReach => 40f;//量程全族最远
        protected override float BladeLength => 44f;
        protected override float CollisionWidth => 18f;//尺薄，刺线最细
        protected override float TipGreedRadius => 20f;
        protected override float ThrustEasePower => 2.55f;
        protected override int HitstopFrames => 1;
        protected override float LeanAmp => 0.024f;
        protected override float ThrustPitch => 0.35f;//木尺破空的轻脆高音

        /// <summary>本次命中是否量到了尺头（ModifyHit 与 OnHit 同链，供反馈分流）</summary>
        private bool sweetHit;

        /// <summary>丈量判定：目标碰撞箱到刺尖距离 ≤ 刺线全长两成 = 尺头命中</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            float lineLen = holdout + BladeLength;
            sweetHit = target.Hitbox.Distance(TipPos) <= lineLen * 0.20f;
            modifiers.FinalDamage *= sweetHit ? 1.6f : 0.85f;
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (VaultUtils.isServer) {
                return;
            }
            if (!sweetHit) {
                //量歪的钝响：挨了一下但不痛快
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.3f, Pitch = -0.4f }, target.Center);
                return;
            }
            if (!firstOnTarget) {
                return;
            }
            //丈量到位的升级反馈：升级音
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.35f }, target.Center);
        }
    }
}
