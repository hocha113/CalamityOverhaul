using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 钯金长枪重铸：生血枪锋。<br/>
    /// 材质：温血钯金枪刃。签名行为：①命中积攒「生机」层
    /// ②叠满 5 层触发生机迸发——回复 5 点生命并清层
    /// ③生机迸发的那一击带治愈钟音，与普通命中的软音明确区分
    /// </summary>
    internal class GsPalladiumPike : GsSpearScheme
    {
        public override int TargetItemID => ItemID.PalladiumPike;

        protected override string GsDescFallback =>
            "Reforged: each hit stores a charge of vigor in the warm palladium edge;\nat five charges the spear bursts them, restoring 5 life to its wielder";
        protected override int HeldProjType => ModContent.ProjectileType<GsPalladiumPikeHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//回复收益温和，底伤补零头，综合 DPS 落在原版 108%~118%
    }

    /// <summary>每玩家生机层数（跨攻击持久，故不放共享单例方案里）</summary>
    internal class GsPalladiumPikePlayer : ModPlayer
    {
        /// <summary>生机层数 0~5</summary>
        internal int vigor;
        /// <summary>断层倒计时：5 秒没有新命中则清层</summary>
        internal int vigorDecay;

        public override void PostUpdate() {
            if (vigorDecay > 0 && --vigorDecay == 0) {
                vigor = 0;
            }
        }
    }

    /// <summary>
    /// 钯金长枪手持突刺：命中积生机层（owner 端守门写 ModPlayer），
    /// 叠满 5 层触发回复并清层
    /// </summary>
    internal class GsPalladiumPikeHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.PalladiumPike;

        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 5f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 62f;
        protected override float BladeLength => 90f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 26f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.038f;
        protected override int HitboxSize => 50;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.16f;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //命中反馈：音色偏软（与钴钢脆响区分）
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            }
            //生机层只在 owner 端结算（ModPlayer 是每玩家状态，写入守 IsOwnedByLocalPlayer）
            if (!firstOnTarget || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            GsPalladiumPikePlayer mp = Owner.GetModPlayer<GsPalladiumPikePlayer>();
            mp.vigor++;
            mp.vigorDecay = 300;
            if (mp.vigor < 5) {
                return;
            }
            //生机迸发：回复 5 点生命并清层（Heal 自带治疗数字与联机同步）
            mp.vigor = 0;
            Owner.Heal(5);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = 0.35f }, Owner.Center);
        }
    }
}
