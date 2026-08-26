using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 猩红魔杖「血雨祭域」：R150 悬空血云领域，驻场 10s。<br/>
    /// 本体不带接触判定，伤害由 owner 端周期生成的原版血雨滴承载（Parent 源承签打标，
    /// 命中叠血蚀由方案回调处理）；域内玩家由各端本地结算 +1HP/2s；
    /// 雨滴基准伤害烘焙在本弹幕 damage 上
    /// </summary>
    internal class GsCrimsonDomainProj : GsDomainProj
    {
        protected override int DomainRadius => 150;
        protected override int DomainLife => 600;
        protected override bool DealsContactDamage => false;

        protected override Color RingBright => new(255, 120, 120);
        protected override Color RingMain => new(178, 26, 46);
        protected override Color RingDeep => new(84, 8, 22);

        protected override void DomainAI() {
            //血雨滴：owner 端每 6t 自域顶随机横位落下一滴（Parent 源，标记自动传染）
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.timeLeft % 6 == 0) {
                float offX = Main.rand.NextFloat(-0.82f, 0.82f) * DomainRadius;
                Vector2 pos = Projectile.Center + new Vector2(offX, -DomainRadius - 30f);
                Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(8f, 10f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, vel,
                    ProjectileID.BloodRain, Projectile.damage, 0.6f, Projectile.owner);
            }
            //祭域滋养：各端只结算本端玩家（自身 HP 归本端权威）
            Player local = Main.LocalPlayer;
            if (!Main.dedServ && local.active && !local.dead
                && local.Center.DistanceSQ(Projectile.Center) < (float)DomainRadius * DomainRadius
                && Projectile.timeLeft % 120 == 0) {
                local.Heal(1);
            }
        }

        protected override void EmitAmbient() {
            //域内血珠缓落 + 心跳微光（预算 ≤2/帧）
            if (Projectile.timeLeft % 4 == 0) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(DomainRadius * 0.8f, DomainRadius * 0.6f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                    new Color(196, 40, 56), Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(20, 32), 0.1f, 0.99f);
            }
            Lighting.AddLight(Projectile.Center, 0.32f, 0.06f, 0.1f);
        }

        protected override void OnMigrateVisual(Vector2 oldCenter) {
            //迁移反馈：旧址血雾散逸（各端可见）
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    oldCenter + Main.rand.NextVector2Circular(40f, 40f),
                    Main.rand.NextVector2Circular(2f, 2f),
                    new Color(178, 26, 46), Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 22));
            }
        }
    }
}
