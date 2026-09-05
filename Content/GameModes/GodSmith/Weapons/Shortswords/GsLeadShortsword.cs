using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 铅短剑重铸「铅蚀」。<br/>
    /// 材质：钝重铅刃，刃口泛病绿毒锈。签名行为：①命中挂中毒 ②对已中毒目标伤害 ×1.25，
    /// 补刀时淤浊低音 ③钝重低音的迟滞刺击手感
    /// </summary>
    internal class GsLeadShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.LeadShortsword;

        protected override string GsDescFallback =>
            "Reforged: the corroded lead edge poisons whatever it touches;\nstriking an already poisoned foe bites 25% deeper into the rot";
        protected override int HeldProjType => ModContent.ProjectileType<GsLeadShortswordHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.20f;//弱势开局武器，毒蚀收益要先铺毒再兑现，底伤补两成

    }

    /// <summary>
    /// 铅短剑手持突刺：钝重迟滞的时间线（出 3 刺 4 驻 3 收 6）。
    /// 命中挂原版中毒；对已中毒目标（判定先于本次挂毒）伤害 ×1.25
    /// </summary>
    internal class GsLeadShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.LeadShortsword;

        protected override float WindupFrames => 3f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 6f;
        protected override float PullbackDist => 11f;
        protected override float StabReach => 32f;
        protected override float BladeLength => 44f;
        protected override float ThrustEasePower => 2.5f;//钝头刺出没那么锐利
        protected override int HitstopFrames => 2;
        protected override float LeanAmp => 0.036f;
        protected override float ThrustPitch => -0.18f;//铅的钝重低音

        /// <summary>本次命中是否吃到了蚀毒增伤（ModifyHit 与 OnHit 同链，供反馈分流）</summary>
        private bool rotBite;

        /// <summary>蚀毒：对已中毒目标增伤（先判后挂，首击铺毒、次击兑现）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            rotBite = target.HasBuff(BuffID.Poisoned);
            if (rotBite) {
                modifiers.FinalDamage *= 1.25f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //铅锈铺毒：每次命中都续毒
            target.AddBuff(BuffID.Poisoned, 240);

            //蚀毒兑现的升级反馈：淤浊低音
            if (rotBite && firstOnTarget && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.45f, Pitch = -0.3f }, target.Center);
            }
        }
    }
}
