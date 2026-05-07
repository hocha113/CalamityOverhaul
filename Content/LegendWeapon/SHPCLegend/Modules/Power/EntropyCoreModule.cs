using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>
    /// 熵核：能量球飞行时持续从附近敌人吸取"熵蓄能"，引爆时按累积量释放第二次余波爆破
    /// 用 whoAmI→entropy 字典追踪每颗球的累积，钩子结束时清理
    /// </summary>
    internal sealed class EntropyCoreModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //熵核暗紫
        public override Color TintColor => new(170, 50, 220);

        private const float ScanRange = 380f;
        private const float MaxEntropy = 5f;
        private readonly Dictionary<int, float> _entropy = new();

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += -0.2f;
            ctx.ManaCostMul += 0.5f;
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            //每 5 帧扫描一次
            int id = orb.Projectile.whoAmI;
            int frame = (int)Main.GameUpdateCount + id;
            if (frame % 5 != 0) return;
            float gain = 0f;
            float rangeSq = ScanRange * ScanRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, orb.Projectile.Center) > rangeSq) continue;
                gain += 0.06f;
                if (gain > 0.6f) break;
            }
            if (gain <= 0f) return;
            if (!_entropy.TryGetValue(id, out float e)) e = 0f;
            e = MathF.Min(e + gain, MaxEntropy);
            _entropy[id] = e;

            //每次累积时在球周喷一点暗紫色熵粒子
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            for (int i = 0; i < 2; i++) {
                Vector2 angle = Main.rand.NextVector2CircularEdge(2.5f, 2.5f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    orb.Projectile.Center, angle,
                    new Color(180, 80, 255), new Color(80, 20, 160),
                    Main.rand.NextFloat(0.7f, 1.4f), Main.rand.Next(15, 25)));
            }
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            int id = orb.Projectile.whoAmI;
            if (!_entropy.TryGetValue(id, out float e) || e <= 0.4f) return;

            //余波爆炸：伤害与半径都按熵蓄能比例放大
            if (orb.Projectile.owner == Main.myPlayer) {
                float ratio = MathHelper.Clamp(e / MaxEntropy, 0f, 1f);
                int dmg = Math.Max((int)(orb.Projectile.damage * (0.4f + ratio * 0.6f)), 1);
                int idx = Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                    orb.Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<CyberDetonationProj>(),
                    dmg, 0f, orb.Projectile.owner, ai0: 0.4f + ratio * 0.4f);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    //余波半径 120-640 像素
                    Main.projectile[idx].localAI[2] = MathHelper.Lerp(120f, 640f, ratio);
                }
            }
        }

        public override void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) {
            _entropy.Remove(orb.Projectile.whoAmI);
        }
    }
}
