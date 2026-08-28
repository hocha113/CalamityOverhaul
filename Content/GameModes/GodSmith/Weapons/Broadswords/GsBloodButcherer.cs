using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【血铁屠刀】材质：猩红血铁锻的剁骨屠刀。签名：①每记重剁给目标叠一层「放血」，
    /// 层数越高体表滴血越密 ②终结剖割引爆全部放血层，每层炸出一跳小范围血爆
    /// ③满三层引爆时持刀人吮血回 2 生命
    /// </summary>
    internal class GsBloodButcherer : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BloodButcherer;

        protected override int HeldProjID => ModContent.ProjectileType<GsBloodButchererHeld>();

        protected override string GsDescFallback =>
            "Reforged: heavy chops stack Exsanguination on the victim; " +
            "the carving finisher detonates every stack into a blood burst, and a full three-stack burst feeds you life";

        //血铁色板
        internal static readonly Color GoreBright = new(255, 96, 96);   //鲜血亮红
        internal static readonly Color GoreMain = new(168, 32, 40);     //血铁暗红
        internal static readonly Color GoreHot = new(255, 40, 24);      //迸血炽红
        internal static readonly Color GoreDeep = new(38, 8, 12);       //凝血近黑

        //底伤 +2%：普通拍 1.0x，终结 1.25x，引爆期望每循环 0.5~0.7x（2 层常态、3 层要跨循环经营），
        //按三拍摊算综合 DPS 约为原版 106%~116%，回血 2 点是操作奖励不进伤害账
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 血铁屠刀手持：三拍剁割连击。0/1 交替重剁（长滞帧短挥程，剁而非扫），
    /// 2 剖割终结（大弧贯穿+前压+引爆放血层）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBloodButchererHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BloodButcherer;
        protected override Color EdgeBright => GsBloodButcherer.GoreBright;
        protected override Color BodyMain => GsBloodButcherer.GoreMain;
        protected override Color HotAccent => GsBloodButcherer.GoreHot;
        protected override Color DeepShadow => GsBloodButcherer.GoreDeep;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //剖割终结：大弧贯穿到底，引爆放血层
                return new GsBroadBeat {
                    Raise = 9, Hold = 4, Slash = 5, Recover = 12,
                    RaiseBack = 2.3f, Follow = 1.35f, ReachScale = 1.16f, LeanAmp = 0.09f,
                    DamageMult = 1.25f, Hitstop = 3, LungeSpeed = 2.6f, SwingPitch = -0.38f,
                };
            }
            //重剁：长滞帧蓄劲、短挥程急落，剁而非扫
            return new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 3, Recover = 10,
                RaiseBack = 2.0f, Follow = 0.75f, ReachScale = 1f, LeanAmp = 0.06f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? -0.2f : -0.26f,
            };
        }

        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsBloodButcherer.GoreHot : GsBloodButcherer.GoreMain;

        //血铁吸光，刃面常年泛着凝血暗泽
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsBloodButcherer.GoreDeep, 0.22f);

        protected override void PlaySwingSound() {
            //剁击厚重，终结补一记撕裂声
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.3f }, Owner.Center);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            GsBloodButchererGlobalNPC bleed = target.GetGlobalNPC<GsBloodButchererGlobalNPC>();
            if (!IsFinisher) {
                //重剁叠放血层（攻击方端计数，可见结果经弹幕过线）
                bleed.AddStack();
                return;
            }
            int stacks = bleed.Stacks;
            if (stacks <= 0) {
                return;
            }
            bleed.ClearStacks();
            //引爆：每层 35% 底伤并入一跳血爆（除回 DamageMult 取底伤，账目见方案注释）
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            int burstDamage = Math.Max(1, (int)(baseDamage * 0.35f * stacks));
            SpawnOwnedProj(ModContent.ProjectileType<GsBloodButchererBurstProj>(),
                target.Center, Vector2.Zero, burstDamage, 2f, stacks);
            //满三层吮血：owner 端守门回 2 生命（Heal 自带回血演出并同步）
            if (stacks >= 3 && Owner.whoAmI == Main.myPlayer) {
                Owner.Heal(2);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //屠刀命中血雾浓重：暗红雾片 + 垂滴血珠
            if (CWRLoad.NPCValue.ISTheofSteel(target)) {
                return;
            }
            int mist = IsFinisher ? 9 : 5;
            for (int i = 0; i < mist; i++) {
                Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Blood, Main.rand.NextVector2Circular(2.6f, 1.8f) + new Vector2(0f, 0.8f),
                    120, default, Main.rand.NextFloat(1.2f, 1.9f));
                d.noGravity = false;
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //举刀期刃口垂滴血珠（有重力的血红火星，屠刀常年不干）
            if (phase is PhaseRaise or PhaseHold && Main.rand.NextBool(3)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 0.95f));
                PRTLoader.NewParticle<PRT_Spark>(at, new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                    GsBloodButcherer.GoreMain, Main.rand.NextFloat(0.22f, 0.34f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
        }
    }

    /// <summary>
    /// 放血层记录（攻击方本地量：命中钩子只在攻击方端执行，引爆经弹幕生成包过线）。
    /// 层数上限 3，5 秒不续层即衰减清零；层数越高体表滴血越密
    /// </summary>
    internal class GsBloodButchererGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private const int MaxStacks = 3;
        private const uint DecayTicks = 300;

        private int stacks;
        private uint staleAt;

        /// <summary>当前有效层数（过期自动读 0）</summary>
        internal int Stacks {
            get {
                if (stacks > 0 && Main.GameUpdateCount >= staleAt) {
                    stacks = 0;
                }
                return stacks;
            }
        }

        internal void AddStack() {
            stacks = Math.Min(MaxStacks, Stacks + 1);
            staleAt = Main.GameUpdateCount + DecayTicks;
        }

        internal void ClearStacks() {
            stacks = 0;
            staleAt = 0;
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //放血可见：层数越高滴血越密（1 层约 1/8 帧率，3 层约 1/3）
            int live = Stacks;
            if (live <= 0 || Main.dedServ) {
                return;
            }
            if (Main.rand.NextBool(10 - live * 3)) {
                Dust d = Dust.NewDustPerfect(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.42f, npc.height * 0.42f),
                    DustID.Blood, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(1f, 2f)),
                    100, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = false;
            }
        }
    }

    /// <summary>
    /// 血爆：引爆放血层的一跳小范围血浪。ai[0]=引爆层数（定半径与演出量）。
    /// 暗体用真 alpha 贴图 Extra_98 染深红压出血浪厚度，亮红边走加色；
    /// 绘制抖动全部 identity 播种
    /// </summary>
    internal class GsBloodButchererBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeTicks = 26;
        private const int DamageWindow = 12;

        private int BurstStacks => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private float MaxRadius => 62f + BurstStacks * 16f;
        private float Age => LifeTicks - Projectile.timeLeft;

        /// <summary>血浪半径：前 8 帧猛涨后驻定，尾段随消散回缩</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp(Age / 8f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
                return MaxRadius * (1f - (1f - grow) * (1f - grow)) * (0.4f + 0.6f * fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//一跳 AoE，同目标只结算一次
            Projectile.timeLeft = LifeTicks;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Age <= DamageWindow ? null : false;

        public override void AI() {
            if (Age == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath21 with { Volume = 0.55f, Pitch = 0.25f }, Projectile.Center);
                //迸血珠：出爆瞬间外抛一圈带重力的血珠
                int beads = 6 + BurstStacks * 3;
                for (int i = 0; i < beads; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6.5f + BurstStacks);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel,
                        Main.rand.NextBool(3) ? GsBloodButcherer.GoreHot : GsBloodButcherer.GoreBright,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(16, 26));
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                        vel * 0.7f, 90, default, Main.rand.NextFloat(1.1f, 1.7f));
                    d.noGravity = false;
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                    GsBloodButcherer.GoreHot, 0.2f + BurstStacks * 0.06f)?.Configure(10, 0.85f);
            }
            Lighting.AddLight(Projectile.Center,
                GsBloodButcherer.GoreMain.ToVector3() * (0.5f * (RadiusNow / MaxRadius)));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = RadiusNow;
            if (r < 8f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).LengthSquared() <= r * r;
        }

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return false;
            }
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)LifeTicks, 0f, 1f);
            Vector2 center = Projectile.Center - Main.screenPosition;

            //血浪暗体：真 alpha 深红团块拼出的一圈浪涌（加色物理上做不出血的厚重）
            int lobes = 5 + BurstStacks;
            for (int i = 0; i < lobes; i++) {
                float ang = MathHelper.TwoPi * i / lobes + SegRand(i) * 0.8f;
                float dist = r * (0.42f + 0.3f * SegRand(i + 30));
                float s = (r / blot.Width) * (1.1f + 0.6f * SegRand(i + 60));
                Color dark = GsBloodButcherer.GoreDeep * (fade * 0.6f);
                Main.EntitySpriteDraw(blot, center + ang.ToRotationVector2() * dist, null, dark,
                    ang, blot.Size() * 0.5f, new Vector2(s, s * 0.62f), SpriteEffects.None, 0);
            }

            //亮红浪缘：加色光斑挂在血浪外沿，明灭相位各瓣错开
            for (int i = 0; i < lobes; i++) {
                float ang = MathHelper.TwoPi * i / lobes + SegRand(i + 90) * 0.7f;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + SegRand(i + 120) * 6.28f);
                Color edge = GsBloodButcherer.GoreHot * (fade * 0.55f * pulse);
                edge.A = 0;
                Main.EntitySpriteDraw(glow, center + ang.ToRotationVector2() * (r * 0.92f), null, edge,
                    0f, glow.Size() * 0.5f, (r / glow.Width) * 0.75f, SpriteEffects.None, 0);
            }

            //爆心闪：出爆前半程的炽红核
            if (Age <= 10) {
                Color core = GsBloodButcherer.GoreBright * ((1f - Age / 10f) * 0.7f);
                core.A = 0;
                Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f,
                    r / glow.Width * 1.5f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
