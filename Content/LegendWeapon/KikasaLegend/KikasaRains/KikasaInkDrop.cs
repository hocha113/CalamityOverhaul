using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨雨:一笔会追人的水墨,普攻的演出主角。
    /// 弹道两段，抛洒段=真弹道学上抛(重力+微阻力,不追踪),被抛起的水先是水;
    /// 越过顶点切入坠落段,有目标时曲率限幅平滑追击:转向率随追踪渐拧紧、
    /// 真拦截提前量、远距先绕到目标头顶再俯冲,近距按几何可达放开转向并刹车收弯,
    /// 轨迹恒为圆滑弧线,无锐角;无目标时重力坠向光标列。
    /// 追太久、擦身而过或近场绕满一圈半即放弃追踪转坠落,不绕圈。
    /// 血湖是介质不是地板:入水留墨膜与水花后穿入继续飞,水下粘阻+追击变钝,
    /// 死在湖底地形照常留渍。集中绘制在 <see cref="KikasaRainRender"/>,本体 PreDraw 不画
    /// </summary>
    internal class KikasaInkDrop : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 弹道参数 ====================

        /// <summary>抛洒段重力与出顶判速:先演完被抛起来这件事</summary>
        private const float TossGravity = 0.32f;
        private const float TossTipSpeed = 1.5f;

        /// <summary>坠落加速度与终速:必须是加速曲线,匀速的雨是失败的雨</summary>
        private const float PlungeGravity = 0.95f;
        private const float PlungeMaxSpeed = 26f;

        /// <summary>追踪转向率包络(弧度/帧):起手轻、渐拧紧,中远距的弧线语言由它定调</summary>
        private const float HomingTurnStart = 0.035f;
        private const float HomingTurnFull = 0.10f;

        /// <summary>
        /// 近距曲率执照:纯追击要咬中,转弯半径 R=v/ω 不得超过半交战距离,
        /// 包络转向率近距远远不够(拧满也是 260px 半径),目标落进转向圆内就只能绕圈打不中。
        /// 距离进 CloseGate 起平滑放开转向率上限,按几何需求 2v/d 带余量现算,顶到 CloseTurnCap;
        /// 中远距零介入,起手轻的优雅弧线一寸不动
        /// </summary>
        private const float CloseGate = 160f;
        private const float CloseTurnCap = 0.5f;

        /// <summary>追踪加速度系数(乘在坠落加速度上)与放弃追踪的时长护栏</summary>
        private const float HomingAccelMul = 0.55f;
        private const int HomingGiveUpFrames = 90;

        /// <summary>头顶偏置基准:远距先瞄目标上方再俯冲,雨还是从上面来的</summary>
        private const float ApexAboveTarget = 116f;

        private enum DropPhase : byte
        {
            /// <summary>抛洒段:真弹道学上抛,越过顶点交给坠落</summary>
            Toss,
            /// <summary>坠落段:有目标平滑追击,无目标重力坠向光标列</summary>
            Plunge
        }

        /// <summary>锁定目标 whoAmI,-1 为无目标(落向光标列)</summary>
        private ref float TargetAi => ref Projectile.ai[0];

        /// <summary>无目标时的坠落列世界 X</summary>
        private ref float FallbackXAi => ref Projectile.ai[1];

        //ai[2] 低位是弹道相位与标记位,bit3..9 符标签、bit10..23 符载荷
        //(KikasaTalismanHooks 位段口径),全部随生成包同步
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

        /// <summary>符标签(0=无符),ModifyDropSpawn 打上,落点/命中/绘制按此分支</summary>
        internal int TalismanTagId => KikasaTalismanHooks.ReadTagId(Projectile.ai[2]);

        /// <summary>符标签载荷(语义由标签符自定,如霓的色序)</summary>
        internal int TalismanTagPayload => KikasaTalismanHooks.ReadTagPayload(Projectile.ai[2]);

        //弹道参数:各端由生成包内容首帧确定性解出;可被弹道挂钩改写(霄高坠、霎急坠等)
        private bool motionSolved;
        private float tossDur = 14f;
        private float plungeGravity = PlungeGravity;
        private float plungeMaxSpeed = PlungeMaxSpeed;
        private float overheadBias = ApexAboveTarget * 0.5f;

        //追踪状态:端本地,由同步的目标与确定性规则推导,端间近似一致(伤害只在归属端结算)
        private float homingT;
        private float minTargetDist = float.MaxValue;
        private bool homingGaveUp;
        private bool retargeted;
        //擦身黏滞:第一次擦身不放弃,重置护栏再咬一口(反馈七·#42)
        private bool stickyRebitten;
        //近场绕圈保险:累计同向转角,绕满约一圈半判死圈(曲率执照下常规几何到不了这里)
        private float nearWind;
        //坠落段碰撞武装:首次身处空气后才生效,室内上抛扎进天花板的滴穿回房内再落(反馈七·#74)
        private bool plungeArmed;

        /// <summary>
        /// 墨印位:亲手指挥的滴(手动墨雨/墨瀑散射)命中给目标盖墨印。
        /// 生成后由伞/瀑在归属端赋值,命中钩只在归属端跑,端本地即可不需同步(同 penetrate 先例)
        /// </summary>
        internal bool AppliesTag;

        //本地表现
        private float life;
        /// <summary>弓身:转向角速度的平滑量,笔触随轨迹弯</summary>
        private float bend;
        private Vector2 prevVel;
        //身在湖中:穿水态(湖是介质不是地板),粘阻+追踪变钝+拖尾冒泡;
        //入/出水沿各留一次边沿谢幕,各端从同步领域态自算同一答案
        private bool inLake;
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
            if (!motionSolved) {
                SolveMotion();
                prevVel = Projectile.velocity;
                //特大墨滴(scale>1)的判定同步放大
                if (Projectile.scale > 1.01f) {
                    int size = (int)(22 * Projectile.scale);
                    Projectile.Resize(size, size);
                }
            }

            switch (Phase) {
                case DropPhase.Toss:
                    UpdateToss(target);
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

            //域内湖水是介质不是地板(2026-08 改制,原为过线即 Kill):
            //入水沿保留全部触水谢幕(墨膜/水花/入水声),弹体穿入继续飞,水下略慢
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            bool belowLine = lakeAlive && Projectile.Center.Y >= kdp.LakeWorldY + 4f;
            if (!inLake && belowLine) {
                //入水沿:墨在水面晕开一片墨膜,稠血咬住来势
                inLake = true;
                float ke = MathHelper.Clamp(Projectile.velocity.Length() / plungeMaxSpeed, 0.25f, 1f);
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 0.8f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 5);
                }
                KikasaInkFX.AddLakeBlot(Projectile.owner, Projectile.Center.X,
                    (36f + ke * 30f) * Projectile.scale);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.42f, -0.25f, 4);
                Projectile.velocity *= 0.65f;
            }
            else if (inLake && lakeAlive && Projectile.Center.Y < kdp.LakeWorldY - 6f) {
                //出水沿:破水而出,小水花复常速(迟滞带防贴线抖动)
                inLake = false;
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 0.5f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 3);
                }
            }
            else if (inLake && !lakeAlive) {
                //湖收了,水下态静默解除
                inLake = false;
            }

            if (inLake) {
                //水下粘阻:每帧轻咬,追击终速约降到六成五;拖尾冒细泡
                Projectile.velocity *= 0.97f;
                if ((int)life % 4 == 0 && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_KikasaLakeBubble>(
                        Projectile.Center - Projectile.velocity.UnitVector() * 8f
                            + Main.rand.NextVector2Circular(4f, 4f),
                        new Vector2(0f, -0.25f), default,
                        Main.rand.NextFloat(0.35f, 0.6f) * Projectile.scale)
                        ?.Configure(Main.rand.Next(30, 56), kdp.LakeWorldY);
                }
            }

            //坠落段的实心检测。碰撞在坠落段首次身处空气后才武装:室内上抛扎进天花板的滴
            //先穿回房内再正常落地,不死在顶上(反馈七·#74);
            //穿水改制后湖下地形照常接墨,滴能死在湖底,渍斑与墨洼一并落底
            if (Phase == DropPhase.Plunge) {
                bool insideSolid = Collision.SolidCollision(
                    Projectile.position, Projectile.width, Projectile.height);
                if (!plungeArmed) {
                    plungeArmed = !insideSolid;
                }
                else if (insideSolid) {
                    onTileHit = true;
                    Projectile.Kill();
                }
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
        /// 首帧解弹道:抛洒段时长/上抛力度/坠落参数一次定死,后续逐帧确定性推进。
        /// 唤雨符弹道挂钩在默认参数备齐后叠改(各端同参,实现须确定性)。
        /// 口径重映射:ArcDur→抛洒段时长(霎符减半=抢先入坠照旧),
        /// ApexAboveTarget→上抛力度与头顶偏置(霄符高抛狠坠照旧),
        /// PlungeGravity/PlungeMaxSpeed→追踪加速度与极速,兼无目标坠落(雹符坠更沉照旧)
        /// </summary>
        private void SolveMotion() {
            motionSolved = true;
            float jit = Seed / 3.71f;

            KikasaDropCurve curve = new() {
                ApexAboveTarget = ApexAboveTarget,
                PlungeGravity = PlungeGravity,
                PlungeMaxSpeed = PlungeMaxSpeed,
                ArcDur = 26f + jit * 8f,
            };
            KikasaTalismanHookRunner hooks = KikasaTalismanHooks.ForOwner(Projectile.owner);
            if (!hooks.IsEmpty) {
                hooks.ModifyDropCurve(Projectile, ref curve);
            }
            plungeGravity = MathF.Max(curve.PlungeGravity, 0.05f);
            plungeMaxSpeed = MathF.Max(curve.PlungeMaxSpeed, 4f);

            //抛洒段时长:弧段时长口径折半沿用
            tossDur = MathHelper.Clamp(curve.ArcDur * 0.5f, 3f, 26f);
            //上抛力度:顶点高度口径折成初速倍率(抛体高度∝v²,开方保比例)
            float apexRatio = MathF.Max(curve.ApexAboveTarget / ApexAboveTarget, 0.1f);
            if (Projectile.velocity.Y < 0f) {
                Projectile.velocity.Y *= MathF.Sqrt(apexRatio);
            }
            //洞穴天花板压制:探头顶实心,把上抛初速钳到撞不进顶板
            //(抛洒段不吃地形碰撞,坠落段吃;不钳的话相位一切换就死在天花板里)
            if (Projectile.velocity.Y < 0f) {
                float rise = Projectile.velocity.Y * Projectile.velocity.Y / (2f * TossGravity);
                float clearance = CeilingClearance(Projectile.Center, rise + 24f);
                if (clearance < rise) {
                    Projectile.velocity.Y = -MathF.Sqrt(MathF.Max(
                        2f * TossGravity * MathF.Max(clearance - 10f, 8f), 4f));
                }
            }
            //头顶偏置:远距先绕到目标上方的高度,同吃霄符的顶点口径
            overheadBias = MathHelper.Clamp(curve.ApexAboveTarget * 0.5f, 36f, 220f);
        }

        /// <summary>自滴位向上逐格探实心,返回到顶板的净空(px);探满 maxRise 未见实心即返回 maxRise</summary>
        private static float CeilingClearance(Vector2 from, float maxRise) {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f) - 1;
            int steps = (int)(maxRise / 16f) + 1;
            for (int i = 0; i < steps; i++) {
                int y = startY - i;
                if (y < 1) {
                    break;
                }
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return MathF.Max(from.Y - (y * 16f + 16f), 0f);
                }
            }
            return maxRise;
        }

        //==================== 两段弹道 ====================

        /// <summary>抛洒段:真被抛起的水,重力+微阻力,不追踪</summary>
        private void UpdateToss(NPC target) {
            Projectile.velocity.Y += TossGravity;
            Projectile.velocity *= 0.995f;

            bool tipped = Projectile.velocity.Y > TossTipSpeed;
            bool timeUp = life >= tossDur;
            //目标在上方时提早收抛:追飞行目标不必演完整段上抛
            bool targetAbove = target != null && target.Center.Y < Projectile.Center.Y - 60f
                && life >= MathF.Min(6f, tossDur);
            if (tipped || timeUp || targetAbove) {
                Phase = DropPhase.Plunge;
            }
        }

        /// <summary>坠落段:有目标平滑追击,无目标(或放弃后)重力坠向光标列</summary>
        private void UpdatePlunge(NPC target) {
            if (target == null && !retargeted && !homingGaveUp) {
                //目标中途失效:就近重锁一次(各端同规则确定性推导)
                retargeted = true;
                target = FindRetarget();
                if (target != null) {
                    TargetAi = target.whoAmI;
                    //换锁即清护栏:旧目标的擦身记录不能拿来判新目标,追踪时长也放宽一截
                    minTargetDist = float.MaxValue;
                    homingT = MathF.Min(homingT, 30f);
                    nearWind = 0f;
                }
            }
            if (target == null || homingGaveUp) {
                UpdateGravityFall();
                return;
            }

            homingT++;
            float dist = Vector2.Distance(target.Center, Projectile.Center);
            minTargetDist = MathF.Min(minTargetDist, dist);

            //防绕圈护栏:追太久,或已擦身而过且渐行渐远,放弃追踪转坠落
            bool flyby = minTargetDist < 100f && dist > minTargetDist + 140f;
            if (homingT > HomingGiveUpFrames || flyby) {
                if (flyby && !stickyRebitten && homingT <= HomingGiveUpFrames) {
                    //第一次擦身不放弃:护栏清零、转向包络直接拉满,圆滑回折再咬一口(反馈七·#42)
                    stickyRebitten = true;
                    minTargetDist = float.MaxValue;
                    homingT = MathF.Max(homingT, 20f);
                    nearWind = 0f;
                }
                else {
                    homingGaveUp = true;
                    UpdateGravityFall();
                    return;
                }
            }

            float speed = Projectile.velocity.Length();
            //真拦截提前量:远打提前近打身
            float lead = MathHelper.Clamp(dist / MathF.Max(speed, 8f), 0f, 14f);
            Vector2 aim = target.Center + target.velocity * lead;
            //远距头顶偏置:先绕到目标上方再俯冲,雨从上落的语言不丢
            if (dist > 180f) {
                float bias = MathHelper.Clamp((dist - 180f) / 260f, 0f, 1f);
                aim.Y -= overheadBias * bias;
            }

            //曲率限幅转向:转向率随追踪渐拧紧,中远距轨迹恒为圆滑弧线
            float dAng = MathHelper.WrapAngle((aim - Projectile.Center).ToRotation()
                - Projectile.velocity.ToRotation());
            float ramp = MathHelper.Clamp(homingT / 20f, 0f, 1f);
            float maxTurn = MathHelper.Lerp(HomingTurnStart, HomingTurnFull, ramp);
            //近距曲率执照:按几何可达现算最低转向率,平滑混入且无视起手渐进——
            //这段路短到没资格演起手轻,不果决就是绕着目标转圈
            float closeIn = MathHelper.Clamp((CloseGate - dist) / 70f, 0f, 1f);
            if (closeIn > 0f) {
                float needTurn = MathF.Min(2.2f * speed / MathF.Max(dist, 32f), CloseTurnCap);
                maxTurn = MathF.Max(maxTurn, needTurn * closeIn);
            }
            if (inLake) {
                //稠血里拧不动那么急,水下追击弧更钝
                maxTurn *= 0.8f;
            }
            float applied = MathHelper.Clamp(dAng, -maxTurn, maxTurn);
            Projectile.velocity = Projectile.velocity.RotatedBy(applied);

            //近场绕圈保险:同向累计转过约一圈半还没咬中即判死圈,放弃追踪转坠落
            //(执照生效后常规几何到不了这里,留给瞬移/急折目标的病理场景)
            if (dist < 240f) {
                nearWind += applied;
                if (MathF.Abs(nearWind) > 8.5f) {
                    homingGaveUp = true;
                }
            }

            //速度包络:大角度贴近时刹车收弯(转弯半径随速度同缩,越贴身刹得越狠),
            //其余时候平滑加速俯冲
            if (MathF.Abs(dAng) > 1.2f && dist < 140f) {
                speed = MathF.Max(speed * MathHelper.Lerp(0.96f, 0.9f, closeIn), 10f);
            }
            else {
                speed = MathF.Min(speed + plungeGravity * HomingAccelMul, plungeMaxSpeed);
            }
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * speed;
        }

        /// <summary>无主的雨:重力加速坠落,横速轻收,微弱寻列让雨幕仍落在光标列附近</summary>
        private void UpdateGravityFall() {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + plungeGravity, plungeMaxSpeed);
            float drift = MathHelper.Clamp((FallbackXAi - Projectile.Center.X) * 0.0022f, -0.14f, 0.14f);
            Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X * 0.985f + drift, -7f, 7f);
        }

        /// <summary>就近重锁:目标中途失效时在滴周围找下一个可追击者</summary>
        private NPC FindRetarget() {
            NPC best = null;
            float bestDist = 480f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
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

        //==================== 命中挂钩 ====================

        //引擎保证命中钩只在归属端跑,不需再设 owner 门

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退方向按"玩家→敌人"定,不按滴子落点定:贴脸时墨滴常追到敌人身后,
            //原生按弹体相对位置算会把敌人怼向玩家(反馈七·#6)
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true) {
                modifiers.HitDirectionOverride = target.Center.X >= owner.Center.X ? 1 : -1;
            }
            KikasaTalismanHooks.ForOwner(Projectile.owner)
                .ModifyRainHitNPC(Projectile, KikasaRainSourceKind.Drop, target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //墨印:亲手指挥的滴命中即盖印,AddBuff 骑原版 buff 同步;结算在役从平衡口径
            if (AppliesTag) {
                target.AddBuff(ModContent.BuffType<KikasaInkTag>(), KikasaInkTag.TagFrames);
            }
            KikasaTalismanHooks.ForOwner(Projectile.owner)
                .OnRainHitNPC(Projectile, KikasaRainSourceKind.Drop, target, in hit, damageDone);
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //落点挂钩(霰碎珠/霏雾团/霜碎镜等):非服务器各端派发,生成物由实现自设 owner 门
            KikasaTalismanHooks.ForOwner(Projectile.owner).OnDropKill(Projectile, onTileHit);

            Vector2 impactVel = Projectile.velocity;
            float ke = MathHelper.Clamp(impactVel.Length() / plungeMaxSpeed, 0.25f, 1f);
            float splatSize = 20f + ke * 42f;

            //渍斑归属:贴地>沾敌>空中散尽。NPC 命中只在所有者端跑 OnHitNPC,
            //这里按死点就近找宿主,各端跑同一套规则,旁观者也看得到渍
            NPC host = null;
            if (onTileHit) {
                Vector2 into = impactVel.SafeNormalize(Vector2.UnitY) * 8f;
                KikasaInkFX.AddGroundSplat(Projectile.Center + into, impactVel, splatSize);
                //湖倾档:落点积成一汪滞留的墨洼,持续烫伤踩进来的东西;
                //近处已有同主墨洼则只续命,一波齐掷不铺一地重叠洼;
                //潦符的寿命/半径倍率随生成参数带给洼(ai 通道随生成包同步)
                if (LeavesPuddle && Main.myPlayer == Projectile.owner) {
                    SpawnOrRefreshPuddleAt(Projectile.Center);
                }
            }
            else {
                host = FindSplatHost();
                if (host != null) {
                    KikasaInkFX.AddNpcSplat(host, Projectile.Center, impactVel, splatSize * 0.8f);
                    //湖倾档也认打在敌人身上:洼起在宿主脚下的地形（平台也算），
                    //追踪流打空中怪同样吃得到墨洼构筑线（反馈七·#36 拍板）
                    if (LeavesPuddle && Main.myPlayer == Projectile.owner
                        && TryFindGroundBelow(host.Bottom, out Vector2 groundAt)) {
                        SpawnOrRefreshPuddleAt(groundAt);
                    }
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
        /// <summary>湖倾档起洼/续命共用路径：落点由调用方给（贴地=死点，沾敌=宿主脚下地形）</summary>
        private void SpawnOrRefreshPuddleAt(Vector2 at) {
            KikasaTalismanProfile talismans =
                KikasaTalismanCombat.Resolve(Main.player[Projectile.owner]);
            int puddleLife = (int)(KikasaInkPuddle.LifeFrames * talismans.PuddleLifeMul);
            //霜符"不可叠寿":禁合并续命,每滴各起各的洼
            Projectile near = talismans.PuddleNoRefresh
                ? null : FindNearOwnPuddle(56f * talismans.PuddleRadiusMul, at);
            if (near != null) {
                near.timeLeft = Math.Max(near.timeLeft, puddleLife);
                near.netUpdate = true;
            }
            else {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    at, Vector2.Zero,
                    ModContent.ProjectileType<KikasaInkPuddle>(),
                    (int)(Projectile.damage * 0.35f * talismans.PuddleDamageMul),
                    0f, Projectile.owner,
                    talismans.PuddleRadiusMul, talismans.PuddleLifeMul);
            }
        }

        /// <summary>从起点向下找首个可站立地表（实心或平台，最多 20 格），命中返回贴地点</summary>
        private static bool TryFindGroundBelow(Vector2 from, out Vector2 ground) {
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            for (int dy = 0; dy < 20; dy++) {
                int y = ty + dy;
                if (!WorldGen.InWorld(tx, y, 8)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasTile
                    && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) {
                    ground = new Vector2(from.X, y * 16f - 2f);
                    return true;
                }
            }
            ground = default;
            return false;
        }

        private Projectile FindNearOwnPuddle(float radius, Vector2 at) {
            int puddleType = ModContent.ProjectileType<KikasaInkPuddle>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Projectile.owner && proj.type == puddleType
                    && Vector2.Distance(proj.Center, at) < radius) {
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
            //抛洒段近顶点滞空的张力抖动比飞行时明显
            bool apexDwell = Phase == DropPhase.Toss && Projectile.velocity.Y > -1.2f;
            float wobAmp = apexDwell ? 0.15f : 0.06f;
            //色板逐滴上载:鬼滴换鬼青,带符标签的滴再过一道符绘制挂钩(霓染色/雹冰蓝等);
            //共享参数会被上一颗滴污染,必须全量重设
            KikasaDropDrawParams draw = new() {
                Body = IsGhostDrop ? KikasaInk.GhostBody : KikasaInk.InkBody,
                Deep = IsGhostDrop ? KikasaInk.GhostDeep : KikasaInk.InkDeep,
                Core = IsGhostDrop ? KikasaInk.GhostCore : KikasaInk.BloodCore,
                SizeMul = 1f,
            };
            if (TalismanTagId != 0) {
                KikasaTalismanHooks.ModifyDropDraw(Projectile, ref draw);
            }
            fx.Parameters["uColBody"]?.SetValue(draw.Body.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(draw.Deep.ToVector3());
            fx.Parameters["uColCore"]?.SetValue(draw.Core.ToVector3());
            fx.Parameters["uStretch"]?.SetValue(stretch);
            fx.Parameters["uWobAmp"]?.SetValue(wobAmp);
            fx.Parameters["uWobPhase"]?.SetValue(life * 0.5f + Seed * 6f);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFade"]?.SetValue(VisualFade);
            fx.Parameters["uBend"]?.SetValue(MathHelper.Clamp(bend, -1f, 1f));
            fx.CurrentTechnique = fx.Techniques["TechDrop"];
            fx.CurrentTechnique.Passes[0].Apply();

            //Projectile.scale 承载特大墨滴(墨瀑散射),符绘制倍率只动画布不动判定
            float side = (62f + stretch * 30f) * Projectile.scale * draw.SizeMul;
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
            //符绘制挂钩与着色器路径同一套参数,回退路径不缺席
            KikasaDropDrawParams draw = new() {
                Body = IsGhostDrop ? KikasaInk.GhostBody : KikasaInk.InkBody,
                Deep = IsGhostDrop ? KikasaInk.GhostDeep : KikasaInk.InkDeep,
                Core = IsGhostDrop ? KikasaInk.GhostCore : KikasaInk.BloodCore,
                SizeMul = 1f,
            };
            if (TalismanTagId != 0) {
                KikasaTalismanHooks.ModifyDropDraw(Projectile, ref draw);
            }
            Vector2 bodyScale = new Vector2(0.24f * (1f - stretch * 0.3f), 0.36f * (1f + stretch * 1.7f))
                * jiggle * Projectile.scale * draw.SizeMul;
            sb.Draw(tex, pos, null, draw.Deep * (0.9f * fade), Projectile.rotation, origin,
                bodyScale * new Vector2(1.3f, 1.06f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, draw.Body * fade, Projectile.rotation, origin,
                bodyScale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, draw.Core * (0.4f * fade), Projectile.rotation, origin,
                bodyScale * new Vector2(0.3f, 0.7f), SpriteEffects.None, 0f);
        }
    }
}
