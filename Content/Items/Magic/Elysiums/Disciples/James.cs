using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 雅各·雷霆之子(席位2)：雷霆审判。
    /// 冷却好时向最近的敌人劈下连锁圣雷，在至多四个目标间跳跃
    /// </summary>
    internal class James : BaseDisciple
    {
        public override int Seat => 2;

        private const float CastRange = 520f;

        protected override bool TryCast() => FindNearestEnemy() >= 0;

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.85f, Pitch = 0.25f }, Projectile.Center);
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int target = FindNearestEnemy();
            if (target < 0) {
                return;
            }
            int damage = (int)(ElysiumPlayer.GetElysiumDamage(Owner) * 0.85f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<JamesChainStrike>(), damage, 3f, Projectile.owner, target);
        }

        private int FindNearestEnemy() {
            int found = -1;
            float closest = CastRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closest) {
                    closest = dist;
                    found = i;
                }
            }
            return found;
        }
    }

    /// <summary>
    /// 雅各的连锁圣雷：从雅各劈向首个目标，再在邻近目标间跳跃(至多4跳)。
    /// 链路各端按同一贪心规则自建(视觉允许微分叉)，伤害仅主人端结算。
    /// ai[0]=首个目标索引
    /// </summary>
    internal class JamesChainStrike : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxJumps = 4;
        private const float JumpRange = 300f;
        private const int DamageWindow = 8;
        private const float HitWidth = 26f;

        private readonly List<Vector2> nodes = [];
        private bool built;
        private int Timer => 26 - Projectile.timeLeft;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 26;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (!built) {
                built = true;
                BuildChain();
            }
        }

        /// <summary>贪心链路：首靶由生成参数钉死，后续按最近未访问目标跳跃</summary>
        private void BuildChain() {
            nodes.Add(Projectile.Center);

            int first = (int)Projectile.ai[0];
            if (first < 0 || first >= Main.maxNPCs || !Main.npc[first].active) {
                Projectile.Kill();
                return;
            }

            Span<bool> visited = stackalloc bool[Main.maxNPCs];
            int current = first;
            for (int jump = 0; jump < MaxJumps && current >= 0; jump++) {
                NPC npc = Main.npc[current];
                nodes.Add(npc.Center);
                visited[current] = true;

                int next = -1;
                float closest = JumpRange;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC cand = Main.npc[i];
                    if (visited[i] || !cand.active || !cand.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    float dist = Vector2.Distance(cand.Center, npc.Center);
                    if (dist < closest) {
                        closest = dist;
                        next = i;
                    }
                }
                current = next;
            }

            //各端本地演出：逐段落雷 + 节点星迸
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.55f, Pitch = 0.5f }, nodes[^1]);
            if (Main.dedServ) {
                return;
            }
            Color boltColor = new(255, 240, 170);
            for (int i = 1; i < nodes.Count; i++) {
                PRTLoader.NewParticle<PRT_SkyBolt>(nodes[i], Vector2.Zero, boltColor, 0.8f)
                    ?.Configure(nodes[i - 1], nodes[i], 20);
                for (int s = 0; s < 4; s++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(nodes[i], VaultUtils.RandVr(2f, 5f)
                        , boltColor, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(10, 16));
                }
            }
        }

        /// <summary>沿链路整线判定，仅前几帧开窗</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer > DamageWindow || nodes.Count < 2) {
                return false;
            }
            float point = 0f;
            for (int i = 0; i < nodes.Count - 1; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , nodes[i], nodes[i + 1], HitWidth, ref point)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
