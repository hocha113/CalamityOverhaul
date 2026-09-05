using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛】风暴矛重铸：引雷驻相。<br/>
    /// 材质：风暴淬蓝的引雷枪。签名行为：①驻相拉长——刺出后枪尖定格蓄电
    /// ②驻相内命中即放电，电链劈向最近一名其他敌人（50% 伤害）；
    /// 雨天引雷入云，可链两名 ③放电有雷鸣与小震屏，与普通刺击层次分明
    /// </summary>
    internal class GsThunderSpear : GsSpearScheme
    {
        public override int TargetItemID => ItemID.ThunderSpear;

        protected override string GsDescFallback =>
            "Reforged: the tip charges while held at full extension; land a hit during that moment\nand lightning arcs to the nearest other enemy, or two in the rain";
        protected override int HeldProjType => ModContent.ProjectileType<GsThunderSpearHeld>();

        //电链是白送的第二段伤害，底伤只小补，综合 DPS 落在原版 104%~116%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 风暴矛手持突刺：驻相帧拉长到 6（驻相有理由），
    /// 驻相内命中放电链至最近 1 名其他敌人（雨天 2 名）
    /// </summary>
    internal class GsThunderSpearHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.ThunderSpear;

        /// <summary>电链搜索半径（px）</summary>
        private const float ChainRange = 340f;

        protected override float WindupFrames => 5f;
        protected override float ThrustFrames => 5f;
        protected override float DwellFrames => 6f;//驻相拉长：蓄电窗就是签名
        protected override float RecoverFrames => 10f;//收势对齐原版 28 帧节奏
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 68f;
        protected override float BladeLength => 90f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 28f;
        protected override float ThrustEasePower => 2.8f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.04f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.15f;

        protected override void OnDwellStart() {
            if (VaultUtils.isServer) {
                return;
            }
            //蓄电起手一声电噼啪
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, TipPos);
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            bool dwellHit = CurrentPhase == PhaseDwell;
            //命中反馈：电噼啪，驻相命中更沉
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = dwellHit ? -0.2f : 0.1f, MaxInstances = 3 },
                    Vector2.Lerp(TipPos, target.Center, 0.5f));
            }
            //引雷放电：只在驻相内命中触发，一刺只放一次（owner 端生成）
            if (!dwellHit || !firstOnTarget || Projectile.numHits > 1
                || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int chains = Owner.ZoneRain ? 2 : 1;
            //找伤口附近最近的其他敌人（雨天取最近两名）
            NPC best = null, second = null;
            float bestDist = ChainRange, secondDist = ChainRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = npc.Distance(target.Center);
                if (dist < bestDist) {
                    second = best; secondDist = bestDist;
                    best = npc; bestDist = dist;
                }
                else if (dist < secondDist) {
                    second = npc; secondDist = dist;
                }
            }
            int fired = 0;
            foreach (NPC chainTo in new[] { best, second }) {
                if (chainTo == null || fired >= chains) {
                    break;
                }
                fired++;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), chainTo.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsThunderSpearArcProj>(),
                    (int)(BaseDamage * 0.50f), 0f, Owner.whoAmI);
            }
            //放了电才有的升级反馈：雷鸣 + 小震屏
            if (fired > 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.7f, Pitch = -0.1f }, target.Center);
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        target.Center, stabUnit, 3.5f, 6f, 8, 520f, FullName));
                }
            }
        }
    }

    /// <summary>
    /// 引雷电链：驻相命中放出的第二段判定，钉在链目标处瞬间放电。原版磁球电弧贴图默认绘制
    /// </summary>
    internal class GsThunderSpearArcProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MagnetSphereBolt;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ThunderSpear");

        private const int LifeFrames = 14;

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>只在头三帧有判定，其后纯演出滞留</summary>
        public override bool? CanDamage() => Timer <= 3f ? null : false;

        public override void AI() {
            Timer++;
            if (Timer == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            }
        }
    }
}
