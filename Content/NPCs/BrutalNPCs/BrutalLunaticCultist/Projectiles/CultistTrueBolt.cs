using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 真言弹：真身阶段弹，体帧按阶段取 vanilla 精灵，镜像仪式里的动态识真线索<br/>
    /// ai[0]=阶段(0星旋 1星云 2星尘 3日耀 4月明) ai[1]=模式(0直线 1咏唱环轨) ai[2]=环轨初相
    /// </summary>
    internal class CultistTrueBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Element => (int)Projectile.ai[0];
        private bool IsOrbit => Projectile.ai[1] == 1f;

        /// <summary>阶段体帧的 vanilla 弹幕ID:电球/苍球/冰雾/火球/电球</summary>
        private int BodyId => Element switch {
            1 => ProjectileID.CultistBossFireBallClone,
            2 => ProjectileID.CultistBossIceMist,
            3 => ProjectileID.CultistBossFireBall,
            _ => ProjectileID.CultistBossLightningOrb,
        };

        /// <summary>咏唱环轨半径</summary>
        internal const float OrbitRadius = 195f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (IsOrbit) {
                //环轨护体：绑定场上唯一真身，角速度恒定（各端确定性推导）
                int bossIndex = NPC.FindFirstNPC(NPCID.CultistBoss);
                if (bossIndex < 0) {
                    Projectile.Kill();
                    return;
                }
                NPC boss = Main.npc[bossIndex];
                float angle = Projectile.ai[2] + (300 - Projectile.timeLeft) * 0.026f;
                Vector2 desired = boss.Center + angle.ToRotationVector2() * OrbitRadius;
                Projectile.velocity = desired - Projectile.Center;
                if (Projectile.velocity.Length() > 40f) {
                    //远端漂移直接贴回，防解体
                    Projectile.Center = desired;
                    Projectile.velocity = Vector2.Zero;
                }
                Projectile.rotation += 0.08f;
            }
            else {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            //帧动画（464 单帧自跳过）
            int frames = Main.projFrames[BodyId];
            if (frames > 1 && ++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % frames;
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(Element).ToVector3() * 0.45f);
        }

        public override void OnKill(int timeLeft) {
            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(Element), 0.7f, playSound: timeLeft <= 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(BodyId);
            Texture2D tex = TextureAssets.Projectile[BodyId].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int frames = Main.projFrames[BodyId];
            int frameHeight = tex.Height / frames;
            Rectangle frame = new(0, frameHeight * Math.Min(Projectile.frame, frames - 1), tex.Width, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color core = CultistMotion.PhaseCore(Element);
            Color edge = CultistMotion.PhaseEdge(Element);

            //底晕
            Main.EntitySpriteDraw(glow, pos, null, edge with { A = 0 } * 0.5f, 0f,
                glow.Size() * 0.5f, Projectile.scale * 0.6f, SpriteEffects.None, 0);
            //vanilla 体帧：日耀火球原色直画，其余轻染阶段色
            Color bodyColor = Element == 3 ? Color.White : Color.Lerp(Color.White, core, 0.35f);
            Main.EntitySpriteDraw(tex, pos, frame, bodyColor, Projectile.rotation, origin,
                Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
