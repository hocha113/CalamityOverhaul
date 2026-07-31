using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>
    /// 鬼切教程专用练习鬼影 NPC。
    /// 伤害 0、无掉落、不可自然生成；死亡时复位生命而非真正死亡。
    /// 按教程阶段切换 Pose（站桩/疾走通道/面影错位）。
    /// 完整实现见 tutorial-target TODO；此文件提供编译所需的静态方法骨架。
    /// </summary>
    internal sealed class OnikiriTutorialWraith : ModNPC
    {
        //====服务端目标注册表（playerIndex → npcIndex）====
        private static readonly Dictionary<int, int> serverTargets = [];

        //====本地客户端追踪====
        private static int localNpcIndex = -1;

        //====Pose 枚举====
        internal enum WraithPose : byte
        {
            Idle = 0,
            DashChannel = 1,     //疾走通道：略偏，让玩家有穿身空间
            PaperOffset = 2,     //面影错位：移动到另一侧
        }

        internal WraithPose CurrentPose { get; private set; } = WraithPose.Idle;

        //====ModNPC 基础设置====

        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults()
        {
            NPC.width = 44;
            NPC.height = 88;
            NPC.aiStyle = -1;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 99999;
            NPC.HitSound = Terraria.ID.SoundID.NPCHit1;
            NPC.DeathSound = Terraria.ID.SoundID.NPCDeath1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.knockBackResist = 0f;
            NPC.dontTakeDamage = false;
        }

        public override void SetStaticDefaults()
        {
            //不登录战旗/图鉴/试炼
            Terraria.ID.NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Hide = true };
            Terraria.ID.NPCID.Sets.NPCBestiaryDrawOffset[Type] = drawModifiers;
        }

        public override bool CheckDead()
        {
            //死亡时复位生命而非真正死亡
            NPC.life = NPC.lifeMax;
            NPC.active = true;
            return false;
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
            => NPC.CanBeChasedBy(projectile) ? (bool?)null : false;

        //====静态 API（供 OnikiriTutorialNet 和 OnikiriTutorialFlow 调用）====

        internal static void EnsureLocalTarget()
        {
            if (localNpcIndex >= 0 && localNpcIndex < Main.maxNPCs && Main.npc[localNpcIndex].active
                && Main.npc[localNpcIndex].type == ModContent.NPCType<OnikiriTutorialWraith>())
                return;

            Player p = Main.LocalPlayer;
            Vector2 spawnPos = p.Center + new Vector2(p.direction * 180f, -48f);
            localNpcIndex = NPC.NewNPC(p.GetSource_Misc("CWR_OnikiriTutorial"),
                (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<OnikiriTutorialWraith>(),
                ai0: p.whoAmI);
        }

        internal static void ReleaseLocalTarget()
        {
            if (localNpcIndex >= 0 && localNpcIndex < Main.maxNPCs)
            {
                NPC npc = Main.npc[localNpcIndex];
                if (npc.active && npc.type == ModContent.NPCType<OnikiriTutorialWraith>())
                    npc.active = false;
            }
            localNpcIndex = -1;
        }

        internal static int EnsureServerTarget(int playerWhoAmI)
        {
            if (!VaultUtils.isServer) return -1;
            if (serverTargets.TryGetValue(playerWhoAmI, out int existing)
                && existing >= 0 && existing < Main.maxNPCs
                && Main.npc[existing].active
                && Main.npc[existing].type == ModContent.NPCType<OnikiriTutorialWraith>())
                return existing;

            Player p = playerWhoAmI >= 0 && playerWhoAmI < Main.maxPlayers
                ? Main.player[playerWhoAmI] : null;
            if (p == null || !p.active) return -1;

            Vector2 spawnPos = p.Center + new Vector2(p.direction * 180f, -48f);
            int idx = NPC.NewNPC(p.GetSource_Misc("CWR_OnikiriTutorial"),
                (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<OnikiriTutorialWraith>(),
                ai0: playerWhoAmI);
            serverTargets[playerWhoAmI] = idx;
            return idx;
        }

        internal static void ReleaseServerTarget(int playerWhoAmI)
        {
            if (!VaultUtils.isServer) return;
            if (!serverTargets.TryGetValue(playerWhoAmI, out int idx)) return;
            serverTargets.Remove(playerWhoAmI);
            if (idx >= 0 && idx < Main.maxNPCs)
            {
                NPC npc = Main.npc[idx];
                if (npc.active && npc.type == ModContent.NPCType<OnikiriTutorialWraith>())
                    npc.active = false;
            }
        }

        internal static void OnServerTargetConfirmed(int npcIndex)
            => localNpcIndex = npcIndex;

        internal static void OnServerTargetReleased()
            => localNpcIndex = -1;

        internal static void OnPoseSynced(byte pose)
        {
            if (localNpcIndex < 0 || localNpcIndex >= Main.maxNPCs) return;
            NPC npc = Main.npc[localNpcIndex];
            if (npc.active && npc.ModNPC is OnikiriTutorialWraith w)
                w.CurrentPose = (WraithPose)pose;
        }

        /// <summary>本地客户端当前追踪的练习鬼影；无效返回 null</summary>
        internal static NPC GetLocalTarget()
        {
            if (localNpcIndex < 0 || localNpcIndex >= Main.maxNPCs) return null;
            NPC npc = Main.npc[localNpcIndex];
            return npc.active && npc.type == ModContent.NPCType<OnikiriTutorialWraith>() ? npc : null;
        }

        /// <summary>世界切换时清理服务端状态</summary>
        internal static void ClearServerState()
        {
            serverTargets.Clear();
            localNpcIndex = -1;
        }
    }
}