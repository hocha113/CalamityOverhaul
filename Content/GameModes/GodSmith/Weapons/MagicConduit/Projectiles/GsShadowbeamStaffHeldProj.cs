using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 影束折线通道（暗影束法杖全接管，原版瞬发反射束通道化）。
    /// 镜像 GsHeatBeamProj 的束端部收口纪律，但束是折线：逐段 LaserScan 探墙，
    /// 入射角反射角（法线用命中面 X/Y 近似），常态至多 3 段，白热 +1 段且束宽 ×1.4。<br/>
    /// 折线全部由同步瞄准方向 + 各端一致的 tile 数据确定性重建，不入包；
    /// 判定沿全部折线段（逐段 AABB 对线段），拐点各有暗紫辉结与影蚀残渣
    /// </summary>
    internal class GsShadowbeamStaffHeldProj : GsConduitHeldProj
    {
        internal static readonly Color ShadowBright = new(214, 150, 255);
        internal static readonly Color ShadowMain = new(140, 60, 210);
        internal static readonly Color ShadowDeep = new(52, 18, 96);

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

            //沿折线光照（各端）
            for (int i = 0; i < NodeCount - 1; i++) {
                Lighting.AddLight(Vector2.Lerp(Nodes[i], Nodes[i + 1], 0.5f), ShadowMain.ToVector3() * 0.35f);
            }

            if (VaultUtils.isServer || collapse01 > 0.5f) {
                return;
            }
            //拐点影蚀残渣：白热更密（拐点是折线的物理答案，收口演出钉在拐点上）
            if (Main.GameUpdateCount % (HeatStageSync >= 1 ? 2 : 4) == 0 && NodeCount > 2) {
                int bend = 1 + (int)(Main.GameUpdateCount / 4 % (NodeCount - 2));
                PRTLoader.NewParticle<PRT_Light>(Nodes[bend] + Main.rand.NextVector2Circular(5f, 5f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f), ShadowMain,
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(Main.rand.Next(14, 24), 0.7f);
            }
            //落点暗尘
            if (Main.GameUpdateCount % 3 == 0) {
                Vector2 impact = Nodes[NodeCount - 1];
                PRTLoader.NewParticle<PRT_Light>(impact + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(0.8f, 0.8f), ShadowBright,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(10, 18), 0.65f);
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
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, ShadowBright, 0.1f)?.Configure(7, 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float vis = VisWidth(lastCollapse01);
            if (vis < 0.8f || NodeCount < 2) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            bool whiteHot = HeatStageSync >= 1;
            float flick = 1f + 0.07f * MathF.Sin(Main.GlobalTimeWrappedHourly * 37f + Projectile.identity * 0.66f);

            //逐段三层影束：越折越暗（衰减 0.85/段）
            float alpha = 1f;
            for (int i = 0; i < NodeCount - 1; i++) {
                Vector2 a = Nodes[i];
                Vector2 b = Nodes[i + 1];
                float len = Vector2.Distance(a, b);
                GsConduitVFX.DrawBeam(sb, a, (b - a).ToRotation(), len, vis * flick,
                    ShadowMain, whiteHot ? ShadowBright : Color.Lerp(ShadowMain, ShadowBright, 0.5f), alpha);
                alpha *= 0.85f;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float scale = vis / 52f;
            //源头辉光
            sb.Draw(glow, Nodes[0] - Main.screenPosition, null, ShadowMain with { A = 0 } * 0.85f, 0f,
                glow.Size() / 2f, scale * 1.8f * flick, SpriteEffects.None, 0f);
            sb.Draw(star, Nodes[0] - Main.screenPosition, null, ShadowBright with { A = 0 } * 0.7f,
                Main.GlobalTimeWrappedHourly * 2.7f, star.Size() / 2f, scale * 0.45f, SpriteEffects.None, 0f);
            //拐点辉结 + 落点收口
            for (int i = 1; i < NodeCount; i++) {
                bool tail = i == NodeCount - 1;
                sb.Draw(glow, Nodes[i] - Main.screenPosition, null,
                    (tail ? ShadowBright : ShadowDeep) with { A = 0 } * (tail ? 0.8f : 0.65f) * flick, 0f,
                    glow.Size() / 2f, scale * (tail ? 1.2f : 0.95f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
