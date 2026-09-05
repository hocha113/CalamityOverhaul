using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 影束折线通道（暗影束法杖全接管，原版瞬发反射束通道化）。
    /// 镜像 GsHeatBeamProj 的束端部收口纪律，但束是折线：逐段 LaserScan 探墙，
    /// 入射角反射角（法线用命中面 X/Y 近似），常态至多 3 段，白热 +1 段且束宽 ×1.4。<br/>
    /// 折线全部由同步瞄准方向 + 各端一致的 tile 数据确定性重建，不入包；
    /// 判定沿全部折线段（逐段 AABB 对线段）
    /// </summary>
    internal class GsShadowbeamStaffHeldProj : GsConduitHeldProj
    {
        private const float TotalBudget = 1500f;
        private const float BaseWidth = 16f;
        private const float WhiteHotWidthMult = 1.4f;
        private const int GrowTicks = 6;
        private const int MaxNodes = 6;

        public override string LocalizationCategory => "GodSmithMagicConduit";

        protected override int BoundItemID => ItemID.ShadowbeamStaff;
        protected override float ManaPerSecond => 6f;
        protected override float HeatPerTick => 0.9f;
        protected override int HitCooldown => 4;
        protected override float TickDamageCoef => 0.22f;
        protected override bool UseChannelFlag => false;//原版非 channel 物品，读 controlUseItem
        protected override float MuzzleOffset => 24f;

        /// <summary>折线节点（源头/各拐点/落点），每帧确定性重建</summary>
        internal readonly Vector2[] Nodes = new Vector2[MaxNodes];
        /// <summary>有效节点数（≥2）</summary>
        internal int NodeCount = 2;

        private float widthCur = BaseWidth;
        private float lastCollapse01;
        private readonly float[] laserSamples = new float[3];

        private float GrowProgress => MathHelper.Clamp(Projectile.localAI[1] / GrowTicks, 0f, 1f);

        private float VisWidth(float collapse01)
            => widthCur * VaultUtils.EaseOutCubic(GrowProgress) * (1f - VaultUtils.EaseInQuad(collapse01));

        /// <summary>白热 +1 段：常态 3 段（2 次反射）</summary>
        private int MaxSegments => HeatStageSync >= 1 ? 4 : 3;

        protected override void ChannelAI(float collapse01) {
            lastCollapse01 = collapse01;
            float targetWidth = HeatStageSync >= 1 ? BaseWidth * WhiteHotWidthMult : BaseWidth;
            widthCur = MathHelper.Lerp(widthCur, targetWidth, 0.12f);

            RebuildPolyline();

            if (Projectile.localAI[1] == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Center);
            }
        }

        /// <summary>
        /// 折线重建：逐段探墙 + 反射（输入是同步方向与各端一致的 tile，结果确定）
        /// </summary>
        private void RebuildPolyline() {
            Vector2 pos = Projectile.Center;
            Vector2 dir = AimUnit;
            float budget = TotalBudget;
            Nodes[0] = pos;
            NodeCount = 1;
            int segments = MaxSegments;
            for (int s = 0; s < segments && budget > 24f && NodeCount < MaxNodes; s++) {
                float segMax = budget;
                Collision.LaserScan(pos, dir, VisWidth(lastCollapse01) * 0.5f, segMax, laserSamples);
                float len = (laserSamples[0] + laserSamples[1] + laserSamples[2]) / 3f;
                pos += dir * len;
                budget -= len;
                Nodes[NodeCount++] = pos;
                //没打到墙（本段扫满预算）就不再反射
                if (len >= segMax - 1f || budget <= 24f) {
                    break;
                }
                dir = ReflectAt(pos, dir);
            }
            if (NodeCount < 2) {
                Nodes[1] = pos + dir * 24f;
                NodeCount = 2;
            }
        }

        /// <summary>命中面法线近似：探拐点两侧 X/Y 向的实心格，翻转对应分量</summary>
        private static Vector2 ReflectAt(Vector2 hit, Vector2 dir) {
            bool solidX = SolidAt(hit + new Vector2(MathF.Sign(dir.X) * 12f, 0f));
            bool solidY = SolidAt(hit + new Vector2(0f, MathF.Sign(dir.Y) * 12f));
            if (solidX && !solidY) {
                return new Vector2(-dir.X, dir.Y);
            }
            if (solidY && !solidX) {
                return new Vector2(dir.X, -dir.Y);
            }
            return -dir;
        }

        private static bool SolidAt(Vector2 world) {
            int tx = (int)(world.X / 16f);
            int ty = (int)(world.Y / 16f);
            if (!WorldGen.InWorld(tx, ty, 10)) {
                return false;
            }
            Tile tile = Framing.GetTileSafely(tx, ty);
            return tile.HasUnactuatedTile && Main.tileSolid[tile.TileType];
        }

        protected override bool? DamageGate() => GrowProgress >= 0.4f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            float w = VisWidth(lastCollapse01) * 0.7f;
            for (int i = 0; i < NodeCount - 1; i++) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Nodes[i], Nodes[i + 1], w, ref point)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            //先画杖体（法杖斜握），影束压在其上
            DrawWeaponBody();
            float vis = VisWidth(lastCollapse01);
            if (vis < 0.8f || NodeCount < 2) {
                return false;
            }
            //逐段一笔拉伸原版束条（原版暗影束贴图是 1×1 尘粒占位，借用棱镜束条）
            for (int i = 0; i < NodeCount - 1; i++) {
                Vector2 a = Nodes[i];
                Vector2 b = Nodes[i + 1];
                DrawBeamStrip(ProjectileID.LastPrismLaser, a, b - a, Vector2.Distance(a, b), vis, lightColor);
            }
            return false;
        }
    }
}
