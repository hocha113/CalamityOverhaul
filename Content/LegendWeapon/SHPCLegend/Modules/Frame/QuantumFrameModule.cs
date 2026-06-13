using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Frame
{
    /// <summary>量子机匣：连击两目标纠缠 5s，35% 伤害互传，一方死亡半额轰入幸存者</summary>
    internal sealed class QuantumFrameModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Frame;
        //量子虚空紫
        public override Color TintColor => new(150, 80, 255);

        private const int LinkDuration = 300;
        private const float LinkMaxRange = 1100f;
        private const float ReplicateRatio = 0.35f;

        private static readonly Color VoidViolet = new(170, 100, 255);
        private static readonly Color VoidDeep = new(60, 20, 130);

        private int npcA = -1;
        private int npcB = -1;
        private int linkTimer;
        private int accumDamage;
        private int lastHitNpc = -1;

        public override void Apply(ref ShootContext ctx) {
            ctx.HomingMul += 0.2f;
            ctx.ManaCostMul += 0.25f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            HandleHit(beam.Projectile, target, hit, damageDone);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            HandleHit(laser.Projectile, target, hit, damageDone);
        }

        private void HandleHit(Projectile source, NPC target, NPC.HitInfo hit, int damageDone) {
            if (source.owner != Main.myPlayer) return;

            //已纠缠：命中任一方时把伤害复制给另一方
            if (LinkActive()) {
                NPC partner = null;
                if (target.whoAmI == npcA) partner = Main.npc[npcB];
                else if (target.whoAmI == npcB) partner = Main.npc[npcA];
                if (partner != null && partner.active) {
                    int rep = Math.Max((int)(damageDone * ReplicateRatio), 1);
                    partner.SimpleStrikeNPC(rep, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
                    accumDamage += rep;
                    if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                        for (int i = 0; i < 4; i++) {
                            PRTLoader.NewParticle<PRT_CyberSquare>(partner.Center,
                                Main.rand.NextVector2CircularEdge(3.5f, 3.5f),
                                VoidViolet, Main.rand.NextFloat(0.5f, 0.9f))
                                .Configure(VoidDeep, Main.rand.Next(10, 18));
                        }
                    }
                }
                return;
            }

            //未纠缠：记录上次命中目标，命中第二个不同目标时建立纠缠
            if (lastHitNpc >= 0 && lastHitNpc != target.whoAmI) {
                NPC first = Main.npc[lastHitNpc];
                if (first.active && !first.friendly
                    && Vector2.DistanceSquared(first.Center, target.Center) <= LinkMaxRange * LinkMaxRange) {
                    Entangle(source, first, target);
                    return;
                }
            }
            lastHitNpc = target.whoAmI;
        }

        private void Entangle(Projectile source, NPC first, NPC second) {
            npcA = first.whoAmI;
            npcB = second.whoAmI;
            linkTimer = LinkDuration;
            accumDamage = 0;
            lastHitNpc = -1;

            int linkDmg = Math.Max(source.damage / 3, 1);
            Projectile.NewProjectile(source.GetSource_FromThis(),
                (first.Center + second.Center) * 0.5f, Vector2.Zero,
                ModContent.ProjectileType<SHPCQuantumLinkProj>(),
                linkDmg, 0f, source.owner,
                ai0: npcA, ai1: npcB);

            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.55f, Pitch = 0.4f }, second.Center);
                foreach (NPC npc in new[] { first, second }) {
                    PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                        VoidViolet with { A = 0 }, 0.05f).Configure(0.05f, 0.35f, 18);
                }
            }
        }

        public override void OnPlayerUpdate(Player player) {
            if (!LinkActive()) return;
            if (player.whoAmI != Main.myPlayer) return;

            linkTimer--;
            NPC a = Main.npc[npcA];
            NPC b = Main.npc[npcB];
            bool aGone = !a.active || a.friendly;
            bool bGone = !b.active || b.friendly;

            //坍缩：任一方消亡，把累积复制伤害的一半轰入幸存者
            if (aGone || bGone) {
                NPC survivor = aGone ? b : a;
                if (!(aGone && bGone) && survivor.active && accumDamage > 0) {
                    int burst = Math.Max(accumDamage / 2, 1);
                    survivor.SimpleStrikeNPC(burst, 0, false, 0f, DamageClass.Magic, false, 0f, true);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.6f, Pitch = -0.1f }, survivor.Center);
                        for (int i = 0; i < 14; i++) {
                            //坍缩内爆：粒子向心收束
                            Vector2 offset = Main.rand.NextVector2CircularEdge(60f, 60f);
                            PRTLoader.NewParticle<PRT_CyberConverge>(survivor.Center + offset, Vector2.Zero,
                                VoidViolet, Main.rand.NextFloat(0.6f, 1.1f))
                                .Configure(survivor.Center, VoidDeep, Main.rand.Next(12, 20), 1f);
                        }
                        PRTLoader.NewParticle<PRT_StarPulseRing>(survivor.Center, Vector2.Zero,
                            VoidViolet with { A = 0 }, 0.05f).Configure(0.05f, 0.55f, 22);
                    }
                }
                ClearLink();
                return;
            }

            if (linkTimer <= 0
                || Vector2.DistanceSquared(a.Center, b.Center) > LinkMaxRange * LinkMaxRange * 2.25f) {
                ClearLink();
            }
        }

        private bool LinkActive() => npcA >= 0 && npcB >= 0 && linkTimer > 0;

        private void ClearLink() {
            npcA = -1;
            npcB = -1;
            linkTimer = 0;
            accumDamage = 0;
        }
    }

    /// <summary>量子丝线：双目标虚空螺旋 Additive，穿行伤；任一端失效崩解</summary>
    internal sealed class SHPCQuantumLinkProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Color StrandBright = new(190, 130, 255);
        private static readonly Color StrandDim = new(90, 40, 190);

        private int IdA => (int)Projectile.ai[0];
        private int IdB => (int)Projectile.ai[1];

        private float fadeAlpha;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 310;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        private bool TryGetEnds(out NPC a, out NPC b) {
            a = IdA >= 0 && IdA < Main.maxNPCs ? Main.npc[IdA] : null;
            b = IdB >= 0 && IdB < Main.maxNPCs ? Main.npc[IdB] : null;
            return a != null && b != null && a.active && b.active && !a.friendly && !b.friendly;
        }

        public override void AI() {
            if (!TryGetEnds(out NPC a, out NPC b)) {
                //端点失效：快速崩解
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 10);
                fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
                return;
            }
            fadeAlpha = MathF.Min(fadeAlpha + 0.08f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            Projectile.Center = (a.Center + b.Center) * 0.5f;

            //沿丝线偶发量子涨落微粒
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(a.Center, b.Center, t);
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2Circular(1f, 1f),
                    StrandBright, Main.rand.NextFloat(0.25f, 0.55f)).Configure(StrandDim, Main.rand.Next(8, 16));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!TryGetEnds(out NPC a, out NPC b)) return false;
            //纠缠对本身不吃丝线伤害
            if (targetHitbox.Intersects(a.Hitbox) || targetHitbox.Intersects(b.Hitbox)) return false;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                a.Center, b.Center, 22f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (!TryGetEnds(out NPC a, out NPC b) || fadeAlpha < 0.02f) return;
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null) return;

            Vector2 start = a.Center;
            Vector2 end = b.Center;
            Vector2 dir = end - start;
            float dist = dir.Length();
            if (dist < 8f) return;
            dir /= dist;
            Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
            int segments = Math.Clamp((int)(dist / 14f), 6, 90);
            float time = (float)Main.timeForVisualEffects * 0.11f;

            SpriteBatch sb = spriteBatch;
            //双股螺旋：相位相差 π 的两条正弦光带，交点处亮结
            for (int strand = 0; strand < 2; strand++) {
                float phase = strand * MathHelper.Pi;
                Vector2 prev = start;
                for (int i = 1; i <= segments; i++) {
                    float t = i / (float)segments;
                    float envelope = MathF.Sin(t * MathHelper.Pi); //两端收口
                    float wave = MathF.Sin(t * dist * 0.045f + time + phase) * 13f * envelope;
                    Vector2 point = start + dir * (dist * t) + normal * wave;
                    Vector2 delta = point - prev;
                    float segLen = delta.Length();
                    if (segLen > 0.01f) {
                        Color col = Color.Lerp(StrandDim, StrandBright,
                            0.5f + 0.5f * MathF.Sin(t * 9f + time * 2f + phase));
                        Color drawCol = (col * (fadeAlpha * 0.85f)) with { A = 0 };
                        sb.Draw(white, prev - Main.screenPosition, null, drawCol,
                            delta.ToRotation(), new Vector2(0f, 0.5f),
                            new Vector2(segLen, 2.2f), SpriteEffects.None, 0f);
                    }
                    prev = point;
                }
            }
            //端点纠缠光晕
            if (glow != null) {
                Color endGlow = (StrandBright * (fadeAlpha * 0.6f)) with { A = 0 };
                foreach (Vector2 endPos in new[] { start, end }) {
                    sb.Draw(glow, endPos - Main.screenPosition, null,
                        endGlow, 0f, glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
