using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠弹幕路由（逐实例 GlobalProjectile）。两条增强通道：<br/>
    /// 1. 打标通道：OnSpawn 从 ItemUse 类出生源读源物品，源物品持有方案且模式开启则打标；
    ///    标记与 <see cref="MarkData"/> 走 SendExtraAI 随生成包过线（OnSpawn 先于生成包发出），
    ///    远端与服务端因此看得到同一增强形态。<br/>
    /// 2. 类型通道：方案在加载期注册弹幕类型（仆从/哨兵/驻场），逐帧按类型分发，无需出生源。<br/>
    /// 所有分发入口先查 <see cref="GameModeSystem.GodSmithActive"/>：
    /// 模式关闭时打标残留无害，在场仆从即刻退回原版行为
    /// </summary>
    internal class GodSmithProjRouter : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        //==================== 类型通道注册表 ====================

        private static Dictionary<int, GodSmithScheme> channelByProjType = [];

        /// <summary>注册按类型分发的增强通道；重复认领打日志并以后者生效</summary>
        internal static void RegisterChannel(GodSmithScheme scheme, int[] projTypes) {
            foreach (int type in projTypes) {
                if (!channelByProjType.TryAdd(type, scheme)) {
                    CWRMod.Instance.Logger.Error(
                        $"[GodSmith] 弹幕类型 {type} 的增强通道被重复认领：{channelByProjType[type].FullName} 与 {scheme.FullName}，后者生效");
                    channelByProjType[type] = scheme;
                }
            }
        }

        internal static void ClearRegistry() => channelByProjType = [];

        //==================== 实例态 ====================

        /// <summary>打标来源方案；null = 未打标。经 SendExtraAI 以物品 ID 过线重建</summary>
        internal GodSmithScheme MarkScheme;

        /// <summary>方案自由使用的打标数据（如蓄力档位），随生成包过线；出生后改动需 netUpdate</summary>
        internal float MarkData;

        /// <summary>第二打标数据槽（如轨迹模式/指令参数），同 <see cref="MarkData"/> 一起过线</summary>
        internal float MarkData2;

        /// <summary>
        /// 方案私有的每弹幕本地状态包：各端各自持有，不过线，弹幕消亡随实例丢弃。
        /// 需要跨端一致的量仍走 MarkData/MarkData2 或弹幕 ai[] + netUpdate
        /// </summary>
        internal object LocalState;

        /// <summary>惰性取用本地状态包（方案自定义状态类，须有无参构造）</summary>
        internal T GetOrCreateState<T>() where T : class, new() => (T)(LocalState ??= new T());

        /// <summary>本弹幕已被打标</summary>
        internal bool IsMarked => MarkScheme != null;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //EntitySource_ItemUse_WithAmmo 派生自 EntitySource_ItemUse，一并覆盖
            if (source is EntitySource_ItemUse itemUse && itemUse.Item != null
                && GodSmithScheme.TryGetScheme(itemUse.Item.type, out GodSmithScheme scheme)) {
                MarkScheme = scheme;
                scheme.GsProjOnSpawnMarked(projectile, this);
                return;
            }
            //子弹幕承签：父弹幕已打标则整套标记传染（集束子雷/弹片/分裂类），链式继承天然成立
            if (source is EntitySource_Parent parentSource && parentSource.Entity is Projectile parentProj
                && parentProj.TryGetGlobalProjectile(out GodSmithProjRouter parentRouter)
                && parentRouter.IsMarked) {
                MarkScheme = parentRouter.MarkScheme;
                MarkData = parentRouter.MarkData;
                MarkData2 = parentRouter.MarkData2;
                MarkScheme.GsProjOnSpawnInherited(projectile, this, parentProj, parentRouter);
            }
        }

        //==================== 标记同步 ====================

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bitWriter.WriteBit(IsMarked);
            if (IsMarked) {
                binaryWriter.Write(MarkScheme.TargetItemID);
                binaryWriter.Write(MarkData);
                binaryWriter.Write(MarkData2);
            }
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) {
            //先读净负载再落地，保持流对齐
            bool marked = bitReader.ReadBit();
            if (!marked) {
                return;
            }
            int itemType = binaryReader.ReadInt32();
            float data = binaryReader.ReadSingle();
            float data2 = binaryReader.ReadSingle();
            if (GodSmithScheme.TryGetScheme(itemType, out GodSmithScheme scheme)) {
                MarkScheme = scheme;
                MarkData = data;
                MarkData2 = data2;
            }
        }

        //==================== 分发 ====================

        /// <summary>取本帧应当分发的方案对：打标方案 + 类型通道方案（相同只派一次）</summary>
        private void Resolve(int projType, out GodSmithScheme marked, out GodSmithScheme channel) {
            marked = null;
            channel = null;
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            marked = MarkScheme;
            if (channelByProjType.TryGetValue(projType, out GodSmithScheme byType) && byType != marked) {
                channel = byType;
            }
        }

        public override bool PreAI(Projectile projectile) {
            Resolve(projectile.type, out GodSmithScheme marked, out GodSmithScheme channel);
            bool result = true;
            if (marked != null) {
                result &= marked.GsProjPreAI(projectile, this);
            }
            if (channel != null) {
                result &= channel.GsProjPreAI(projectile, this);
            }
            return result;
        }

        public override void PostAI(Projectile projectile) {
            Resolve(projectile.type, out GodSmithScheme marked, out GodSmithScheme channel);
            marked?.GsProjPostAI(projectile, this);
            channel?.GsProjPostAI(projectile, this);
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            Resolve(projectile.type, out GodSmithScheme marked, out GodSmithScheme channel);
            marked?.GsProjModifyHitNPC(projectile, target, ref modifiers, this);
            channel?.GsProjModifyHitNPC(projectile, target, ref modifiers, this);
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            Resolve(projectile.type, out GodSmithScheme marked, out GodSmithScheme channel);
            marked?.GsProjOnHitNPC(projectile, target, hit, damageDone, this);
            channel?.GsProjOnHitNPC(projectile, target, hit, damageDone, this);
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor) {
            //双通道语义与其他回调一致：两方案都调用；结果保守合并，任一 false 即阻断，均 null 则放行原版
            Resolve(projectile.type, out GodSmithScheme marked, out GodSmithScheme channel);
            bool? markedResult = marked?.GsProjPreDraw(projectile, ref lightColor, this);
            bool? channelResult = channel?.GsProjPreDraw(projectile, ref lightColor, this);
            if (markedResult == false || channelResult == false) {
                return false;
            }
            return markedResult ?? channelResult ?? true;
        }

        public override void PostDraw(Projectile projectile, Color lightColor) {
            Resolve(projectile.type, out GodSmithScheme marked, out GodSmithScheme channel);
            marked?.GsProjPostDraw(projectile, lightColor, this);
            channel?.GsProjPostDraw(projectile, lightColor, this);
        }

        public override void OnKill(Projectile projectile, int timeLeft) {
            Resolve(projectile.type, out GodSmithScheme marked, out GodSmithScheme channel);
            marked?.GsProjOnKill(projectile, timeLeft, this);
            channel?.GsProjOnKill(projectile, timeLeft, this);
        }
    }
}
