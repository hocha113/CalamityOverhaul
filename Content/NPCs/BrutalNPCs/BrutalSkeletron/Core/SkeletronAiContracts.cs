using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core
{
    /// <summary>宏观阶段，头 npc.ai[0]</summary>
    internal static class SkeletronPhase
    {
        /// <summary>刚生成，尚未初始化</summary>
        public const int Uninit = 0;
        /// <summary>诅咒仪式登场</summary>
        public const int Intro = 1;
        /// <summary>一阶段，双手健在</summary>
        public const int Bound = 2;
        /// <summary>二阶段，断手狂化</summary>
        public const int Unbound = 3;
        /// <summary>死亡演出</summary>
        public const int DeathShow = 4;
    }

    /// <summary>骷髅王 ai[] 槽位契约</summary>
    internal static class SkeletronAiSlots
    {
        /// <summary>头 ai[0] 宏观阶段 <see cref="SkeletronPhase"/></summary>
        public const int HeadPhase = 0;
        /// <summary>头 ai[1] 通用广播参数A（按状态语义复用；合掌拍捉=被抓玩家 whoAmI+1）</summary>
        public const int HeadParamA = 1;
        /// <summary>头 ai[2] 状态机槽 <see cref="SkeletronStateIndex"/></summary>
        public const int HeadStateSlot = 2;
        /// <summary>头 ai[3] 通用广播参数B（旋杀锁定角、合掌拍捉子相位等）</summary>
        public const int HeadParamB = 3;

        /// <summary>头 Override ai[0] 编队旋转时钟，各端确定性自增</summary>
        public const int OverrideOrbitClock = 0;

        /// <summary>手 ai[0] 侧 -1/1（原版骨臂绘制依赖，禁改语义）</summary>
        public const int HandSide = 0;
        /// <summary>手 ai[1] 头部 whoAmI（原版骨臂绘制依赖，禁改语义）</summary>
        public const int HandHeadIndex = 1;
        /// <summary>手 ai[2] 状态机槽 <see cref="SkeletronHandStateIndex"/></summary>
        public const int HandStateSlot = 2;
        /// <summary>手 ai[3] 备用</summary>
        public const int HandFree = 3;
    }

    /// <summary>跨类共享的骷髅王事实查询</summary>
    internal static class SkeletronFacts
    {
        /// <summary>统计从属于该头的存活手</summary>
        public static int CountHands(NPC head, out NPC left, out NPC right) {
            left = null;
            right = null;
            if (head == null) {
                return 0;
            }
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != NPCID.SkeletronHand) {
                    continue;
                }
                if ((int)npc.ai[SkeletronAiSlots.HandHeadIndex] != head.whoAmI) {
                    continue;
                }
                count++;
                if (npc.ai[SkeletronAiSlots.HandSide] < 0f) {
                    left = npc;
                }
                else {
                    right = npc;
                }
            }
            return count;
        }

        /// <summary>自世界坐标垂直向下扫掠地面，返回首个实心面顶部Y；失败返回 -1</summary>
        public static float FindGroundY(Vector2 worldPos, int maxTiles = 70) {
            int tileX = (int)(worldPos.X / 16f);
            int tileY = (int)(worldPos.Y / 16f);
            if (tileX < 5 || tileX > Main.maxTilesX - 5) {
                return -1f;
            }
            if (tileY < 5) {
                tileY = 5;
            }
            int end = System.Math.Min(tileY + maxTiles, Main.maxTilesY - 5);
            for (int y = tileY; y <= end; y++) {
                Terraria.Tile tile = Terraria.Framing.GetTileSafely(tileX, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return y * 16f;
                }
            }
            return -1f;
        }

        /// <summary>清空本Boss产出的敌对弹幕（转阶段/死亡公平阀）</summary>
        public static void ClearHostileProjectiles() {
            if (VaultUtils.isClient) {
                return;
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.hostile) {
                    continue;
                }
                if (proj.ModProjectile is Projectiles.SkeletronCursedSkull
                    or Projectiles.SkeletronBoneShard
                    or Projectiles.SkeletronBoneSpike
                    or Projectiles.SkeletronCurseWisp
                    or Projectiles.SkeletronGhostArmProj
                    or Projectiles.SkeletronOrbitSkull
                    or Projectiles.SkeletronBoneWheel) {
                    proj.Kill();
                }
            }
        }
    }
}
