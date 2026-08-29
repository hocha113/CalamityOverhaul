using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee.DawnshatterAzures;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Golem
{
    /// <summary>
    /// 日核重拳：向光标方向的巨型火焰拳，穿透，伤害随蓄能层数放大（24 层封顶）。
    /// 本体=原版石巨人拳贴图（不透明核心），叠 GolemMagmaVein 脉络与
    /// GolemThruster 尾焰；首个命中目标炸出日耀新星并向两侧掀起熔岩链爆。
    /// ai[0]=蓄能层数（视觉体量用） ai[1]=原地引拳标记（新星半径 +30%）
    /// </summary>
    internal class SolarCoreFistPunch : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float LaunchSpeed = 8f;
        private const float MaxSpeed = 27f;
        /// <summary>起手蓄压更新数（extraUpdates=1，2更新=1帧）</summary>
        private const int LaunchUpdates = 6;

        private int updatesAlive;
        private bool novaFired;
        private Vector2 muzzlePos;
        private Vector2 muzzleDir;

        private int Stacks => (int)Projectile.ai[0];
        private bool Braced => Projectile.ai[1] >= 1f;
        private float VisualScale => 1.45f + Math.Min(Stacks, 20) * 0.02f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 190;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            if (updatesAlive == 0) {
                muzzlePos = Projectile.Center;
                muzzleDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            }
            updatesAlive++;

            //起手蓄压 → 推进器点火复合加速（禁匀速平移）
            if (updatesAlive > LaunchUpdates) {
                float speed = Projectile.velocity.Length();
                if (speed < MaxSpeed) {
                    Projectile.velocity *= 1.075f;
                    if (Projectile.velocity.Length() > MaxSpeed) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * MaxSpeed;
                    }
                }
            }

            //拳体自旋（旋转贴图的运动签名交给残影与火星螺线）
            Projectile.rotation += 0.30f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.62f, 0.24f) * 0.9f);

            if (VaultUtils.isServer) {
                return;
            }

            //火星螺线：双股绕飞行轴缠绕，甩出率随速升
            float speedNow = Projectile.velocity.Length();
            int shedGate = speedNow > 20f ? 1 : 3;
            if (updatesAlive % shedGate == 0) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                float phase = updatesAlive * 0.5f;
                for (int strand = -1; strand <= 1; strand += 2) {
                    Vector2 off = perp * MathF.Sin(phase) * 20f * strand * VisualScale;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + off - dir * 18f,
                        -dir * Main.rand.NextFloat(1f, 3f) + off * 0.03f,
                        Color.Lerp(new Color(255, 170, 60), new Color(255, 120, 30), Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.9f, 1.4f)).Configure(false, Main.rand.Next(10, 18), null);
                }
            }
            //尾流余烬
            if (updatesAlive % 4 == 0) {
                PRTLoader.NewParticle<PRT_DawnEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f) * VisualScale,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    new Color(255, 190, 90), Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(22, 36));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 300);
            target.AddBuff(BuffID.Daybreak, 300);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit41 with { Pitch = -0.3f, Volume = 0.9f }, target.Center);
            }

            //主目标结算：日耀新星（重拳 ×0.6）+ 双侧熔岩链爆（×0.25 ×6 段，左右各 3）。
            //命中钩子只在所有者端运行，生成即同步。满层全链 2000+1200+6×500=6200 基伤
            if (novaFired) {
                return;
            }
            novaFired = true;

            Vector2 impact = target.Center;
            int novaDamage = (int)(Projectile.damage * 0.6f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), impact, Vector2.Zero,
                ModContent.ProjectileType<SolarCoreNova>(), novaDamage, 8f, Projectile.owner,
                Braced ? 1f : 0f);

            int chainDamage = (int)(Projectile.damage * 0.25f);
            for (int side = -1; side <= 1; side += 2) {
                for (int i = 1; i <= 3; i++) {
                    Vector2 pos = new(impact.X + side * i * 115f, impact.Y);
                    pos.Y = ScanGroundY(pos);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, Vector2.Zero,
                        ModContent.ProjectileType<SolarChainBurst>(), chainDamage, 6f, Projectile.owner,
                        4 + i * 5, i);
                }
            }
        }

        /// <summary>自命中点向下探地（30格内），无地则原高度悬爆</summary>
        internal static float ScanGroundY(Vector2 from) {
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            for (int i = 0; i < 30; i++) {
                int y = ty + i;
                if (!WorldGen.InWorld(tx, y, 10)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) {
                    return y * 16f - 8f;
                }
            }
            return from.Y;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //拳体熄灭：余烬向后散逸（余韵活过弹体）
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_DawnEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    -dir * Main.rand.NextFloat(1f, 4f) + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    new Color(255, 170, 70), Main.rand.NextFloat(0.8f, 1.3f)).Configure(Main.rand.Next(26, 44));
            }
        }

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.GolemFistLeft);
            Texture2D fist = TextureAssets.Npc[NPCID.GolemFistLeft].Value;
            int frameCount = Math.Max(Main.npcFrameCount[NPCID.GolemFistLeft], 1);
            Rectangle frame = new(0, 0, fist.Width, fist.Height / frameCount);
            Vector2 origin = frame.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float scale = VisualScale;
            SpriteBatch sb = Main.spriteBatch;

            //残影：位置+旋转双重拖影（旋转的运动签名），默认预乘批 A=0 即加色
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = i / (float)Projectile.oldPos.Length;
                Color ghost = Color.Lerp(new Color(255, 150, 50, 0), new Color(120, 40, 10, 0), t) * (0.4f * (1f - t));
                sb.Draw(fist, oldCenter, frame, ghost, Projectile.oldRot[i], origin, scale * (1f - t * 0.2f), SpriteEffects.None, 0f);
            }
            //拳头辉光底衬
            Texture2D soft = CWRAsset.SoftGlow.Value;
            sb.Draw(soft, drawPos, null, new Color(255, 140, 40, 0) * 0.75f,
                0f, soft.Size() / 2f, 1.5f * scale, SpriteEffects.None, 0f);

            //推进器尾焰 + 出膛闪（Additive Immediate，noise 即 quad 本体，合同同 GolemRenderHelper.DrawFistThruster）
            DrawThrusterAndMuzzle(sb);

            //不透明拳体核心（实体感锚点：原版石拳贴图，熔火过热调色）
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sb.Draw(fist, drawPos, frame, new Color(255, 225, 205), Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

            //岩浆脉络覆盖（GolemMagmaVein VeinTech，贴体采拳贴图）
            Effect vein = EffectLoader.GolemMagmaVein?.Value;
            if (vein != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                vein.CurrentTechnique = vein.Techniques["VeinTech"];
                vein.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                vein.Parameters["uGlow"]?.SetValue(1f);
                vein.Parameters["uCrumble"]?.SetValue(0f);
                vein.Parameters["uFrame"]?.SetValue(new Vector4(
                    frame.X / (float)fist.Width, frame.Y / (float)fist.Height,
                    frame.Width / (float)fist.Width, frame.Height / (float)fist.Height));
                //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成拳贴图
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                vein.CurrentTechnique.Passes[0].Apply();
                sb.Draw(fist, drawPos, frame, Color.White, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>尾焰与出膛闪：共用一段 Additive Immediate 批</summary>
        private void DrawThrusterAndMuzzle(SpriteBatch sb) {
            Effect thruster = EffectLoader.GolemThruster?.Value;
            Effect flare = EffectLoader.GolemSolarFlare?.Value;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float speed = Projectile.velocity.Length();
            float power = MathHelper.Clamp(speed / MaxSpeed, 0.35f, 1f);
            float len = (46f + speed * 4.2f) * VisualScale;
            float width = 34f * VisualScale;
            Vector2 nozzle = Projectile.Center - dir * 20f * VisualScale - Main.screenPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (thruster != null) {
                Texture2D noise = CWRAsset.PerlinNoise.Value;
                thruster.CurrentTechnique = thruster.Techniques["FlameTech"];
                thruster.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                thruster.Parameters["uPower"]?.SetValue(power);
                thruster.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.37f);
                thruster.Parameters["uAspect"]?.SetValue(len / Math.Max(width, 1f));
                thruster.CurrentTechnique.Passes[0].Apply();
                //quad 本体即噪声贴图（刻意 s0，LinearWrap 批；origin 左端中点，+X 即喷向）
                sb.Draw(noise, nozzle, null, Color.White, (-dir).ToRotation(),
                    new Vector2(0f, noise.Height / 2f),
                    new Vector2(len / noise.Width, width / noise.Height), SpriteEffects.None, 0f);
            }

            //出膛闪：发射点残留的辐条光束，快速熄灭
            int muzzleWindow = 12;
            if (flare != null && updatesAlive < muzzleWindow && muzzleDir != Vector2.Zero) {
                float mt = 1f - updatesAlive / (float)muzzleWindow;
                Texture2D quad = VaultAsset.placeholder2.Value;
                flare.CurrentTechnique = flare.Techniques["BeamTech"];
                flare.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                flare.Parameters["uProgress"]?.SetValue(mt);
                flare.Parameters["uIntensity"]?.SetValue(mt);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                Vector2 mPos = muzzlePos - Main.screenPosition;
                float beamLen = 190f * mt + 60f;
                for (int i = -1; i <= 1; i += 2) {
                    flare.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(quad, mPos, null, Color.White, muzzleDir.ToRotation() + i * 0.24f * mt,
                        new Vector2(0f, quad.Height / 2f),
                        new Vector2(beamLen / quad.Width, 30f / quad.Height), SpriteEffects.None, 0f);
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// 日耀新星：重拳主目标命中处的太阳爆发（GolemSolarFlare CoreTech），
    /// 展开期圆判定一次性结算（基础 260px，原地引拳 +30%），余辉带余烬。
    /// 首帧各端本地推白闪/震屏（带距离门）。ai[0]=引拳标记
    /// </summary>
    internal class SolarCoreNova : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int TotalFrames = 40;
        private const int ExpandEnd = 14;
        private const float BaseRadius = 260f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;
        /// <summary>沉腰蓄劲奖励：引拳新星半径 +30%</summary>
        private float MaxRadius => Projectile.ai[0] >= 1f ? BaseRadius * 1.3f : BaseRadius;
        private float Radius => MaxRadius * VaultUtils.EaseOutCubic(MathHelper.Clamp(Elapsed / (float)ExpandEnd, 0f, 1f));

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            if (Elapsed == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f, Volume = 1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with { Pitch = -0.15f, Volume = 0.9f }, Projectile.Center);
                //白闪/震屏各端自播，距离门防跨屏打扰（复用石巨人全屏后效通道）
                if (Main.LocalPlayer.Distance(Projectile.Center) < 1300f) {
                    GolemScreenEffects.PushSunFlash(Projectile.Center, 0.5f, 24);
                    GolemScreenEffects.PushShockRing(Projectile.Center, 0.85f, 430f, 26);
                    GolemScreenEffects.Shake(6.5f);
                }
                for (int i = 0; i < 22; i++) {
                    float angle = MathHelper.TwoPi * i / 22f;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, angle.ToRotationVector2() * Main.rand.NextFloat(6f, 13f),
                        Color.Lerp(new Color(255, 200, 90), new Color(255, 120, 30), Main.rand.NextFloat()),
                        Main.rand.NextFloat(1.2f, 2f)).Configure(true, Main.rand.Next(18, 30), null);
                }
            }

            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.7f, 0.3f) * (Radius / MaxRadius) * 1.4f);

            //余辉余烬
            if (!VaultUtils.isServer && Elapsed >= ExpandEnd && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_DawnEmber>(
                    Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.7f, Radius * 0.6f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.6f, -0.4f)),
                    new Color(255, 180, 80), Main.rand.NextFloat(0.7f, 1.2f)).Configure(Main.rand.Next(24, 40));
            }
        }

        public override bool? CanDamage() => Elapsed < ExpandEnd ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = Radius + 20f;
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(nearest, Projectile.Center) <= r * r;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Daybreak, 300);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Elapsed > ExpandEnd
                ? MathHelper.Clamp(1f - (Elapsed - ExpandEnd) / (float)(TotalFrames - ExpandEnd), 0f, 1f)
                : 1f;
            Effect shader = EffectLoader.GolemSolarFlare?.Value;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (shader != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                Texture2D quad = VaultAsset.placeholder2.Value;
                shader.CurrentTechnique = shader.Techniques["CoreTech"];
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uProgress"]?.SetValue(1f);
                shader.Parameters["uIntensity"]?.SetValue(fade);
                shader.CurrentTechnique.Passes[0].Apply();
                //CoreTech 日盘半径占画布 0.42：全尺寸=判定半径/0.42
                float size = Radius / 0.42f;
                sb.Draw(quad, drawPos, null, Color.White, 0f, quad.Size() / 2f,
                    new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                //无着色器兜底：双层辉光
                Texture2D soft = CWRAsset.SoftGlow.Value;
                sb.Draw(soft, drawPos, null, new Color(255, 140, 40, 0) * (0.9f * fade),
                    0f, soft.Size() / 2f, Radius / 22f, SpriteEffects.None, 0f);
                sb.Draw(soft, drawPos, null, new Color(255, 230, 160, 0) * fade,
                    0f, soft.Size() / 2f, Radius / 36f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>
    /// 熔岩链爆：新星向两侧的行进爆发，贴地喷发。
    /// ai[0]=起爆延迟帧，ai[1]=链序（音高错拍）。延迟期地面熔光预兆，
    /// 起爆窗 120px 圆判定，火舌+余烬+暗烟收尾
    /// </summary>
    internal class SolarChainBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int EruptWindow = 10;
        private const int AfterGlow = 24;
        private const float BurstRadius = 120f;

        private int Delay => (int)Projectile.ai[0];
        private int elapsed;
        private bool erupted;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            elapsed++;

            if (elapsed < Delay) {
                //预兆：地缝熔光聚拢
                Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.32f, 0.08f) * (elapsed / (float)Math.Max(Delay, 1)));
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), Main.rand.NextFloat(-4f, 8f)),
                        DustID.SolarFlare, new Vector2(0f, Main.rand.NextFloat(-2.4f, -0.8f)), 0, default, 1.1f);
                    dust.noGravity = true;
                }
                return;
            }

            if (!erupted) {
                erupted = true;
                Erupt();
            }

            float glowT = 1f - MathHelper.Clamp((elapsed - Delay - EruptWindow) / (float)AfterGlow, 0f, 1f);
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.6f, 0.2f) * glowT);

            if (elapsed > Delay + EruptWindow + AfterGlow) {
                Projectile.Kill();
            }
        }

        /// <summary>起爆拍（各端本地演出）</summary>
        private void Erupt() {
            if (VaultUtils.isServer) {
                return;
            }
            int chainIndex = (int)Projectile.ai[1];
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f + chainIndex * 0.07f, Volume = 0.62f }, Projectile.Center);
            if (Main.LocalPlayer.Distance(Projectile.Center) < 1100f) {
                GolemScreenEffects.Shake(2.2f);
            }

            //熔岩火舌（上冲为主）
            for (int i = 0; i < 4; i++) {
                Vector2 up = new Vector2(Main.rand.NextFloat(-0.35f, 0.35f), -1f).SafeNormalize(-Vector2.UnitY);
                PRTLoader.NewParticle<PRT_DawnTongue>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-46f, 46f), 4f),
                    up * 0.5f, new Color(255, 170, 60), Main.rand.NextFloat(0.8f, 1.25f))
                    .Configure(up, Main.rand.NextFloat(0.8f, 1.3f), Main.rand.Next(14, 22));
            }
            //迸溅熔滴
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-7.5f, -2.5f));
                PRTLoader.NewParticle<PRT_DawnEmber>(Projectile.Center + Main.rand.NextVector2Circular(30f, 10f),
                    vel, new Color(255, 160, 55), Main.rand.NextFloat(0.8f, 1.3f)).Configure(Main.rand.Next(24, 40), 0.012f);
            }
            //烬后暗烟（真 alpha 暗层：Extra_98，黑底图画不出暗形）
            for (int i = 0; i < 3; i++) {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(26f, 8f),
                    DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.4f, -0.6f)),
                    120, new Color(40, 32, 28), 1.6f);
                smoke.noGravity = true;
            }
        }

        public override bool? CanDamage() => erupted && elapsed <= Delay + EruptWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(nearest, Projectile.Center) <= BurstRadius * BurstRadius;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D soft = CWRAsset.SoftGlow.Value;

            if (!erupted) {
                //预兆地光
                float t = elapsed / (float)Math.Max(Delay, 1);
                sb.Draw(soft, drawPos, null, new Color(255, 120, 30, 0) * (0.5f * t),
                    0f, soft.Size() / 2f, new Vector2(1.7f, 0.55f) * (0.6f + 0.5f * t), SpriteEffects.None, 0f);
                return false;
            }

            float burstT = MathHelper.Clamp((elapsed - Delay) / (float)EruptWindow, 0f, 1f);
            float fade = 1f - MathHelper.Clamp((elapsed - Delay - EruptWindow) / (float)AfterGlow, 0f, 1f);

            Effect shader = EffectLoader.GolemSolarFlare?.Value;
            if (shader != null && fade > 0.02f) {
                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;

                Texture2D quad = VaultAsset.placeholder2.Value;
                shader.CurrentTechnique = shader.Techniques["CoreTech"];
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.53f);
                shader.Parameters["uProgress"]?.SetValue(1f);
                shader.Parameters["uIntensity"]?.SetValue(fade * (0.55f + 0.45f * burstT));
                shader.CurrentTechnique.Passes[0].Apply();
                float size = BurstRadius * VaultUtils.EaseOutCubic(burstT) / 0.42f;
                sb.Draw(quad, drawPos, null, Color.White, 0f, quad.Size() / 2f,
                    new Vector2(size / quad.Width, size / quad.Height), SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else if (fade > 0.02f) {
                sb.Draw(soft, drawPos, null, new Color(255, 150, 45, 0) * (0.9f * fade),
                    0f, soft.Size() / 2f, BurstRadius * burstT / 24f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
