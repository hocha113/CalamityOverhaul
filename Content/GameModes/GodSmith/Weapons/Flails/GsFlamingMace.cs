using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·烈焰钉头锤】烈焰钉头锤重铸：燃焦铸铁锤。签名行为：①甩转期按转速离心甩出带重力的火星弹，
    /// 转速越高甩越快甩越远 ②火星触敌或落地小燃爆并点燃
    /// </summary>
    internal class GsFlamingMace : GsFlailScheme
    {
        public override int TargetItemID => ItemID.FlamingMace;

        protected override int FlailProjType => ModContent.ProjectileType<GsFlamingMaceHead>();

        protected override string GsDescFallback =>
            "Reforged: spinning the mace flings burning embers outward\nFaster spins throw the embers harder and farther, igniting whatever they strike";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;
    }

    /// <summary>
    /// 烈焰钉头锤锤头。甩转期离心甩火（owner 端生成，充能低于 0.3 不甩，单场上限 10）
    /// </summary>
    internal class GsFlamingMaceHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.FlamingMace;
        public override int VanillaProjID => ProjectileID.FlamingMace;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain43;

        /// <summary>火星弹伤害系数</summary>
        private const float EmberDamageMul = 0.3f;
        /// <summary>全场火星弹上限</summary>
        private const int EmberCapTotal = 10;

        protected override void OnSpinTick(float charge) {
            //离心甩火：初速沿切线，转速越高甩越快、频率越密；owner 端生成随包广播
            if (charge < 0.3f || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int interval = (int)MathHelper.Lerp(10f, 5f, charge);
            if (spinTimer % interval != 0
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<GsFlamingMaceEmberProj>()] >= EmberCapTotal) {
                return;
            }
            Vector2 tangent = (spinAngle + MathHelper.PiOver2 * swingSign).ToRotationVector2();
            float fling = MathHelper.Lerp(3.5f, 9.5f, charge);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, tangent * fling,
                ModContent.ProjectileType<GsFlamingMaceEmberProj>(),
                Math.Max(1, (int)(Projectile.damage * EmberDamageMul)), 0.4f, Projectile.owner);
        }
    }

    /// <summary>
    /// 离心火星弹：甩转沿切线抛出的燃屑，带重力坠落，触敌或落地小燃爆并点燃。
    /// 原版火球贴图默认绘制
    /// </summary>
    internal class GsFlamingMaceEmberProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private const int LifeFrames = 90;
        private const int FadeInFrames = 4;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;//触敌即爆
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeFrames;
        }

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            //抛体弹道：重力主导，横向微阻——离心甩出去就得往下砸；淡入交给默认绘制的 alpha
            Projectile.velocity.Y += 0.24f;
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.Opacity = Opacity;
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);

        public override void OnKill(int timeLeft) {
            //小燃爆：触敌或落地都走这里，一声轻响
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.25f, Pitch = 0.3f }, Projectile.Center);
        }
    }
}
