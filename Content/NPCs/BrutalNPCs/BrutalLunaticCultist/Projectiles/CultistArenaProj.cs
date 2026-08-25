using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 限制圈:教徒画下的法阵外环留在场上成为战斗边界,同时是仪式充能表(uFill 读 owner.ai[3])<br/>
    /// ai[0]=宿主npc ai[1]=阶段 0展开 1常驻 2收拢<br/>
    /// 推回只作用于本机玩家(玩家物理本地权威),无伤害,软墙
    /// </summary>
    internal class CultistArenaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GrowFrames = 70;

        private int OwnerWho => (int)Projectile.ai[0];
        private int Stage => (int)Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>当前可见半径</summary>
        private ref float Radius => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Timer++;
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            bool ownerAlive = owner != null && owner.active && owner.type == NPCID.CultistBoss;

            if (!ownerAlive && Stage != 2) {
                Projectile.ai[1] = 2;
                Timer = 0;
            }

            switch (Stage) {
                case 0: {
                    float t = MathHelper.Clamp(Timer / GrowFrames, 0f, 1f);
                    Radius = CultistStateContext.ArenaRadius * (1f - (1f - t) * (1f - t));
                    if (Timer >= GrowFrames && !VaultUtils.isClient) {
                        Projectile.ai[1] = 1;
                        Projectile.netUpdate = true;
                    }
                    break;
                }
                case 1:
                    Radius = CultistStateContext.ArenaRadius;
                    break;
                default:
                    Radius *= 0.94f;
                    if (Radius < 40f) {
                        Projectile.Kill();
                        return;
                    }
                    break;
            }
            if (Projectile.timeLeft < 120 && Stage != 2) {
                Projectile.timeLeft = 120;
            }

            PushLocalPlayerInside();
        }

        /// <summary>软墙:出界的本机玩家被持续推回,越远推力越大</summary>
        private void PushLocalPlayerInside() {
            if (Main.dedServ || Radius < 200f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (!player.Alives()) {
                return;
            }
            Vector2 delta = player.Center - Projectile.Center;
            float dist = delta.Length();
            float wall = Radius - 30f;
            if (dist <= wall) {
                return;
            }
            Vector2 inward = (-delta).SafeNormalize(Vector2.UnitY);
            float overshoot = dist - wall;
            player.velocity += inward * MathHelper.Clamp(0.55f + overshoot * 0.02f, 0.55f, 3.2f);
            //撞墙反馈:轻符文散射
            if (Main.GameUpdateCount % 10 == 0) {
                int ownerPhase = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? (int)Main.npc[OwnerWho].ai[0] : 0;
                CultistMotion.RuneBurst(player.Center + inward * -20f, CultistMotion.PhaseCore(ownerPhase), 1, 2f);
            }
        }

        /// <summary>命令收拢(权威端)</summary>
        internal static void BeginCollapse(int ownerWho) {
            int type = ModContent.ProjectileType<CultistArenaProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho && (int)proj.ai[1] != 2) {
                    proj.ai[1] = 2;
                    proj.localAI[0] = 0f;
                    proj.netUpdate = true;
                }
            }
        }

        public override bool ShouldUpdatePosition() => false;

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Radius < 30f) {
                return false;
            }
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int phase = owner != null && owner.active ? (int)owner.ai[0] : 0;
            //仪式表:充能扇区直接读同步的 ai[3]
            float fill = owner != null && owner.active
                ? MathHelper.Clamp(owner.ai[3] / CultistStateContext.RitualMax, 0f, 1f) : 0f;
            float reveal = MathHelper.Clamp(Radius / CultistStateContext.ArenaRadius, 0f, 1f);

            CultistRenderHelper.DrawSigil(Main.spriteBatch, Projectile.Center, Radius,
                CultistMotion.PhaseCore(phase), reveal, fill > 0.95f ? 0.4f : 0f, fill, 0.42f);
            return false;
        }
    }
}
