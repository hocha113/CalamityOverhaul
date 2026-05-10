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
            ctx.BeamSpeedMul += -0.12f;
            ctx.BeamLifeMul += 0.8f;
            ctx.ManaCostMul += 0.4f;
        }

        //同主同时存在的湿苔斑块上限
        private const int MaxConcurrentPatches = 4;
        //同点 130px 内已有斑块时跳过本次生成（避免聚簇）
        private const float MinSpacing = 130f;
        //单条光束的生成节奏（间隔帧数）
        private const int SpawnInterval = 36;
        //每颗能量球能吸收的湿苔斑块上限
        private const int MaxAbsorbPerOrb = 5;

        //每颗能量球独立计数已吸收的斑块数；OnOrbKill 中清理
        private static readonly Dictionary<int, int> _absorbedByOrb = new();

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % SpawnInterval != 0) return;
            int patchType = ModContent.ProjectileType<SHPCMossPatchProj>();
            //节流：上限 + 聚簇过滤
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, patchType) >= MaxConcurrentPatches) return;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, patchType, beam.Projectile.Center, MinSpacing)) return;
            int idx = Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                beam.Projectile.Center, Vector2.Zero,
                patchType, Math.Max(beam.Projectile.damage / 8, 1), 0f, beam.Projectile.owner);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[0] = 0f;
            }
        }

        public override void OnOrbFlyingAI(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            //跨帧累积已吸收数：到达 MaxAbsorbPerOrb 后该球永不再吸收
            if (!_absorbedByOrb.TryGetValue(orb.Projectile.whoAmI, out int already)) already = 0;
            int budget = MaxAbsorbPerOrb - already;
            if (budget <= 0) return;
            int absorbed = 0;
            int patchType = ModContent.ProjectileType<SHPCMossPatchProj>();
            for (int i = 0; i < Main.maxProjectiles && absorbed < budget; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != orb.Projectile.owner) continue;
                if (proj.type != patchType) continue;
                if (Vector2.DistanceSquared(proj.Center, orb.Projectile.Center) > 180f * 180f) continue;
                proj.Kill();
                absorbed++;
            }
            if (absorbed > 0) {
                _absorbedByOrb[orb.Projectile.whoAmI] = already + absorbed;
                orb.ExplosionRadiusMul += 0.06f * absorbed;
            }
        }

        public override void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) {
            _absorbedByOrb.Remove(orb.Projectile.whoAmI);
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

        //斑块状态扫描节流：每 6 帧才完整扫一次 NPC 列表
        private const int ScanInterval = 6;
        //缠根触发后冷却帧数（≈1 秒）
        private const int BurstCooldown = 60;
        //缓存的本帧最高苔藓堆叠数（用于触发缠根判定）
        private int cachedPeakStacks;

        public override void AI() {
            age++;
            float radius = 70f + Projectile.localAI[0] * 20f;
            //每 6 帧执行一次全 NPC 扫描，刷新苔藓与堆叠峰值
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % ScanInterval == 0) {
                cachedPeakStacks = 0;
                float r2 = radius * radius;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > r2) continue;
                    if (npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) {
                        //ApplyMoss 内部用 Math.Max 刷新时长，6 帧间隔内仍能维持 90 帧寿命
                        eff.ApplyMoss(90, 1);
                        cachedPeakStacks = Math.Max(cachedPeakStacks, eff.MossStacks);
                    }
                }
            }
            //每 18 帧伸出一条藤蔓到最近敌人，视觉化"缠绕"
            if (age % 18f == 0f) {
                NPC near = Projectile.Center.FindClosestNPC(radius * 1.4f, false, true);
                if (near != null) {
                    SpawnVine(Projectile.Center, near.Center);
                }
            }
            //缠根触发：MossStacks ≥ 4 且当前未冷却时，AOE 伤害 + 视觉
            if (cachedPeakStacks >= 4 && Projectile.localAI[1] <= 0f) {
                Projectile.localAI[1] = BurstCooldown;
                BurstRoots(radius);
            }
            if (Projectile.localAI[1] > 0f) Projectile.localAI[1]--;

            //更新藤蔓寿命
            for (int i = vines.Count - 1; i >= 0; i--) {
                Vine v = vines[i];
                v.Age++;
                vines[i] = v;
                if (v.Age >= v.MaxAge) vines.RemoveAt(i);
            }
            //常规苔藓孢子粒子（节流到 12 帧一次）
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 12 != 0) return;
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

        private void BurstRoots(float radius) {
            //AOE 伤害：被苔藓缠绕到 4 层的目标在缠根爆发时受到斑块伤害的 6 倍一次性打击
            //SimpleStrikeNPC 在 server / 单机均有效，且不需要 NetMessage 同步
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int dmg = Math.Max(Projectile.damage * 6, 1);
                float r2 = radius * radius;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > r2) continue;
                    if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff) || eff.MossStacks < 4) continue;
                    int hitDir = npc.Center.X >= Projectile.Center.X ? 1 : -1;
                    npc.SimpleStrikeNPC(dmg, hitDir, false, 1.2f, DamageClass.Magic, false, 0f, true);
                }
            }
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
            SHPCNaturalFx.Shake(1.5f);
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
