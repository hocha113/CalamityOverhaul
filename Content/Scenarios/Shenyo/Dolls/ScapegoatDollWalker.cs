using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Dolls
{
    /// <summary>
    /// 替死娃娃的遛弯演出:从玩家身上蹦出,贴地跟着走一段,再一跃跳回玩家身上。
    /// 纯装饰(零伤害),owner 端生成走原版同步;各端用玩家位置各自积分,漂移无碍
    /// </summary>
    internal class ScapegoatDollWalker : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.GuideVoodooDoll;

        //运动口径与伞奴(KikasaThrallProj)一致
        private const float WalkMaxSpeed = 1.6f;
        private const float WalkAccel = 0.08f;
        private const float Gravity = 0.35f;
        private const float MaxFallSpeed = 10f;
        /// <summary>跟随时与玩家保持的横向身位</summary>
        private const float FollowGap = 46f;

        private const int EmergeFrames = 30;
        private const int WalkFrames = 420;
        private const int ReturnTimeout = 150;
        private const float ReturnCatchDist = 30f;
        /// <summary>玩家甩开这个距离就提前跳回</summary>
        private const float StrayReturnDist = 520f;
        private const float DespawnDist = 1400f;

        private const int PhaseEmerge = 0;
        private const int PhaseWalk = 1;
        private const int PhaseReturn = 2;

        private int phase;
        private int timer;
        private float side;
        private bool facingLeft;
        private float waddlePhase;

        private Player Owner => Main.player[Projectile.owner];

        private bool Grounded => Projectile.velocity.Y == 0f;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = EmergeFrames + WalkFrames + ReturnTimeout + 120;
        }

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            float dist = Projectile.Distance(owner.Center);
            if (dist > DespawnDist) {
                Projectile.Kill();
                return;
            }

            timer++;
            switch (phase) {
                case PhaseEmerge:
                    UpdateEmerge(owner);
                    break;
                case PhaseWalk:
                    UpdateWalk(owner, dist);
                    break;
                default:
                    UpdateReturn(owner, dist);
                    break;
            }

            UpdateFacingAndGait();
        }

        private void UpdateEmerge(Player owner) {
            //出膛小弧线:保横速,落地即转行走
            WalkIntegrate(Projectile.velocity.X, keepMomentum: true);
            if (timer >= 8 && (Grounded || timer >= EmergeFrames)) {
                side = Math.Sign(Projectile.Center.X - owner.Center.X);
                if (side == 0f) {
                    side = 1f;
                }
                EnterPhase(PhaseWalk);
            }
        }

        private void UpdateWalk(Player owner, float dist) {
            Vector2 goal = owner.Center + new Vector2(side * FollowGap, 0f);
            float dx = goal.X - Projectile.Center.X;
            float desired = MathHelper.Clamp(dx * 0.08f, -WalkMaxSpeed, WalkMaxSpeed);
            WalkIntegrate(desired);

            if (Grounded) {
                bool blocked = Math.Abs(Projectile.velocity.X) < 0.25f && Math.Abs(dx) > 48f;
                if (blocked) {
                    Projectile.velocity.Y = -6.2f;   //越障跳
                }
                else if (Math.Abs(dx) < 60f && timer % 132 == 66) {
                    Projectile.velocity.Y = -3.4f;   //跟上了就原地蹦一下
                }
            }

            if (timer >= WalkFrames || dist > StrayReturnDist) {
                Projectile.velocity.Y = -6.6f;       //起跳回身
                EnterPhase(PhaseReturn);
            }
        }

        private void UpdateReturn(Player owner, float dist) {
            //鬼气一跃:无视地形,弧线渐拢直线归位
            float speed = MathHelper.Lerp(4f, 11f, Math.Min(timer / 40f, 1f));
            Vector2 dir = (owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * speed, 0.2f);

            if (dist < ReturnCatchDist || timer > ReturnTimeout) {
                ReturnPoof();
                Projectile.Kill();
            }
        }

        private void EnterPhase(int next) {
            phase = next;
            timer = 0;
        }

        /// <summary>贴地积分,与伞奴同口径:台阶蹭上 → 物块裁剪 → 斜坡贴合</summary>
        private void WalkIntegrate(float desiredX, bool keepMomentum = false) {
            if (!keepMomentum) {
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, desiredX, WalkAccel);
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFallSpeed);

            Vector2 position = Projectile.position;
            Vector2 velocity = Projectile.velocity;
            if (velocity.Y >= 0f) {
                float stepSpeed = 1f;
                float gfxOffY = 0f;
                Collision.StepUp(ref position, ref velocity, Projectile.width, Projectile.height,
                    ref stepSpeed, ref gfxOffY, 1, false, 1);
                Projectile.position = position;
            }
            velocity = Collision.TileCollision(Projectile.position, velocity,
                Projectile.width, Projectile.height);
            Vector4 slope = Collision.SlopeCollision(Projectile.position, velocity,
                Projectile.width, Projectile.height, Gravity, false);
            Projectile.position = new Vector2(slope.X, slope.Y);
            Projectile.velocity = new Vector2(slope.Z, slope.W);
        }

        private void UpdateFacingAndGait() {
            if (Math.Abs(Projectile.velocity.X) > 0.15f) {
                facingLeft = Projectile.velocity.X < 0f;
            }

            if (phase == PhaseWalk && Grounded) {
                //走路左右摇,幅度随步速
                waddlePhase += MathHelper.Clamp(Math.Abs(Projectile.velocity.X), 0.35f, 2f) * 0.16f;
                Projectile.rotation = MathF.Sin(waddlePhase) * 0.17f;
            }
            else {
                //空中顺着横速歪头
                Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.06f, -0.5f, 0.5f);
            }
        }

        private void ReturnPoof() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                    Main.rand.NextVector2Circular(2.2f, 2.2f), 120, default, Main.rand.NextFloat(0.7f, 1.2f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            //以脚底为轴心摇摆,布偶蹒跚感
            Vector2 origin = new(tex.Width * 0.5f, tex.Height - 2f);
            Vector2 drawPos = Projectile.Bottom - Main.screenPosition + new Vector2(0f, 2f);
            SpriteEffects fx = facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, Projectile.rotation, origin, 1f, fx);
            return false;
        }
    }
}
