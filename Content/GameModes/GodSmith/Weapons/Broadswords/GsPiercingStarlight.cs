using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【昼星终曲】材质：白金星芒淬锋的星光刺剑。
    /// 签名：①挥砍换成突刺运动语言：一次使用=一段三连刺（疾出过冲、刺尖驻帧、缓回收），
    /// 三刺各有伤害窗
    /// ②第三刺射出贯穿星光弹（减速滑行消散）
    /// ③连续三段三刺全中，星光弹升格星暴，命中炸开星暴环
    /// </summary>
    internal class GsPiercingStarlight : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PiercingStarlight;

        protected override int HeldProjID => ModContent.ProjectileType<GsPiercingStarlightHeld>();

        /// <summary>一次使用就是完整一段三连刺</summary>
        protected override int ComboBeats => 1;

        protected override string GsDescFallback =>
            "Reforged: each use is a three-thrust starlight cadence; the final thrust fires a piercing star bolt, and three faultless cadences in a row make the bolt burst into a starburst on hit";
        internal static readonly Color StarBright = new(240, 248, 255); //白金星芒
        internal static readonly Color StarMain = new(150, 214, 255);   //淡青彗尾
        internal static readonly Color StarHot = new(255, 244, 198);    //昼星暖芯

        /// <summary>连续全中段数（0~3，满 3 段星光弹升格星暴）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Cadence;

        //底伤不加成（原版快刺 DPS 已高）：三刺 0.88/0.88/1.02（第三刺经 ModifyHitExtra 补 1.16x）
        //+ 星光弹 0.50x + 星暴 0.30x（三段全中才有），单段合计 3.28~3.58x，
        //不超原版一个 useAnimation 周期（3 刺 x1.0 = 3.0x）的 120% 上限；
        //段长约 20 帧对原版 18 帧，基线 DPS 约 98%，穿透 3 与星暴 AoE 把综合抬进 100%~110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 昼星终曲手持：整体重写 UpdateBladeTransform 换成突刺运动语言。
    /// 时间线：蓄势后引（Raise）→ 三连刺（Slash 均分三份：疾出 35% / 驻帧 20% / 缓回 45%）
    /// → 收势（Recover）。突刺角度基本不变，基类扫角判定退化，
    /// CanDamage/Colliding/CutTiles 全部重接成刺线；localNPCHitCooldown 改短让三刺各中一次。
    /// ai[0]=拍号（恒 0）ai[1]=交替符号（三刺扇形偏角次序）
    /// </summary>
    internal class GsPiercingStarlightHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PiercingStarlight;
        protected override int BeatCount => 1;
        protected override Color EdgeBright => GsPiercingStarlight.StarBright;
        protected override Color BodyMain => GsPiercingStarlight.StarMain;
        protected override Color HotAccent => GsPiercingStarlight.StarHot;

        //刺剑：窄刺线判定，贴身兜底小
        protected override float BaseReach => 122f;
        protected override float CollisionWidth => 26f;
        protected override float PointBlankRadius => 36f;

        /// <summary>三刺完整命中记录</summary>
        private readonly bool[] thrustLanded = new bool[3];
        /// <summary>各刺出手事件已放</summary>
        private readonly bool[] thrustCalled = new bool[3];

        private int thrustIndex = -1;   //当前刺序（-1=未进入连刺）
        private float subPhase;         //当前刺内进度 0~1
        private bool damageActive;      //本帧伤害窗
        private bool boltFired;

        private GsPiercingStarlight Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsPiercingStarlight : null;

        protected override GsBroadBeat GetBeat(int stage) => new() {
            //Slash=13 均分三刺；Raise 短蓄、Recover 短收，整段约 20 帧（除以攻速）
            Raise = 2, Hold = 1, Slash = 13, Recover = 4,
            RaiseBack = 0.3f, Follow = 0.1f, ReachScale = 1f, LeanAmp = 0.03f,
            DamageMult = 0.88f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.3f,
        };

        protected override void SetSwordDefaults() {
            //短冷却：三刺可对同一目标各结算一次（刺间隔约 4~5 帧，单刺伤害窗约 2 帧不致重复吃）
            Projectile.localNPCHitCooldown = 3;
        }

        /// <summary>三刺微扇形偏角：先低后高再正中（次序符号随 ai[1] 交替）</summary>
        private float FanOffset(int idx) => idx switch {
            0 => -0.055f * swingDir,
            1 => 0.048f * swingDir,
            _ => 0f,
        };

        /// <summary>突刺运动语言：角度锁死出手向（带微扇偏角），行程做出-驻-回三段</summary>
        protected override void UpdateBladeTransform(int phase) {
            float reach01;
            switch (phase) {
                case PhaseRaise: {
                    //蓄势后引：刃往手边收
                    float p = timer / (float)raiseDur;
                    reach01 = MathHelper.Lerp(0.62f, 0.40f, EaseOutQuad(p));
                    mainAngle = baseAngle + FanOffset(0);
                    thrustIndex = -1;
                    subPhase = 0f;
                    damageActive = false;
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    //瞄定一帧
                    reach01 = 0.40f;
                    mainAngle = baseAngle + FanOffset(0);
                    thrustIndex = -1;
                    subPhase = 0f;
                    damageActive = false;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float s = (timer - raiseDur - holdDur) / (float)slashDur; //(0,1]
                    slashProgress = s;
                    int idx = Math.Min(2, (int)(s * 3f - 0.0001f));
                    float pThird = MathHelper.Clamp(s * 3f - idx, 0f, 1f);
                    if (idx != thrustIndex) {
                        //新刺开跑
                        thrustIndex = idx;
                    }
                    subPhase = pThird;
                    mainAngle = baseAngle + FanOffset(idx);
                    float deep = idx == 2 ? 1.10f : 1f; //终刺贯得更深
                    if (pThird < 0.35f) {
                        //疾出：1~2 帧带 6% 过冲
                        float q = pThird / 0.35f;
                        reach01 = MathHelper.Lerp(0.42f, 1.06f * deep, 1f - MathF.Pow(1f - q, 3f));
                        damageActive = true;
                    }
                    else if (pThird < 0.55f) {
                        //刺尖驻帧：过冲回坐
                        float q = (pThird - 0.35f) / 0.2f;
                        reach01 = MathHelper.Lerp(1.06f, 1f, SmoothStep01(q)) * deep;
                        damageActive = true;
                    }
                    else {
                        //缓回收
                        float q = (pThird - 0.55f) / 0.45f;
                        reach01 = MathHelper.Lerp(deep, 0.45f, SmoothStep01(q));
                        damageActive = false;
                    }
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    reach01 = MathHelper.Lerp(0.45f, 0.60f, EaseOutQuad(q));
                    mainAngle = baseAngle;
                    damageActive = false;
                    slashProgress = 1f;
                    break;
                }
            }
            mainReach = FullReach * reach01;
            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>自管事件编排：每刺出手音与终刺星光弹（不走基类单次斩切事件；阔剑族不做体术位移）</summary>
        protected override void HandlePhaseEvents(int phase) {
            if (phase != PhaseSlash || thrustIndex < 0) {
                return;
            }
            int idx = thrustIndex;
            //每刺出手瞬间：星芒短哨
            if (!thrustCalled[idx]) {
                thrustCalled[idx] = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.30f + idx * 0.07f }, Owner.Center);
                }
            }
            //终刺驻帧瞬间射出星光弹（方案段数只在 owner 读）
            if (idx == 2 && !boltFired && subPhase >= 0.35f) {
                boltFired = true;
                if (Projectile.owner == Main.myPlayer) {
                    GsPiercingStarlight scheme = Scheme;
                    float burst = scheme != null && scheme.Cadence >= 3 ? 1f : 0f;
                    int boltDamage = Math.Max(1, (int)(Projectile.damage * 0.57f));
                    SpawnOwnedProj(ModContent.ProjectileType<GsPiercingStarlightBoltProj>(),
                        mainTip, baseAngle.ToRotationVector2() * 16f, boltDamage, Projectile.knockBack * 0.5f, burst);
                }
            }
        }

        public override bool? CanDamage() => damageActive ? null : false;

        /// <summary>刺线判定：手→刺尖线段 + 贴身兜底</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!damageActive) {
                return false;
            }
            Rectangle box = targetHitbox;
            box.Inflate(6, 6);
            Vector2 hand = Hand;
            if (box.Distance(hand) <= PointBlankRadius) {
                return true;
            }
            float cp = 0f;
            Vector2 tip = hand + mainAngle.ToRotationVector2() * (mainReach * 1.05f + 8f);
            return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(), hand, tip, CollisionWidth, ref cp);
        }

        public override void CutTiles() {
            if (!damageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Hand, mainTip, CollisionWidth * 0.85f, DelegateMethods.CutTiles);
        }

        /// <summary>终刺贯劲：0.88 拍基线补至约 1.02x</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (thrustIndex == 2) {
                modifiers.SourceDamage *= 1.16f;
            }
        }

        /// <summary>记录当前刺命中（供整段全中判定）+ 清脆星音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (thrustIndex >= 0) {
                thrustLanded[thrustIndex] = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.3f, Pitch = 0.5f }, target.Center);
            }
        }

        /// <summary>段末记账：三刺窗口走完且三刺全中 → 段数 +1，否则清零（守 myPlayer）</summary>
        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsPiercingStarlight scheme = Scheme;
            if (scheme == null) {
                return;
            }
            bool completed = timer > raiseDur + holdDur + slashDur;
            if (completed && thrustLanded[0] && thrustLanded[1] && thrustLanded[2]) {
                int old = scheme.Cadence;
                scheme.Cadence = Math.Min(3, scheme.Cadence + 1);
                if (old < 3 && scheme.Cadence == 3) {
                    //满段提示：一声上扬星哨
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.4f, Pitch = 0.5f }, Owner.Center);
                }
            }
            else {
                scheme.Cadence = 0;
            }
        }
    }

    /// <summary>
    /// 贯穿星光弹：用原版超级星星贴图，出膛 16 减速滑行至约 5 后消散，穿透 3。
    /// ai[0]=星暴旗（三段全中的升格：首次命中炸开星暴环）
    /// </summary>
    internal class GsPiercingStarlightBoltProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SuperStar;

        private bool Burst => Projectile.ai[0] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];
        /// <summary>星暴已放（owner 端权威，生成走同步包）</summary>
        private ref float BurstDone => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 42;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }
            //减速滑行：16 → 约 5，昼星滑向消散
            if (Projectile.velocity.Length() > 5f) {
                Projectile.velocity *= 0.955f;
            }
            Projectile.rotation += 0.22f * (Projectile.velocity.X >= 0f ? 1f : -1f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //星暴升格：首次命中炸开星屑环（owner 端生成，随生成包同步）
            if (Burst && BurstDone == 0f) {
                BurstDone = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    int ringDamage = Math.Max(1, (int)(Projectile.damage * 0.6f));
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsPiercingStarlightBurstProj>(), ringDamage,
                        Projectile.knockBack, Projectile.owner);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.4f }, target.Center);
        }
    }

    /// <summary>
    /// 星暴：星光弹升格后的命中爆环。7 帧过冲撑满后回坐，伤害只在扩张期结算一次；
    /// 用原版泡泡贴图按半径画一笔作范围提示
    /// </summary>
    internal class GsPiercingStarlightBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

        private const int TotalLife = 20;
        private const float MaxRadius = 88f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：7 帧过冲 5% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 7f, 0f, 1f);
                float burst = p < 0.7f ? 1.05f * (p / 0.7f) : MathHelper.Lerp(1.05f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.6f }, Projectile.Center);
            }
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 8f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>范围提示：原版泡泡贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Radius * 2f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * (1f - Life01),
                0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
