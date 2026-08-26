using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Serpents
{
    /// <summary>
    /// 圣蛇：由化蛇术转化而来的猎杀灵体
    /// 出世上冲，蜿蜒索敌，盘身蓄势后突刺贯穿，寿命将尽时升天化光
    /// ai[0]=蓄力比0~1(体型与威能)
    /// </summary>
    internal class HolySerpent : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float ChargeRatio => ref Projectile.ai[0];

        private enum SerpentState
        {
            Emerge,   //破身而出
            Hunt,     //蜿蜒索敌
            Coil,     //盘身蓄势
            Lunge,    //突刺贯穿
            Recover,  //收势回旋
            Ascend    //升天化光
        }

        private const int LifeTime = 330;
        private const int EmergeTime = 14;
        private const int CoilTime = 9;
        private const int LungeTime = 11;
        private const int RecoverTime = 12;
        private const int AscendTime = 42;
        private const int MaxPathPoints = 34;

        private SerpentState state = SerpentState.Emerge;
        private int stateTimer;
        private int timer;
        private float widthGrow;
        private float ascendFade = 1f;
        private float lungeGlow;
        private int targetIndex = -1;

        //身体路径：首元素为尾，末元素为头
        private readonly List<Vector2> path = [];

        private Player Owner => Main.player[Projectile.owner];
        private float SizeMul => 0.9f + ChargeRatio * 0.35f;
        //相位错开用标识散列(identity 跨端一致)
        private float PhaseSeed => Projectile.identity * 2.399f;
        private float FadeAlpha => widthGrow * ascendFade;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            timer++;
            stateTimer++;
            RecordPath();

            //寿命将尽：进入升天
            if (state != SerpentState.Ascend && Projectile.timeLeft <= AscendTime) {
                state = SerpentState.Ascend;
                stateTimer = 0;
            }

            switch (state) {
                case SerpentState.Emerge:
                    EmergeBehavior();
                    break;
                case SerpentState.Hunt:
                    HuntBehavior();
                    break;
                case SerpentState.Coil:
                    CoilBehavior();
                    break;
                case SerpentState.Lunge:
                    LungeBehavior();
                    break;
                case SerpentState.Recover:
                    RecoverBehavior();
                    break;
                case SerpentState.Ascend:
                    AscendBehavior();
                    break;
            }

            lungeGlow = Math.Max(lungeGlow - 0.05f, 0f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿身金光
            if (!Main.dedServ && path.Count > 4) {
                for (int i = 0; i < path.Count; i += 6) {
                    Lighting.AddLight(path[i], 0.32f * FadeAlpha, 0.28f * FadeAlpha, 0.16f * FadeAlpha);
                }
            }
        }

        #region 行为
        private void EmergeBehavior() {
            widthGrow = VaultUtils.EaseOutCubic(Math.Min(stateTimer / (float)EmergeTime, 1f));
            //出世上冲减速，末段轻微外翻
            Projectile.velocity *= 0.955f;
            Projectile.velocity.Y += 0.06f;

            if (stateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
            }

            if (stateTimer >= EmergeTime) {
                state = SerpentState.Hunt;
                stateTimer = 0;
            }
        }

        private void HuntBehavior() {
            widthGrow = 1f;
            UpdateTarget();

            if (targetIndex >= 0) {
                NPC target = Main.npc[targetIndex];
                Vector2 toTarget = target.Center - Projectile.Center;
                float dist = toTarget.Length();

                //蜿蜒游动：航向摆动，近身时收敛
                float slitherMul = dist < 200f ? 0.4f : 1f;
                float slither = MathF.Sin(timer * 0.28f + PhaseSeed) * 0.045f * slitherMul;
                float desired = toTarget.ToRotation() + slither;
                TurnToward(desired, 0.055f);

                //复利续力加速逼近
                float speed = Projectile.velocity.Length();
                speed = Math.Min(speed * 1.035f + 0.1f, 16.5f);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;

                //近身且有速度：转入盘身蓄势
                if (dist < 150f && speed > 11f) {
                    state = SerpentState.Coil;
                    stateTimer = 0;
                }
            }
            else {
                //无猎物：绕主人巡游
                Vector2 anchor = Owner.Center + new Vector2(
                    MathF.Cos(timer * 0.021f + PhaseSeed) * 260f,
                    MathF.Sin(timer * 0.016f + PhaseSeed * 1.7f) * 150f - 60f);
                Vector2 toAnchor = anchor - Projectile.Center;
                float slither = MathF.Sin(timer * 0.24f + PhaseSeed) * 0.06f;
                TurnToward(toAnchor.ToRotation() + slither, 0.04f);

                float speed = Projectile.velocity.Length();
                float targetSpeed = toAnchor.Length() > 400f ? 13f : 8f;
                speed = MathHelper.Lerp(speed, targetSpeed, 0.05f);
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;
            }
        }

        private void CoilBehavior() {
            //盘身：骤减速并回拉一口气(反向预备)，头锁定猎物
            Projectile.velocity *= stateTimer <= 3 ? 0.72f : 0.86f;

            if (targetIndex >= 0 && Main.npc[targetIndex].active) {
                NPC target = Main.npc[targetIndex];
                Vector2 lead = target.Center + target.velocity * 6f - Projectile.Center;
                TurnToward(lead.ToRotation(), 0.2f);
            }

            if (stateTimer >= CoilTime) {
                //突刺出击
                state = SerpentState.Lunge;
                stateTimer = 0;
                lungeGlow = 1f;

                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                if (targetIndex >= 0 && Main.npc[targetIndex].active) {
                    NPC target = Main.npc[targetIndex];
                    dir = (target.Center + target.velocity * 3f - Projectile.Center).SafeNormalize(dir);
                }
                Projectile.velocity = dir * 27f;
                SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f, Pitch = 0.25f }, Projectile.Center);
            }
        }

        private void LungeBehavior() {
            //直线贯穿，缓慢泄力
            Projectile.velocity *= 0.985f;
            if (stateTimer >= LungeTime) {
                state = SerpentState.Recover;
                stateTimer = 0;
            }
        }

        private void RecoverBehavior() {
            Projectile.velocity *= 0.93f;
            //收势中轻微上扬回旋
            TurnToward(Projectile.velocity.ToRotation() - 0.03f, 0.03f);
            if (stateTimer >= RecoverTime || Projectile.velocity.Length() < 10f) {
                state = SerpentState.Hunt;
                stateTimer = 0;
            }
        }

        private void AscendBehavior() {
            //升天：航向渐转向上，身形化光消散
            ascendFade = Math.Max(0f, 1f - stateTimer / (float)AscendTime);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, new Vector2(0f, -3.4f), 0.06f);

            if (!Main.dedServ && stateTimer % 3 == 0 && path.Count > 2) {
                Vector2 pos = path[Main.rand.Next(path.Count)];
                PRTLoader.NewParticle<PRT_Light>(pos, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.5f, 3.5f))
                    , new Color(255, 236, 190), Main.rand.NextFloat(0.22f, 0.4f))?.Configure(Main.rand.Next(24, 40), 0.9f);
            }
        }

        /// <summary>曲率限幅转向：恒为弧线不折角</summary>
        private void TurnToward(float desired, float maxTurn) {
            float current = Projectile.velocity.ToRotation();
            float diff = MathHelper.WrapAngle(desired - current);
            float turn = MathHelper.Clamp(diff, -maxTurn, maxTurn);
            Projectile.velocity = Projectile.velocity.RotatedBy(turn);
        }

        /// <summary>就近索敌(各端确定性)</summary>
        private void UpdateTarget() {
            if (targetIndex >= 0) {
                NPC current = Main.npc[targetIndex];
                if (!current.active || !current.CanBeChasedBy(Projectile)
                    || Vector2.Distance(current.Center, Projectile.Center) > 1400f) {
                    targetIndex = -1;
                }
            }

            if (targetIndex >= 0) {
                return;
            }

            float closest = 1150f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closest) {
                    closest = dist;
                    targetIndex = i;
                }
            }
        }

        private void RecordPath() {
            if (path.Count == 0 || Vector2.DistanceSquared(path[^1], Projectile.Center) > 16f) {
                path.Add(Projectile.Center);
                if (path.Count > MaxPathPoints) {
                    path.RemoveAt(0);
                }
            }
        }
        #endregion

        #region 判定
        public override bool? CanDamage() => state is SerpentState.Emerge or SerpentState.Ascend ? false : null;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //头部默认碰撞 + 前半段身体线判定
            if (projHitbox.Intersects(targetHitbox)) {
                return true;
            }
            if (path.Count < 4) {
                return false;
            }
            float bodyWidth = 18f * SizeMul;
            int start = path.Count / 2;
            float point = 0f;
            for (int i = start; i < path.Count - 1; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , path[i], path[i + 1], bodyWidth, ref point)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Volume = 0.75f, Pitch = 0.35f }, target.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 6f);
                Color color = Color.Lerp(new Color(255, 216, 130), Color.White, Main.rand.NextFloat(0.2f, 0.55f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center, vel, color, Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(false, Main.rand.Next(12, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            //升天余韵：光尘上飘，寿命长过弹幕本体
            for (int i = 0; i < 10; i++) {
                Vector2 pos = path.Count > 2 ? path[Main.rand.Next(path.Count)] : Projectile.Center;
                Vector2 vel = new(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.2f, 3.2f));
                PRTLoader.NewParticle<PRT_Light>(pos, vel, new Color(255, 240, 200), Main.rand.NextFloat(0.25f, 0.45f))
                    ?.Configure(Main.rand.Next(30, 50), 0.95f);
            }
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>蛇身条带 + 蛇头画布(实体层图元，进来时无活动批次)</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (path.Count < 3 || FadeAlpha < 0.02f) {
                return;
            }

            Effect effect = EffectLoader.SerpentTrail?.Value;
            if (effect == null) {
                return;
            }

            SetSerpentPalette(effect);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(FadeAlpha);
            effect.Parameters["glowIntensity"]?.SetValue(1f + lungeGlow * 0.6f);

            DrawBodyStrip(effect);
            DrawHeadQuad(effect);
        }

        private void DrawBodyStrip(Effect effect) {
            int count = path.Count;
            //弧长参数化，让鳞纹沿身均匀
            float[] cum = new float[count];
            float total = 0f;
            for (int i = 1; i < count; i++) {
                total += Vector2.Distance(path[i - 1], path[i]);
                cum[i] = total;
            }
            if (total < 24f) {
                return;
            }

            float halfWidth = 10.5f * SizeMul;
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = cum[i] / total;
                Vector2 dir = i == 0
                    ? path[1] - path[0]
                    : i == count - 1
                        ? path[i] - path[i - 1]
                        : path[i + 1] - path[i - 1];
                Vector2 normal = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float w = halfWidth * WidthProfile(t) * widthGrow;
                verts[i * 2] = new VertexPositionColorTexture((path[i] + normal * w).ToVector3(), Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((path[i] - normal * w).ToVector3(), Color.White, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = effect.Techniques["SerpentBody"];
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>蛇头走 SpriteBatch 管线(该 technique 无自带顶点着色器)</summary>
        private void DrawHeadQuad(Effect effect) {
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (canvas == null) {
                return;
            }

            float heading = Projectile.velocity.ToRotation();
            Vector2 headPos = Projectile.Center + heading.ToRotationVector2() * 8f - Main.screenPosition;
            float quadSize = 58f * SizeMul * widthGrow;

            effect.CurrentTechnique = effect.Techniques["SerpentHead"];

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, headPos, null, Color.White, heading, canvas.Size() * 0.5f, quadSize, SpriteEffects.None, 0f);
            sb.End();
        }

        private static void SetSerpentPalette(Effect effect) {
            effect.Parameters["holyGold"]?.SetValue(new Vector3(1f, 0.85f, 0.47f));
            effect.Parameters["scaleGreen"]?.SetValue(new Vector3(0.36f, 0.62f, 0.42f));
            effect.Parameters["pureWhite"]?.SetValue(new Vector3(1f, 0.98f, 0.92f));
            effect.Parameters["mysticPurple"]?.SetValue(new Vector3(0.72f, 0.5f, 0.95f));
        }

        private static float WidthProfile(float t) {
            //尾尖细，腹部满宽，颈部收窄(头由头部画布补足)
            float body = MathHelper.SmoothStep(0.14f, 1f, Math.Min(t / 0.42f, 1f));
            float neck = 1f - 0.38f * MathHelper.SmoothStep(0f, 1f, Math.Max((t - 0.72f) / 0.28f, 0f));
            return body * neck;
        }

        /// <summary>加色层辉光：头部光球，突刺时沿速度拉伸</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || FadeAlpha < 0.02f) {
                return;
            }

            Vector2 headPos = Projectile.Center - Main.screenPosition;
            float glowScale = 0.32f * SizeMul * (1f + lungeGlow * 0.5f);
            //加色批源因子是SourceAlpha，染色必须带A
            spriteBatch.Draw(glow, headPos, null, new Color(255, 222, 150) * (0.55f * FadeAlpha), 0f
                , glow.Size() / 2f, glowScale, SpriteEffects.None, 0f);

            if (lungeGlow > 0.05f) {
                Vector2 stretch = new(1.9f, 0.55f);
                spriteBatch.Draw(glow, headPos, null, new Color(255, 240, 190) * (0.45f * lungeGlow * FadeAlpha)
                    , Projectile.rotation, glow.Size() / 2f, glowScale * stretch, SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
