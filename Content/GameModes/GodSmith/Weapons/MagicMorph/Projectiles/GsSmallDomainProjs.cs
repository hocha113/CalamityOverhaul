using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 流星法杖 A 形态「熔坑」：流星落点残留 1.2s 的贴地熔浆判定，踩踏受灼
    /// </summary>
    internal class GsScorchDomainProj : GsDomainProj
    {
        protected override int DomainRadius => 50;
        protected override int DomainLife => 72;
        protected override int DomainTickRate => 18;
        protected override float RingSquish => 0.45f;

        protected override Color RingBright => new(255, 200, 120);
        protected override Color RingMain => new(235, 105, 40);
        protected override Color RingDeep => new(110, 30, 8);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 60);

        protected override void EmitAmbient() {
            if (Projectile.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_HellFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(DomainRadius * 0.7f, 8f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f),
                    new Color(255, 150, 60), Main.rand.NextFloat(0.35f, 0.6f));
            }
            Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.05f);
        }
    }

    /// <summary>
    /// 寒霜法杖 A 形态「霜痕」：冰束落点残留 1.5s 霜地，轻判定并温和减速（服务端权威写入）
    /// </summary>
    internal class GsFrostTrailDomainProj : GsDomainProj
    {
        protected override int DomainRadius => 60;
        protected override int DomainLife => 90;
        protected override int DomainTickRate => 30;
        protected override float RingSquish => 0.45f;

        protected override Color RingBright => new(210, 240, 255);
        protected override Color RingMain => new(120, 185, 235);
        protected override Color RingDeep => new(36, 66, 128);

        protected override void DomainAI() {
            //域内微减速：NPC 位移是服务端权威量，客户端不写
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.boss && !npc.dontTakeDamage && npc.knockBackResist > 0f
                    && npc.Center.DistanceSQ(Projectile.Center) < (float)DomainRadius * DomainRadius) {
                    npc.velocity.X *= 0.96f;
                }
            }
        }

        protected override void EmitAmbient() {
            if (Projectile.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    Projectile.Center + Main.rand.NextVector2Circular(DomainRadius * 0.75f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.7f),
                    new Color(190, 230, 255), Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
            }
        }
    }

    /// <summary>
    /// 冰霜之花 A 形态「冰晶尘」：霜球每次弹跳残留 0.6s 的触碰细尘
    /// </summary>
    internal class GsFrostDustDomainProj : GsDomainProj
    {
        protected override int DomainRadius => 40;
        protected override int DomainLife => 36;
        protected override int DomainTickRate => 20;

        protected override Color RingBright => new(225, 245, 255);
        protected override Color RingMain => new(150, 200, 240);
        protected override Color RingDeep => new(50, 80, 140);

        protected override void EmitAmbient() {
            if (Projectile.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_DefFrostGlint>(
                    Projectile.Center + Main.rand.NextVector2Circular(DomainRadius * 0.7f, DomainRadius * 0.7f),
                    Main.rand.NextVector2Circular(0.4f, 0.4f),
                    new Color(210, 240, 255), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
        }
    }

    /// <summary>
    /// 水晶风暴 B 形态「晶暴领域」：光标处 3s 折射旋涡（本体无伤），
    /// 自机碎晶穿域获得弹速与穿透增益（增益判定在方案侧的碎晶回调里）
    /// </summary>
    internal class GsCrystalDomainProj : GsDomainProj
    {
        internal const int Radius = 130;

        protected override int DomainRadius => Radius;
        protected override int DomainLife => 180;
        protected override bool DealsContactDamage => false;

        protected override Color RingBright => new(255, 200, 240);
        protected override Color RingMain => new(232, 122, 200);
        protected override Color RingDeep => new(96, 30, 90);

        protected override void EmitAmbient() {
            //折射旋涡：两粒晶闪沿域内螺旋轨迹（纯表现）
            if (Projectile.timeLeft % 3 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float r = Main.rand.NextFloat(0.35f, 0.95f) * DomainRadius;
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
                Vector2 swirl = (ang + MathHelper.PiOver2).ToRotationVector2() * (2.4f - r / DomainRadius);
                Color c = Main.rand.NextBool() ? new Color(255, 190, 235) : new Color(210, 160, 255);
                PRTLoader.NewParticle<PRT_Sparkle>(pos, swirl, c, 0.24f)?.Configure(c, 12, 0.25f, 0.9f);
            }
            Lighting.AddLight(Projectile.Center, 0.3f, 0.16f, 0.28f);
        }
    }

    /// <summary>
    /// 毒液/毒牙法杖 B 形态「毒雾团」：毒瀑中央针落点残留 2s 的毒雾判定。
    /// ai[0]=1 时为毒牙紫雾（挂酸性毒液），否则绿雾（挂剧毒）
    /// </summary>
    internal class GsPoisonFieldProj : GsDomainProj
    {
        protected override int DomainRadius => 90;
        protected override int DomainLife => 120;
        protected override int DomainTickRate => 24;

        private bool VenomMode => Projectile.ai[0] >= 1f;

        protected override Color RingBright => VenomMode ? new(230, 140, 255) : new(180, 255, 120);
        protected override Color RingMain => VenomMode ? new(150, 60, 200) : new(96, 190, 60);
        protected override Color RingDeep => VenomMode ? new(60, 16, 90) : new(30, 80, 20);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(VenomMode ? BuffID.Venom : BuffID.Poisoned, 240);

        protected override void EmitAmbient() {
            if (Projectile.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_ToxicMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(DomainRadius * 0.8f, DomainRadius * 0.6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f),
                    VenomMode ? new Color(170, 90, 220) : new Color(120, 200, 80),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(20, 32), Main.rand.NextFloat(0.3f, 0.7f));
            }
        }
    }
}
