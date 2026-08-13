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
    /// <summary>苔藓枪管，光束铺湿苔，右键球吸苔扩爆</summary>
    internal sealed class MossboundBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(70, 175, 75);

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += -0.15f;
            ctx.BeamLifeMul += 0.08f;
            ctx.ManaCostMul += 0.48f;
            ctx.OrbExplosionRadiusMul += 0.08f;
        }

        //同主湿苔上限
        private const int MaxConcurrentPatches = 8;
        //同点130px内已有则跳过
        private const float MinSpacing = 80f;
        //单束生成间隔帧
        private const int SpawnInterval = 36;
        //每球吸苔上限
        private const int MaxAbsorbPerOrb = 5;

        //每球已吸计数，OnOrbKill 清
        private static readonly Dictionary<int, int> _absorbedByOrb = new();

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % SpawnInterval != 0) return;
            int patchType = ModContent.ProjectileType<SHPCMossPatchProj>();
            //上限+间距节流
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
            //跨帧累吸，满额停
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
                //吸苔表现，孢子被卷向球
                if (Main.netMode != NetmodeID.Server) {
                    Vector2 suck = (orb.Projectile.Center - proj.Center).SafeNormalize(Vector2.UnitY);
                    for (int k = 0; k < 5; k++) {
                        PRTLoader.NewParticle<PRT_Sparkle>(proj.Center + Main.rand.NextVector2Circular(26f, 14f),
                            suck * Main.rand.NextFloat(5f, 9f) + Main.rand.NextVector2Circular(1f, 1f),
                            new Color(130, 225, 110), Main.rand.NextFloat(0.3f, 0.55f))
                            .Configure(new Color(45, 120, 55), Main.rand.Next(14, 24), 0f, 0.6f);
                    }
                }
                proj.Kill();
                absorbed++;
            }
            if (absorbed > 0) {
                _absorbedByOrb[orb.Projectile.whoAmI] = already + absorbed;
                orb.ExplosionRadiusMul += 0.06f * absorbed;
                //球侧吸收反馈
                if (Main.netMode != NetmodeID.Server) {
                    PRTLoader.NewParticle<PRT_DWave>(orb.Projectile.Center, Vector2.Zero,
                        new Color(120, 220, 90), 0.05f).Configure(new Vector2(1f, 1f), 0f, 0.32f, 14);
                }
            }
        }

        public override void OnOrbKill(CyberChargeOrbProj orb, int timeLeft) {
            _absorbedByOrb.Remove(orb.Projectile.whoAmI);
        }
    }

    /// <summary>湿苔斑</summary>
    internal sealed class SHPCMossPatchProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Vector3 MossCoreVec = new Color(140, 230, 110).ToVector3();
        private static readonly Vector3 MossGlowVec = new Color(60, 130, 60).ToVector3();

        //藤蔓笔触(6顶点)，仅视觉
        private struct Vine { public Vector2[] Pts; public int Age; public int MaxAge; }
        private readonly List<Vine> vines = new();
        //全部藤蔓共用一条 Trail，避免逐藤新建 GPU 缓冲
        private Trail vineTrail;
        private float age;
        //苔簇布局种子，客户端惰性播种
        private float lobeSeed = -1f;

        //确定性 0-1 哈希，苔簇逐瓣布局
        private static float Hash01(float x) {
            float v = MathF.Sin(x * 12.9898f) * 43758.5453f;
            return v - MathF.Floor(v);
        }

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

        //NPC 扫描节流，每6帧
        private const int ScanInterval = 6;
        //缠根冷却帧(~1s)
        private const int BurstCooldown = 60;
        //本帧最高苔层，缠根判定
        private int cachedPeakStacks;

        public override void AI() {
            age++;
            float radius = 70f + Projectile.localAI[0] * 20f;
            //每6帧扫 NPC 刷苔
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % ScanInterval == 0) {
                cachedPeakStacks = 0;
                float r2 = radius * radius;
                bool authority = Main.netMode != NetmodeID.MultiplayerClient;
                // 联机刷苔只在权威端写；owner 客户端只读 ExtraAI 镜像做缠根预览，避免双倍叠层
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > r2) continue;
                    if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) continue;
                    if (authority) {
                        int before = eff.MossStacks;
                        eff.ApplyMossAuthority(90, 1);
                        if (Main.netMode == NetmodeID.Server
                            && eff.MossStacks != before) {
                            npc.netUpdate = true;
                        }
                    }
                    cachedPeakStacks = Math.Max(cachedPeakStacks, eff.MossStacks);
                }
            }
            //每18帧伸藤到最近敌，纯视觉不上服务端
            if (Main.netMode != NetmodeID.Server && age % 18f == 0f) {
                NPC near = Projectile.Center.FindClosestNPC(radius * 1.4f, false, true);
                if (near != null) {
                    SpawnVine(Projectile.Center, near.Center);
                }
            }
            //缠根，苔≥4且未冷却
            if (cachedPeakStacks >= 4 && Projectile.localAI[1] <= 0f) {
                Projectile.localAI[1] = BurstCooldown;
                BurstRoots(radius);
            }
            if (Projectile.localAI[1] > 0f) Projectile.localAI[1]--;

            //藤蔓寿命
            for (int i = vines.Count - 1; i >= 0; i--) {
                Vine v = vines[i];
                v.Age++;
                vines[i] = v;
                if (v.Age >= v.MaxAge) vines.RemoveAt(i);
            }
            //孢子，12帧节流
            if (Main.netMode == NetmodeID.Server || Main.GameUpdateCount % 12 != 0) return;
            PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(radius, radius * 0.35f), new Vector2(0f, Main.rand.NextFloat(-0.5f, 0.2f)), new Color(120, 220, 110), Main.rand.NextFloat(0.25f, 0.55f)).Configure(new Color(40, 110, 50), Main.rand.Next(20, 45), Main.rand.NextFloat(-0.1f, 0.1f), 0.7f);
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
            vines.Add(new Vine { Pts = pts, Age = 0, MaxAge = 18 });
        }

        private void BurstRoots(float radius) {
            //缠根AOE，苔≥4打6倍斑块伤；SimpleStrikeNPC server/SP
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
                PRTLoader.NewParticle<PRT_CorrosionWave>(spawn, Vector2.Zero, Color.White, 0.05f).Configure(0.6f, 28, angle);
            }
            //根脉脉冲环，加色批禁 A=0
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, new Color(120, 220, 90), 0.05f).Configure(new Vector2(1.4f, 0.55f), 0f, 0.55f, 24);
            SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.45f, Pitch = -0.2f }, Projectile.Center);
            //旁观客户端也走此路径，震屏按距离衰减
            float shakeAtten = MathHelper.Clamp(1f - Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) / 900f, 0f, 1f);
            SHPCNaturalFx.Shake(1.5f * shakeAtten);
        }

        public override void OnKill(int timeLeft) {
            //苔斑消散孢子云，吸收与自然消亡两条路径都有余韵
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.4f, 1.4f) + new Vector2(0f, -0.8f);
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(40f, 18f), vel,
                    new Color(120, 220, 110), Main.rand.NextFloat(0.25f, 0.5f))
                    .Configure(new Color(40, 110, 50), Main.rand.Next(18, 34), Main.rand.NextFloat(-0.1f, 0.1f), 0.65f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float radius = 70f + Projectile.localAI[0] * 20f;
            //淡入12f，淡出末30f
            float fadeIn = MathHelper.Clamp(age / 12f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float alpha = MathHelper.Clamp(fadeIn * fadeOut, 0f, 1f);
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            if (lobeSeed < 0f) lobeSeed = Main.rand.NextFloat(100f, 900f);

            //赛博地纹压淡作底
            Texture2D tile = CWRAsset.TileHightlight?.Value;
            if (tile != null) {
                Vector2 origin = tile.Size() * 0.5f;
                Color tint = new Color(80, 200, 90, 0) * alpha * 0.3f;
                float scale = radius / tile.Width * 1.6f;
                Main.spriteBatch.Draw(tile, baseScreen, null, tint, MathHelper.PiOver4, origin, scale, SpriteEffects.None, 0f);
            }
            //苔簇本体，Fog 真alpha 多瓣，暗湿绿压底+亮梢，逐瓣错帧长出
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Vector2 fogOrigin = fog.Size() * 0.5f;
                for (int i = 0; i < 6; i++) {
                    float h1 = Hash01(lobeSeed + i * 17.31f);
                    float h2 = Hash01(lobeSeed + i * 29.7f + 3.1f);
                    float h3 = Hash01(lobeSeed + i * 41.9f + 7.7f);
                    float h4 = Hash01(lobeSeed + i * 53.3f + 11.4f);
                    float grow = MathHelper.Clamp((age - i * 3f) / 16f, 0f, 1f);
                    grow = 1f - (1f - grow) * (1f - grow);
                    if (grow <= 0f) continue;
                    float ang = MathHelper.TwoPi * i / 6f + h1 * 1.2f;
                    Vector2 offset = ang.ToRotationVector2() * radius * (0.2f + 0.42f * h2);
                    offset.Y *= 0.45f;
                    float lobeScale = radius / fog.Width * (1.05f + 0.9f * h3) * grow;
                    float rot = h3 * MathHelper.TwoPi;
                    SpriteEffects flip = h4 > 0.5f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    //湿暗底瓣
                    Main.spriteBatch.Draw(fog, baseScreen + offset, null,
                        new Color(26, 64, 34) * (alpha * 0.85f * grow), rot, fogOrigin, lobeScale, flip, 0f);
                    //受光亮梢，同瓣上移错位
                    Main.spriteBatch.Draw(fog, baseScreen + offset - new Vector2(2f, 6f), null,
                        new Color(96, 190, 92) * (alpha * 0.38f * grow), rot, fogOrigin, lobeScale * 0.62f, flip, 0f);
                }
            }
            return false;
        }

        private float VineWidth(float progress) {
            //根粗梢细，纤维藤蔓
            return MathHelper.Lerp(9.5f, 2.2f, progress);
        }

        private Color VineColor(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (vines.Count == 0) return;
            //专属纤维材质，缺 fxc 回退共享电弧
            Effect vineFx = EffectLoader.SHPCModMossVine?.Value;
            Effect shader = vineFx ?? EffectLoader.CyberDataArc?.Value;
            if (shader == null) return;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.06f);
            shader.Parameters["coreColor"]?.SetValue(MossCoreVec);
            shader.Parameters["glowColor"]?.SetValue(MossGlowVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            if (vineFx != null) {
                Texture2D noise = CWRAsset.PerlinNoise?.Value;
                if (noise == null) return;
                //s1 显式绑定，shader 内 register(s1)
                device.Textures[1] = noise;
                device.SamplerStates[1] = SamplerState.LinearWrap;
                device.BlendState = BlendState.AlphaBlend;
            }
            else {
                Texture2D noise = CWRAsset.ThunderTrail?.Value ?? CWRAsset.Extra_193?.Value;
                if (noise == null) return;
                shader.Parameters["uNoiseTex"]?.SetValue(noise);
                device.BlendState = BlendState.Additive;
            }
            //藤蔓随苔斑一同淡出
            float patchFade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            for (int i = 0; i < vines.Count; i++) {
                Vine v = vines[i];
                float fade = (1f - v.Age / (float)v.MaxAge) * patchFade;
                shader.Parameters["fadeAlpha"]?.SetValue(fade);
                vineTrail ??= new Trail(v.Pts, VineWidth, VineColor);
                vineTrail.TrailPositions = v.Pts;
                vineTrail.DrawTrail(shader);
            }
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float radius = 70f + Projectile.localAI[0] * 20f;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float fadeIn = MathHelper.Clamp(age / 12f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float pulse = (0.55f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.15f)) * fadeIn * fadeOut;
            //真加色批，A 必须随强度走，A=0 整层不显示
            Color inner = new Color(150, 240, 130) * pulse * 0.4f;
            Color outer = new Color(40, 110, 50) * pulse * 0.22f;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, radius / 32f, 0f, 3);
        }
    }
}
