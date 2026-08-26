using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 星珠:司祭与诸星共用的天体弹(实体星芒本体,暗缘+主体+热芯三层)<br/>
    /// ai[0]=阶段色 0~4 ai[1]=模式 0巡星(微增速) 1滞星(减速悬停短命) 2疾星(复利加速)
    /// </summary>
    internal class CultistStarBead : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Palette => (int)Projectile.ai[0];
        private int Mode => (int)Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            Projectile.rotation += 0.06f + Projectile.velocity.Length() * 0.004f;

            switch (Mode) {
                case 1:
                    //滞星:泄劲悬停,短命地雷
                    Projectile.velocity *= 0.965f;
                    if (Projectile.timeLeft > 150) {
                        Projectile.timeLeft = 150;
                    }
                    break;
                case 2:
                    //疾星:复利加速
                    if (Projectile.velocity.Length() < 21f) {
                        Projectile.velocity *= 1.014f;
                    }
                    break;
                default:
                    //巡星:缓增速,拒绝匀速直线
                    if (Projectile.velocity.Length() < 13f) {
                        Projectile.velocity *= 1.006f;
                    }
                    break;
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(Palette).ToVector3() * 0.42f);
        }

        public override bool? CanDamage() => Timer > 6f;

        public override void OnKill(int timeLeft) {
            //余痕:撞灭后火花与残辉活过弹体
            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(Palette), 0.5f, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Color mid = CultistMotion.PhaseCore(Palette);
            Color edge = CultistMotion.PhaseEdge(Palette);
            float twinkle = 1f + 0.08f * (float)Math.Sin(Timer * 0.35f + Projectile.identity * 1.7f);
            float scale = 0.24f * Projectile.scale * twinkle;

            //拖尾:同材质星芒回溯重画(横轴比≈1,同料)
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                CultistOrreryRenderer.DrawStarBead(sb, ghostPos, mid, edge,
                    scale * (0.4f + 0.5f * t), 0.34f * t, Projectile.rotation - i * 0.08f);
            }

            CultistOrreryRenderer.DrawStarBead(sb, Projectile.Center - Main.screenPosition,
                mid, edge, scale, 1f, Projectile.rotation);
            return false;
        }
    }
}
