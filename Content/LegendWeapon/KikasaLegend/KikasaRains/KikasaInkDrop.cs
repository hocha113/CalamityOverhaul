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
    /// 墨雨:一笔会追人的水墨,普攻的演出主角。
    /// 弹道两段，弧段=三次贝塞尔(出手方向定 P1 保切线连续、P2 悬在顶点正上方
    /// 保证末端切线朝下),追踪藏在端点的阻尼平移里,曲线整体缓慢变形,无锐角;
    /// 坠落段=重力加速+曲率限幅转向(只转方向不改速率),轨迹恒为光滑弧线。
    /// 滞空拍是弧段末的速度极小值,不是急停。
    /// 集中绘制在 <see cref="KikasaRainRender"/>,本体 PreDraw 不画
    /// </summary>
    internal class KikasaInkDrop : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 弹道参数 ====================

        /// <summary>坠落加速度与终速:必须是加速曲线,匀速的雨是失败的雨</summary>
        private const float PlungeGravity = 0.95f;
        private const float PlungeMaxSpeed = 26f;

        /// <summary>顶点在目标上方的理想高度</summary>
        private const float ApexAboveTarget = 116f;

        private enum DropPhase : byte
        {
            /// <summary>贝塞尔弧段:甩出→上抛→顶点,末端切线朝下</summary>
            Arc,
            /// <summary>垂直加速砸下,仅曲率限幅微调</summary>
            Plunge
        }

        /// <summary>锁定目标 whoAmI,-1 为无目标(落向光标列)</summary>
        private ref float TargetAi => ref Projectile.ai[0];

        /// <summary>无目标时的坠落列世界 X</summary>
        private ref float FallbackXAi => ref Projectile.ai[1];

        //ai[2] 低位是弹道相位,高位是生成时写死的标记位,随生成包同步
        /// <summary>鬼滴:伞下鬼的侧掷,换鬼青调</summary>
        internal const int FlagGhost = 2;
        /// <summary>湖倾档:落地留墨洼</summary>
        internal const int FlagPuddle = 4;

        private DropPhase Phase {
            get => (DropPhase)((int)Projectile.ai[2] & 1);
            set => Projectile.ai[2] = ((int)Projectile.ai[2] & ~1) | (int)value;
        }

        internal bool IsGhostDrop => ((int)Projectile.ai[2] & FlagGhost) != 0;

        private bool LeavesPuddle => ((int)Projectile.ai[2] & FlagPuddle) != 0;

        //弧段曲线:各端由生成包内容首帧确定性解出
        private bool curveSolved;
        private Vector2 p0, p1, p2, p3;
        private float arcT;
        private float arcDur = 28f;

        //本地表现
        private float life;
        /// <summary>弓身:转向角速度的平滑量,笔触随轨迹弯</summary>
        private float bend;
        private Vector2 prevVel;
        //落湖被收走:谢幕换涟漪不走溅斑
        private bool lakeSwallowed;
        //实心命中:AI 的地形检测各端确定性一致,渍斑贴地
        private bool onTileHit;

        /// <summary>确定性相位:绘制与曲线抖动都用它,多端一致</summary>
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
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 420;
            //不吃引擎地形碰撞:弧段允许穿墙(妖伞的墨),坠落段手动检测实心
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //同一波的多滴允许同帧咬中同一目标
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            life++;

            NPC target = ResolveTarget();
            if (!curveSolved) {
                SolveCurve(target);
                prevVel = Projectile.velocity;
                //特大墨滴(scale>1)的判定同步放大
                if (Projectile.scale > 1.01f) {
                    int size = (int)(22 * Projectile.scale);
                    Projectile.Resize(size, size);
                }
            }

            switch (Phase) {
                case DropPhase.Arc:
                    UpdateArc(target);
                    break;
                case DropPhase.Plunge:
                    UpdatePlunge(target);
                    break;
            }

            Vector2 normal = Projectile.velocity.UnitVector();
            PRTLoader.NewParticle<PRT_KikasaInkMist>(Projectile.Center + normal * 6f,
                normal * Main.rand.NextFloat(0.4f, 1f), KikasaInk.InkDeep,
                Main.rand.NextFloat(0.6f, 0.8f) * Projectile.scale)?.Configure(Main.rand.Next(18, 26));

            //弓身量:速度方向的角变化率,平滑后交给笔触
            if (prevVel.LengthSquared() > 0.01f && Projectile.velocity.LengthSquared() > 0.01f) {
                float dAng = MathHelper.WrapAngle(
                    Projectile.velocity.ToRotation() - prevVel.ToRotation());
                bend = MathHelper.Lerp(bend, MathHelper.Clamp(dAng * 10f, -1f, 1f), 0.25f);
            }
            prevVel = Projectile.velocity;

            //墨条沿运动方向立起;近停时保持竖直待落
            if (Projectile.velocity.Length() > 0.8f) {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
            else {
                Projectile.rotation = Utils.AngleLerp(Projectile.rotation, 0f, 0.25f);
            }

            //域内落湖:湖收走自己的墨,涟漪+墨晕谢幕
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive && Projectile.Center.Y >= kdp.LakeWorldY + 4f) {
                lakeSwallowed = true;
                float ke = MathHelper.Clamp(Projectile.velocity.Length() / PlungeMaxSpeed, 0.25f, 1f);
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 0.8f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 5);
                }
                //墨入水:水面晕开一片墨膜
                KikasaInkFX.AddLakeBlot(Projectile.owner, Projectile.Center.X,
                    (36f + ke * 30f) * Projectile.scale);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.42f, -0.25f, 4);
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

        //==================== 曲线求解 ====================

        private NPC ResolveTarget() {
            int who = (int)TargetAi;
            if (who < 0 || who >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[who];
            return npc?.active == true && npc.CanBeChasedBy(Projectile) ? npc : null;
        }

        /// <summary>
        /// 首帧解弧线:P1 沿出手方向(切线连续),P3 压在目标头顶上方
        /// (天花板向下钳制),P2 悬在 P3 正上方，末端切线天然朝下,
        /// 弧段飞完直接切入坠落,交接处无折角
        /// </summary>
        private void SolveCurve(NPC target) {
            curveSolved = true;
            float jit = Seed / 3.71f;
            Vector2 flickDir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
            p0 = Projectile.Center;

            float apexX = target != null
                ? target.Center.X + target.velocity.X * 9f
                : FallbackXAi;
            float idealY = target != null
                ? MathF.Min(p0.Y, target.Hitbox.Top - ApexAboveTarget - jit * 44f)
                : p0.Y - 16f - jit * 30f;
            float apexY = target != null ? CeilingClampApex(target.Top, idealY) : idealY;

            p3 = new Vector2(apexX, apexY);
            p1 = p0 + flickDir * (54f + jit * 50f);
            p2 = p3 - new Vector2(0f, 62f + jit * 40f);
            arcDur = 26f + jit * 8f;
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

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t) {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        //==================== 两段弹道 ====================

        private void UpdateArc(NPC target) {
            //追踪柔化:端点向目标阻尼平移,曲线整体缓慢变形,不打折
            if (target != null) {
                float tx = target.Center.X + target.velocity.X * 6f;
                p3.X = MathHelper.Lerp(p3.X, tx, 0.06f);
                p2.X = MathHelper.Lerp(p2.X, tx, 0.05f);
            }
            arcT += 1f / arcDur;
            float t = MathHelper.Clamp(arcT, 0f, 1f);
            //参数速度:出手快、近顶点慢，滞空拍是速度极小值,保留 0.15 底速不归零
            float eased = 0.15f * t + 0.85f * (1f - (1f - t) * (1f - t));
            Vector2 pos = Bezier(p0, p1, p2, p3, eased);
            //位置差写进 velocity,引擎推进,旋转与拉伸自然继承
            Projectile.velocity = pos - Projectile.Center;

            if (arcT >= 1f) {
                Phase = DropPhase.Plunge;
                //交接:沿末端切线续走,横向残速轻收,不砍速度
                Projectile.velocity = new Vector2(Projectile.velocity.X * 0.6f,
                    MathF.Max(Projectile.velocity.Y, 2.2f));
            }
        }

        private void UpdatePlunge(NPC target) {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + PlungeGravity, PlungeMaxSpeed);
            if (target == null) {
                return;
            }
            //曲率限幅转向:只转方向不改速率,转率随速度升高收紧，永远是弧,不是折线
            Vector2 want = target.Center + target.velocity * 6f - Projectile.Center;
            float dAng = MathHelper.WrapAngle(want.ToRotation() - Projectile.velocity.ToRotation());
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / PlungeMaxSpeed, 0f, 1f);
            float maxTurn = MathHelper.Lerp(0.016f, 0.006f, speedT);
            Projectile.velocity = Projectile.velocity.RotatedBy(MathHelper.Clamp(dAng, -maxTurn, maxTurn));
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            Vector2 impactVel = Projectile.velocity;
            float ke = MathHelper.Clamp(impactVel.Length() / PlungeMaxSpeed, 0.25f, 1f);
            float splatSize = 20f + ke * 42f;

            //渍斑归属:贴地>沾敌>空中散尽。NPC 命中只在所有者端跑 OnHitNPC,
            //这里按死点就近找宿主,各端跑同一套规则,旁观者也看得到渍
            NPC host = null;
            if (onTileHit) {
                Vector2 into = impactVel.SafeNormalize(Vector2.UnitY) * 8f;
                KikasaInkFX.AddGroundSplat(Projectile.Center + into, impactVel, splatSize);
                //湖倾档:落点积成一汪滞留的墨洼,持续烫伤踩进来的东西;
                //近处已有同主墨洼则只续命,一波齐掷不铺一地重叠洼
                if (LeavesPuddle && Main.myPlayer == Projectile.owner) {
                    Projectile near = FindNearOwnPuddle(56f);
                    if (near != null) {
                        near.timeLeft = Math.Max(near.timeLeft, KikasaInkPuddle.LifeFrames);
                        near.netUpdate = true;
                    }
                    else {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                            Projectile.Center, Vector2.Zero,
                            ModContent.ProjectileType<KikasaInkPuddle>(),
                            (int)(Projectile.damage * 0.35f), 0f, Projectile.owner);
                    }
                }
            }
            else {
                host = FindSplatHost();
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
                    Main.rand.NextFloat(0.14f, 0.22f) * Projectile.scale)?.Configure(Main.rand.Next(18, 30));
            }
            PRTLoader.NewParticle<PRT_KikasaInkMist>(Projectile.Center + normal * 6f,
                normal * Main.rand.NextFloat(0.4f, 1f), KikasaInk.InkDeep,
                Main.rand.NextFloat(0.8f, 1.2f) * Projectile.scale)?.Configure(Main.rand.Next(28, 40));

            KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.42f + 0.22f * ke, -0.35f, 5);
            if (host != null) {
                KikasaInk.Play(SoundID.NPCHit13, Projectile.Center, 0.32f + 0.12f * ke, -0.45f, 4);
            }
        }

        /// <summary>死点附近同主的既有墨洼,用于合并续命</summary>
        private Projectile FindNearOwnPuddle(float radius) {
            int puddleType = ModContent.ProjectileType<KikasaInkPuddle>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Projectile.owner && proj.type == puddleType
                    && Vector2.Distance(proj.Center, Projectile.Center) < radius) {
                    return proj;
                }
            }
            return null;
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
            //弧段末尾滞空的张力抖动比飞行时明显
            bool apexDwell = Phase == DropPhase.Arc && arcT > 0.7f;
            float wobAmp = apexDwell ? 0.15f : 0.06f;
            //色板逐滴上载:鬼滴换鬼青,普通滴回填标准色(共享参数会被上一颗鬼滴污染)
            if (IsGhostDrop) {
                fx.Parameters["uColBody"]?.SetValue(KikasaInk.GhostBody.ToVector3());
                fx.Parameters["uColDeep"]?.SetValue(KikasaInk.GhostDeep.ToVector3());
                fx.Parameters["uColCore"]?.SetValue(KikasaInk.GhostCore.ToVector3());
            }
            else {
                fx.Parameters["uColBody"]?.SetValue(KikasaInk.InkBody.ToVector3());
                fx.Parameters["uColDeep"]?.SetValue(KikasaInk.InkDeep.ToVector3());
                fx.Parameters["uColCore"]?.SetValue(KikasaInk.BloodCore.ToVector3());
            }
            fx.Parameters["uStretch"]?.SetValue(stretch);
            fx.Parameters["uWobAmp"]?.SetValue(wobAmp);
            fx.Parameters["uWobPhase"]?.SetValue(life * 0.5f + Seed * 6f);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFade"]?.SetValue(VisualFade);
            fx.Parameters["uBend"]?.SetValue(MathHelper.Clamp(bend, -1f, 1f));
            fx.CurrentTechnique = fx.Techniques["TechDrop"];
            fx.CurrentTechnique.Passes[0].Apply();

            //Projectile.scale 承载特大墨滴(墨瀑散射)
            float side = (62f + stretch * 30f) * Projectile.scale;
            Vector2 scale = new(side / canvas.Width, side / canvas.Height);
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>精灵回退:分层 Extra_98：暗缘给体积、墨体近黑、血芯与 A=0 加色玻头</summary>
        internal void DrawInk(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || VisualFade <= 0.01f) {
                return;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.045f, 0f, 1.4f);
            float wob = MathF.Sin(life * 0.5f + Seed * 6f) * 0.08f;
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
                        origin, new Vector2(0.16f, 0.26f) * ta, SpriteEffects.None, 0f);
                }
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 bodyScale = new Vector2(0.24f * (1f - stretch * 0.3f), 0.36f * (1f + stretch * 1.7f)) * jiggle * Projectile.scale;
            Color deep = IsGhostDrop ? KikasaInk.GhostDeep : KikasaInk.InkDeep;
            Color bodyCol = IsGhostDrop ? KikasaInk.GhostBody : KikasaInk.InkBody;
            Color core = IsGhostDrop ? KikasaInk.GhostCore : KikasaInk.BloodCore;
            sb.Draw(tex, pos, null, deep * (0.9f * fade), Projectile.rotation, origin,
                bodyScale * new Vector2(1.3f, 1.06f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, bodyCol * fade, Projectile.rotation, origin,
                bodyScale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, core * (0.4f * fade), Projectile.rotation, origin,
                bodyScale * new Vector2(0.3f, 0.7f), SpriteEffects.None, 0f);
        }
    }
}
