using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Revelations
{
    /// <summary>
    /// 后三印审判：三个有名字的演出拍与一记终幕，各拍有自己的物理答案。
    /// 第五印·殉道者显圣：十一道殉道之灵自主人身上盘旋而出，银白圣环荡开；
    /// 第六印·天地震动：环阵落雷四起，大地持续震颤，金环再荡；
    /// 第七印·静默与号角：万籁俱寂一息，随后号角撕裂寂静，炽环吞没四野；
    /// 终幕·世界审判：白光吞屏，审判落于一切敌人，启示录随之落幕。
    /// 伤害逐拍升幅(经 ModifyHitNPC)，终幕后主人端收束启示录
    /// </summary>
    internal class RevelationSealJudgment : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //拍点(帧)
        private const int Seal5At = 10;
        private const int Seal6At = 76;
        private const int SilenceAt = 142;
        private const int Seal7At = 162;
        private const int FinaleAt = 218;
        private const int TotalLife = 268;

        //各拍半径与倍率
        private const float Seal5Radius = 520f;
        private const float Seal6Radius = 700f;
        private const float Seal7Radius = 900f;
        private const float FinaleRadius = 1300f;

        private int Timer => TotalLife - Projectile.timeLeft;
        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = Owner.Center;
            int t = Timer;

            switch (t) {
                case Seal5At:
                    BeatSeal5();
                    break;
                case Seal6At:
                    BeatSeal6();
                    break;
                case SilenceAt:
                    //静默拍：刻意无声无粒子，只留收缩的预兆环(绘制侧)
                    break;
                case Seal7At:
                    BeatSeal7();
                    break;
                case FinaleAt:
                    BeatFinale();
                    break;
            }

            //第六印期间大地持续震颤
            if (t is > Seal6At and < Seal6At + 50 && t % 8 == 0) {
                Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 2.2f);
            }

            //终幕后主人端收束启示录
            if (t == TotalLife - 4 && Projectile.IsOwnedByLocalPlayer()
                && Owner.TryGetModPlayer(out ElysiumPlayer ep)) {
                ep.DeactivateRevelation();
            }

            float glow = 0.6f + 0.4f * MathF.Sin(t * 0.2f);
            Lighting.AddLight(Owner.Center, glow, glow * 0.9f, glow * 0.7f);
        }

        /// <summary>第五印·殉道者显圣：十一道殉道之灵盘旋而出</summary>
        private void BeatSeal5() {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.1f, Pitch = -0.1f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.9f, Pitch = 0.2f }, Owner.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 11; i++) {
                float angle = MathHelper.TwoPi * i / 11f;
                Vector2 vel = angle.ToRotationVector2().RotatedBy(0.6f) * Main.rand.NextFloat(6f, 10f);
                PRTLoader.NewParticle<PRT_Light>(Owner.Center, vel, new Color(235, 240, 255)
                    , Main.rand.NextFloat(0.34f, 0.5f))?.Configure(Main.rand.Next(36, 54), 1f);
            }
        }

        /// <summary>第六印·天地震动：环阵落雷四起</summary>
        private void BeatSeal6() {
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.9f, Pitch = -0.1f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.3f }, Owner.Center);
            Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 5f);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + 0.3f;
                Vector2 strike = Owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(280f, 460f);
                PRTLoader.NewParticle<PRT_SkyBolt>(strike, Vector2.Zero, new Color(255, 232, 150), 0.85f)
                    ?.Configure(strike - new Vector2(Main.rand.NextFloat(-60f, 60f), 640f), strike, 24);
            }
        }

        /// <summary>第七印·静默与号角：寂静之后号角撕裂长空</summary>
        private void BeatSeal7() {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.3f, Pitch = -0.5f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 1.2f, Pitch = -0.3f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1.1f, Pitch = -0.4f }, Owner.Center);
            Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 6f);
            if (Main.dedServ) {
                return;
            }
            //号角光柱：八方放射
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                for (int j = 0; j < 4; j++) {
                    Vector2 pos = Owner.Center + angle.ToRotationVector2() * (60f + j * 80f);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, angle.ToRotationVector2() * (5f + j * 2f)
                        , new Color(255, 205, 130), Main.rand.NextFloat(0.8f, 1.3f))?.Configure(false, Main.rand.Next(18, 28));
                }
            }
        }

        /// <summary>终幕·世界审判</summary>
        private void BeatFinale() {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.5f, Pitch = -0.6f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 1.2f, Pitch = -0.4f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1.3f, Pitch = 0.1f }, Owner.Center);
            Owner.CWR().ScreenShakeValue = Math.Max(Owner.CWR().ScreenShakeValue, 9f);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Owner.Center
                    , angle.ToRotationVector2() * Main.rand.NextFloat(8f, 15f)
                    , new Color(255, 244, 210), Main.rand.NextFloat(0.9f, 1.5f))?.Configure(false, Main.rand.Next(20, 34));
            }
            for (int i = 0; i < 14; i++) {
                PRTLoader.NewParticle<PRT_Light>(Owner.Center + Main.rand.NextVector2Circular(300f, 200f)
                    , new Vector2(0f, -Main.rand.NextFloat(2f, 6f)), new Color(255, 240, 205)
                    , Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(30, 50), 0.95f);
            }
        }

        #region 判定
        private static bool InWindow(int t, int beatAt) => t >= beatAt && t <= beatAt + 10;

        /// <summary>当前拍的判定半径，无拍时0</summary>
        private float ActiveRadius {
            get {
                int t = Timer;
                if (InWindow(t, Seal5At)) {
                    return Seal5Radius;
                }
                if (InWindow(t, Seal6At)) {
                    return Seal6Radius;
                }
                if (InWindow(t, Seal7At)) {
                    return Seal7Radius;
                }
                if (InWindow(t, FinaleAt)) {
                    return FinaleRadius;
                }
                return 0f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = ActiveRadius;
            if (radius <= 0f) {
                return false;
            }
            Vector2 nearest = new(MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right)
                , MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(Projectile.Center, nearest) <= radius;
        }

        /// <summary>伤害逐拍升幅，终幕受死亡骑士放大</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int t = Timer;
            float mul = 0.9f;
            if (InWindow(t, Seal6At)) {
                mul = 1.2f;
            }
            else if (InWindow(t, Seal7At)) {
                mul = 1.6f;
            }
            else if (InWindow(t, FinaleAt)) {
                mul = 2.8f;
                if (Owner.TryGetModPlayer(out ElysiumPlayer ep) && ep.HasDeathHorseman) {
                    mul = 3.4f;
                }
            }
            modifiers.SourceDamage *= mul;
        }
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            if (canvas == null) {
                return false;
            }
            int t = Timer;

            //各拍冲击环(拍后26帧内荡开)
            DrawBeatRing(sb, t, Seal5At, Seal5Radius, new Color(240, 244, 255), new Color(200, 208, 235), new Color(90, 96, 130));
            DrawBeatRing(sb, t, Seal6At, Seal6Radius, new Color(255, 240, 190), new Color(250, 205, 100), new Color(140, 105, 45));
            DrawBeatRing(sb, t, Seal7At, Seal7Radius, new Color(255, 224, 190), new Color(245, 150, 85), new Color(140, 70, 40));
            DrawBeatRing(sb, t, FinaleAt, FinaleRadius, new Color(255, 250, 235), new Color(255, 226, 150), new Color(150, 120, 70));

            //静默拍：一圈细环向内收拢(预兆)
            if (t is >= SilenceAt and < Seal7At) {
                float prog = (t - SilenceAt) / (float)(Seal7At - SilenceAt);
                float radius = MathHelper.Lerp(Seal7Radius, 60f, VaultUtils.EaseInQuad(prog));
                ShockRingDraw.Draw(sb, Owner.Center, radius, 4f,
                    new Color(255, 236, 200), new Color(220, 180, 120), new Color(90, 70, 45),
                    0.5f + prog * 0.4f, timeSeed: Projectile.identity * 0.117f);
            }

            //终幕白光吞屏
            if (t >= FinaleAt && t < FinaleAt + 30) {
                float flash = 1f - (t - FinaleAt) / 30f;
                flash *= flash;
                sb.Draw(canvas, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight)
                    , new Rectangle(0, 0, 1, 1), Color.White * (flash * 0.9f));
            }
            return false;
        }

        private void DrawBeatRing(SpriteBatch sb, int t, int beatAt, float radius, Color bright, Color main, Color deep) {
            if (t < beatAt || t > beatAt + 26) {
                return;
            }
            float prog = (t - beatAt) / 26f;
            ShockRingDraw.Draw(sb, Owner.Center, MathHelper.Lerp(60f, radius + 40f, VaultUtils.EaseOutCubic(prog))
                , 12f, bright, main, deep, (1f - prog) * 0.9f, innerGlow: 0.25f
                , timeSeed: Projectile.identity * 0.153f + beatAt);
        }
        #endregion
    }
}
