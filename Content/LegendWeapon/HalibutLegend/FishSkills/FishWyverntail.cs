using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishWyverntail : FishSkill
    {
        public override int UnlockFishID => ItemID.Wyverntail;
        public override int DefaultCooldown => 60 * (25 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 35;

        private static bool PlayerHasController(Player player) {
            int type = ModContent.ProjectileType<WhiteWyvernTailController>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].owner == player.whoAmI && Main.projectile[i].type == type) {
                    return true;
                }
            }
            return false;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown > 0) {
                return null;
            }

            if (!PlayerHasController(player)) {
                int proj = Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<WhiteWyvernTailController>(), 0, 0f, player.whoAmI,
                    ai0: damage, ai1: knockback);
                if (proj >= 0) {
                    SpawnSummonEffect(player.Center);
                }
                SetCooldown();
            }
            return null;
        }

        /// <summary>召唤云涡：云絮向心聚拢+收缩环+轻声高吟，materialize 禁 pop-in</summary>
        private static void SpawnSummonEffect(Vector2 position) {
            SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 0.35f, Pitch = 0.4f }, position);
            FishWyverntailVFX.SummonBurst(position);
        }
    }

    internal class WhiteWyvernTailController : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private static int BaseLifeTime => 60 * (10 + HalibutData.GetDomainLayer());
        private ref float BaseDamage => ref Projectile.ai[0];
        private ref float BaseKnockback => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.tileCollide = false;
            Projectile.timeLeft = BaseLifeTime;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || !FishSkill.GetT<FishWyverntail>().Active(Owner)) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Owner.Center + new Vector2(0, -40);
            Timer++;
            int layer = HalibutData.GetDomainLayer(Owner);
            int interval = Math.Clamp(120 - layer * 8, 35, 120);
            int batch = 1 + layer / 4;

            if (Timer % interval == 0) {
                NPC target = Owner.Center.FindClosestNPC(1400f);
                Vector2 firePos = Projectile.Center + Main.rand.NextVector2Circular(24f, 24f);
                Vector2 burstDir = Vector2.UnitY;

                for (int i = 0; i < batch; i++) {
                    Vector2 dir;
                    if (target != null && target.active && target.CanBeChasedBy()) {
                        Vector2 predictive = target.Center + target.velocity * 18f;
                        dir = (predictive - firePos).SafeNormalize(Vector2.UnitY);
                    }
                    else {
                        dir = Main.rand.NextVector2Unit();
                    }
                    if (i == 0) {
                        burstDir = dir;
                    }

                    float speed = Main.rand.NextFloat(12f, 16f) + layer * 0.8f;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), firePos, dir * speed,
                            ModContent.ProjectileType<MiniWhiteWyvern>(),
                            (int)(BaseDamage * (1.6f + layer * 0.4f)),
                            BaseKnockback * 0.35f,
                            Owner.whoAmI,
                            ai0: target?.whoAmI ?? -1);
                        if (proj >= 0) {
                            Main.projectile[proj].scale = 0.9f + layer * 0.03f;
                        }
                    }
                }

                //出膛破云：低吼+锥形云爆
                SoundEngine.PlaySound(SoundID.DD2_OgreRoar with { Volume = 0.6f, Pitch = -0.2f }, firePos);
                FishWyverntailVFX.MuzzleBurst(firePos, burstDir);
            }

            Lighting.AddLight(Projectile.Center, 0.12f, 0.16f, 0.24f);
        }

        public override bool? CanDamage() => false;
        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// 云蛟白龙：龙身为沿轨迹重采样的 TriangleStrip 连续条带（FishWyverntailBody.fx，
    /// 珍珠白靠灰蓝暗部塑形+背脊金鬃），体节游动波从头向尾传播；飞行沿身蜕云絮；
    /// 命中云爆后龙身冻在原地从尾向头化云蚀散（穿透残影）<br/>
    /// ai[0]：≥0 目标索引 / -1 无目标 / -2 命中化散 / -3 自然化散
    /// </summary>
    internal class MiniWhiteWyvern : ModProjectile, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.WyvernHead;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];
        private ref float SerpentinePhase => ref Projectile.ai[2];

        /// <summary>化散演出时长（帧）：残影从尾向头蚀完的窗口</summary>
        private const int DissolveDuration = 18;
        private const int MaxStripPoints = 26;
        private const float StripStep = 16f;//重采样弧长步长，龙长 ≈ 400px

        private NPC target;
        private float desiredRot;
        private float serpAmplitude;
        private float serpFrequency;
        private float swimPhase;//体节游动相位，随速度增速，纯视觉不同步

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = Main.npcFrameCount[NPCID.WyvernHead];
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 50;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;//命中语义由 CanDamage+化散状态门控，引擎不自杀以保留残影
            Projectile.timeLeft = 60 * 8;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => TargetIndex <= -2f ? false : true;

        public override void AI() {
            if (TargetIndex <= -2f) {
                DissolveAI();
                return;
            }

            StateTimer++;
            SerpentinePhase += 0.15f;
            swimPhase += 0.11f + Projectile.velocity.Length() * 0.006f;

            //获取目标
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[(int)TargetIndex].active && Main.npc[(int)TargetIndex].CanBeChasedBy()) {
                target = Main.npc[(int)TargetIndex];
            }
            else {
                target = Projectile.Center.FindClosestNPC(1000f);
                if (target != null) TargetIndex = target.whoAmI;
            }

            //初始化蛇形参数
            if (StateTimer == 1) {
                serpAmplitude = Main.rand.NextFloat(12f, 24f);
                serpFrequency = Main.rand.NextFloat(1.2f, 2f);
            }

            //追踪逻辑
            if (target != null) {
                Vector2 toTarget = target.Center - Projectile.Center;
                float dist = toTarget.Length();
                Vector2 dir = toTarget.SafeNormalize(Vector2.UnitX);

                //蛇形偏移
                Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
                float wave = (float)Math.Sin(SerpentinePhase * serpFrequency) * serpAmplitude *
                    MathHelper.Clamp(dist / 400f, 0.2f, 1f);

                //贯影拍：近身收幅提速，化作直线白影贯穿
                float lunge = 1f;
                float steer = 0.15f;
                if (dist < 200f) {
                    serpAmplitude *= 0.90f;
                    lunge = MathHelper.Lerp(1.45f, 1f, dist / 200f);
                    steer = 0.24f;
                }
                Vector2 desiredVel = dir * MathHelper.Clamp(18f + dist * 0.01f, 12f, 32f) * lunge + normal * wave;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, steer);
                desiredRot = Projectile.velocity.ToRotation();
            }
            else {
                Projectile.velocity *= 0.97f;
                desiredRot = Projectile.velocity.ToRotation();
            }

            Projectile.rotation = desiredRot;

            //帧动画
            int frameCount = Main.projFrames[Projectile.type];
            float animSpeed = MathHelper.Clamp(Projectile.velocity.Length() / 16f, 0.3f, 1.4f);
            if (++Projectile.frameCounter >= 6 / animSpeed) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= frameCount) Projectile.frame = 0;
            }

            //蜕云：龙是云所化，沿身随机骨节掉云屑，速度快蜕得密
            if (!VaultUtils.isServer) {
                int shedOdds = Projectile.velocity.Length() > 24f ? 2 : 4;
                if (Main.rand.NextBool(shedOdds)) {
                    ShedFromBody();
                }
            }

            //自然到期转静默化散
            if (Projectile.timeLeft <= DissolveDuration && Projectile.IsOwnedByLocalPlayer()) {
                TargetIndex = -3f;
                Projectile.netUpdate = true;
            }

            Lighting.AddLight(Projectile.Center, 0.18f, 0.24f, 0.34f);
        }

        /// <summary>化散：急停冻住，视觉一次性触发（远端由 netUpdate 后的 ai 值驱动），尾向头蚀由 shader 承担</summary>
        private void DissolveAI() {
            Projectile.velocity *= 0.82f;
            swimPhase += 0.05f;

            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (TargetIndex == -2f) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
                    FishWyverntailVFX.ImpactBurst(Projectile.Center, Projectile.velocity, Projectile.GetSource_FromThis());
                }
                else {
                    FishWyverntailVFX.QuietDissolve(Projectile.Center);
                }
            }

            //化云头几帧沿身补蜕几片云屑
            if (!VaultUtils.isServer && Projectile.timeLeft > DissolveDuration - 8 && Projectile.timeLeft % 2 == 0) {
                ShedFromBody();
            }

            //帧动画减速残喘
            if (++Projectile.frameCounter >= 10) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            float dissolveT = 1f - Projectile.timeLeft / (float)DissolveDuration;
            Lighting.AddLight(Projectile.Center, 0.18f * (1f - dissolveT), 0.24f * (1f - dissolveT), 0.34f * (1f - dissolveT));
        }

        private void ShedFromBody() {
            int idx = Main.rand.Next(4, 30);
            Vector2 pos;
            if (idx < Projectile.oldPos.Length && Projectile.oldPos[idx] != Vector2.Zero) {
                pos = Projectile.oldPos[idx] + Projectile.Size / 2f;
            }
            else {
                pos = Projectile.Center;
            }
            FishWyverntailVFX.ShedFluff(pos, -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.8f, 0.8f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits > 0 || TargetIndex <= -2f) {
                return;
            }
            Vector2 impactVel = Projectile.velocity;

            //直击一次即范围结算（原语义），随后转入穿透残影化散
            Projectile.Explode(180, default, false);

            Projectile.velocity *= 0.25f;
            TargetIndex = -2f;
            Projectile.timeLeft = DissolveDuration;
            Projectile.netUpdate = true;

            //本端零延迟触发命中演出，远端在 DissolveAI 里各自补触发
            Projectile.localAI[0] = 1f;
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
            FishWyverntailVFX.ImpactBurst(Projectile.Center, impactVel, Projectile.GetSource_FromThis());
        }

        /// <summary>沿 oldPos 链按固定弧长重采样：龙长恒定、密度均匀、出生时自然从头长出</summary>
        private int ResampleBody(Span<Vector2> pts) {
            Vector2 half = Projectile.Size / 2f;
            Vector2 walkPos = Projectile.Center;
            pts[0] = walkPos;
            int n = 1;
            float remaining = StripStep;

            for (int k = 0; k < Projectile.oldPos.Length && n < MaxStripPoints; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    break;
                }
                Vector2 segEnd = Projectile.oldPos[k] + half;
                float segLen = Vector2.Distance(walkPos, segEnd);
                while (segLen >= remaining && n < MaxStripPoints) {
                    walkPos += (segEnd - walkPos).SafeNormalize(Vector2.Zero) * remaining;
                    pts[n++] = walkPos;
                    segLen = Vector2.Distance(walkPos, segEnd);
                    remaining = StripStep;
                }
                remaining -= segLen;
                walkPos = segEnd;
            }
            return n;
        }

        /// <summary>体节游动波：头不摆、波从头向尾传播、身体延迟跟随（幅度沿体渐入）</summary>
        private void ApplySwimWave(Span<Vector2> pts, Span<Vector2> raw, int n) {
            for (int i = 0; i < n; i++) {
                raw[i] = pts[i];
            }
            for (int i = 2; i < n; i++) {
                Vector2 tangent = (raw[i] - raw[i - 1]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float amp = 7f * Projectile.scale * MathHelper.Clamp((i - 1) / 5f, 0f, 1f);
                pts[i] += normal * ((float)Math.Sin(swimPhase - i * 0.62f) * amp);
            }
        }

        /// <summary>颈段快速铺满→向尾幂衰减收尖，体节呼吸微调宽度</summary>
        private float WidthAt(float t, int i) {
            float neck = t < 0.10f
                ? MathHelper.Lerp(0.78f, 1f, t / 0.10f)
                : MathF.Pow(1f - (t - 0.10f) / 0.90f, 0.82f);
            float breath = 1f + 0.09f * (float)Math.Sin(swimPhase * 2f - i * 1.1f);
            return 15f * Projectile.scale * neck * breath;
        }

        /// <summary>龙身条带：珍珠白靠灰蓝暗部塑形，顶点色 r 存该段哪侧朝天供 shader 布光</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ) {
                return;
            }
            Effect fx = FishWyverntailAssets.FishWyverntailBody;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            Span<Vector2> pts = stackalloc Vector2[MaxStripPoints];
            Span<Vector2> raw = stackalloc Vector2[MaxStripPoints];
            int n = ResampleBody(pts);
            if (n < 3) {
                return;
            }
            ApplySwimWave(pts, raw, n);

            var verts = new VertexPositionColorTexture[n * 2];
            for (int i = 0; i < n; i++) {
                float t = i / (float)(n - 1);
                Vector2 tangent = i < n - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float w = WidthAt(t, i);
                //+normal 侧(uv.y=0)朝天权重，屏幕上方向为 -Y
                float upness = MathHelper.Clamp(0.5f - normal.Y * 0.75f, 0f, 1f);
                Color vc = new(upness, 0f, 0f);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * w).ToVector3(), vc, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * w).ToVector3(), vc, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            float dissolveT = TargetIndex <= -2f ? 1f - Projectile.timeLeft / (float)DissolveDuration : 0f;
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.61f % 1f);
            fx.Parameters["uFade"]?.SetValue(MathHelper.Clamp(StateTimer / 12f, 0f, 1f));
            fx.Parameters["uSwimPhase"]?.SetValue(swimPhase);
            fx.Parameters["uDissolve"]?.SetValue(dissolveT);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        /// <summary>头部：条带之上盖 WyvernHead 贴图（夹心遮接缝），急转留旋转拖影，化云时淡出微上飘</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch sb) {
            if (Main.dedServ) {
                return;
            }
            Main.instance.LoadNPC(NPCID.WyvernHead);
            Texture2D tex = TextureAssets.Npc[NPCID.WyvernHead].Value;
            int frameHeight = tex.Height / Main.npcFrameCount[NPCID.WyvernHead];
            Rectangle source = new(0, Projectile.frame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = source.Size() / 2f;
            const float drawOffset = MathHelper.PiOver2;

            float dissolveT = TargetIndex <= -2f ? 1f - Projectile.timeLeft / (float)DissolveDuration : 0f;
            float headAlpha = (1f - dissolveT) * MathHelper.Clamp(StateTimer / 8f, 0f, 1f);
            if (headAlpha <= 0.01f) {
                return;
            }

            //珍珠白保底光：夜里也读得出白龙，仍吃 55% 环境光保体积
            Color tileLight = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color lit = Color.Lerp(tileLight, Color.White, 0.45f) * headAlpha;
            SpriteEffects flip = Projectile.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //急转拖影：角速度大时头部留旋转残影（位置残影表达不了转向）
            if (dissolveT <= 0f && Projectile.oldPos.Length > 6 && Projectile.oldPos[6] != Vector2.Zero) {
                float turn = Math.Abs(MathHelper.WrapAngle(Projectile.oldRot[0] - Projectile.oldRot[6]));
                if (turn > 0.35f) {
                    for (int g = 1; g <= 2; g++) {
                        int gi = g * 3;
                        Vector2 gPos = Projectile.oldPos[gi] + Projectile.Size / 2f - Main.screenPosition;
                        float gRot = Projectile.oldRot[gi] + drawOffset;
                        sb.Draw(tex, gPos, source, lit * (g == 1 ? 0.28f : 0.13f), gRot, origin,
                            Projectile.scale * (1f - g * 0.05f), flip, 0f);
                    }
                }
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            drawPos.Y -= dissolveT * 6f;
            sb.Draw(tex, drawPos, source, lit, Projectile.rotation + drawOffset, origin, Projectile.scale, flip, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
