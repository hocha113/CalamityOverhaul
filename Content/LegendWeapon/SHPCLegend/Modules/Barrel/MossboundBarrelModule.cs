using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>
    /// 苔藓枪管：光束铺设湿苔区域，右键能量球吸收苔痕扩大最终爆炸。
    /// </summary>
    internal sealed class MossboundBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(70, 175, 75);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += -0.18f;
            ctx.BeamLifeMul += 0.20f;
            ctx.OrbExplosionRadiusMul += 0.10f;
            ctx.ManaCostMul += 0.16f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % 18 != 0) return;
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<SHPCMossPatchProj>(),
                Math.Max(beam.Projectile.damage / 8, 1), 0f, beam.Projectile.owner);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[0] = 0f;
            }
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            int absorbed = 0;
            for (int i = 0; i < Main.maxProjectiles && absorbed < 5; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != orb.Projectile.owner) continue;
                if (proj.type != ModContent.ProjectileType<SHPCMossPatchProj>()) continue;
                if (Vector2.DistanceSquared(proj.Center, orb.Projectile.Center) > 180f * 180f) continue;
                proj.Kill();
                absorbed++;
            }
            if (absorbed > 0) {
                orb.ExplosionRadiusMul += 0.06f * absorbed;
            }
        }
    }

    /// <summary>
    /// 湿苔斑
    /// </summary>
    internal sealed class SHPCMossPatchProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private static readonly Vector3 MossCoreVec = new Color(140, 230, 110).ToVector3();
        private static readonly Vector3 MossGlowVec = new Color(60, 130, 60).ToVector3();

        //当前帧活跃的藤蔓笔触（每条 6 顶点），仅视觉
        private struct Vine { public Vector2[] Pts; public int Age; public int MaxAge; public Trail TrailRef; }
        private readonly List<Vine> vines = new();
        private float age;
        private float burstShakeQueued;

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            age++;
            float radius = 70f + Projectile.localAI[0] * 20f;
            int peakStacks = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radius * radius) continue;
                if (npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                    eff.ApplyMoss(90, 1);
                    peakStacks = Math.Max(peakStacks, eff.MossStacks);
                }
            }
            //每 12 帧伸出一条藤蔓到最近敌人，视觉化"缠绕"
            if (age % 12f == 0f) {
                NPC near = Projectile.Center.FindClosestNPC(radius * 1.4f, false, true);
                if (near != null) {
                    SpawnVine(Projectile.Center, near.Center);
                }
            }
            //缠根触发：MossStacks ≥ 4 且当前未冷却时，发射腐蚀环 + 屏幕震动
            if (peakStacks >= 4 && Projectile.localAI[1] <= 0f) {
                Projectile.localAI[1] = 60f; //冷却 1 秒
                BurstRoots();
            }
            if (Projectile.localAI[1] > 0f) Projectile.localAI[1]--;

            //更新藤蔓寿命
            for (int i = vines.Count - 1; i >= 0; i--) {
                Vine v = vines[i];
                v.Age++;
                vines[i] = v;
                if (v.Age >= v.MaxAge) vines.RemoveAt(i);
            }
            //常规苔藓孢子粒子
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 8 != 0) return;
            PRTLoader.AddParticle(new PRT_Sparkle(
                Projectile.Center + Main.rand.NextVector2Circular(radius, radius * 0.35f),
                new Vector2(0f, Main.rand.NextFloat(-0.5f, 0.2f)),
                new Color(120, 220, 110), new Color(40, 110, 50),
                Main.rand.NextFloat(0.25f, 0.55f), Main.rand.Next(20, 45),
                Main.rand.NextFloat(-0.1f, 0.1f), 0.7f));
        }

        private void SpawnVine(Vector2 from, Vector2 to) {
            Vector2[] pts = new Vector2[6];
            Vector2 dir = to - from;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float len = dir.Length();
            float amp = MathF.Min(len * 0.1f, 14f);
            float seed = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < pts.Length; i++) {
                float t = i / (float)(pts.Length - 1);
                float taper = MathF.Sin(t * MathHelper.Pi);
                pts[i] = Vector2.Lerp(from, to, t) + perp * MathF.Sin(seed + t * 7f) * taper * amp;
            }
            vines.Add(new Vine { Pts = pts, Age = 0, MaxAge = 18, TrailRef = null });
        }

        private void BurstRoots() {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 spawn = Projectile.Center + angle.ToRotationVector2() * 24f;
                PRTLoader.AddParticle(new PRT_CorrosionWave(spawn, 0.05f, 0.6f, 28, angle));
            }
            //大圆波再补一次脉冲环，形成"地下根脉爆发"
            PRTLoader.AddParticle(new PRT_DWave(
                Projectile.Center, Vector2.Zero,
                new Color(120, 220, 90, 0), new Vector2(1.4f, 0.55f), 0f, 0.05f, 0.55f, 24));
            SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.45f, Pitch = -0.2f }, Projectile.Center);
            burstShakeQueued = 1.5f;
            if (burstShakeQueued > 0f) {
                SHPCNaturalFx.Shake(burstShakeQueued);
                burstShakeQueued = 0f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float radius = 70f + Projectile.localAI[0] * 20f;
            //淡入淡出：0~12f fadeIn, 末 30f fadeOut
            float fadeIn = MathHelper.Clamp(age / 12f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float alpha = MathHelper.Clamp(fadeIn * fadeOut, 0f, 1f);
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;

            //地面贴花
            Texture2D tile = CWRAsset.TileHightlight?.Value;
            if (tile != null) {
                Vector2 origin = tile.Size() * 0.5f;
                Color tint = new Color(80, 200, 90, 0) * alpha * 0.55f;
                float scale = radius / tile.Width * 1.6f;
                Main.spriteBatch.Draw(tile, baseScreen, null, tint, MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
            }
            return false;
        }

        private float VineWidth(float progress) {
            float taper = MathF.Sin(MathHelper.Clamp(progress * MathHelper.Pi, 0f, MathHelper.Pi));
            return 3f + taper * 6f;
        }

        private Color VineColor(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (vines.Count == 0) return;
            Effect shader = EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.06f);
            shader.Parameters["coreColor"]?.SetValue(MossCoreVec);
            shader.Parameters["glowColor"]?.SetValue(MossGlowVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            for (int i = 0; i < vines.Count; i++) {
                Vine v = vines[i];
                float fade = 1f - v.Age / (float)v.MaxAge;
                shader.Parameters["fadeAlpha"]?.SetValue(fade);
                v.TrailRef ??= new Trail(v.Pts, VineWidth, VineColor);
                v.TrailRef.TrailPositions = v.Pts;
                v.TrailRef.DrawTrail(shader);
                vines[i] = v;
            }
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float radius = 70f + Projectile.localAI[0] * 20f;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float pulse = 0.55f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.15f);
            Color inner = new Color(150, 240, 130, 0) * pulse * 0.55f;
            Color outer = new Color(40, 110, 50, 0) * pulse * 0.3f;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, radius / 32f, 0f, 3);
        }
    }
}
