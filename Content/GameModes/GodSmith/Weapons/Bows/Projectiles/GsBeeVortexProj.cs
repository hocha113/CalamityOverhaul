using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 蜂膝弓 T3 蜂后之怒：命中点悬置 1.5 秒的隐形蜂涡（自身无伤、不绘制），
    /// 每 20 帧放出 1 只蜂共 4 只（owner 端生成，respect 蜂巢背包与 12 只蜂池上限）
    /// </summary>
    internal class GsBeeVortexProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //不注册新键，显示名指向原版物品键
        public override LocalizedText DisplayName => Language.GetText("ItemName.BeesKnees");

        private const int LifeFrames = 90;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            //懒浮：identity 定相的微幅漂移
            Projectile.velocity = new Vector2(
                MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + Projectile.identity) * 0.22f,
                MathF.Cos(Main.GlobalTimeWrappedHourly * 1.7f + Projectile.identity * 0.6f) * 0.18f);

            int elapsed = LifeFrames - Projectile.timeLeft;
            //每 20 帧放 1 蜂共 4 只（owner 端权威）
            if (Projectile.IsOwnedByLocalPlayer() && elapsed > 0 && elapsed % 20 == 0 && elapsed <= 80) {
                GsBeesKnees.SpawnBee(Main.player[Projectile.owner], Projectile.GetSource_FromThis(),
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), 12);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
