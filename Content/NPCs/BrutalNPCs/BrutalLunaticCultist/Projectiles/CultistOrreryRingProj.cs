using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 离体仪环:浑天仪环拆下当回旋刃掷出<br/>
    /// ai[0]=环序 0~2 ai[1]=宿主npc ai[2]=段(取绝对值) 0瞄准(侧立刃线=预告) 1掷出 ±2回旋(符号=旋向) 3回收<br/>
    /// 回旋段绕定圆扫一轮,扫满 2π 断环回收<br/>
    /// 预告即承诺:瞄准段末锁死方向;伤害窗=速度窗(|v|&gt;7),仅瞄准段无害<br/>
    /// 公平阀:回旋圆心即安全眼(眼半径≈回旋半径-刃宽);回收限转率导引+捕获扩张+超时自散,不存在永旋
    /// </summary>
    internal class CultistOrreryRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int AimFrames = 22;
        private const float LaunchSpeed = 25f;
        /// <summary>伤害速度阈:低于此速读作收势,无害</summary>
        private const float DamageSpeed = 7f;
        /// <summary>回旋巡航速度(px/f);圆半径=速度/角速度,半径由下方常量单独声明(19→23.75,+25% 调参)</summary>
        private const float LoopSpeed = 23.75f;
        /// <summary>回旋半径基准(px),按环序加档错圈</summary>
        private const float LoopRadiusBase = 240f;
        private const float LoopRadiusStep = 25f;
        /// <summary>回旋总角程(一轮 2π),扫满即断环回收</summary>
        private const float LoopTotalSweep = MathHelper.TwoPi;

        private int RingIndex => (int)Projectile.ai[0];
        private int OwnerWho => (int)Projectile.ai[1];
        /// <summary>段号(ai[2] 绝对值;回旋段用符号载旋向)</summary>
        private int Stage => Math.Abs((int)Projectile.ai[2]);
        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>进动相位(本地演出量)</summary>
        private ref float Precession => ref Projectile.localAI[1];

        private float RingRadius => CultistOrreryRig.RingRadius[Math.Clamp(RingIndex, 0, 2)];

        private int lastSeenStage = -1;
        /// <summary>回旋累计角程(各端本地,段拍清零)</summary>
        private float loopSwept;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            bool ownerAlive = owner != null && owner.active && owner.type == NPCID.CultistBoss;
            if (!ownerAlive) {
                //宿主没了:散成符文
                CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(PaletteOf(owner)), 8, 5f);
                Projectile.Kill();
                return;
            }

            //段切换的各端本地拍点(ai[2] 已同步);计时/角程是本地量,在此对齐,不依赖权威端的 localAI
            if (Stage != lastSeenStage) {
                Timer = 0f;
                loopSwept = 0f;
                OnStageBeat(owner);
                lastSeenStage = Stage;
            }

            switch (Stage) {
                case 0: {
                    //瞄准:随宿主,侧立成刃线,进动锁 0
                    Precession = MathHelper.Lerp(Precession, 0f, 0.2f);
                    Vector2 aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    Projectile.Center = owner.Center + aim * (44f + RingIndex * 10f);
                    if (!VaultUtils.isClient && Timer >= AimFrames) {
                        //掷出:一帧满速,方向即锁死的瞄准向
                        Projectile.ai[2] = 1;
                        Projectile.velocity = aim * LaunchSpeed;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                case 1: {
                    //掷出:近直线+复利续力,进动醒来(刃面翻滚)
                    Precession += 0.16f + Timer * 0.002f;
                    if (Projectile.velocity.Length() < 34f) {
                        Projectile.velocity *= 1.012f;
                    }
                    if (!VaultUtils.isClient && (Timer >= 30 || Projectile.Center.Distance(owner.Center) > 760f)) {
                        //旋向抉择(权威端一次定死,符号随 ai[2] 同步):朝目标所在侧盘旋,圆带扫回玩家区
                        Player target = owner.HasValidTarget ? Main.player[owner.target] : null;
                        Vector2 toRef = (target != null && target.Alives() ? target.Center : owner.Center) - Projectile.Center;
                        float crossZ = Projectile.velocity.X * toRef.Y - Projectile.velocity.Y * toRef.X;
                        Projectile.ai[2] = crossZ >= 0f ? 2f : -2f;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                case 2: {
                    //回旋一轮:每帧按 角速度=速度/目标半径 旋进速度向,路径即定半径圆(各端由同步速度确定性重演)
                    Precession += 0.20f;
                    float radius = LoopRadiusBase + RingIndex * LoopRadiusStep;
                    float speed = MathHelper.Lerp(Projectile.velocity.Length(), LoopSpeed, 0.16f);
                    float omega = speed / radius;
                    float sign = Projectile.ai[2] < 0f ? -1f : 1f;
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(omega * sign) * speed;
                    loopSwept += omega;
                    //扫满一轮断环回收,计时仅作兜底
                    if (!VaultUtils.isClient && (loopSwept >= LoopTotalSweep || Timer >= 120f)) {
                        Projectile.ai[2] = 3f;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                default: {
                    //回收:限转率导引扑回;转率/速度随时爬升+捕获半径扩张+超时自散,杜绝绕宿主永旋
                    Precession += 0.22f;
                    Vector2 toOwner = owner.Center - Projectile.Center;
                    if (toOwner.Length() < 70f + Timer * 1.2f) {
                        //归位:接环拍
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.5f, Pitch = 0.4f + RingIndex * 0.15f }, owner.Center);
                        }
                        CultistMotion.RuneBurst(owner.Center, CultistMotion.PhaseCore(PaletteOf(owner)), 6, 4f);
                        Projectile.Kill();
                        return;
                    }
                    if (Timer >= 140f) {
                        //兜底:导引超时就地散成符文,不许赖场
                        CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(PaletteOf(owner)), 8, 5f);
                        Projectile.Kill();
                        return;
                    }
                    float speed = MathHelper.Min(16f + Timer * 0.5f, 30f);
                    float maxTurn = 0.06f + Timer * 0.004f;
                    float heading = Projectile.velocity.ToRotation().AngleTowards(toOwner.ToRotation(), maxTurn);
                    Projectile.velocity = heading.ToRotationVector2() * speed;
                    break;
                }
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(PaletteOf(owner)).ToVector3() * 0.5f);
        }

        /// <summary>段切换演出(各端本地)</summary>
        private void OnStageBeat(NPC owner) {
            int palette = PaletteOf(owner);
            switch (Stage) {
                case 1:
                    //掷出拍:一帧内爆发
                    CultistMotion.Shake(Projectile.Center, 5f, 10, Projectile.velocity);
                    CultistMotion.CastFlash(Projectile.Center, CultistMotion.PhaseCore(palette), 1.1f);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.9f, Pitch = -0.3f + RingIndex * 0.12f }, Projectile.Center);
                    }
                    break;
                case 2:
                    //入圈拍:回旋开始的清音
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
                    }
                    break;
            }
        }

        private static int PaletteOf(NPC owner) => owner != null && owner.active ? (int)owner.ai[0] : 0;

        /// <summary>速度窗:掷出/回旋/回收咬人,仅瞄准段无害</summary>
        public override bool CanHitPlayer(Player target) {
            return Stage >= 1 && Projectile.velocity.Length() > DamageSpeed;
        }

        /// <summary>胶囊判定:沿投影主轴(=飞行向)的刃线</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Stage < 1) {
                return false;
            }
            Vector2 axis = Projectile.velocity.SafeNormalize(Vector2.UnitX) * RingRadius * 0.92f;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - axis, Projectile.Center + axis, 30f, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int palette = PaletteOf(owner);
            Color mid = CultistMotion.PhaseCore(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Vector2 flightDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float spin = Main.GlobalTimeWrappedHourly * 1.6f + RingIndex * 0.7f;
            float charge = Stage >= 1 ? 1f : 0.55f;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            //旋转涂抹:回溯位置重画残环(自转的表达=转体残影)
            if (Stage >= 1) {
                for (int i = 4; i >= 2; i -= 2) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghost = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    CultistOrreryRig.GetHurlBasis(flightDir, Precession - i * 0.16f, out Vector3 ge1, out Vector3 ge2);
                    CultistOrreryRenderer.DrawRing(ghost, ge1, ge2, RingRadius, CultistOrreryRig.RingWidth[RingIndex],
                        spin - i * 0.05f, mid, bright, charge, 0.16f * (5 - i) * 0.5f, RingIndex * 0.37f + 0.11f, 0);
                }
            }

            CultistOrreryRig.GetHurlBasis(flightDir, Precession, out Vector3 e1, out Vector3 e2);
            CultistOrreryRenderer.DrawRing(Projectile.Center, e1, e2, RingRadius,
                CultistOrreryRig.RingWidth[RingIndex] * 1.2f, spin, mid, bright, charge, 1f,
                RingIndex * 0.37f + 0.11f, 0);

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //瞄准段:刃线预告(锁向的可见承诺)
            if (Stage == 0) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float warn = MathHelper.Clamp(Timer / AimFrames, 0f, 1f);
                    Vector2 drawPos = Projectile.Center - Main.screenPosition;
                    Color glint = (bright with { A = 0 }) * (0.30f + 0.35f * warn);
                    sb.Draw(glow, drawPos + flightDir * 300f, null, glint, flightDir.ToRotation(),
                        glow.Size() * 0.5f, new Vector2(11f, 0.16f + warn * 0.1f), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
