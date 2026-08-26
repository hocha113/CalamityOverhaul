using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative.Common
{
    /// <summary>
    /// 基于 <see cref="HitEffect"/> 的死亡追踪基类（各端都会跑）
    /// <para/>
    /// 蠕虫/分段 Boss：最后一击常打在体节上，而 <c>checkDead</c>/<c>OnKill</c> 落在
    /// <see cref="NPC.realLife"/> 头节点。此处将回调统一映射到头节点，并在头实例上去重
    /// </summary>
    internal class DeathTrackingNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool _deathHandled;

        /// <summary>禁止子类走 OnKill：仅服务端，且与 HitEffect 语义重复，易在 MP 漏端</summary>
        public sealed override void OnKill(NPC npc) { }

        public sealed override void HitEffect(NPC npc, NPC.HitInfo hit) {
            // HitEffect 在 checkDead 将 active=false 之前调用；手动击杀包路径也是 life=0 后立刻 HitEffect
            if (npc.life > 0 && npc.active) {
                return;
            }

            NPC deathNpc = ResolveDeathNpc(npc);
            //体节先没、共享血池的头还活着(单节被沉溺清体/处决等):不登记,
            //等头真死那一击再记,礼物结算不再插进战斗中(反馈十一·#30)
            if (deathNpc.whoAmI != npc.whoAmI && deathNpc.active && deathNpc.life > 0) {
                return;
            }
            DeathTrackingNPC tracker = this;
            if (deathNpc.whoAmI != npc.whoAmI
                && deathNpc.TryGetGlobalNPC(this, out DeathTrackingNPC headTracker)) {
                tracker = headTracker;
            }

            if (tracker._deathHandled) {
                return;
            }

            tracker._deathHandled = true;
            tracker.OnNPCDeath(deathNpc);
        }

        /// <summary>
        /// 解析真实死亡实体：体节 → <see cref="NPC.realLife"/> 头节点
        /// </summary>
        private static NPC ResolveDeathNpc(NPC npc) {
            int realLife = npc.realLife;
            if (realLife < 0 || realLife >= Main.maxNPCs || realLife == npc.whoAmI) {
                return npc;
            }

            return Main.npc[realLife];
        }

        /// <summary>
        /// NPC 被击杀时调用。<paramref name="npc"/> 已是 realLife 头节点（若有）
        /// HitEffect 路径下客户端与服务端都会进入；需要仅本地进度的子类应自行 <c>if (Main.dedServ) return;</c>
        /// </summary>
        public virtual void OnNPCDeath(NPC npc) { }
    }
}
