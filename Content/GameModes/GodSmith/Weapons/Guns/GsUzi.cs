using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 乌兹重铸：泼洒 + 曳光火控。<br/>
    /// 签名行为：①每第 4 发换装曳光弹 ②曳光命中「点亮」目标 1.5 秒，乌兹子弹对其 +10%。<br/>
    /// [双持乱射]：+30% 射速、附加散布
    /// </summary>
    internal class GsUzi : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Uzi;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: sprays 30% faster with a loose pattern\nEvery 4th round is an amber tracer: tracer hits light the target up, and your Uzi rounds dig 10% deeper into lit targets";
        /// <summary>曳光计数（每 4 发一枚）；只在 owner 射击链读写</summary>
        private int tracerCounter;

        /// <summary>曳光标记：目标编号/类型/截止帧。owner 本地量，收益只走攻击方端结算</summary>
        private int markNpc = -1;
        private int markNpcType;
        private uint markUntil;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeAkimbo", EnName = "Akimbo",
                UseSpeed = 1.30f, DamageMul = 0.87f,
                ExtraSpread = MathHelper.ToRadians(6f),
            },
        ];

        //冲锋枪后坐：轻快高频
        protected override float RecoilShift => 2.4f;
        protected override float RecoilKick => 0.04f;
        protected override float RecoilScale(Item item, Player player, GsFireMode mode) => 0.9f;

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //曳光换装：每第 4 发
            if (++tracerCounter % 4 == 0) {
                type = ModContent.ProjectileType<GsUziTracerProj>();
            }
        }

        /// <summary>攻击方端结算：曳光命中点亮目标；被点亮目标吃乌兹弹 +10%</summary>
        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ModContent.ProjectileType<GsUziTracerProj>()
                || target.friendly || !target.active) {
                return;
            }
            markNpc = target.whoAmI;
            markNpcType = target.type;
            markUntil = Main.GameUpdateCount + 90;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.4f, Pitch = 0.65f }, target.Center);
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //标记是 owner 本地量，判定端即攻击方端，读到即权威
            if (target.whoAmI == markNpc && target.type == markNpcType
                && Main.GameUpdateCount < markUntil) {
                modifiers.FinalDamage *= 1.10f;
            }
        }

        internal override void GsGunHeldReset(Player player) {
            tracerCounter = 0;
            markNpc = -1;
            markUntil = 0;
        }
    }

    /// <summary>
    /// 乌兹曳光弹：高速直飞的曳光弹。命中点亮目标（收益在方案侧结算）
    /// </summary>
    internal class GsUziTracerProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletHighVelocity;

        public override string LocalizationCategory => "GodSmithGuns";

        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 200;
            Projectile.extraUpdates = 3;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            //原版子弹贴图朝上，转向补 PiOver2
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
