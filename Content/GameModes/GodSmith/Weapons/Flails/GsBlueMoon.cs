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
    /// 【连枷·蓝月】蓝月重铸：圣蓝秘银月锤。签名行为：①转速档位决定命中迸出的穿透新月刃数，
    /// 半充一枚、满充三枚 ②新月刃朝目标后方扇形飞出并轻微减速
    /// </summary>
    internal class GsBlueMoon : GsFlailScheme
    {
        public override int TargetItemID => ItemID.BlueMoon;

        protected override int FlailProjType => ModContent.ProjectileType<GsBlueMoonHead>();

        protected override string GsDescFallback =>
            "Reforged: charged strikes loose piercing crescent blades through the target\nHalf charge looses one blade, a full charge looses three";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 蓝月锤头。族默认链体参数；命中按出手转速迸出新月刃（owner 端生成）
    /// </summary>
    internal class GsBlueMoonHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.BlueMoon;
        public override int VanillaProjID => ProjectileID.BlueMoon;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain3;

        /// <summary>新月刃伤害系数</summary>
        private const float CrescentDamageMul = 0.45f;

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer() || State != StateLaunch) {
                return;
            }
            //月辉蓄力：半充一枚、满充三枚穿透新月刃
            int count = LaunchCharge >= 0.99f ? 3 : LaunchCharge >= 0.5f ? 1 : 0;
            if (count <= 0) {
                return;
            }
            //朝目标后方（顺着出手方向穿过目标）扇形飞出
            Vector2 through = Owner.MountedCenter.To(target.Center).SafeNormalize(Vector2.UnitX * Owner.direction);
            for (int i = 0; i < count; i++) {
                float fan = count == 1 ? 0f : (i - (count - 1) * 0.5f) * 0.42f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    target.Center, through.RotatedBy(fan) * 11.5f,
                    ModContent.ProjectileType<GsBlueMoonCrescentProj>(),
                    Math.Max(1, (int)(Projectile.damage * CrescentDamageMul)), 1f, Projectile.owner);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.15f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 穿透新月刃：命中迸出的月弧，穿透 2 个目标，轻微减速淡出。
    /// 原版附魔剑气贴图默认绘制，淡入淡出走 Opacity
    /// </summary>
    internal class GsBlueMoonCrescentProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EnchantedBeam;

        private const int LifeFrames = 42;
        private const int FadeInFrames = 5;
        private const int FadeOutFrames = 12;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;//穿透 2 个目标
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//穿透型，同目标只结算一次
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
            //轻微减速曲线：月刃越飞越缓，尾段随淡出泄力；淡入淡出交给默认绘制的 alpha
            Projectile.velocity *= 0.955f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.Opacity = Opacity;
        }

        public override bool? CanDamage() => Opacity > 0.4f ? null : false;
    }
}
