using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaBrain
{
    /// <summary>
    /// 鬼奴克脑的献祭凝块：被心脏挤出轨道的一颗 Creeper 血凝块，
    /// 惊惶的心跳碎片，起步一帧定速、复利续力微转向扑向猎物，
    /// 沿途甩珠、表面高频悸动；到位/贴壁/超时按同一确定性规则起爆，
    /// 范围伤害只开在起爆窗（6 帧），飞行段不结算接触。
    /// ai[0]=目标 NPC 槽位（spawn 参数自带，2.7 安全），起爆各端同规则自算
    /// </summary>
    internal class KikasaBrainClot : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>转向解锁前的直飞帧数：出膛先读直线</summary>
        private const int SteerDelay = 5;

        /// <summary>飞行超时，到点没追上也炸</summary>
        private const int FlightTimeout = 56;

        /// <summary>起爆伤害窗帧数</summary>
        private const int BurstWindow = 6;

        /// <summary>起爆判距</summary>
        private const float BurstRange = 48f;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float Life => ref Projectile.ai[1];

        //本地表现闩：起爆演出只放一次
        private bool detonated;
        private int detonateTick;

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color VeinGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        /// <summary>连续量抖动的确定性相位（9.1：不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 4.13f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 9;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //贴壁即爆改走 AI 内手动检测（只认水线以上的真地形）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = 150;
        }

        /// <summary>伤害只开在起爆窗，与可见的爆发严格对齐；飞行段不结算</summary>
        public override bool? CanDamage()
            => detonated && detonateTick <= BurstWindow ? null : false;

        public override bool? CanCutTiles() => false;

        public override void AI() {
            Life++;

            if (detonated) {
                detonateTick++;
                Projectile.velocity = Vector2.Zero;
                //余韵交给比弹幕活得久的粒子，窗口过后收场
                if (detonateTick >= BurstWindow + 4) {
                    Projectile.Kill();
                }
                return;
            }

            //追踪：直飞解锁后小角度转向 + 复利续力，凝块越追越急
            int targetIdx = (int)TargetIndex;
            NPC target = targetIdx >= 0 && targetIdx < Main.maxNPCs ? Main.npc[targetIdx] : null;
            bool targetValid = target?.active == true && target.CanBeChasedBy(Projectile);
            if (Life > SteerDelay && targetValid) {
                float wantAngle = (target.Center - Projectile.Center).ToRotation();
                float speed = MathF.Min(Projectile.velocity.Length() * 1.035f, 27f);
                Projectile.velocity = Projectile.velocity.ToRotation()
                    .AngleTowards(wantAngle, 0.055f).ToRotationVector2() * speed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            //起爆裁决（确定性规则，各端同拍）：到位或超时
            if (targetValid && Vector2.Distance(Projectile.Center, target.Center) < BurstRange
                || Life >= FlightTimeout) {
                Detonate();
                return;
            }

            //贴壁即爆（机制身份保留）：手动地形检测替代 tileCollide
            //湖线以下的真地形被湖面盖住，撞上去像凭空截停，不算贴壁
            Player owner = Main.player[Projectile.owner];
            bool underLake = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY - 2f;
            if (!underLake && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Detonate();
                return;
            }

            //飞行四相之"飞行"：失稳甩珠 + 高频悸动光在绘制层
            if (!Main.dedServ && (int)Life % 2 == 0) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - dir * Main.rand.NextFloat(8f, 18f),
                    Projectile.velocity * Main.rand.NextFloat(0.15f, 0.35f)
                        + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(Main.rand.Next(14, 24));
            }

            float glow = 0.5f + 0.25f * MathF.Sin(Life * 0.55f + Seed);
            Lighting.AddLight(Projectile.Center, 0.42f * glow, 0.10f * glow, 0.09f * glow);
        }

        /// <summary>起爆：撑大命中窗一帧到位，演出走血新星+半球血珠+扩散环+血雾</summary>
        private void Detonate() {
            if (detonated) {
                return;
            }
            detonated = true;
            detonateTick = 0;
            Projectile.Resize(180, 180);
            Projectile.velocity = Vector2.Zero;

            //爆响两层：闷心跳重拍 + 湿裂
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.55f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit9 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
            if (ViewedOwner) {
                ShakeViewer(2.4f);
            }
            if (Main.dedServ) {
                return;
            }

            //半球血珠扇 + 沉重血团：迸得开，坠得急
            for (int i = 0; i < 18; i++) {
                float ang = MathHelper.TwoPi * i / 18f;
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.6f, 8.2f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), vel,
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.45f, 0.85f))?.Configure(Main.rand.Next(22, 38));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(Projectile.Center,
                    (-Vector2.UnitY).RotatedByRandom(0.9f) * Main.rand.NextFloat(1.8f, 4f),
                    BloodDeep, Main.rand.NextFloat(0.95f, 1.3f))?.Configure(Main.rand.Next(28, 44), 0.42f);
            }
            //扩散环双层：主环大而快，回环小而滞
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.1f)
                ?.Configure(new Vector2(1f, 1f), 0f, 0.5f, 11);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, VeinGlow, 0.06f)
                ?.Configure(new Vector2(1f, 1f), 0f, 0.3f, 16);
            //余韵：比弹幕活得久的血雾
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.2f, 0.6f)),
                    MistBlood * 0.8f, Main.rand.NextFloat(0.7f, 1.05f))
                    ?.Configure(Main.rand.Next(60, 100));
            }
        }

        public override void OnKill(int timeLeft) {
            //异常移除（超时/清场）也留一口血：没爆过就小声散珠
            if (Main.dedServ || detonated) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(1.6f, 1.6f),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(16f, 16f),
                    Main.rand.NextVector2Circular(2.4f, 2.4f) - Vector2.UnitY * 1.2f,
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.Creeper);
            Texture2D tex = TextureAssets.Npc[NPCID.Creeper]?.Value;
            if (tex == null || detonated) {
                //起爆后本体已散作粒子，不再画凝块
                return false;
            }
            Rectangle frame = new(0, 0, tex.Width, tex.Height);
            SpriteBatch sb = Main.spriteBatch;
            float fade = MathHelper.Clamp(Life / 4f, 0f, 1f);

            //拖影：速度门控，追击时才拉出残像
            float speed = Projectile.velocity.Length();
            if (speed > 10f && Projectile.oldPos != null) {
                Vector2 origin = frame.Size() * 0.5f;
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (Projectile.oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(tex, oldCenter - Main.screenPosition, frame,
                        BloodMain * (0.3f * fall * fade), Projectile.oldRot[k],
                        origin, 0.86f - k * 0.02f, SpriteEffects.None, 0f);
                }
            }

            DrawBody(sb, tex, frame, fade);

            //高频悸动光：惊惶的小心跳
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null) {
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                float flutter = 0.3f + 0.22f * MathF.Sin(Life * 0.55f + Seed);
                float r = 18f + 5f * flutter;
                sb.Draw(glowTex, Projectile.Center - Main.screenPosition, null,
                    VeinGlow * (flutter * fade), 0f, glowTex.Size() * 0.5f,
                    new Vector2(r * 2f / glowTex.Width), SpriteEffects.None, 0f);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            return false;
        }

        /// <summary>凝块本体：血湖材质 + 速度拉伸 + 表面张力抖动</summary>
        private void DrawBody(SpriteBatch sb, Texture2D tex, Rectangle frame, float fade) {
            Effect form = EffectLoader.KikasaItemForm?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            bool shaderOk = form != null && noise != null;

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color color;
            if (shaderOk) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                form.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                form.Parameters["uSeed"]?.SetValue(Seed);
                form.Parameters["uForm"]?.SetValue(0.5f + MathF.Sin(Life * 0.3f + Seed) * 0.06f);
                form.Parameters["uDissolve"]?.SetValue(0f);
                form.Parameters["uScanMode"]?.SetValue(0f);
                form.Parameters["uUvRect"]?.SetValue(new Vector4(0f, 0f, 1f, 1f));
                form.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                form.Parameters["uAspect"]?.SetValue(frame.Width / (float)frame.Height);
                form.CurrentTechnique.Passes[0].Apply();
                color = new Color(255, 255, 255, (byte)(fade * 255f));
            }
            else {
                color = Color.Lerp(Color.White, BloodMain, 0.55f) * fade;
            }

            //速度拉伸 + 张力反相抖动：飞着的凝块在晃
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.4f);
            float wob = MathF.Sin(Life * 0.5f + Seed * 5f) * 0.08f;
            Vector2 scale = new Vector2(0.9f - stretch * 0.3f + wob, 0.9f + stretch - wob * 0.7f);
            sb.Draw(tex, Projectile.Center - Main.screenPosition, frame, color,
                Projectile.rotation, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
