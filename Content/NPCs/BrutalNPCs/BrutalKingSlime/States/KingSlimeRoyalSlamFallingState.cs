using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 皇家砸地——下坠阶段：高速下坠，落地后产生大型冲击波 + 多发横向皇冠光柱
    /// </summary>
    internal class KingSlimeRoyalSlamFallingState : KingSlimeStateBase
    {
        public override string StateName => "RoyalSlamFalling";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.RoyalSlamFalling;

        private const int RecoverDuration = 24;
        private bool landed;
        private int recoverTimer;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            //初速度向下
            npc.velocity = new Vector2(0f, 14f);
            //保持碰撞，遇到地面就触发砸地
            npc.noTileCollide = false;
            landed = false;
            recoverTimer = 0;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            if (!landed) {
                //空中：纵向加速 + 轻微锁定玩家水平位置
                npc.velocity.Y = MathHelper.Min(npc.velocity.Y + 2.0f, 38f);
                float aim = MathHelper.Clamp((player.Center.X - npc.Center.X) * 0.05f, -3f, 3f);
                npc.velocity.X = MathHelper.Lerp(npc.velocity.X, aim, 0.18f);

                //可视拉伸——纵向被压成"水滴"
                context.SquishY = -0.40f;
                context.SetChargeState(1, 1f);

                //尾部留迹
                if (!VaultUtils.isServer) {
                    Dust trail = Dust.NewDustDirect(npc.Center - new Vector2(8, 8) + Main.rand.NextVector2Circular(20, 4),
                        16, 16, DustID.RedTorch, 0, 0, 100, default, 1.6f);
                    trail.noGravity = true;
                    trail.velocity = -npc.velocity * 0.2f;
                }

                //落地
                if (npc.collideY && npc.velocity.Y >= 0f) {
                    landed = true;
                    npc.velocity = Vector2.Zero;
                    OnSlamLand(context);
                }

                Timer++;
                //超时保护：极端地形下避免无限下坠
                if (Timer > 240 && !landed) {
                    landed = true;
                    OnSlamLand(context);
                }
            }
            else {
                //砸地恢复阶段：可视回弹 + 短暂硬直
                npc.velocity.X *= 0.85f;
                float t = MathHelper.Clamp(recoverTimer / (float)RecoverDuration, 0f, 1f);
                context.SquishY = MathHelper.SmoothStep(0.42f, 0f, t);
                context.SetChargeState(1, MathHelper.Lerp(1f, 0f, t));

                recoverTimer++;
                if (recoverTimer >= RecoverDuration) {
                    return new KingSlimeHopState();
                }
            }
            return null;
        }

        private static void OnSlamLand(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            //整段震屏
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item62, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath6, npc.Center);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    npc.Bottom, Vector2.UnitY, 18f, 8f, 24, 1500f, "KingSlimeSlam"));
            }

            //大型皇室冲击波
            KingSlimeRenderHelper.DoLandingShockwave(npc, context, 1.6f);

            //横向飞溅小波——分两侧扩散
            if (!VaultUtils.isClient) {
                int slamProj = ModContent.ProjectileType<KingSlimeShockwaveProj>();
                int dmg = (int)(CWRRef.GetProjectileDamage(npc, ProjectileID.None) * 1.0f);
                if (dmg < 30) dmg = 30;

                //中央大圈
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom + new Vector2(0, -8),
                    Vector2.Zero, slamProj, dmg, 4f, Main.myPlayer, ai0: 1.0f);
                //左右两个稍小的扩散
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom + new Vector2(-90, -8),
                    Vector2.Zero, slamProj, dmg, 4f, Main.myPlayer, ai0: 0.7f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom + new Vector2(90, -8),
                    Vector2.Zero, slamProj, dmg, 4f, Main.myPlayer, ai0: 0.7f);
            }

            //落地后向两侧扩散血荆棘，玩家须跳跃或位移规避
            if (!VaultUtils.isClient) {
                int thornDmg = (int)(CWRRef.GetProjectileDamage(npc, ProjectileID.None) * 0.75f);
                if (thornDmg < 20) thornDmg = 20;
                int thornCount = 5;
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < thornCount; i++) {
                        float prog = (i + 1f) / thornCount;
                        float downRatio = MathHelper.Lerp(0.06f, 0.38f, prog);
                        float speed = MathHelper.Lerp(9f, 18f, prog);
                        Vector2 vel = new Vector2(side, downRatio).SafeNormalize(Vector2.Zero) * speed;
                        Projectile.NewProjectile(npc.GetSource_FromAI(),
                            npc.Bottom + new Vector2(side * 20f, -10f),
                            vel, ProjectileID.SharpTears, thornDmg, 2f, Main.myPlayer);
                    }
                }
            }
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.SquishY = 0f;
        }
    }
}
