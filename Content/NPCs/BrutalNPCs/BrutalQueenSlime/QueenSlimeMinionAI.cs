using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime
{
    /// <summary>皇后随从接管基类：ai[0]=角色 ai[1]=槽位 ai[2]=皇后whoAmI ai[3]=角色计时</summary>
    internal abstract class QueenMinionOverrideBase : BrutalNPCOverride
    {
        /// <summary>localAI[0] 管辖锁存：0未判 1本系统 2原版放行</summary>
        private const int LatchUnset = 0;
        private const int LatchManaged = 1;
        private const int LatchVanilla = 2;

        /// <summary>本随从对应角色</summary>
        protected abstract int Role { get; }

        public override bool? CanBrutalOverride() {
            return null;
        }

        protected NPC Queen {
            get {
                int idx = (int)npc.ai[2];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC queen = Main.npc[idx];
                return queen.active && queen.type == NPCID.QueenSlimeBoss ? queen : null;
            }
        }

        public sealed override bool AI() {
            //首帧锁存判定：角色标记+属主校验双条件
            if ((int)localAI[0] == LatchUnset) {
                bool managed = (int)npc.ai[0] == Role && Queen != null;
                localAI[0] = managed ? LatchManaged : LatchVanilla;
            }
            if ((int)localAI[0] == LatchVanilla) {
                return true;
            }

            //血上限各端确定性重建(SyncNPC不携带lifeMax，客户端血条会按原版150封顶)
            if ((int)localAI[2] == 0) {
                localAI[2] = 1;
                int want = ManagedLifeMax();
                npc.lifeMax = want;
                if (npc.life > want) {
                    npc.life = want;
                }
            }

            //属主失效：碎裂退场(服务端裁定，走原生同步死亡链让各端都有演出)
            if (Queen == null) {
                if (!VaultUtils.isClient) {
                    QueenMotion.ScriptKill(npc);
                }
                return false;
            }

            npc.timeLeft = 120;
            MinionAI();

            if (!VaultUtils.isClient && Main.GameUpdateCount % 20 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        /// <summary>角色AI，仅管辖状态调用</summary>
        protected abstract void MinionAI();

        /// <summary>管辖态血上限(须各端确定性一致)</summary>
        protected abstract int ManagedLifeMax();

        /// <summary>是否处于管辖状态(供绘制钩子)</summary>
        protected bool IsManaged => (int)localAI[0] == LatchManaged;

        /// <summary>死亡碎裂演出</summary>
        public override bool? SpecialOnKill() {
            if (IsManaged && !VaultUtils.isServer) {
                QueenMotion.CrystalShatterBurst(npc.Center, 1.05f, npc.whoAmI * 0.13f % 1f);
            }
            return null;
        }
    }

    /// <summary>棱晶节点(蓝·水晶史莱姆)：静锚悬浮的可破坏折射晶体</summary>
    internal class QueenPrismNodeAI : QueenMinionOverrideBase
    {
        public override int TargetID => NPCID.QueenSlimeMinionBlue;
        protected override int Role => QueenMinionRole.PrismNode;

        /// <summary>圣殿柱槽位偏移：ai[1]≥100为圣殿柱(更耐打)，排序不受影响</summary>
        internal const int CathedralSlotOffset = 100;

        /// <summary>节点血量(随难度)</summary>
        public static int PrismNodeLife(bool cathedral = false) {
            float baseLife = 560f;
            if (Main.masterMode) {
                baseLife *= 2.2f;
            }
            else if (Main.expertMode) {
                baseLife *= 1.65f;
            }
            if (cathedral) {
                baseLife *= 1.35f;
            }
            return (int)baseLife;
        }

        protected override int ManagedLifeMax() => PrismNodeLife((int)npc.ai[1] >= CathedralSlotOffset);

        protected override void MinionAI() {
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.knockBackResist = 0f;
            npc.damage = 0;
            //静锚：不移动(位置即锚点)，呼吸浮动交给绘制
            npc.velocity = Vector2.Zero;
            npc.rotation = 0f;
            //物化年龄(各端本地，纯视觉)
            localAI[1]++;
            //馈能视觉信号衰减(npc.localAI[3]由馈线光束逐帧写入，各端本地)
            npc.localAI[3] = Math.Max(0f, npc.localAI[3] - 0.045f);

            Lighting.AddLight(npc.Center, QueenMotion.CrystalBlue.ToVector3() * (0.55f + npc.localAI[3] * 0.35f));

            //寿命倒计时(服务端)，归零碎裂
            if (!VaultUtils.isClient && npc.ai[3] > 0f) {
                npc.ai[3]--;
                if (npc.ai[3] <= 0f) {
                    QueenMotion.ScriptKill(npc);
                }
            }

            //环境闪星(低频)
            if (!VaultUtils.isServer && Main.rand.NextBool(26)) {
                Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(30f, 34f),
                    DustID.TintableDust, new Vector2(0f, -0.6f), 150, QueenMotion.GetQueenDustColor(), 1.1f);
                d.noGravity = true;
            }
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //管辖节点由着色器晶壳接管全部绘制
            if (!IsManaged) {
                return null;
            }
            QueenPrismNodeRenderer.DrawNode(spriteBatch, npc, localAI[1], screenPos, drawColor);
            return false;
        }
    }

    /// <summary>凝胶伴舞(粉·弹性史莱姆)：绕皇后编队巡舞，低频珠滴；协同招式期让拍</summary>
    internal class QueenGelDancerAI : QueenMinionOverrideBase
    {
        public override int TargetID => NPCID.QueenSlimeMinionPink;
        protected override int Role => QueenMinionRole.GelDancer;

        public static int DancerLife() {
            float baseLife = 240f;
            if (Main.masterMode) {
                baseLife *= 2.2f;
            }
            else if (Main.expertMode) {
                baseLife *= 1.65f;
            }
            return (int)baseLife;
        }

        protected override int ManagedLifeMax() => DancerLife();

        /// <summary>皇后正处协同招式(由状态直接指挥仆从开火)，自主火力让拍防叠压</summary>
        internal static bool QueenCommandingVolley(NPC queen) {
            int state = (int)queen.ai[2];
            return state == (int)Core.QueenSlimeStateIndex.SkySpikeCascade
                || state == (int)Core.QueenSlimeStateIndex.SpikeRing
                || state == (int)Core.QueenSlimeStateIndex.GelSplitSummon;
        }

        protected override void MinionAI() {
            NPC queen = Queen;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.damage = 0;

            //编队相位自推进，各端本地积分+周期同步校正
            npc.ai[3] += 0.028f;
            int slot = (int)npc.ai[1];
            float phase = npc.ai[3] + slot * MathHelper.Pi;
            Vector2 orbit = queen.Center + new Vector2(
                (float)Math.Cos(phase) * 150f,
                40f + (float)Math.Sin(phase * 2f) * 44f);
            QueenMotion.SpringHover(npc, orbit, 0.03f, 0.16f, 22f);
            npc.rotation = npc.velocity.X * 0.05f;
            npc.spriteDirection = npc.velocity.X > 0f ? 1 : -1;

            //低频珠滴(服务端，槽位错帧；协同期让拍)
            if (!VaultUtils.isClient && !QueenCommandingVolley(queen)
                && Main.GameUpdateCount % 132 == (uint)(slot * 60 % 132)) {
                Player target = Main.player[queen.target];
                if (target.Alives()) {
                    Vector2 vel = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 6.8f;
                    vel.Y -= 2.2f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                        ModContent.ProjectileType<QueenGelPearlProj>(), QueenGelPearlProj.PearlDamage, 0f, Main.myPlayer,
                        0f, 0f, slot * 0.4f);
                    if (!VaultUtils.isServer) {
                        QueenMotion.GelSplashBurst(npc.Center, 0.5f, 3);
                    }
                }
            }
        }
    }

    /// <summary>翼卫(紫·天堂史莱姆)：镜像护航编队，低频瞄准尖刺；协同招式期让拍</summary>
    internal class QueenWingedEscortAI : QueenMinionOverrideBase
    {
        public override int TargetID => NPCID.QueenSlimeMinionPurple;
        protected override int Role => QueenMinionRole.WingedEscort;

        public static int EscortLife() {
            float baseLife = 300f;
            if (Main.masterMode) {
                baseLife *= 2.2f;
            }
            else if (Main.expertMode) {
                baseLife *= 1.65f;
            }
            return (int)baseLife;
        }

        protected override int ManagedLifeMax() => EscortLife();

        protected override void MinionAI() {
            NPC queen = Queen;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.damage = 0;

            int slot = (int)npc.ai[1];
            int side = slot == 0 ? -1 : 1;
            //皇后高速时收拢并翼，慢速时展开成仪仗
            float queenSpeed = queen.velocity.Length();
            float spreadX = MathHelper.Lerp(210f, 120f, MathHelper.Clamp(queenSpeed / 20f, 0f, 1f));
            Vector2 anchor = queen.Center + new Vector2(side * spreadX, -46f) - queen.velocity * 2.2f;
            QueenMotion.SpringHover(npc, anchor, 0.026f, 0.13f, 26f);
            npc.rotation = MathHelper.Clamp(npc.velocity.X * 0.04f, -0.4f, 0.4f);
            npc.spriteDirection = queen.Center.X > npc.Center.X ? 1 : -1;

            //低频瞄准尖刺(服务端，槽位错帧；协同期让拍——协同齐射由状态直接指挥)
            if (!VaultUtils.isClient && !QueenGelDancerAI.QueenCommandingVolley(queen)
                && Main.GameUpdateCount % 150 == (uint)(slot * 75 % 150)) {
                Player target = Main.player[queen.target];
                if (target.Alives()) {
                    QueenMotion.SpawnSpikeFan(npc, npc.Center, target.Center, 1, 0f, 8.4f,
                        Projectiles.QueenCrystalSpikeProj.SpikeDamage, 0.6f + slot * 0.2f);
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = 0.55f, MaxInstances = 3 }, npc.Center);
                }
            }
        }
    }

    /// <summary>随从血量静态入口(供状态生成用)</summary>
    internal static class QueenSlimeMinionAI
    {
        public static int PrismNodeLife(bool cathedral = false) => QueenPrismNodeAI.PrismNodeLife(cathedral);
        public static int DancerLife() => QueenGelDancerAI.DancerLife();
        public static int EscortLife() => QueenWingedEscortAI.EscortLife();
    }
}
