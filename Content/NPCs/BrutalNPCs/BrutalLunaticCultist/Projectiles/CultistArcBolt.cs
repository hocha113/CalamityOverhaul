using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 雷律弧闪：锚点驻留，拍点快照→细弧预告 26 帧→落雷 10 帧（预告期间无害，锁线不追人——公平阀）<br/>
    /// ai[0..1]=拍点快照（权威端写后同步） ai[2]=起拍延迟
    /// </summary>
    internal class CultistArcBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.localAI[0];
        private int Delay => (int)Projectile.ai[2];
        private Vector2 StrikeEnd => new(Projectile.ai[0], Projectile.ai[1]);
        private bool HasSnapshot => Projectile.ai[0] != 0f || Projectile.ai[1] != 0f;

        private const int WarnFrames = 26;
        private const int StrikeFrames = 10;
        private const int FadeFrames = 12;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>快照后经过的帧数，负=还没起拍</summary>
        private int PhaseTime => (int)Timer - Delay;

        public override void AI() {
            Timer++;

            if (PhaseTime < 0) {
                return;
            }

            //起拍：权威端快照玩家位置（预告线自此锁死）
            if (PhaseTime == 0 && !VaultUtils.isClient && !HasSnapshot) {
                int idx = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                Player target = Main.player[idx];
                Vector2 aim = target.Alives()
                    ? target.Center + target.velocity * 8f
                    : Projectile.Center + Vector2.UnitY * 600f;
                Projectile.ai[0] = aim.X;
                Projectile.ai[1] = aim.Y;
                Projectile.netUpdate = true;
            }

            //预告起音（各非服务端）
            if (PhaseTime == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = 0.6f }, Projectile.Center);
            }

            //落雷瞬间
            if (PhaseTime == WarnFrames) {
                if (!VaultUtils.isServer && HasSnapshot) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.85f, Pitch = -0.1f }, StrikeEnd);
                    SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.5f, Pitch = 0.4f }, StrikeEnd);
                    CultistMotion.ImpactBurst(StrikeEnd, 2, 1.2f, playSound: false);
                    CultistMotion.Shake(StrikeEnd, 5f, 10);
                }
            }

            if (PhaseTime >= WarnFrames + StrikeFrames + FadeFrames) {
                Projectile.Kill();
            }

            if (HasSnapshot && PhaseTime >= WarnFrames && PhaseTime < WarnFrames + StrikeFrames) {
                Lighting.AddLight(Vector2.Lerp(Projectile.Center, StrikeEnd, 0.5f),
                    CultistMotion.StormCore.ToVector3() * 1.1f);
            }
        }

        /// <summary>只在落雷窗口咬人</summary>
        public override bool CanHitPlayer(Player target)
            => PhaseTime >= WarnFrames && PhaseTime < WarnFrames + StrikeFrames;

        /// <summary>命中判定：锚点→拍点的线段</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!HasSnapshot || PhaseTime < WarnFrames || PhaseTime >= WarnFrames + StrikeFrames) {
                return false;
            }
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, StrikeEnd, 22f, ref collisionPoint);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 90);
        }

        /// <summary>确定性折线抖动：identity+段序做种，4 帧换形</summary>
        private static float JitterHash(int seed) {
            float v = (float)Math.Sin(seed * 12.9898f) * 43758.5453f;
            return v - (float)Math.Floor(v) - 0.5f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!HasSnapshot || PhaseTime < 1) {
                return false;
            }

            Texture2D beam = CWRUtils.GetT2DAsset(CWRConstant.Masking + "ThunderTrail")?.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D orbTex = TextureAssets.Projectile[ProjectileID.CultistBossLightningOrb].Value;
            Main.instance.LoadProjectile(ProjectileID.CultistBossLightningOrb);
            if (beam == null) {
                return false;
            }

            Vector2 start = Projectile.Center;
            Vector2 end = StrikeEnd;
            int phase = PhaseTime;

            //锚点微型电球：vanilla 465 首帧
            int orbFrameH = orbTex.Height / Main.projFrames[ProjectileID.CultistBossLightningOrb];
            Rectangle orbFrame = new(0, 0, orbTex.Width, orbFrameH);
            float anchorPulse = 0.55f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
            Main.EntitySpriteDraw(orbTex, start - Main.screenPosition, orbFrame,
                Color.White * anchorPulse, Projectile.rotation, orbFrame.Size() * 0.5f, 0.7f, SpriteEffects.None, 0);

            if (phase < WarnFrames) {
                //细弧预告：低亮直线，亮度缓升——读线的时间
                float warnT = phase / (float)WarnFrames;
                DrawPolyline(start, end, beam, CultistMotion.StormCore with { A = 0 } * (0.16f + warnT * 0.2f),
                    5f, jitterAmp: 6f, segments: 6);
            }
            else if (phase < WarnFrames + StrikeFrames + FadeFrames) {
                //落雷：宽晕+白芯折线，尾段渐熄
                float fade = phase < WarnFrames + StrikeFrames ? 1f
                    : 1f - (phase - WarnFrames - StrikeFrames) / (float)FadeFrames;
                DrawPolyline(start, end, beam, CultistMotion.StormEdge with { A = 0 } * (0.75f * fade),
                    26f, jitterAmp: 22f, segments: 7);
                DrawPolyline(start, end, beam, Color.White * (0.9f * fade),
                    10f, jitterAmp: 22f, segments: 7);
                //落点闪辉
                Main.EntitySpriteDraw(glow, end - Main.screenPosition, null,
                    CultistMotion.StormCore with { A = 0 } * (0.8f * fade), 0f, glow.Size() * 0.5f,
                    1.6f * fade + 0.6f, SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>折线绘制：确定性抖动，4 帧换种</summary>
        private void DrawPolyline(Vector2 start, Vector2 end, Texture2D beam, Color color,
            float widthPx, float jitterAmp, int segments) {
            Vector2 dir = end - start;
            float totalLen = dir.Length();
            if (totalLen < 8f) {
                return;
            }
            dir /= totalLen;
            Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
            int flickSeed = Projectile.identity * 131 + (int)Timer / 4;

            Vector2 prev = start;
            for (int i = 1; i <= segments; i++) {
                float t = i / (float)segments;
                Vector2 point = start + dir * totalLen * t;
                //端点不抖，中段抖
                if (i < segments) {
                    point += normal * JitterHash(flickSeed + i) * jitterAmp * 2f;
                }
                Vector2 seg = point - prev;
                float segLen = seg.Length();
                float rot = seg.ToRotation();
                Main.EntitySpriteDraw(beam, prev - Main.screenPosition, null, color, rot,
                    new Vector2(0f, beam.Height * 0.5f),
                    new Vector2(segLen / beam.Width, widthPx / beam.Height), SpriteEffects.None, 0);
                prev = point;
            }
        }
    }
}
