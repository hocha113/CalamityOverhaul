using CalamityMod.NPCs;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Core
{
    /// <summary>
    /// 提供一个强行覆盖目标NPC行为性质的基类，通过On钩子为基础运行
    /// </summary>
    internal class NPCOverride
    {
        #region Data
        /// <summary>
        /// 要修改的NPC的ID值，在目前为止，每一个类型的NPC只能有一个实例对应
        /// </summary>
        public virtual int TargetID => NPCID.None;
        /// <summary>
        /// 模组实例
        /// </summary>
        public static Mod mod => CWRMod.Instance;
        /// <summary>
        /// 最大AI槽位数量，12个槽位，那么最大的数组索引就是11
        /// </summary>
        internal const int MaxAISlot = 12;
        /// <summary>
        /// ai槽位，使用<see cref="NetAISend()"/>来在多人游戏中同步这个字段的内容，在合适的时机调用它
        /// </summary>
        public float[] ai = new float[MaxAISlot];
        /// <summary>
        /// 本地ai槽位，这个值不会自动多人同步，如果有需要，重载<see cref="OtherNetWorkReceive(BinaryReader)"/>来同步它
        /// </summary>
        public float[] localAI = new float[MaxAISlot];
        /// <summary>
        /// 这个实例对应的NPC实例
        /// </summary>
        public NPC npc { get; private set; }
        /// <summary>
        /// 这个实例对应的CWRNpc类型实例
        /// </summary>
        public CWRNpc cwrNPC { get; private set; }
        /// <summary>
        /// 这个实例对应的CalamityGlobalNPC类型实例
        /// </summary>
        public CalamityGlobalNPC calNPC { get; private set; }
        //不要直接设置这个
        private bool _netOtherWorkSend;
        //不要直接设置这个
        private bool _netAIWorkSend;
        /// <summary>
        /// 用于网络同步，只能在服务端进行设置，其他端口永远返回<see langword="false"/>，
        /// 当设置为<see langword="true"/>时，会自动调用<see cref="OtherNetWorkReceiveHander(BinaryReader)"/>进行网络数据同步
        /// </summary>
        public bool NetOtherWorkSend {
            get {
                if (!VaultUtils.isServer) {
                    return false;
                }
                return _netOtherWorkSend;
            }
            set => _netOtherWorkSend = value;
        }
        /// <summary>
        /// 用于网络同步，只能在服务端进行设置，其他端口永远返回<see langword="false"/>，
        /// 当设置为<see langword="true"/>时，会自动调用<see cref="NetAISend()"/>进行网络数据同步
        /// </summary>
        public bool NetAIWorkSend {
            get {
                if (!VaultUtils.isServer) {
                    return false;
                }
                return _netAIWorkSend;
            }
            set => _netAIWorkSend = value;
        }
        #endregion
        /// <summary>
        /// 克隆这个实例，注意，克隆出的新对象与原实例将不再具有任何引用关系
        /// </summary>
        /// <returns></returns>
        public NPCOverride Clone() => (NPCOverride)Activator.CreateInstance(GetType());
        /// <summary>
        /// 是否加载这个实例，默认返回<see langword="true"/>
        /// </summary>
        /// <returns></returns>
        public virtual bool CanLoad() { return true; }
        /// <summary>
        /// 是否修改该npc，返回null则不进行拦截操作
        /// </summary>
        /// <returns></returns>
        public virtual bool? CanOverride() {
            return null;
        }
        /// <summary>
        /// 寻找对应NPC实例的重载实例
        /// </summary>
        /// <param name="id"></param>
        /// <param name="npcOverride"></param>
        /// <returns></returns>
        public static bool TryFetchByID(int id, out NPCOverride npcOverride) {
            npcOverride = null;

            if (!NPCSystem.IDToNPCSetDic.TryGetValue(id, out var value)) {
                return false;
            }

            bool canOverrideByNPC = value.CanOverride() ?? CWRServerConfig.Instance.BiologyOverhaul;
            if (canOverrideByNPC) {
                npcOverride = value.Clone();
                return true;
            }

            return false;
        }

        public static void SetDefaults(NPC npc, CWRNpc cwr, CalamityGlobalNPC cal) {
            if (!TryFetchByID(npc.type, out NPCOverride inds) || inds == null) {
                return;
            }
            inds.ai = new float[MaxAISlot];
            inds.localAI = new float[MaxAISlot];
            inds.npc = npc;
            inds.cwrNPC = cwr;
            inds.calNPC = cal;
            inds.SetProperty();
            npc.CWR().NPCOverride = inds;
        }

        #region NetWork
        //统一管理的网络行为，自动运行在更新后
        internal void DoNet() {
            if (!VaultUtils.isServer) {
                return;
            }

            if (NetAIWorkSend) {
                NetAISend();
                NetAIWorkSend = false;
            }
            if (NetOtherWorkSend) {
                OtherNetWorkSendHander();
                NetOtherWorkSend = false;
            }
        }

        //不要在实例中调用这个，而是使用netWorkSend
        internal static void OtherNetWorkReceiveHander(BinaryReader reader) {
            NPC npc = Main.npc[reader.ReadInt32()];
            npc.CWR().NPCOverride.OtherNetWorkReceive(reader);
        }

        internal void OtherNetWorkSendHander() {
            if (!VaultUtils.isServer) {
                return;
            }

            ModPacket netMessage = mod.GetPacket();
            netMessage.Write((byte)CWRMessageType.NPCOverrideOtherAI);
            netMessage.Write(npc.whoAmI);
            OtherNetWorkSend(netMessage);
        }

        /// <summary>
        /// 发送网络数据，同步额外的网络数据，重载编写时需要注意与<see cref="OtherNetWorkReceive"/>对应
        /// ，将<see cref="NetOtherWorkSend"/>设置为<see langword="true"/>后自动进行一次发包
        /// </summary>
        public virtual void OtherNetWorkSend(ModPacket netMessage) { }

        /// <summary>
        /// 接受网络数据，同步额外的网络数据，重载编写时需要注意与<see cref="OtherNetWorkSend"/>对应
        /// </summary>
        public virtual void OtherNetWorkReceive(BinaryReader reader) { }

        /// <summary>
        /// 发送网络数据，同步<see cref="ai"/>的值，只会在服务端上运行
        /// </summary>
        internal void NetAISend() {
            if (!VaultUtils.isServer) {
                return;
            }

            var netMessage = mod.GetPacket();
            netMessage.Write((byte)CWRMessageType.NPCOverrideAI);
            netMessage.Write(npc.whoAmI);

            NPCOverride pCOverride = npc.CWR().NPCOverride;
            float[] ai = pCOverride.ai;

            foreach (var aiValue in ai) {
                netMessage.Write(aiValue);
            }

            netMessage.Send();
        }
        /// <summary>
        /// 发送网络数据，同步<see cref="ai"/>的值，只会在服务端上运行
        /// </summary>
        /// <param name="npc"></param>
        internal static void NetAISend(NPC npc) {
            if (VaultUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.NPCOverrideAI);
                netMessage.Write(npc.whoAmI);

                NPCOverride pCOverride = npc.CWR().NPCOverride;
                float[] ai = pCOverride.ai;

                foreach (var aiValue in ai) {
                    netMessage.Write(aiValue);
                }

                netMessage.Send();
            }
        }

        internal static void NetAIReceive(BinaryReader reader) {
            NPC npc = Main.npc[reader.ReadInt32()];
            float[] receiveAI = new float[MaxAISlot];
            for (int i = 0; i < MaxAISlot; i++) {
                receiveAI[i] = reader.ReadSingle();
            }
            if (npc.active) {
                NPCOverride pCOverride = npc.CWR().NPCOverride;
                for (int i = 0; i < MaxAISlot; i++) {
                    pCOverride.ai[i] = receiveAI[i];
                }
            }
        }

        #endregion
        /// <summary>
        /// 在npc生成的时候调用一次，用于初始化一些实例数据
        /// </summary>
        public virtual void SetProperty() { }
        /// <summary>
        /// 允许编辑ai或者阻断ai运行，返回默认值<see langword="true"/>则会继续运行后续ai行为
        /// ，返回<see langword="false"/>会阻断所有后续ai行为的调用
        /// </summary>
        /// <returns></returns>
        public virtual bool AI() { return true; }
        /// <summary>
        /// 允许编辑死亡事件，返回非null值可以阻断后续逻辑的运行
        /// </summary>
        /// <returns></returns>
        public virtual bool? On_PreKill() { return null; }
        /// <summary>
        /// 允许编辑死亡检测逻辑，返回非null值可以阻断后续逻辑的运行
        /// </summary>
        /// <returns></returns>
        public virtual bool? CheckDead() { return null; }
        /// <summary>
        /// 允许编辑活跃检测逻辑
        /// </summary>
        /// <returns></returns>
        public virtual bool CheckActive() => true;
        /// <summary>
        /// 编辑NPC在地图上的图标ID
        /// </summary>
        /// <param name="index"></param>
        public virtual void BossHeadSlot(ref int index) { }
        /// <summary>
        /// 编辑NPC在地图上的头像旋转角
        /// </summary>
        /// <param name="rotation"></param>
        public virtual void BossHeadRotation(ref float rotation) { }
        /// <summary>
        /// 编辑NPC的掉落，注意，这个方法不会被生物AI设置阻止，注意，如果需要使用NPC实例，必须使用给出的参数thisNPC，而不是尝试访问<see cref="npc"/>
        /// </summary>
        /// <param name="npcLoot"></param>
        public virtual void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) { }
        /// <summary>
        /// 修改被物品击中的伤害
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="player"></param>
        /// <param name="item"></param>
        /// <param name="modifiers"></param>
        public virtual void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers) { }
        /// <summary>
        /// 修改被弹幕击中的伤害
        /// </summary>
        /// <param name="npc"></param>
        /// <param name="projectile"></param>
        /// <param name="modifiers"></param>
        public virtual void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) { }
        /// <summary>
        /// 修改绘制
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="screenPos"></param>
        /// <param name="drawColor"></param>
        /// <returns></returns>
        public virtual bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }
        /// <summary>
        /// 修改后层绘制
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="screenPos"></param>
        /// <param name="drawColor"></param>
        /// <returns></returns>
        public virtual bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
