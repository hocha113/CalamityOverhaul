using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 大墨滴:普攻的演出主角,一颗有体积的墨,不是粒子雨。
    /// 两段式弹道——伞缘切向甩出横漂至目标上空(追踪藏在这一段),
    /// 顶点被表面张力拉圆滞空一拍,随后近乎垂直加速砸下;
    /// "雨是从上往下落的"这个读感靠垂直坠落段保住。
    /// 集中绘制在 <see cref="KikasaRainRender"/>,本体 PreDraw 不画
    /// </summary>
    internal class KikasaInkDrop : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 弹道参数 ====================

        /// <summary>甩出段最长帧数,超时原地转入顶点</summary>
        private const int FlickMaxFrames = 30;

        /// <summary>坠落加速度与终速:必须是加速曲线,匀速的雨是失败的雨</summary>
        private const float PlungeGravity = 0.95f;
        private const float PlungeMaxSpeed = 26f;

        /// <summary>顶点在目标上方的理想高度</summary>
        private const float ApexAboveTarget = 116f;

        private enum DropPhase : byte
        {
            /// <summary>伞缘甩出,横向漂移到目标上空</summary>
            Flick,
            /// <summary>顶点滞空,速度泄尽、墨被拉圆</summary>
            Apex,
            /// <summary>垂直加速砸下,仅小幅横向修正</summary>
            Plunge
        }

        /// <summary>锁定目标 whoAmI,-1 为无目标(落向光标列)</summary>
        private ref float TargetAi => ref Projectile.ai[0];

        /// <summary>无目标时的坠落列世界 X</summary>
        private ref float FallbackXAi => ref Projectile.ai[1];

        private DropPhase Phase {
            get => (DropPhase)Projectile.ai[2];
            set => Projectile.ai[2] = (float)value;
        }

        //本地表现计时:各端自走,起点由生成包对齐
        private float life;
        private float phaseTimer;
        private float apexY;
        private bool apexSolved;
        //落湖被收走:谢幕换涟漪不走溅斑
        private bool lakeSwallowed;
        //实心命中:AI 的地形检测各端确定性一致,渍斑贴地
        private bool onTileHit;

        /// <summary>确定性相位:绘制与顶点抖动都用它,多端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入,第一帧不硬弹</summary>
        internal float VisualFade => MathHelper.Clamp(life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Projectile.penetrate = 1;
            Projectile.timeLeft = 420;
            //不吃引擎地形碰撞:甩出段允许穿墙(妖伞的墨),坠落段手动检测实心
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //同一波的多滴允许同帧咬中同一目标
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            life++;
            phaseTimer++;

            NPC target = ResolveTarget();
            if (!apexSolved) {
                SolveApex(target);
                //特大墨滴(scale>1)的判定同步放大
                if (Projectile.scale > 1.01f) {
                    int size = (int)(22 * Projectile.scale);
                    Projectile.Resize(size, size);
                }
            }

            switch (Phase) {
                case DropPhase.Flick:
                    UpdateFlick(target);
                    break;
                case DropPhase.Apex:
                    UpdateApex();
                    break;
                case DropPhase.Plunge:
                    UpdatePlunge(target);
                    break;
            }

            //墨条沿运动方向立起;顶点低速时保持竖直待落
            if (Projectile.velocity.Length() > 0.8f) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
            else {
                Projectile.rotation = Utils.AngleLerp(Projectile.rotation, 0f, 0.25f);
            }

            //域内落湖:湖收走自己的墨,涟漪谢幕
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive && Projectile.Center.Y >= kdp.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 0.8f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 5);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
                return;
            }

            //坠落段的实心检测(湖线以上才算,湖下地形被湖面盖着)
            if (Phase == DropPhase.Plunge
                && (!lakeAlive || Projectile.Center.Y < kdp.LakeWorldY - 2f)
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                onTileHit = true;
                Projectile.Kill();
            }
        }

        //==================== 目标与顶点 ====================

        private NPC ResolveTarget() {
            int who = (int)TargetAi;
            if (who < 0 || who >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[who];
            return npc?.active == true && npc.CanBeChasedBy(Projectile) ? npc : null;
        }

        /// <summary>
        /// 首帧解顶点:有目标时压在其头顶上方(天花板向下钳制,保证坠落路径通畅),
        /// 无目标时就在伞高处直接转入待落
        /// </summary>
        private void SolveApex(NPC target) {
            apexSolved = true;
            float jit = Seed / 3.71f;
            if (target != null) {
                float ideal = MathF.Min(Projectile.Center.Y,
                    target.Hitbox.Top - ApexAboveTarget - jit * 44f);
                apexY = CeilingClampApex(target.Top, ideal);
            }
            else {
                apexY = Projectile.Center.Y - 16f - jit * 30f;
            }
        }

        /// <summary>自目标顶部向上逐格探实心,顶点被天花板压回其下沿</summary>
        private static float CeilingClampApex(Vector2 from, float idealY) {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f) - 1;
            int endY = Math.Max((int)(idealY / 16f), 1);
            for (int y = startY; y >= endY; y--) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return y * 16f + 22f;
                }
            }
            return idealY;
        }

        private float ApexX(NPC target)
            => target != null
                ? target.Center.X + target.velocity.X * 9f
                : FallbackXAi;

        //==================== 三段弹道 ====================

        private void UpdateFlick(NPC target) {
            Vector2 apex = new(ApexX(target), apexY);
            Vector2 toApex = apex - Projectile.Center;
            //弹簧式追点:甩出的初速自然衰减进漂移,不是匀速平移
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toApex * 0.085f, 0.15f);
            if (Projectile.velocity.Length() > 18f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 18f;
            }
            if (toApex.LengthSquared() < 26f * 26f || phaseTimer > FlickMaxFrames) {
                Phase = DropPhase.Apex;
                phaseTimer = 0f;
            }
        }

        private void UpdateApex() {
            //表面张力把墨拉圆,速度泄尽
            Projectile.velocity *= 0.68f;
            int dwell = 4 + (int)(Seed / 3.71f * 3f);
            if (phaseTimer >= dwell) {
                Phase = DropPhase.Plunge;
                phaseTimer = 0f;
            }
        }

        private void UpdatePlunge(NPC target) {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + PlungeGravity, PlungeMaxSpeed);
            if (target != null) {
                //只修横向,雨仍是"落"的;修正量小,躲得开
                float dx = target.Center.X + target.velocity.X * 6f - Projectile.Center.X;
                Projectile.velocity.X += MathHelper.Clamp(dx * 0.02f, -0.6f, 0.6f);
                Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X * 0.965f, -8f, 8f);
            }
            else {
                Projectile.velocity.X *= 0.97f;
            }
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            Vector2 impactVel = Projectile.velocity;
            float ke = MathHelper.Clamp(impactVel.Length() / PlungeMaxSpeed, 0.25f, 1f);
            float splatSize = 40f + ke * 42f;

            //渍斑归属:贴地>沾敌>空中散尽。NPC 命中只在所有者端跑 OnHitNPC,
            //这里按死点就近找宿主,各端跑同一套规则,旁观者也看得到渍
            if (onTileHit) {
                Vector2 into = impactVel.SafeNormalize(Vector2.UnitY) * 8f;
                KikasaInkFX.AddGroundSplat(Projectile.Center + into, impactVel, splatSize);
            }
            else {
                NPC host = FindSplatHost();
                if (host != null) {
                    KikasaInkFX.AddNpcSplat(host, Projectile.Center, impactVel, splatSize * 0.8f);
                }
            }

            //迸溅:半球墨珠反弹(贴法线快)+一口墨雾在空气里晕开,预算 ≤6 粒
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float mainAngle = normal.ToRotation();
            int count = (int)(2 + 3 * ke);
            for (int i = 0; i < count; i++) {
                float spread = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float speedRatio = 1f - MathF.Abs(spread) / MathHelper.PiOver2;
                Vector2 vel = (mainAngle + spread).ToRotationVector2()
                    * Main.rand.NextFloat(1.8f, 6.5f) * (0.35f + 0.65f * speedRatio) * (0.5f + ke);
                PRTLoader.NewParticle<PRT_KikasaInkBead>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    vel, Main.rand.NextBool(3) ? KikasaInk.InkDeep : KikasaInk.InkBody,
                    Main.rand.NextFloat(0.4f, 0.75f) * Projectile.scale)?.Configure(Main.rand.Next(18, 30));
            }
            PRTLoader.NewParticle<PRT_KikasaInkMist>(Projectile.Center + normal * 6f,
                normal * Main.rand.NextFloat(0.4f, 1f), KikasaInk.InkDeep,
                Main.rand.NextFloat(0.8f, 1.2f) * Projectile.scale)?.Configure(Main.rand.Next(28, 40));

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 4 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 4 }, Projectile.Center);
        }

        /// <summary>死点附近最近的可沾渍宿主</summary>
        private NPC FindSplatHost() {
            NPC best = null;
            float bestDist = 76f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.dontTakeDamage) {
                    continue;
                }
                Rectangle box = npc.Hitbox;
                box.Inflate(24, 24);
                if (!box.Contains(Projectile.Center.ToPoint())) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist + MathF.Max(npc.width, npc.height) * 0.5f) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        //==================== 绘制(由 KikasaRainRender 集中调用) ====================

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>着色器路径:逐滴参数上载+方形 quad,批与共享参数由渲染层备好</summary>
        internal void DrawInkQuad(SpriteBatch sb, Effect fx, Texture2D canvas) {
            if (VisualFade <= 0.01f) {
                return;
            }
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 1.4f);
            //顶点滞空的张力抖动比飞行时明显
            float wobAmp = Phase == DropPhase.Apex ? 0.17f : 0.07f;
            fx.Parameters["uStretch"]?.SetValue(stretch);
            fx.Parameters["uWobAmp"]?.SetValue(wobAmp);
            fx.Parameters["uWobPhase"]?.SetValue(life * 0.5f + Seed * 6f);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFade"]?.SetValue(VisualFade);
            fx.CurrentTechnique = fx.Techniques["TechDrop"];
            fx.CurrentTechnique.Passes[0].Apply();

            //Projectile.scale 承载特大墨滴(墨瀑散射)
            float side = (58f + stretch * 44f) * Projectile.scale;
            Vector2 scale = new(side / canvas.Width, side / canvas.Height);
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>精灵回退:分层 Extra_98——暗缘给体积、墨体近黑、血芯与 A=0 加色玻头</summary>
        internal void DrawInk(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || VisualFade <= 0.01f) {
                return;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.045f, 0f, 1.4f);
            //顶点滞空的表面张力呼吸
            float wob = MathF.Sin(life * 0.5f + Seed * 6f) * (Phase == DropPhase.Apex ? 0.16f : 0.07f);
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //尾迹残影:旧位上渐淡渐小的墨影
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos != null && stretch > 0.25f) {
                for (int i = 2; i < oldPos.Length; i += 2) {
                    if (oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float ta = 1f - i / (float)oldPos.Length;
                    Vector2 gp = oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    sb.Draw(tex, gp, null, KikasaInk.InkBody * (0.3f * ta * fade), Projectile.rotation,
                        origin, new Vector2(0.2f, 0.3f) * ta, SpriteEffects.None, 0f);
                }
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 bodyScale = new Vector2(0.34f * (1f - stretch * 0.3f), 0.42f * (1f + stretch * 1.7f)) * jiggle;
            //暗血缘略宽一圈
            sb.Draw(tex, pos, null, KikasaInk.InkDeep * (0.9f * fade), Projectile.rotation, origin,
                bodyScale * new Vector2(1.3f, 1.06f), SpriteEffects.None, 0f);
            //墨体
            sb.Draw(tex, pos, null, KikasaInk.InkBody * fade, Projectile.rotation, origin,
                bodyScale, SpriteEffects.None, 0f);
            //血芯:中心透一线暗红
            sb.Draw(tex, pos, null, KikasaInk.BloodCore * (0.55f * fade), Projectile.rotation, origin,
                bodyScale * new Vector2(0.34f, 0.72f), SpriteEffects.None, 0f);
            //湿反光玻头:小面积 A=0 加色
            Color sheen = KikasaInk.WetSheen with { A = 0 };
            sb.Draw(tex, pos, null, sheen * (0.4f * fade), Projectile.rotation, origin,
                bodyScale * new Vector2(0.16f, 0.3f), SpriteEffects.None, 0f);
        }
    }
}
