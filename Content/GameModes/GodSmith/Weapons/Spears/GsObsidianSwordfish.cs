using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛】黑曜剑鱼重铸：淬火迸屑。<br/>
    /// 材质：熔岩湖里淬出的黑曜石吻骨，芯里锁着余温。签名行为：①命中点燃目标
    /// ②淬火状态（自身着火或浸岩浆）矛芯回温，伤害 +15% ③命中带岩石脆响与火燎声，重矛慢而狠
    /// </summary>
    internal class GsObsidianSwordfish : GsSpearScheme
    {
        public override int TargetItemID => ItemID.ObsidianSwordfish;

        protected override string GsDescFallback =>
            "Reforged: strikes set enemies on fire and burst obsidian shards from the wound;\nwhile you are burning or soaked in lava the core reheats, dealing 15% more damage";
        protected override int HeldProjType => ModContent.ProjectileType<GsObsidianSwordfishHeld>();

        //点燃+淬火条件增伤吃掉大半预算，底伤只补零头，综合 DPS 落在原版 104%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 黑曜剑鱼手持突刺：六把里最沉的矛；命中点燃，
    /// 淬火状态（Owner 着火/岩浆湿身）伤害 +15%
    /// </summary>
    internal class GsObsidianSwordfishHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.ObsidianSwordfish;

        //六把里最沉的时间线：黑曜石又重又狠
        protected override float WindupFrames => 6f;
        protected override float ThrustFrames => 6f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => 16f;
        protected override float StabReach => 60f;
        protected override float BladeLength => 84f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override float ThrustEasePower => 3f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.05f;
        protected override int HitboxSize => 50;
        protected override int HitstopFrames => 3;
        protected override float ThrustPitch => -0.32f;

        /// <summary>淬火状态：自身着火或浸岩浆，矛芯回温</summary>
        private bool Quenched => Owner.lavaWet || Owner.HasBuff(BuffID.OnFire);

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            //淬火回温：+15% 伤害
            if (Quenched) {
                modifiers.FinalDamage *= 1.15f;
            }
        }

        /// <summary>命中：点燃目标；岩石脆响与火燎声</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            target.AddBuff(BuffID.OnFire, Quenched ? 300 : 180);
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.35f, Pitch = 0.1f, MaxInstances = 3 }, pos);
        }
    }
}
