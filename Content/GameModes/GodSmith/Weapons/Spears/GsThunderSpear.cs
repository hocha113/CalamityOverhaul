using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛】风暴矛重铸：引雷驻相。<br/>
    /// 材质：风暴淬蓝的引雷枪，尖上常年游走细电。签名行为：①驻相拉长——刺出后枪尖定格蓄电，
    /// 电弧粒子密集缠尖 ②驻相内命中即放电，电链劈向最近一名其他敌人（50% 伤害 + 电光拉丝）；
    /// 雨天引雷入云，可链两名 ③放电有雷鸣与小震屏，与普通刺击层次分明
    /// </summary>
    internal class GsThunderSpear : GsSpearScheme
    {
        public override int TargetItemID => ItemID.ThunderSpear;

        protected override string GsDescFallback =>
            "Reforged: the tip charges while held at full extension; land a hit during that moment" +
            "\nand lightning arcs to the nearest other enemy, or two in the rain";

        protected override int HeldProjType => ModContent.ProjectileType<GsThunderSpearHeld>();

        //电链是白送的第二段伤害，底伤只小补，综合 DPS 落在原版 104%~116%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 风暴矛手持突刺：驻相帧拉长到 6（驻相有理由），驻相期枪尖蓄电、
    /// 驻相内命中放电链至最近 1 名其他敌人（雨天 2 名）
    /// </summary>
    internal class GsThunderSpearHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.ThunderSpear;

        //电蓝白色板
        internal static readonly Color StormWhite = new(236, 246, 255); //电光白
        internal static readonly Color VoltBlue = new(98, 172, 255);    //伏特蓝
        internal static readonly Color VoltCyan = new(148, 232, 255);   //电青
        internal static readonly Color StormDeep = new(30, 48, 92);     //风暴深底

        /// <summary>电链搜索半径（px）</summary>
        private const float ChainRange = 340f;

        protected override float WindupFrames => 5f;
        protected override float ThrustFrames => 5f;
        protected override float DwellFrames => 6f;//驻相拉长：蓄电窗就是签名
        protected override float RecoverFrames => 10f;//收势对齐原版 28 帧节奏
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 68f;
        protected override float BladeLength => 90f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 28f;
        protected override float ThrustEasePower => 2.8f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.04f;
        protected override int HitboxSize => 52;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.15f;

        protected override Color EdgeColor => StormWhite;
        protected override Color CoreColor => VoltBlue;
        protected override Color ShaftColor => StormDeep with { A = 235 };

        /// <summary>驻相进度 0~1，蓄电可视化用</summary>
        private float DwellT => CurrentPhase == PhaseDwell
            ? MathHelper.Clamp((Elapsed - WindupFrames - ThrustFrames) / DwellFrames, 0f, 1f) : 0f;

        protected override void OnDwellStart() {
            if (VaultUtils.isServer) {
                return;
            }
            //蓄电起手一声电噼啪
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.3f, Pitch = 0.4f, MaxInstances = 3 }, TipPos);
        }

        /// <summary>驻相蓄电：电弧感粒子密集缠尖，越到驻相末越密</summary>
        protected override void OnTick(int phase) {
            if (VaultUtils.isServer || phase != PhaseDwell) {
                return;
            }
            int count = 1 + (int)(DwellT * 2f);
            for (int i = 0; i < count; i++) {
                Vector2 at = TipPos + Main.rand.NextVector2Circular(9f, 9f);
                Color c = Main.rand.NextBool(3) ? StormWhite : VoltCyan;
                //短寿高拉伸的光粒子模拟细电游走
                PRTLoader.NewParticle<PRT_Light>(at, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    c, Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(4, 8), 0.7f, 2.2f);
            }
        }

        /// <summary>蓄电可视化：驻相内枪尖辉光随蓄电升温</summary>
        protected override float ExtraGlowStrength() => DwellT * 0.30f;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //引雷放电：只在驻相内命中触发，一刺只放一次（owner 端生成，起点走 ai 过线）
            if (CurrentPhase != PhaseDwell || !firstOnTarget || Projectile.numHits > 1
                || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int chains = Owner.ZoneRain ? 2 : 1;
            //找伤口附近最近的其他敌人（雨天取最近两名）
            NPC best = null, second = null;
            float bestDist = ChainRange, secondDist = ChainRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = npc.Distance(target.Center);
                if (dist < bestDist) {
                    second = best; secondDist = bestDist;
                    best = npc; bestDist = dist;
                }
                else if (dist < secondDist) {
                    second = npc; secondDist = dist;
                }
            }
            int fired = 0;
            foreach (NPC chainTo in new[] { best, second }) {
                if (chainTo == null || fired >= chains) {
                    break;
                }
                fired++;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), chainTo.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsThunderSpearArcProj>(),
                    (int)(BaseDamage * 0.50f), 0f, Owner.whoAmI, target.Center.X, target.Center.Y);
            }
            //放了电才有的升级反馈：雷鸣 + 小震屏
            if (fired > 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.7f, Pitch = -0.1f }, target.Center);
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        target.Center, stabUnit, 3.5f, 6f, 8, 520f, FullName));
                }
            }
        }

        /// <summary>命中反馈：电光白闪 + 电青火花四射，无血尘（电灼不见血）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            bool dwellHit = CurrentPhase == PhaseDwell;
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, StormWhite,
                dwellHit ? 0.26f : 0.16f)?.Configure(8, 0.85f);
            int sparks = dwellHit ? 9 : 5;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(3.5f, 8.5f);
                Color c = Main.rand.NextBool(3) ? StormWhite : VoltCyan;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.35f, Pitch = dwellHit ? -0.2f : 0.1f, MaxInstances = 3 }, pos);
        }

        /// <summary>驻相蓄电自绘：枪尖周围三段游走细电（LightShot 拉丝，whoAmI 种子，无随机）</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            float dwellT = DwellT;
            if (dwellT <= 0.02f) {
                return;
            }
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return;
            }
            //电花每 3 帧换形：时间量化 + whoAmI 播种
            int flick = (int)(Main.GlobalTimeWrappedHourly * 20f);
            Vector2 tip = TipPos - Main.screenPosition;
            Vector2 texSize = streak.Size();
            for (int i = 0; i < 3; i++) {
                float seed = Projectile.whoAmI * 7.31f + flick * 1.71f + i * 12.9898f;
                float ang = Frac(MathF.Sin(seed) * 43758.5453f) * MathHelper.TwoPi;
                float len = 10f + Frac(MathF.Sin(seed + 1.3f) * 24634.6345f) * 16f * dwellT;
                Vector2 at = tip + ang.ToRotationVector2() * (len * 0.5f);
                Color c = (i == 0 ? StormWhite : VoltCyan) with { A = 0 } * (0.55f * dwellT);
                sb.Draw(streak, at, null, c, ang, texSize / 2f,
                    new Vector2(len / texSize.X, 0.05f), SpriteEffects.None, 0f);
            }
        }

        private static float Frac(float x) => x - MathF.Floor(x);
    }

    /// <summary>
    /// 引雷电链：驻相命中放出的第二段判定，钉在链目标处瞬间放电。
    /// ai[0]/ai[1]=放电起点（伤口坐标，随生成包过线）。<br/>
    /// 自绘锯齿闪电折线：LightShot 分段拉丝，抖动吃 whoAmI 种子（无随机），存活期内每 3 帧换形
    /// </summary>
    internal class GsThunderSpearArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.ThunderSpear");

        private const int Segments = 6;
        private const int LifeFrames = 14;

        private ref float Timer => ref Projectile.localAI[0];
        private Vector2 ArcFrom => new(Projectile.ai[0], Projectile.ai[1]);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>只在头三帧有判定，其后纯演出滞留</summary>
        public override bool? CanDamage() => Timer <= 3f ? null : false;

        public override void AI() {
            Timer++;
            if (Timer == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                //落点电花
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f);
                    Color c = Main.rand.NextBool(3) ? GsThunderSpearHeld.StormWhite : GsThunderSpearHeld.VoltCyan;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(true, Main.rand.Next(10, 16));
                }
            }
            Lighting.AddLight(Vector2.Lerp(ArcFrom, Projectile.Center, 0.5f),
                GsThunderSpearHeld.VoltBlue.ToVector3() * (0.5f * (Projectile.timeLeft / (float)LifeFrames)));
        }

        /// <summary>锯齿闪电折线：中段抖幅大、两端收束，双层描边（伏特蓝宽 + 电光白芯）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return false;
            }
            Vector2 from = ArcFrom;
            Vector2 to = Projectile.Center;
            Vector2 dir = to - from;
            float dist = dir.Length();
            if (dist < 8f) {
                return false;
            }
            Vector2 perp = dir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float fade = Projectile.timeLeft / (float)LifeFrames;
            int flick = (int)(Main.GlobalTimeWrappedHourly * 20f);
            Vector2 texSize = streak.Size();

            //折点：whoAmI + 时间量化播种，两端钉死
            Span<Vector2> pts = stackalloc Vector2[Segments + 1];
            for (int i = 0; i <= Segments; i++) {
                float t = i / (float)Segments;
                float envelope = MathF.Sin(t * MathHelper.Pi);//两端收束
                float seed = Projectile.whoAmI * 7.31f + flick * 1.71f + i * 12.9898f;
                float wobble = (Frac(MathF.Sin(seed) * 43758.5453f) - 0.5f) * 2f;
                pts[i] = Vector2.Lerp(from, to, t) + perp * (wobble * 16f * envelope);
            }
            for (int i = 0; i < Segments; i++) {
                Vector2 a = pts[i] - Main.screenPosition;
                Vector2 b = pts[i + 1] - Main.screenPosition;
                Vector2 seg = b - a;
                float rot = seg.ToRotation();
                Vector2 mid = (a + b) * 0.5f;
                float len = seg.Length();
                Color wide = GsThunderSpearHeld.VoltBlue with { A = 0 } * (0.6f * fade);
                Main.spriteBatch.Draw(streak, mid, null, wide, rot, texSize / 2f,
                    new Vector2(len / texSize.X * 1.1f, 0.075f), SpriteEffects.None, 0f);
                Color core = GsThunderSpearHeld.StormWhite with { A = 0 } * (0.85f * fade);
                Main.spriteBatch.Draw(streak, mid, null, core, rot, texSize / 2f,
                    new Vector2(len / texSize.X, 0.035f), SpriteEffects.None, 0f);
            }
            //链尾落点白闪
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color c = GsThunderSpearHeld.StormWhite with { A = 0 } * (0.7f * fade);
                Main.spriteBatch.Draw(glow, to - Main.screenPosition, null, c, 0f,
                    glow.Size() / 2f, 0.4f * fade + 0.15f, SpriteEffects.None, 0f);
            }
            return false;
        }

        private static float Frac(float x) => x - MathF.Floor(x);
    }
}
