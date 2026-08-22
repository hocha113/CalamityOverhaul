using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Pandemoniums;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaPandemonium
{
    /// <summary>
    /// 械奴的小硫磺阵：万魔殿书奴施放的定点小型法阵。
    /// 不跟玩家、不向主人拉雷（玩家右键阵的雷索是手持身份，械奴不能借），
    /// 有限寿命自走完展开-存续-坍缩；视觉复用 BrimstoneDomain。
    /// 场内灼烧 + 偶发火球/闪电/血雨（owner 生成），ai0 = 半径
    /// </summary>
    internal class KikasaPandemoniumCircle : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ExpandFrames = 24;
        private const int ActiveFrames = 300;
        private const int CollapseFrames = 36;
        private const int TotalFrames = ExpandFrames + ActiveFrames + CollapseFrames;

        /// <summary>灼烧结算节拍</summary>
        private const int BurnTick = 25;

        /// <summary>存续期弹幕节拍：火球 / 闪电 / 血雨错开</summary>
        private const int FireballPeriod = 40;
        private const int LightningPeriod = 70;
        private const int RainPeriod = 90;

        private int timer;
        private bool collapsing;
        private int collapseStart = ExpandFrames + ActiveFrames;
        private float Radius => Projectile.ai[0] > 10f ? Projectile.ai[0] : 170f;

        /// <summary>纯表现弧：各端自演，不入同步</summary>
        private readonly List<Arc> visArcs = [];
        private int lastFireballTick = -1;
        private int lastLightningTick = -1;
        private int lastRainTick = -1;

        private Player Owner => Main.player[Projectile.owner];

        private struct Arc
        {
            public Vector2 Start;
            public Vector2 End;
            public float Life;
            public float MaxLife;
            public List<Vector2> Points;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = TotalFrames + 20;
        }

        public override bool? CanDamage() => false;

        private float DomainAlpha() {
            if (collapsing || timer >= collapseStart) {
                int ct = timer - collapseStart;
                return MathHelper.Clamp(1f - ct / (float)CollapseFrames, 0f, 1f);
            }
            return MathHelper.Clamp(timer / (float)ExpandFrames, 0f, 1f);
        }

        private float CurrentRadius() {
            if (collapsing || timer >= collapseStart) {
                int ct = timer - collapseStart;
                return MathHelper.Lerp(Radius, 48f, EaseInCubic(MathHelper.Clamp(ct / (float)CollapseFrames, 0f, 1f)));
            }
            float expandT = MathHelper.Clamp(timer / (float)ExpandFrames, 0f, 1f);
            float breathe = timer > ExpandFrames
                ? MathF.Sin((timer - ExpandFrames) * 0.06f) * Radius * 0.018f
                : 0f;
            return MathHelper.Lerp(40f, Radius, EaseOutCubic(expandT)) + breathe;
        }

        private static float EaseOutCubic(float x) => 1f - MathF.Pow(1f - x, 3f);

        private static float EaseInCubic(float x) => x * x * x;

        public override void AI() {
            timer++;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = 2 + TotalFrames;

            Player owner = Owner;
            if (!collapsing && (owner == null || !owner.active || owner.dead) && timer < collapseStart) {
                collapsing = true;
                collapseStart = timer;
            }

            float alpha = DomainAlpha();
            float radius = CurrentRadius();

            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                    Volume = 0.72f, Pitch = -0.55f, MaxInstances = 2
                }, Projectile.Center);
            }
            if (timer == ExpandFrames) {
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.55f, Pitch = -0.25f, MaxInstances = 2
                }, Projectile.Center);
            }
            if (timer == collapseStart + 1) {
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.4f, Pitch = -0.55f, MaxInstances = 2
                }, Projectile.Center);
            }

            UpdateBurn(alpha, radius);
            UpdateAttacks(alpha, radius);
            UpdateArcs();
            UpdateVisuals(alpha, radius);

            float flicker = 0.85f + MathF.Sin(timer * 0.18f) * 0.15f;
            Lighting.AddLight(Projectile.Center,
                1.6f * alpha * flicker, 0.45f * alpha * flicker, 0.18f * alpha * flicker);

            if (timer >= collapseStart + CollapseFrames) {
                Projectile.Kill();
            }
        }

        private void UpdateBurn(float alpha, float radius) {
            if (alpha < 0.55f) {
                return;
            }
            bool strikeTick = timer % BurnTick == 0 && Projectile.IsOwnedByLocalPlayer();
            Player owner = Owner;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 1) {
                    continue;
                }
                if (npc.CountsAsACritter && owner?.dontHurtCritters == true) {
                    continue;
                }
                if (Vector2.Distance(npc.Center, Projectile.Center) >= radius) {
                    continue;
                }

                if (strikeTick) {
                    int damage = Math.Max(Projectile.damage, 1);
                    if (npc.boss) {
                        damage = (int)(damage * 1.35f);
                    }
                    npc.SimpleStrikeNPC(damage, npc.direction);
                    npc.AddBuff(BuffID.OnFire3, 180);
                    if (!VaultUtils.isServer) {
                        SpawnBurnDust(npc);
                    }
                }
            }
        }

        private static void SpawnBurnDust(NPC npc) {
            for (int d = 0; d < 3; d++) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.35f, npc.height * 0.35f);
                Dust dust = Dust.NewDustPerfect(pos, CWRID.Dust_Brimstone,
                    Main.rand.NextVector2Circular(1.6f, 1.6f), 80, default, 1.3f);
                dust.noGravity = true;
            }
        }

        /// <summary>存续期吐弹：owner 生成，目标取最近（确定性，不掷 rand）</summary>
        private void UpdateAttacks(float alpha, float radius) {
            if (alpha < 0.7f || timer < ExpandFrames || timer >= collapseStart) {
                return;
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            int target = FindNearest(radius * 2.2f);
            int beat = timer - ExpandFrames;

            if (beat % FireballPeriod == 0 && beat / FireballPeriod > lastFireballTick) {
                lastFireballTick = beat / FireballPeriod;
                if (target >= 0) {
                    NPC npc = Main.npc[target];
                    Vector2 vel = (npc.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 0.1f;
                    int dmg = Math.Max((int)(Projectile.damage * 2.4f), 1);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        ModContent.ProjectileType<PandemoniumFireball>(), dmg, 2f, Projectile.owner, 0f, 2f);
                }
            }

            if (beat % LightningPeriod == 0 && beat / LightningPeriod > lastLightningTick) {
                lastLightningTick = beat / LightningPeriod;
                Vector2 spawn = target >= 0
                    ? Main.npc[target].Center + new Vector2(0f, -80f)
                    : Projectile.Center + new Vector2(0f, -60f);
                int dmg = Math.Max((int)(Projectile.damage * 1.6f), 1);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawn, Vector2.Zero,
                    ModContent.ProjectileType<PandemoniumLightning>(), dmg, 1f, Projectile.owner, 0f, 1f);
            }

            if (beat % RainPeriod == 0 && beat / RainPeriod > lastRainTick) {
                lastRainTick = beat / RainPeriod;
                Vector2 aim = target >= 0 ? Main.npc[target].Center : Projectile.Center;
                int dmg = Math.Max((int)(Projectile.damage * 1.4f), 1);
                for (int k = 0; k < 6; k++) {
                    //确定性扇位,各端误跑也不会分叉数量
                    float lane = (k - 2.5f) * 28f;
                    Vector2 from = Projectile.Center + new Vector2(lane, -220f - k * 8f);
                    Vector2 to = aim + new Vector2(lane * 0.4f, 0f);
                    Vector2 vel = (to - from).SafeNormalize(Vector2.UnitY) * 11f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                        ModContent.ProjectileType<PandemoniumRainDrop>(), dmg, 1f, Projectile.owner);
                }
            }
        }

        private int FindNearest(float range) {
            int best = -1;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        private void UpdateArcs() {
            for (int i = visArcs.Count - 1; i >= 0; i--) {
                Arc arc = visArcs[i];
                arc.Life++;
                if (arc.Life >= arc.MaxLife) {
                    visArcs.RemoveAt(i);
                }
                else {
                    visArcs[i] = arc;
                }
            }

            //表现弧,场心到最近敌或场缘,不拴主人
            if (Main.dedServ || timer % 10 != 0 || DomainAlpha() < 0.4f) {
                return;
            }
            int target = FindNearest(CurrentRadius() * 1.4f);
            Vector2 end = target >= 0
                ? Main.npc[target].Center
                : Projectile.Center + ((timer * 0.37f + Projectile.identity) % MathHelper.TwoPi).ToRotationVector2() * CurrentRadius();
            visArcs.Add(new Arc {
                Start = Projectile.Center,
                End = end,
                Life = 0,
                MaxLife = 14,
                Points = BuildPath(Projectile.Center, end, 5),
            });
        }

        private static List<Vector2> BuildPath(Vector2 start, Vector2 end, int segs) {
            List<Vector2> pts = [start];
            float len = Vector2.Distance(start, end) / segs;
            for (int i = 1; i < segs; i++) {
                float t = i / (float)segs;
                //表现抖动用进度当相位,不掷 Main.rand
                float wobble = MathF.Sin(t * 11.3f + start.X * 0.01f) * len * 0.28f;
                Vector2 perp = (end - start).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                pts.Add(Vector2.Lerp(start, end, t) + perp * wobble);
            }
            pts.Add(end);
            return pts;
        }

        private void UpdateVisuals(float alpha, float radius) {
            if (Main.dedServ || alpha < 0.05f) {
                return;
            }
            if (timer % 3 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * radius * Main.rand.NextFloat(0.55f, 1.05f);
                Dust d = Dust.NewDustPerfect(pos, CWRID.Dust_Brimstone,
                    Vector2.UnitY * Main.rand.NextFloat(-2.2f, -0.4f), 80, default, Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = true;
            }
            if (timer % 5 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 edge = Projectile.Center + ang.ToRotationVector2() * radius;
                Dust f = Dust.NewDustPerfect(edge, DustID.Torch, Main.rand.NextVector2Circular(1.6f, 1.6f),
                    60, Color.OrangeRed, Main.rand.NextFloat(0.8f, 1.4f));
                f.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float alpha = DomainAlpha();
            if (alpha <= 0.01f) {
                return false;
            }
            DrawField(alpha, CurrentRadius());
            DrawArcs();
            return false;
        }

        private void DrawField(float alpha, float radius) {
            Effect shader = EffectLoader.BrimstoneDomain?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) {
                return;
            }

            float drawDiameter = radius * 1.3f * 2f;
            Vector2 center = Projectile.Center - Main.screenPosition;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.016f);
            shader.Parameters["fadeAlpha"]?.SetValue(alpha);
            shader.Parameters["tierLevel"]?.SetValue(1.2f);
            shader.Parameters["expandProgress"]?.SetValue(MathHelper.Clamp(alpha, 0f, 1f));
            shader.Parameters["pulseIntensity"]?.SetValue(0.5f + MathF.Sin(timer * 0.08f) * 0.25f);
            shader.Parameters["coreColor"]?.SetValue(new Vector3(1f, 0.31f, 0.16f));
            shader.Parameters["midColor"]?.SetValue(new Vector3(0.78f, 0.2f, 0.12f));
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.47f, 0.12f, 0.08f));
            shader.Parameters["voidColor"]?.SetValue(new Vector3(0.16f, 0.04f, 0.04f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, center, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawDiameter, drawDiameter),
                SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawArcs() {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null || visArcs.Count == 0) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            foreach (Arc arc in visArcs) {
                if (arc.Points == null || arc.Points.Count < 2) {
                    continue;
                }
                float a = 1f - arc.Life / arc.MaxLife;
                Color c = new Color(255, 140, 80, 200) * a;
                for (int i = 0; i < arc.Points.Count - 1; i++) {
                    Vector2 s = arc.Points[i] - Main.screenPosition;
                    Vector2 e = arc.Points[i + 1] - Main.screenPosition;
                    Vector2 diff = e - s;
                    float len = diff.Length();
                    if (len < 1f) {
                        continue;
                    }
                    sb.Draw(pixel, s, new Rectangle(0, 0, 1, 1), c, diff.ToRotation(),
                        Vector2.Zero, new Vector2(len, 2.2f), SpriteEffects.None, 0f);
                    sb.Draw(pixel, s, new Rectangle(0, 0, 1, 1), c * 0.28f, diff.ToRotation(),
                        Vector2.Zero, new Vector2(len, 5f), SpriteEffects.None, 0f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int k = 0; k < 18; k++) {
                float ang = k / 18f * MathHelper.TwoPi;
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    CWRID.Dust_Brimstone, ang.ToRotationVector2() * Main.rand.NextFloat(3f, 7f),
                    60, default, Main.rand.NextFloat(1.6f, 2.6f));
                d.noGravity = true;
            }
        }
    }
}
