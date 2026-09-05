using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys.RTerraBlades
{
    /// <summary>
    /// 泰拉之光：泰拉之刃每一斩放出的大型追踪光刃，本体是一柄光化的泰拉之刃虚影尖端朝前飞行<br/>
    /// 直射型出膛即加速；绽放型先减速外散再被追踪一齐拽回目标；全程变速，禁匀速直飞<br/>
    /// 首个命中唤出铸剑之魂：光魂=真圣剑自天而降贯穿，夜魂=真永夜之刃在命中处回旋<br/>
    /// ai[0]=魂 0 夜 1 光；ai[1]=绽放帧数（0 直射）；ai[2]=尺寸倍率
    /// </summary>
    internal class TerraLightProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 120;
        private const int HomingDelay = 6;
        private const float MaxSpeed = 24f;
        private const float TurnRate = 0.11f;
        private const float SeekRange = 1100f;
        private const float BloomDrag = 0.9f;
        private const int TrailLen = 18;

        private float Soul => Projectile.ai[0];
        private int BloomFrames => (int)Projectile.ai[1];
        private float SizeMul => Projectile.ai[2] > 0.01f ? Projectile.ai[2] : 1f;
        private int Age => Lifetime - Projectile.timeLeft;
        private int HomingStart => BloomFrames > 0 ? BloomFrames : HomingDelay;

        private bool soulSpent;
        private Trail trail;

        /// <summary>确定性相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity % 89 * 0.071f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                InitSize();
            }

            int age = Age;
            float speed = Projectile.velocity.Length();
            if (age < BloomFrames) {
                //绽放期减速外散，等追踪把整圈一齐拽回
                Projectile.velocity *= BloomDrag;
            }
            else if (speed < MaxSpeed) {
                //复利加定量续力，越飞越快
                speed = Math.Min(MaxSpeed, speed * 1.03f + 0.4f);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;
            }

            if (age >= HomingStart) {
                NPC target = FindTarget();
                if (target != null) {
                    float aim = (target.Center - Projectile.Center).ToRotation();
                    float next = Projectile.velocity.ToRotation().AngleTowards(aim, TurnRate);
                    Projectile.velocity = next.ToRotationVector2() * Projectile.velocity.Length();
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, RTerraBlade.TerraMain.ToVector3() * 1.1f);

            //沿途甩翠屑，速度越快甩得越勤；魂色屑点缀
            int shedGap = speed > 15f ? 2 : 4;
            if (Projectile.timeLeft % shedGap == 0) {
                bool soulTint = Main.rand.NextBool(4);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f) * SizeMul
                    , -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , soulTint ? RTerraBlade.SoulColor(Soul) : RTerraBlade.TerraBright
                    , Main.rand.NextFloat(0.12f, 0.2f) * SizeMul)?.Configure(Main.rand.Next(10, 16), 0.9f);
            }
        }

        private void InitSize() {
            int size = (int)(40 * SizeMul);
            Projectile.Resize(size, size);
            if (VaultUtils.isServer) {
                return;
            }
            //出膛：一圈魂色脉冲环 + 顺出射向的几粒翠火花
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, RTerraBlade.SoulBright(Soul), 0f)
                ?.Configure(0.03f, 0.3f * SizeMul, 9);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, forward.RotatedByRandom(0.6) * Main.rand.NextFloat(2f, 5f)
                    , RTerraBlade.TerraBright, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 10);
            }
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = SeekRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //贯穿第二段衰减
            Projectile.damage = (int)(Projectile.damage * 0.75f);

            if (!VaultUtils.isServer) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                RTerraBladeHeld.SpawnHitBurst(target.Center, dir, 0.7f, Soul);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = 0.2f }, target.Center);
            }

            //首个命中唤魂（owner 生成，随包全端可见）
            if (soulSpent || Projectile.owner != Main.myPlayer) {
                return;
            }
            soulSpent = true;
            int soulDamage = Math.Max(1, (int)(Projectile.damage / 0.75f));
            if (Soul > 0.5f) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), TerraSoulJudgmentProj.SpawnPointAbove(target), Vector2.Zero
                    , ModContent.ProjectileType<TerraSoulJudgmentProj>(), Math.Max(1, (int)(soulDamage * TerraSoulJudgmentProj.DamageMul))
                    , Projectile.knockBack, Projectile.owner, target.whoAmI, target.Center.Y);
            }
            else {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<TerraSoulSpinProj>(), Math.Max(1, (int)(soulDamage * TerraSoulSpinProj.DamageMul))
                    , Projectile.knockBack * 0.5f, Projectile.owner, target.whoAmI);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //余痕比弹体活得久：沿旧位铺一串翠屑，越靠尾越先熄
            Vector2[] oldPos = Projectile.oldPos;
            for (int i = 0; i < oldPos.Length; i += 2) {
                if (oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float age01 = i / (float)oldPos.Length;
                Vector2 pos = oldPos[i] + Projectile.Size * 0.5f;
                PRTLoader.NewParticle<PRT_Light>(pos + Main.rand.NextVector2Circular(4f, 4f)
                    , Main.rand.NextVector2Circular(0.7f, 0.7f) - Vector2.UnitY * 0.3f
                    , Main.rand.NextBool(3) ? RTerraBlade.SoulColor(Soul) : RTerraBlade.TerraBright
                    , Main.rand.NextFloat(0.12f, 0.2f) * SizeMul * (1f - age01 * 0.5f))
                    ?.Configure((int)MathHelper.Lerp(18f, 8f, age01), 0.9f);
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, dir.RotatedByRandom(1.1f) * Main.rand.NextFloat(2f, 5f)
                    , RTerraBlade.TerraBright, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(10, 16));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, RTerraBlade.SoulBright(Soul), 0f)
                ?.Configure(0.04f, 0.42f * SizeMul, 12);
        }

        //==================== 绘制 ====================

        //彗尾走图元层，本体压在彗尾之上，都不在实体层画
        public override bool PreDraw(ref Color lightColor) => false;

        private float TrailWidth(float t) => MathHelper.Lerp(15f, 2.5f, t) * SizeMul;
        private static Color TrailColor(Vector2 uv) => Color.White;

        /// <summary>出生 4 帧淡入，末 10 帧淡出</summary>
        private float VisualFade => MathHelper.Clamp(Age / 4f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!Projectile.active) {
                return;
            }
            DrawComet();

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawBody(sb);
            sb.End();
        }

        /// <summary>彗尾：TerraBlade.fx TechTrail，翠绿体 + 魂色边 + 白翠芯，尾撕成丝</summary>
        private void DrawComet() {
            Effect fx = TerraBladeFX.Shader;
            Texture2D noise = TerraBladeFX.Noise;
            Vector2[] oldPos = Projectile.oldPos;
            if (fx == null || noise == null || oldPos == null || oldPos.Length < 2) {
                return;
            }
            Vector2[] positions = new Vector2[oldPos.Length + 1];
            positions[0] = Projectile.Center;
            Vector2 half = Projectile.Size * 0.5f;
            for (int i = 0; i < oldPos.Length; i++) {
                positions[i + 1] = oldPos[i] == Vector2.Zero ? positions[i] : oldPos[i] + half;
            }
            trail ??= new Trail(positions, TrailWidth, TrailColor);
            trail.TrailPositions = positions;

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            fx.CurrentTechnique = fx.Techniques["TechTrail"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uFade"]?.SetValue(VisualFade);
            fx.Parameters["uSoul"]?.SetValue(Soul);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            trail.DrawTrail(fx);
            TerraBladeFX.ReleaseNoiseSlot(device);
        }

        /// <summary>本体：光化的泰拉之刃虚影尖端朝前，速度残影 + 翠色镀层 + 魂色薄镀 + 尖端魂色星芒</summary>
        private void DrawBody(SpriteBatch sb) {
            Texture2D tex = TerraBladeFX.ItemTex(ItemID.TerraBlade);
            Vector2 origin = tex.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //贴图尖朝右上，补 45 度让尖端沿速度向
            float rot = Projectile.rotation + MathHelper.PiOver4;
            float scale = 1.55f * SizeMul;
            float fade = VisualFade;
            Color soulCol = RTerraBlade.SoulColor(Soul);
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float speed01 = MathHelper.Clamp(Projectile.velocity.Length() / MaxSpeed, 0.2f, 1f);

            //底光垫：小面积魂色软光
            Texture2D glow = TerraBladeFX.SoftGlow;
            if (glow != null) {
                Color under = soulCol * (0.35f * fade);
                under.A = 0;
                sb.Draw(glow, pos, null, under, 0f, glow.Size() / 2f, 0.85f * SizeMul, SpriteEffects.None, 0f);
            }

            //速度拉伸的残影：两影沿反速度向错开，快时拉得更开
            for (int g = 2; g >= 1; g--) {
                Vector2 gPos = pos - forward * (12f * g * speed01 * SizeMul);
                Color gc = RTerraBlade.TerraBright * ((g == 1 ? 0.38f : 0.18f) * fade);
                gc.A = 0;
                sb.Draw(tex, gPos, null, gc, rot, origin, scale * (1f - 0.06f * g), SpriteEffects.None, 0f);
            }

            //光化刀身自发光，不吃环境光
            Color body = new Color(170, 255, 190) * fade;
            sb.Draw(tex, pos, null, body, rot, origin, scale, SpriteEffects.None, 0f);
            Color sheath = RTerraBlade.TerraBright * (0.45f * fade);
            sheath.A = 0;
            sb.Draw(tex, pos, null, sheath, rot, origin, scale * 1.06f, SpriteEffects.None, 0f);
            Color soulSheath = soulCol * (0.25f * fade);
            soulSheath.A = 0;
            sb.Draw(tex, pos, null, soulSheath, rot, origin, scale * 1.10f, SpriteEffects.None, 0f);

            //尖端星芒：魂色双层反向旋转
            Texture2D star = TerraBladeFX.StarBlack;
            if (star != null) {
                Vector2 tipPos = pos + forward * (tex.Size().Length() * 0.5f * scale * 0.9f);
                float time = (float)Main.timeForVisualEffects * 0.05f + Seed;
                float pulse = 0.85f + 0.15f * MathF.Sin(time * 3.3f);
                Color sc = RTerraBlade.SoulBright(Soul) * (0.7f * pulse * fade);
                sc.A = 0;
                sb.Draw(star, tipPos, null, sc, time * 1.7f, star.Size() / 2f, 0.13f * pulse * SizeMul, SpriteEffects.None, 0f);
                sb.Draw(star, tipPos, null, sc * 0.6f, -time * 1.2f + MathHelper.PiOver4, star.Size() / 2f
                    , 0.08f * pulse * SizeMul, SpriteEffects.None, 0f);
            }
        }
    }
}
