using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 弧光死光：角速度走缓动曲线的天体弧线（快→慢→快），或大招期的追踪衰减模式。
    /// ai[0]=宿主 whoAmI，ai[1]=起始角，ai[2]=总扫角(带符号弧度)。
    /// 追踪衰减：宿主为核心且核心状态==VoidRupture 时自动启用（各端确定性判定）
    /// </summary>
    internal class MLordArcRayProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int ExpandTime = 20;
        internal const int SweepFrames = 150;
        internal const int CollapseTime = 18;
        internal const int TotalLife = ExpandTime + SweepFrames + CollapseTime;
        internal const float BeamLength = 4300f;
        internal const float MaxWidth = 104f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float TrackAngle => ref Projectile.localAI[1];
        private NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        private float beamWidth;

        /// <summary>追踪衰减模式：核心大招期间发出的弧线</summary>
        private bool TrackingMode {
            get {
                NPC host = Host;
                return host.Alives() && host.type == NPCID.MoonLordCore
                    && MLordFacts.GetCoreState(host) == MLordStateIndex.VoidRupture;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 30;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC host = Host;

            //宿主消失快进收束
            if (!host.Alives() && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            if (Timer == 0) {
                TrackAngle = Projectile.ai[1];
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.9f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
                    MLordScreenFX.Punch(Projectile.Center, 4f, 10);
                }
            }

            float sweepT = MathHelper.Clamp((Timer - ExpandTime) / SweepFrames, 0f, 1f);
            if (TrackingMode) {
                //追踪衰减：增益随扫描进度衰竭，逼迫走位从周旋转为一次性拉开
                float gain = MathHelper.Lerp(0.046f, 0f, VaultUtils.EaseInQuad(sweepT));
                if (host.Alives() && host.target >= 0 && host.target < Main.maxPlayers) {
                    Player target = Main.player[host.target];
                    if (target.active && !target.dead) {
                        float wantAngle = (target.Center - Projectile.Center).ToRotation();
                        TrackAngle = TrackAngle.AngleTowards(wantAngle, gain);
                    }
                }
                Projectile.rotation = TrackAngle;
            }
            else {
                //天体弧线：InOut 缓动角进给，两端快中段慢，收拢的"呼吸"
                float eased = VaultUtils.EaseInOutCubic(sweepT);
                Projectile.rotation = Projectile.ai[1] + Projectile.ai[2] * eased;
            }

            if (host.Alives()) {
                Projectile.Center = host.Center + Projectile.rotation.ToRotationVector2() * 38f;
            }

            //宽度包络
            float collapseStart = TotalLife - CollapseTime;
            if (Timer < ExpandTime) {
                float t = Timer / ExpandTime;
                beamWidth = MathHelper.Lerp(3f, MaxWidth, VaultUtils.EaseOutCubic(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
            }
            else {
                beamWidth = MaxWidth;
            }
            beamWidth *= 1f + 0.045f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 26f + Projectile.whoAmI);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Lighting.AddLight(Projectile.Center + dir * (BeamLength / 6f * i),
                    MLordDirector.Phantasmal.ToVector3() * 0.7f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //低频震屏刷新
            if ((int)Timer % 7 == 0) {
                MLordScreenFX.Punch(Projectile.Center, 2f, 7, dir);
            }
            //沿束星屑
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 pos = Projectile.Center + dir * BeamLength * along
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.45f, beamWidth * 0.45f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos,
                    dir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(2.5f, 7f),
                    Color.Lerp(MLordDirector.Phantasmal, MLordDirector.MoonWhite, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.55f, 1f))?.Configure(false, Main.rand.Next(14, 22));
            }
            //口部向心聚流
            if (Main.rand.NextBool(3)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                PRTLoader.NewParticle<PRT_HeavenfallStar>(gatherPos, (Projectile.Center - gatherPos) * 0.1f,
                    MLordDirector.DeepViolet, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 15);
            }
        }

        public override bool? CanDamage() => Timer > ExpandTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * BeamLength,
                beamWidth * 0.6f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation, BeamLength, beamWidth,
                MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f), Projectile.whoAmI * 0.211f % 1f);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            MLordRayRender.DrawMuzzle(Projectile.Center, beamWidth / MaxWidth,
                MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f), additiveBatch: true);
        }
    }
}
