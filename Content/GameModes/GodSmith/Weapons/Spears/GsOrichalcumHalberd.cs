using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 山铜戟重铸：花瓣锋阵。<br/>
    /// 材质：山铜粉晶戟刃。签名行为：①每次刺中自目标伤口绽出两枚追击花瓣，
    /// 螺旋咬向近旁猎物（呼应山铜套装的花瓣风暴）
    /// ②命中反馈是花瓣簌落的柔响，与金属矛的脆响截然不同
    /// </summary>
    internal class GsOrichalcumHalberd : GsSpearScheme
    {
        public override int TargetItemID => ItemID.OrichalcumHalberd;

        protected override string GsDescFallback =>
            "Reforged: every thrust that lands blooms two homing petals from the wound,\neach spiraling after nearby prey";
        protected override int HeldProjType => ModContent.ProjectileType<GsOrichalcumHalberdHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//花瓣追击是主要机制收益，底伤只小补，综合 DPS 落在原版 105%~118%
    }

    /// <summary>
    /// 山铜戟手持突刺：首个命中绽出两枚追击花瓣（owner 端生成，各 30% 伤害）
    /// </summary>
    internal class GsOrichalcumHalberdHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.OrichalcumHalberd;

        protected override float WindupFrames => 5f;
        protected override float ThrustFrames => 5f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => 15f;
        protected override float StabReach => 64f;
        protected override float BladeLength => 94f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.04f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.18f;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //命中反馈：花瓣簌落柔响（无金属脆响）
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
                SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
            }
            //每次突刺只在首个命中绽瓣（owner 端生成，随生成包过线）
            if (!firstOnTarget || Projectile.numHits > 1 || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                //两瓣沿刺向两侧张开，ai0=螺旋方向
                float side = i == 0 ? 1f : -1f;
                Vector2 vel = stabUnit.RotatedBy(side * 1.1f) * 6f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, vel,
                    ModContent.ProjectileType<GsOrichalcumHalberdPetalProj>(),
                    (int)(BaseDamage * 0.3f), Projectile.knockBack * 0.2f, Owner.whoAmI, side);
            }
        }
    }

    /// <summary>
    /// 山铜追击花瓣：自伤口绽出，先沿切向张开再螺旋咬向近旁猎物。<br/>
    /// 原版山铜套花瓣贴图（3 帧）默认绘制。ai[0]=螺旋方向
    /// </summary>
    internal class GsOrichalcumHalberdPetalProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;
        public override LocalizedText DisplayName => Language.GetText("ItemName.OrichalcumHalberd");

        private ref float Life => ref Projectile.localAI[0];
        private float SpinDir => Projectile.ai[0] >= 0f ? 1f : -1f;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.FlowerPetal];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 75;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;

            //前 8 帧张开，之后螺旋追击：追踪之上叠一层持续切向旋转 = 螺旋轨迹
            if (Life > 8f) {
                NPC target = Projectile.Center.FindClosestNPC(480f);
                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1.01f, 0.10f);
                    Projectile.velocity = Projectile.velocity.RotatedBy(SpinDir * 0.05f);
                }
            }
            float speed = Projectile.velocity.Length();
            if (speed < 5f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 5f;
            }
            //瓣面自旋
            Projectile.rotation += SpinDir * 0.28f;

            //原版花瓣多帧：最简帧计数
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.3f, Pitch = 0.6f, MaxInstances = 3 }, Projectile.Center);
        }
    }
}
