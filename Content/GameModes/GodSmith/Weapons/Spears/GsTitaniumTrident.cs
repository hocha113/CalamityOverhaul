using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 钛金三叉戟重铸：钛影分身。<br/>
    /// 材质：冷锻钛钢三叉刃。签名行为：①每次刺出的爆发帧，真刃上下各生成一道
    /// 平行钛影幻矛，直飞约 180 像素后消散（呼应钛金套装的影分身）
    /// ②三线同刺——中线真刃重、旁线幻影轻，命中反馈冷冽金属音，沉稳收势
    /// </summary>
    internal class GsTitaniumTrident : GsSpearScheme
    {
        public override int TargetItemID => ItemID.TitaniumTrident;

        protected override string GsDescFallback =>
            "Reforged: each thrust casts two parallel shade-tridents above and below the true blade,\nflying a short lane before dissolving";
        protected override int HeldProjType => ModContent.ProjectileType<GsTitaniumTridentHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//双幻矛已是三线输出，底伤只小补，综合 DPS 落在原版 105%~120%
    }

    /// <summary>
    /// 钛金三叉戟手持突刺：刺出爆发帧自持枪手上下各 24 像素生成平行钛影幻矛（各 35% 伤害）
    /// </summary>
    internal class GsTitaniumTridentHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.TitaniumTrident;

        //钛沉稳：重量阶梯的沉端
        protected override float WindupFrames => 5f;
        protected override float ThrustFrames => 6f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 11f;
        protected override float PullbackDist => 16f;
        protected override float StabReach => 66f;
        protected override float BladeLength => 96f;
        protected override float CollisionWidth => 33f;
        protected override float TipGreedRadius => 28f;
        protected override float ThrustEasePower => 3f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.05f;
        protected override int HitboxSize => 54;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.26f;

        protected override void OnThrustBurst() {
            //爆发帧上下各放一道平行幻矛（owner 端生成，随生成包过线）
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 side = stabUnit.RotatedBy(MathHelper.PiOver2);
                for (int i = 0; i < 2; i++) {
                    float sign = i == 0 ? 1f : -1f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Hand + side * (sign * 24f), stabUnit * 9f,
                        ModContent.ProjectileType<GsTitaniumTridentShadeProj>(),
                        (int)(BaseDamage * 0.35f), Projectile.knockBack * 0.25f, Owner.whoAmI);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.25f, Pitch = 0.4f }, Owner.Center);
        }

        /// <summary>命中反馈：冷冽钛金属音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.35f, MaxInstances = 3 }, target.Center);
        }
    }

    /// <summary>
    /// 钛影幻矛：真刃刺出时上下平行生成的矛影，直飞约 180 像素（末段骤减）后消散。原版钛金三叉戟弹幕贴图默认绘制
    /// </summary>
    internal class GsTitaniumTridentShadeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TitaniumTrident;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TitaniumTrident");

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 22;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            //原版矛贴图尖端指右上，对齐飞行向需补 π/4
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            //末段骤减收势，幻影散在减速里
            if (Life > 14f) {
                Projectile.velocity *= 0.85f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.25f, Pitch = 0.55f, MaxInstances = 3 }, target.Center);
        }
    }
}
