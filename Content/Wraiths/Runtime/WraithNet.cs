using CalamityOverhaul.Content.Players;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.VFX;
using InnoVault.Actors;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>通道子 op，Wraith 消息第二字节</summary>
    internal enum WraithNetOp : byte
    {
        /// <summary>客→服，死机开/关</summary>
        HaltRequest,
        /// <summary>客→服，仪式请求</summary>
        RiteRequest,
        /// <summary>服→发起者，仪式确认</summary>
        RiteConfirm,
        /// <summary>客→服，反噬生成</summary>
        BacklashSpawn,
        /// <summary>服→客，替死触发演出与受害者侵蚀镜像</summary>
        ScapeGhostFx,
        /// <summary>客→服，替死裁定请求（携带伤害参数）</summary>
        ScapeGhostRequest,
        /// <summary>服→受害者，替死无可用代理目标</summary>
        ScapeGhostFail,
        /// <summary>服→客，规则死亡转发</summary>
        RuleKill,
        /// <summary>服→受害者，预警起拍</summary>
        OmenStart,
        /// <summary>服→受害者，预警撤拍</summary>
        OmenCancel,
        /// <summary>服→客，据点锚位镜像</summary>
        SiteSync,
        /// <summary>客→服→全员，复苏进度同步</summary>
        RevivalSync,
    }

    /// <summary>
    /// 联机通道，挂 CWRNetWork 链尾。客→服一律服复核；身份走 Key；实体带 generation
    /// </summary>
    internal static class WraithNet
    {
        /// <summary>死机请求判距，宽于仪式半径</summary>
        private const float HaltRequestRange = WraithRites.RiteRange * 4f;

        //服会话态，反噬冷却；换世界清零
        private static readonly Dictionary<(int player, string key), long> backlashLastSpawn = [];

        /// <summary>清服会话态</summary>
        internal static void ClearSession() {
            backlashLastSpawn.Clear();
        }

        private static ModPacket NewPacket(WraithNetOp op) {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.Wraith);
            packet.Write((byte)op);
            return packet;
        }

        //====发送====

        /// <summary>客→服，死机开/关；duration≤0 取定义默认</summary>
        public static void SendHaltRequest(WraithActor wraith, bool halt, int durationTicks = -1) {
            if (!VaultUtils.isClient || wraith == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.HaltRequest);
            packet.Write((ushort)wraith.WhoAmI);
            packet.Write(wraith.Generation);
            packet.Write(halt);
            packet.Write(durationTicks);
            packet.Send();
        }

        /// <summary>客→服，仪式请求</summary>
        public static void SendRiteRequest(WraithActor wraith) {
            if (!VaultUtils.isClient || wraith == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RiteRequest);
            packet.Write((ushort)wraith.WhoAmI);
            packet.Write(wraith.Generation);
            packet.Send();
        }

        public static void SendBacklashSpawn(WraithDefinition definition) {
            if (!VaultUtils.isClient || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.BacklashSpawn);
            packet.Write(definition.Key);
            packet.Send();
        }

        /// <summary>客→服，替死裁定请求；服务端收到后执行权威逻辑并回包。</summary>
        public static void SendScapeGhostRequest(int victimWhoAmI, double damage, int hitDirection) {
            if (!VaultUtils.isClient || victimWhoAmI < 0 || victimWhoAmI >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.ScapeGhostRequest);
            packet.Write((byte)victimWhoAmI);
            packet.Write(damage);
            packet.Write(hitDirection);
            packet.Send();
        }

        /// <summary>服→受害者客户端，替死无代理目标。</summary>
        public static void SendScapeGhostFail(int victimWhoAmI) {
            if (!VaultUtils.isServer || victimWhoAmI < 0 || victimWhoAmI >= Main.maxPlayers) {
                return;
            }
            NewPacket(WraithNetOp.ScapeGhostFail).Send(victimWhoAmI);
        }

        /// <summary>服→全体客户端，替死血臂；受害者客户端另外镜像侵蚀与回执。</summary>
        public static void SendScapeGhostFx(Vector2 from, Vector2 to, int victimWhoAmI, string targetName = null) {
            if (!VaultUtils.isServer || victimWhoAmI < 0 || victimWhoAmI >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.ScapeGhostFx);
            packet.WriteVector2(from);
            packet.WriteVector2(to);
            packet.Write((byte)victimWhoAmI);
            packet.Write(targetName ?? string.Empty);
            packet.Send();
        }

        /// <summary>服→受害者，规则死亡；reasonKey 空走兜底</summary>
        public static void SendRuleKill(int playerWhoAmI, WraithDefinition definition, string reasonKey = null) {
            if (!VaultUtils.isServer || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RuleKill);
            packet.Write(definition.Key);
            packet.Write(reasonKey ?? string.Empty);
            packet.Send(playerWhoAmI);
        }

        /// <summary>服→受害者，预警起拍</summary>
        public static void SendOmenStart(int playerWhoAmI, WraithDefinition definition, int ticks) {
            if (!VaultUtils.isServer || definition == null) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.OmenStart);
            packet.Write(definition.Key);
            packet.Write(ticks);
            packet.Send(playerWhoAmI);
        }

        /// <summary>客→服，复苏进度同步</summary>
        public static void SendRevivalSync(int victimWhoAmI, float revival) {
            if (!VaultUtils.isClient || victimWhoAmI < 0 || victimWhoAmI >= Main.maxPlayers) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.RevivalSync);
            packet.Write((byte)victimWhoAmI);
            packet.Write(revival);
            packet.Send();
        }

        /// <summary>服→受害者，预警撤拍</summary>
        public static void SendOmenCancel(int playerWhoAmI) {
            if (!VaultUtils.isServer) {
                return;
            }
            NewPacket(WraithNetOp.OmenCancel).Send(playerWhoAmI);
        }

        /// <summary>服→客，据点锚位；toWho=-1 广播</summary>
        public static void SendSiteSync(string key, Vector2 anchor, bool anchored, int toWho = -1) {
            if (!VaultUtils.isServer || string.IsNullOrEmpty(key)) {
                return;
            }
            ModPacket packet = NewPacket(WraithNetOp.SiteSync);
            packet.Write(key);
            packet.WriteVector2(anchor);
            packet.Write(anchored);
            packet.Send(toWho);
        }

        //====接收====

        public static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type != CWRMessageType.Wraith) {
                return;
            }
            WraithNetOp op = (WraithNetOp)reader.ReadByte();
            switch (op) {
                case WraithNetOp.HaltRequest:
                    HandleHaltRequest(reader, whoAmI);
                    break;
                case WraithNetOp.RiteRequest:
                    HandleRiteRequest(reader, whoAmI);
                    break;
                case WraithNetOp.RiteConfirm: {
                    string key = reader.ReadString();
                    WraithRiteKind kind = (WraithRiteKind)reader.ReadByte();
                    if (!VaultUtils.isClient || kind > WraithRiteKind.Resubdue) {
                        break;
                    }
                    WraithRites.ApplyConfirmed(Main.LocalPlayer, key, kind);
                    break;
                }
                case WraithNetOp.BacklashSpawn:
                    HandleBacklashSpawn(reader, whoAmI);
                    break;
                case WraithNetOp.ScapeGhostFx: {
                    Vector2 from = reader.ReadVector2();
                    Vector2 to = reader.ReadVector2();
                    int victim = reader.ReadByte();
                    string targetName = reader.ReadString();
                    if (!VaultUtils.isClient || victim < 0 || victim >= Main.maxPlayers) {
                        break;
                    }
                    //非受害者：只播血臂演出
                    if (victim != Main.myPlayer) {
                        ScapeArmRenderer.Trigger(from, to);
                        break;
                    }
                    //受害者：通过 ApplyScapeResult 清 pending 并触发本地演出+侵蚀
                    Main.LocalPlayer.GetModPlayer<WraithPlayer>().AddErosion(0.30f);
                    Main.LocalPlayer.TryGetOverride(out PlayerDeath pd);
                    if (pd != null) {
                        pd.ApplyScapeResult(true, from, to, targetName);
                    }
                    else {
                        ScapeArmRenderer.Trigger(from, to);
                        string name = string.IsNullOrWhiteSpace(targetName)
                            ? WraithSystemText.ScapeGhostUnknownTarget.Value : targetName;
                        VaultUtils.Text(WraithSystemText.ScapeGhostActivated.Format(name), new Color(178, 34, 44));
                    }
                    break;
                }
                case WraithNetOp.ScapeGhostRequest:
                    HandleScapeGhostRequest(reader, whoAmI);
                    break;
                case WraithNetOp.ScapeGhostFail: {
                    if (!VaultUtils.isClient) {
                        break;
                    }
                    Main.LocalPlayer.TryGetOverride(out PlayerDeath pd);
                    pd?.ApplyScapeResult(false, default, default, null);
                    break;
                }
                case WraithNetOp.RuleKill: {
                    string key = reader.ReadString();
                    string reasonKey = reader.ReadString();
                    if (!VaultUtils.isClient || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                        break;
                    }
                    WraithLethality.KillLocal(Main.LocalPlayer, definition, WraithLethality.ResolveReason(definition, reasonKey));
                    break;
                }
                case WraithNetOp.OmenStart: {
                    string key = reader.ReadString();
                    int ticks = reader.ReadInt32();
                    if (!VaultUtils.isClient || ticks <= 0 || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                        break;
                    }
                    Main.LocalPlayer.GetModPlayer<WraithPlayer>().BeginOmenMirror(definition, ticks);
                    break;
                }
                case WraithNetOp.OmenCancel: {
                    if (VaultUtils.isClient) {
                        Main.LocalPlayer.GetModPlayer<WraithPlayer>().ClearOmenMirror();
                    }
                    break;
                }
                case WraithNetOp.SiteSync: {
                    //先读满再校验，保流对齐
                    string key = reader.ReadString();
                    Vector2 anchor = reader.ReadVector2();
                    bool anchored = reader.ReadBoolean();
                    if (!VaultUtils.isClient || string.IsNullOrEmpty(key)) {
                        break;
                    }
                    WraithSiteSystem.ApplyClientMirror(key, anchor, anchored);
                    break;
                }
                case WraithNetOp.RevivalSync: {
                    int playerIdx = reader.ReadByte();
                    float revivalVal = reader.ReadSingle();
                    if (playerIdx < 0 || playerIdx >= Main.maxPlayers) {
                        break;
                    }
                    if (VaultUtils.isServer) {
                        //服务端转发给其他客户端
                        ModPacket fwd = NewPacket(WraithNetOp.RevivalSync);
                        fwd.Write((byte)playerIdx);
                        fwd.Write(revivalVal);
                        fwd.Send(-1, whoAmI);
                    }
                    else if (VaultUtils.isClient && playerIdx != Main.myPlayer) {
                        //镜像他人复苏值（仅展示用，不触发杀死判定）
                        Player target = Main.player[playerIdx];
                        if (target != null && target.active) {
                            target.GetModPlayer<WraithPlayer>().SetRevivalNoKill(revivalVal);
                        }
                    }
                    break;
                }
            }
        }

        //====服务器受理====

        private static void HandleHaltRequest(BinaryReader reader, int whoAmI) {
            int slot = reader.ReadUInt16();
            ushort generation = reader.ReadUInt16();
            bool halt = reader.ReadBoolean();
            int duration = reader.ReadInt32();
            if (!VaultUtils.isServer) {
                return;
            }
            Player requester = ResolvePlayer(whoAmI);
            WraithActor wraith = ResolveActor(slot, generation);
            //活人+随身载体+判距；仅 AllowExternalHaltRequest 受理
            if (requester == null || wraith == null || wraith.Definition == null
                || !wraith.Definition.AllowExternalHaltRequest
                || !WraithVessels.ResolveCarried(requester).IsValid
                || Vector2.DistanceSquared(requester.Center, wraith.Center) > HaltRequestRange * HaltRequestRange) {
                return;
            }
            if (halt) {
                //时长钳制，非法/超限回落定义窗口
                int windowLimit = System.Math.Max(wraith.Definition.HaltWindowTicks, 1) * 4;
                if (duration <= 0 || duration > windowLimit) {
                    duration = -1;
                }
                wraith.BeginHalt(duration);
            }
            else {
                wraith.EndHalt();
            }
        }

        private static void HandleRiteRequest(BinaryReader reader, int whoAmI) {
            int slot = reader.ReadUInt16();
            ushort generation = reader.ReadUInt16();
            if (!VaultUtils.isServer) {
                return;
            }
            Player requester = ResolvePlayer(whoAmI);
            WraithActor target = ResolveActor(slot, generation);
            if (requester == null || target == null
                || !WraithRites.TryServerPerform(requester, target, out WraithRiteKind kind)) {
                return;
            }
            //复核通过，回执落簿
            ModPacket packet = NewPacket(WraithNetOp.RiteConfirm);
            packet.Write(target.Definition.Key);
            packet.Write((byte)kind);
            packet.Send(whoAmI);
        }

        private static void HandleBacklashSpawn(BinaryReader reader, int whoAmI) {
            string key = reader.ReadString();
            if (!VaultUtils.isServer || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                return;
            }
            Player owner = ResolvePlayer(whoAmI);
            if (owner == null) {
                return;
            }
            //随身 Bound 且躁动
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(owner);
            if (!vessel.IsValid || !vessel.Store.TryGet(key, out WraithProgressRecord record)
                || record.State != WraithBindState.Bound
                || record.Mastery >= WraithDefinition.RestlessThreshold) {
                return;
            }
            //服侧同键冷却
            long now = (long)Main.GameUpdateCount;
            if (backlashLastSpawn.TryGetValue((whoAmI, key), out long last)
                && now - last < WraithBacklash.KeyCooldownTicks) {
                return;
            }
            //生成落地才记冷却
            if (WraithBacklash.SpawnEscaped(whoAmI, definition)) {
                backlashLastSpawn[(whoAmI, key)] = now;
            }
        }

        private static void HandleScapeGhostRequest(BinaryReader reader, int whoAmI) {
            int victim = reader.ReadByte();
            double damage = reader.ReadDouble();
            int hitDirection = reader.ReadInt32();
            if (!VaultUtils.isServer || victim < 0 || victim >= Main.maxPlayers || victim != whoAmI) {
                return;
            }
            Player player = ResolvePlayer(whoAmI);
            if (player == null) {
                return;
            }
            bool success = PlayerDeath.ExecuteScapeGhostAuthority(player, damage, hitDirection, null);
            if (!success) {
                SendScapeGhostFail(whoAmI);
            }
            //成功时 ExecuteScapeGhostAuthority 内部已调 SendScapeGhostFx
        }

        //====解析====

        private static Player ResolvePlayer(int whoAmI) {
            if (whoAmI < 0 || whoAmI >= Main.maxPlayers) {
                return null;
            }
            Player player = Main.player[whoAmI];
            return player != null && player.active && !player.dead ? player : null;
        }

        /// <summary>槽位+代校验，无效/过期返回 null</summary>
        private static WraithActor ResolveActor(int slot, ushort generation) {
            if (slot < 0 || slot >= ActorLoader.MaxActorCount) {
                return null;
            }
            Actor actor = ActorLoader.Actors[slot];
            return actor is WraithActor wraith && wraith.Active && wraith.Generation == generation ? wraith : null;
        }
    }
}
