using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·阳炎之怒】阳炎之怒重铸：狱火黑曜链锤。签名行为：①掷出飞行沿途留悬空灼痕，
    /// 触敌灼伤并点狱火 ②满转掷出灼痕更密
    /// </summary>
    internal class GsSunfury : GsFlailScheme
    {
        public override int TargetItemID => ItemID.Sunfury;

        protected override int FlailProjType => ModContent.ProjectileType<GsSunfuryHead>();

        protected override string GsDescFallback =>
            "Reforged: the flying ball sears hovering scorch marks along its path\nThe marks linger and burn whoever touches them with hellfire";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 阳炎之怒锤头。掷出飞行每 4 帧（满转 3 帧）在头位留一段悬空灼痕（owner 端生成，单掷上限 10）
    /// </summary>
    internal class GsSunfuryHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.Sunfury;
        public override int VanillaProjID => ProjectileID.Sunfury;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain6;

        /// <summary>灼痕伤害系数</summary>
        private const float ScorchDamageMul = 0.3f;
        /// <summary>单掷灼痕上限</summary>
        private const int ScorchCapPerThrow = 10;

        /// <summary>本次掷出已留下的灼痕数</summary>
        private int scorchLaid;

        protected override void OnLaunch(float charge) => scorchLaid = 0;

        protected override void OnLaunchTick(int flightTime) {
            //灼痕轨迹：满转 3 帧一段，否则 4 帧一段；owner 端生成随包广播
            int interval = LaunchCharge >= 0.99f ? 3 : 4;
            if (!Projectile.IsOwnedByLocalPlayer() || flightTime % interval != 0
                || scorchLaid >= ScorchCapPerThrow) {
                return;
            }
            scorchLaid++;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSunfuryScorchProj>(),
                Math.Max(1, (int)(Projectile.damage * ScorchDamageMul)), 0.3f, Projectile.owner);
        }
    }

    /// <summary>
    /// 悬空灼痕：锤头沿途留下的余焰，存续约 108 帧，触敌灼伤并点狱火。
    /// 原版火球贴图默认绘制，淡入淡出走 Opacity
    /// </summary>
    internal class GsSunfuryScorchProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private const int LifeFrames = 108;
        private const int FadeInFrames = 8;
        private const int FadeOutFrames = 20;

        /// <summary>identity 播种的相位，悬停浮动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = LifeFrames;
        }

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                if (Projectile.timeLeft < FadeOutFrames) {
                    return Projectile.timeLeft / (float)FadeOutFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            //悬空驻定，identity 相位轻微呼吸浮动；淡入淡出交给默认绘制的 alpha
            Projectile.velocity = Vector2.Zero;
            Projectile.position.Y += MathF.Sin(Main.GameUpdateCount * 0.04f + Seed) * 0.08f;
            Projectile.Opacity = Opacity;
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire3, 180);
    }
}
