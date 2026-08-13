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
    /// <summary>量子机匣，连击两目标纠缠 5s，35% 伤互传，一方死半额轰入幸存者</summary>
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
        private float linkTimerCarry;
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

            //已纠缠，命中互传
            if (LinkActive()) {
                NPC partner = null;
                if (target.whoAmI == npcA) partner = Main.npc[npcB];
                else if (target.whoAmI == npcB) partner = Main.npc[npcA];
                if (partner != null && partner.active) {
                    int rep = Math.Max((int)(damageDone * ReplicateRatio), 1);
                    partner.SimpleStrikeNPC(rep, hit.HitDirection, false, 0f, hit.DamageType, false, 0f, true);
                    accumDamage += rep;
                    NotifyLinkPulse(source.owner, target.whoAmI);
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

            //未纠缠，二次不同目标建纠缠
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
            linkTimerCarry = 0f;
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
                    //加色批环保留 A，A=0 整环不显示
                    PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                        VoidViolet, 0.05f).Configure(0.05f, 0.35f, 18);
                }
            }
        }

        public override void OnPlayerUpdate(Player player) {
            if (!LinkActive()) return;
            if (player.whoAmI != Main.myPlayer) return;

            TickDown(ref linkTimer, ref linkTimerCarry);
            NPC a = Main.npc[npcA];
            NPC b = Main.npc[npcB];
            bool aGone = !a.active || a.friendly;
            bool bGone = !b.active || b.friendly;

            //坍缩，半额轰入幸存者
            if (aGone || bGone) {
                NPC survivor = aGone ? b : a;
                if (!(aGone && bGone) && survivor.active && accumDamage > 0) {
                    int burst = Math.Max(accumDamage / 2, 1);
                    survivor.SimpleStrikeNPC(burst, 0, false, 0f, DamageClass.Magic, false, 0f, true);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.6f, Pitch = -0.1f }, survivor.Center);
                        for (int i = 0; i < 14; i++) {
                            //坍缩内爆向心收束
                            Vector2 offset = Main.rand.NextVector2CircularEdge(60f, 60f);
                            PRTLoader.NewParticle<PRT_CyberConverge>(survivor.Center + offset, Vector2.Zero,
                                VoidViolet, Main.rand.NextFloat(0.6f, 1.1f))
                                .Configure(survivor.Center, VoidDeep, Main.rand.Next(12, 20), 1f);
                        }
                        PRTLoader.NewParticle<PRT_StarPulseRing>(survivor.Center, Vector2.Zero,
                            VoidViolet, 0.05f).Configure(0.05f, 0.55f, 22);
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

        /// <summary>命中端起传输脉冲，拥有者端表现</summary>
        private void NotifyLinkPulse(int owner, int fromWho) {
            int linkType = ModContent.ProjectileType<SHPCQuantumLinkProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != owner || proj.type != linkType) continue;
                //旧链残留可能同型，端点比对认准当前纠缠对
                if ((int)proj.ai[0] != npcA || (int)proj.ai[1] != npcB) continue;
                if (proj.ModProjectile is SHPCQuantumLinkProj link) {
                    link.NotifyTransfer(fromWho);
                }
                break;
            }
        }

        private bool LinkActive() => npcA >= 0 && npcB >= 0 && linkTimer > 0;

        private void ClearLink() {
            npcA = -1;
            npcB = -1;
            linkTimer = 0;
            linkTimerCarry = 0f;
            accumDamage = 0;
        }
    }

    /// <summary>量子丝线，双目标虚空螺旋 Additive，穿行伤，端失效崩解</summary>
    internal sealed class SHPCQuantumLinkProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color StrandBright = new(190, 130, 255);
        private static readonly Color StrandDim = new(90, 40, 190);
        private static readonly Color PulseCore = new(240, 225, 255);

        private int IdA => (int)Projectile.ai[0];
        private int IdB => (int)Projectile.ai[1];

        private float fadeAlpha;
        //端点消亡后按缓存端点画崩解余韵
        private Vector2 lastPosA;
        private Vector2 lastPosB;
        private bool snapped;
        //崩解抖散量 0→1
        private float snapJitter;

        //传输脉冲，Dir>0 A→B
        private struct TransferPulse
        {
            public float Progress;
            public int Dir;
        }
        private readonly TransferPulse[] pulses = new TransferPulse[6];
        private int pulseCount;

        /// <summary>登记传输脉冲，fromWho 为被击端</summary>
        public void NotifyTransfer(int fromWho) {
            if (pulseCount >= pulses.Length) return;
            pulses[pulseCount++] = new TransferPulse { Progress = 0f, Dir = fromWho == IdA ? 1 : -1 };
        }

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
            //snapped 锁死崩解路径，防 10 帧内槽位复用把丝线接错怪
            if (snapped || !TryGetEnds(out NPC a, out NPC b)) {
                //端点失效快崩，首帧断线迸散
                if (!snapped) {
                    snapped = true;
                    OnSnap();
                }
                snapJitter = MathF.Min(snapJitter + 0.14f, 1f);
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 10);
                fadeAlpha = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f) * 0.85f;
                return;
            }
            lastPosA = a.Center;
            lastPosB = b.Center;
            fadeAlpha = MathF.Min(fadeAlpha + 0.08f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            Projectile.Center = (a.Center + b.Center) * 0.5f;

            UpdatePulses(a.Center, b.Center);

            //沿丝线偶发量子涨落微粒
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(a.Center, b.Center, t);
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2Circular(1f, 1f),
                    StrandBright, Main.rand.NextFloat(0.25f, 0.55f)).Configure(StrandDim, Main.rand.Next(8, 16));
            }
        }

        /// <summary>脉冲沿丝推进，到端迸开</summary>
        private void UpdatePulses(Vector2 posA, Vector2 posB) {
            for (int i = pulseCount - 1; i >= 0; i--) {
                pulses[i].Progress += 0.075f;
                if (pulses[i].Progress < 1f) continue;
                Vector2 arrive = pulses[i].Dir > 0 ? posB : posA;
                if (Main.netMode != NetmodeID.Server) {
                    for (int k = 0; k < 5; k++) {
                        PRTLoader.NewParticle<PRT_CyberSquare>(arrive, Main.rand.NextVector2CircularEdge(2.5f, 2.5f),
                            StrandBright, Main.rand.NextFloat(0.4f, 0.8f)).Configure(StrandDim, Main.rand.Next(8, 16));
                    }
                }
                pulses[i] = pulses[--pulseCount];
            }
        }

        /// <summary>断线瞬间沿线崩解碎粒，各端本地</summary>
        private void OnSnap() {
            //在途脉冲随链一起消散
            pulseCount = 0;
            if (Main.netMode == NetmodeID.Server) return;
            if (lastPosA == Vector2.Zero && lastPosB == Vector2.Zero) return;
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = -0.5f }, Projectile.Center);
            Vector2 span = lastPosB - lastPosA;
            float dist = span.Length();
            Vector2 normal = span.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            int count = Math.Clamp((int)(dist / 26f), 6, 34);
            for (int i = 0; i < count; i++) {
                float t = (i + Main.rand.NextFloat()) / count;
                Vector2 pos = lastPosA + span * t;
                Vector2 vel = normal * Main.rand.NextFloat(-2.2f, 2.2f) + Main.rand.NextVector2Circular(0.8f, 0.8f);
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, vel, StrandBright, Main.rand.NextFloat(0.3f, 0.7f))
                    .Configure(StrandDim, Main.rand.Next(12, 22));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!TryGetEnds(out NPC a, out NPC b)) return false;
            //纠缠对不吃丝线伤
            if (targetHitbox.Intersects(a.Hitbox) || targetHitbox.Intersects(b.Hitbox)) return false;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(
                new Vector2(targetHitbox.X, targetHitbox.Y),
                new Vector2(targetHitbox.Width, targetHitbox.Height),
                a.Center, b.Center, 22f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>螺旋采样点，t 0~1，崩解期叠高频抖散</summary>
        private Vector2 HelixPoint(Vector2 start, Vector2 dir, Vector2 normal, float dist, float t, float phase, float time) {
            float envelope = MathF.Sin(t * MathHelper.Pi); //两端收口
            float wave = MathF.Sin(t * dist * 0.045f + time + phase) * 13f * envelope;
            if (snapJitter > 0f) {
                wave += MathF.Sin(t * 57.3f + time * 37f + phase) * 7f * snapJitter;
            }
            return start + dir * (dist * t) + normal * wave;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.02f) return;
            //断线后改用缓存端点画崩解余韵
            Vector2 start, end;
            if (snapped) {
                if (lastPosA == Vector2.Zero && lastPosB == Vector2.Zero) return;
                start = lastPosA;
                end = lastPosB;
            }
            else {
                if (!TryGetEnds(out NPC a, out NPC b)) return;
                start = a.Center;
                end = b.Center;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (white == null) return;

            Vector2 dir = end - start;
            float dist = dir.Length();
            if (dist < 8f) return;
            dir /= dist;
            Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
            int segments = Math.Clamp((int)(dist / 14f), 6, 90);
            float time = (float)Main.timeForVisualEffects * 0.11f;

            SpriteBatch sb = spriteBatch;
            //暗紫宽底带，静态低频给丝线厚度
            //加色批 tint 一律保留 A，A=0 在源因子 SourceAlpha 下整层不显示
            Color underCol = StrandDim * (fadeAlpha * (0.30f - snapJitter * 0.18f));
            sb.Draw(white, start - Main.screenPosition, null, underCol, dir.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(dist, 7f), SpriteEffects.None, 0f);

            //双股螺旋，相位差 π，亮度与宽度同相脉动
            for (int strand = 0; strand < 2; strand++) {
                float phase = strand * MathHelper.Pi;
                Vector2 prev = HelixPoint(start, dir, normal, dist, 0f, phase, time);
                for (int i = 1; i <= segments; i++) {
                    float t = i / (float)segments;
                    Vector2 point = HelixPoint(start, dir, normal, dist, t, phase, time);
                    Vector2 delta = point - prev;
                    float segLen = delta.Length();
                    if (segLen > 0.01f) {
                        float pulseWave = 0.5f + 0.5f * MathF.Sin(t * 9f + time * 2f + phase);
                        Color drawCol = Color.Lerp(StrandDim, StrandBright, pulseWave) * (fadeAlpha * 0.85f);
                        sb.Draw(white, prev - Main.screenPosition, null, drawCol,
                            delta.ToRotation(), new Vector2(0f, 0.5f),
                            new Vector2(segLen, 2.0f + pulseWave * 1.1f), SpriteEffects.None, 0f);
                    }
                    prev = point;
                }
            }

            if (glow == null) return;

            //传输脉冲，亮头+三段衰减尾沿丝滑行
            for (int p = 0; p < pulseCount; p++) {
                float head = pulses[p].Dir > 0 ? pulses[p].Progress : 1f - pulses[p].Progress;
                for (int g = 2; g >= 0; g--) {
                    float t = head - g * 0.045f * pulses[p].Dir;
                    if (t < 0f || t > 1f) continue;
                    Vector2 pos = HelixPoint(start, dir, normal, dist, t, 0f, time);
                    float k = 1f - g * 0.3f;
                    sb.Draw(glow, pos - Main.screenPosition, null,
                        StrandBright * (fadeAlpha * 0.8f * k), 0f, glow.Size() * 0.5f,
                        0.22f * k, SpriteEffects.None, 0f);
                }
                float headT = MathHelper.Clamp(head, 0f, 1f);
                Vector2 headPos = HelixPoint(start, dir, normal, dist, headT, 0f, time);
                sb.Draw(glow, headPos - Main.screenPosition, null,
                    PulseCore * (fadeAlpha * 0.9f), 0f, glow.Size() * 0.5f, 0.1f, SpriteEffects.None, 0f);
            }

            //端点纠缠光晕
            Color endGlow = StrandBright * (fadeAlpha * 0.6f);
            foreach (Vector2 endPos in new[] { start, end }) {
                sb.Draw(glow, endPos - Main.screenPosition, null,
                    endGlow, 0f, glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0f);
            }
        }
    }
}
