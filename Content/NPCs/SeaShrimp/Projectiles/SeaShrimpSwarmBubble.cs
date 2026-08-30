using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 群泡：合钳爆发的细小泡群一员。锥形涌向玩家，前 55f 带微弱追踪
    /// （每帧最多偏转 ~0.9°，横向拉开即甩掉）随后直飞；
    /// 命中/撞地/寿尽即破膜。水膜批绘复用，identity 定相位摆动。
    /// ai[0]=泡半径，ai[1]/ai[2] 未用
    /// </summary>
    internal class SeaShrimpSwarmBubble : SeaShrimpModProjectile, ISeaShrimpBubbleBody
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float Radius => Projectile.ai[0];

        /// <summary>破膜帧数</summary>
        private const int BurstFrames = 6;
        /// <summary>出生吹胀帧数</summary>
        private const int InflateFrames = 6;

        /// <summary>本地帧龄：逐端计数</summary>
        private int Age => (int)Projectile.localAI[0];
        /// <summary>破膜计数：0=完好，≥1=破膜第 n 帧</summary>
        private bool Bursting => Projectile.localAI[1] > 0;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 170;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            SeaShrimpBubbleRender.PresenceStamp.Stamp();

            if (Bursting) {
                Projectile.velocity = Vector2.Zero;
                Projectile.localAI[1]++;
                if (Projectile.localAI[1] > BurstFrames) {
                    Projectile.Kill();
                }
                return;
            }

            int age = Age;
            //微弱追踪：前 55f 向最近玩家缓转（各端一致输入），横向拉开即甩掉；之后直飞
            if (age < SeaShrimpDirector.SwarmHomingFrames) {
                Player target = FindNearestPlayer();
                if (target != null) {
                    float want = (target.Center - Projectile.Center).ToRotation();
                    float cur = Projectile.velocity.ToRotation();
                    float speed = Projectile.velocity.Length();
                    float turned = cur.AngleTowards(want, SeaShrimpDirector.SwarmHomingRate);
                    Projectile.velocity = turned.ToRotationVector2() * speed;
                }
            }
            //群感摆动：identity 定相位的轻微侧摆，泡团不走死直线
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perp
                * (MathF.Sin(age * 0.31f + Projectile.identity * 1.7f) * 0.045f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, 0.04f, 0.1f, 0.2f);

            //撞地/寿尽破膜（确定性输入）
            if (ShrimpTerrain.SolidAt(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * Radius)
                || Projectile.timeLeft <= BurstFrames + 2) {
                StartBurst();
            }
        }

        private Player FindNearestPlayer() {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float d = Vector2.DistanceSquared(player.Center, Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = player;
                }
            }
            return best;
        }

        private void StartBurst() {
            if (Bursting) {
                return;
            }
            Projectile.localAI[1] = 1f;
            SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 5 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                EverdeepVFX.ShedDroplet(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, Radius * 0.5f),
                    Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(0.6f, 1.8f), 0.7f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中即破：泡是消耗性威胁，不穿人二连
            StartBurst();
        }

        /// <summary>伤害窗=完整膜：吹胀成形前与破膜期无害</summary>
        public override bool? CanDamage() => Age > 5 && !Bursting ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= Radius;
        }

        bool ISeaShrimpBubbleBody.GetBubbleBody(out SeaShrimpBubbleBodyParams body) {
            body = new SeaShrimpBubbleBodyParams {
                Center = Projectile.Center,
                Radius = Radius * MathHelper.Clamp(Age / (float)InflateFrames, 0.3f, 1f),
                Wobble = 0.5f + MathHelper.Clamp(Projectile.velocity.Length() / 24f, 0f, 0.3f),
                Arm = 0.25f,
                Burst = Bursting ? Projectile.localAI[1] / BurstFrames : 0f,
                Fade = MathHelper.Clamp(Age / 4f, 0f, 1f),
                Seed = Projectile.identity,
            };
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            //泡体全部交给批绘层；着色器缺失时的回退也不值得为细泡开销
            return false;
        }
    }
}
