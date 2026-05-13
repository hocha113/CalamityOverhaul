using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 低温核心：能量球爆炸时对范围内敌人施加冰冻
    /// 直接在 OnOrbDetonation 中遍历NPC施加buff，与爆炸半径改件叠加有效
    /// </summary>
    internal sealed class CryoCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //冰冻深蓝
        public override Color TintColor => new(80, 170, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbExplosionRadiusMul += 0.3f;
            ctx.ChargeTimeMul += 0.15f;
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            float baseRadius = 200f * orb.ExplosionRadiusMul;
            float radiusSq = baseRadius * baseRadius;
            int iceType = ModContent.ProjectileType<CryoCoreParclose>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > radiusSq) continue;
                if (npc.boss || npc.IsWormBody() || npc.CWR().IceParclose) continue;
                Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(), npc.Center, Vector2.Zero,
                    iceType, 0, 0, orb.Projectile.owner, npc.whoAmI, npc.type, npc.rotation);
            }
        }
    }

    internal class CryoCoreParclose : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "IceParclose";
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 38;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            NPC npc = Main.npc[(int)Projectile.ai[0]];
            if (!npc.Alives()) {
                Projectile.Kill();
                return;
            }
            if (npc.type != (int)Projectile.ai[1]) {
                Projectile.Kill();
                return;
            }

            if (!Main.dedServ) {
                Projectile.scale = npc.scale * (npc.height / (float)TextureAssets.Projectile[Type].Value.Height) * 2;
            }

            npc.Center = Projectile.Center;
            npc.rotation = Projectile.ai[2];
            npc.CWR().IceParclose = true;
            npc.CWR().FrozenActivity = true;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound("CalamityMod/Sounds/NPCHit/CryogenHit3".GetSound(), Projectile.Center);
            for (int i = 0; i < 10 * Projectile.scale; i++) {
                int index2 = Dust.NewDust(Projectile.Center + VaultUtils.RandVr(Projectile.width * Projectile.scale), 1, 1, DustID.BlueCrystalShard, Projectile.velocity.X, Projectile.velocity.Y, 0, default, 1.1f);
                Main.dust[index2].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
