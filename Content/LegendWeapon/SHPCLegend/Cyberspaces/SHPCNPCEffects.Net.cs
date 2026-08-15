using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// SHPC 命中附加效果的联机通道。OnHit 只在 owner 客户端跑，
    /// 权威写入必须经服务端，再靠 SendExtraAI / netUpdate 铺给各端
    /// </summary>
    internal partial class SHPCNPCEffects
    {
        internal enum EffectKind : byte
        {
            ChronalSlow,
            DataErosion,
            ObsidianCrack,
            Lifebloom,
            //苔藓不走请求：湿苔斑弹幕在权威端直写，见 MossboundBarrelModule
            Pheromone,
            /// <summary>右键引爆已附着的裂纹；owner 端事件，爆发本体只能在权威端执行</summary>
            ObsidianBurst,
        }

        private const float MaxApplyDistance = 3200f;
        private const int MaxDuration = 600;
        private const int MaxTickDamage = 100_000;

        /// <summary>owner 命中后请求服务端施加；单人直接走权威</summary>
        internal static void RequestApply(Player owner, NPC target, EffectKind kind,
            int duration, int valueA = 0, int valueB = 0) {
            if (owner?.active != true || target?.active != true) {
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                if (owner.whoAmI != Main.myPlayer
                    || !IsValidApplyPayload(kind, duration, valueA, valueB)) {
                    return;
                }
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.SHPCNPCEffect);
                packet.Write((ushort)target.whoAmI);
                packet.Write(target.type);
                packet.Write((byte)kind);
                packet.Write((ushort)Math.Clamp(duration, 0, MaxDuration));
                packet.Write(valueA);
                packet.Write(valueB);
                packet.Send();
                return;
            }
            ApplyAuthority(owner, target, kind, duration, valueA, valueB);
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader,
            int whoAmI) {
            if (type != CWRMessageType.SHPCNPCEffect || reader == null) {
                return;
            }
            try {
                int npcIndex = reader.ReadUInt16();
                int npcType = reader.ReadInt32();
                EffectKind kind = (EffectKind)reader.ReadByte();
                int duration = reader.ReadUInt16();
                int valueA = reader.ReadInt32();
                int valueB = reader.ReadInt32();
                if (Main.netMode != NetmodeID.Server) {
                    return;
                }
                if (whoAmI < 0 || whoAmI >= Main.maxPlayers
                    || kind > EffectKind.ObsidianBurst
                    || !IsValidApplyPayload(kind, duration, valueA, valueB)) {
                    //自家客户端不会发出畸形负载，走到这里通常是版本不一致或篡改，留证
                    CWRMod.Instance?.Logger.Warn(
                        $"[SHPCNPCEffect] rejected payload: sender={whoAmI}"
                        + $" kind={(byte)kind} dur={duration} a={valueA} b={valueB}");
                    return;
                }
                Player owner = Main.player[whoAmI];
                if (owner?.active != true || owner.dead
                    || npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                    //发包后一个 RTT 内玩家死亡属正常竞态，静默丢弃，下一次命中会重发
                    return;
                }
                NPC target = Main.npc[npcIndex];
                if (target?.active != true || target.type != npcType
                    || target.friendly
                    || Vector2.DistanceSquared(owner.Center, target.Center)
                        > MaxApplyDistance * MaxApplyDistance) {
                    //击杀瞬间的命中请求到达时目标常已死亡/换占用，同为正常竞态
                    return;
                }
                ApplyAuthority(owner, target, kind, duration, valueA, valueB);
            } catch (EndOfStreamException ex) {
                CWRMod.Instance?.Logger.Warn(
                    "[SHPCNPCEffect] packet underflow: " + ex.Message);
            } catch (IOException ex) {
                CWRMod.Instance?.Logger.Warn(
                    "[SHPCNPCEffect] packet read failed: " + ex.Message);
            }
        }

        private static void ApplyAuthority(Player owner, NPC target, EffectKind kind,
            int duration, int valueA, int valueB) {
            if (!target.TryGetGlobalNPC(out SHPCNPCEffects effects)) {
                return;
            }
            // netUpdate 只在效果生效沿标一次：光束每秒命中多次，逐包同步会放大成
            // NPC 全量广播洪流；存续期间由 PreAI 的 ~10 帧载波补漏（错题本 3.2/9.3）
            switch (kind) {
                case EffectKind.ChronalSlow:
                    effects.ApplyChronalSlowAuthority(target, duration);
                    break;
                case EffectKind.DataErosion: {
                    bool fresh = effects.DataErosionTime <= 0;
                    effects.ApplyDataErosionAuthority(duration, valueA);
                    if (fresh) {
                        MarkNetUpdate(target);
                    }
                    break;
                }
                case EffectKind.ObsidianCrack: {
                    bool fresh = effects.ObsidianCrackTime <= 0;
                    effects.ApplyObsidianCrackAuthority(target, duration,
                        owner.whoAmI, valueA);
                    if (fresh) {
                        MarkNetUpdate(target);
                    }
                    break;
                }
                case EffectKind.Lifebloom: {
                    bool fresh = effects.LifebloomTime <= 0;
                    effects.ApplyLifebloomAuthority(duration, valueA,
                        owner.whoAmI);
                    if (fresh) {
                        MarkNetUpdate(target);
                    }
                    break;
                }
                case EffectKind.Pheromone: {
                    bool fresh = effects.PheromoneTime <= 0;
                    effects.ApplyPheromoneAuthority(duration, owner.whoAmI);
                    if (fresh) {
                        MarkNetUpdate(target);
                    }
                    break;
                }
                case EffectKind.ObsidianBurst:
                    //爆发要求裂纹仍在且归属一致；RTT 期间过期则静默跳过，不回滚客户端预览
                    if (effects.ObsidianCrackTime > 0
                        && effects.ObsidianCrackOwner == owner.whoAmI) {
                        BurstObsidian(target, owner.whoAmI, valueA);
                        effects.ObsidianCrackTime = 0;
                        effects.ObsidianCrackStacks = 0;
                        effects.ObsidianCrackDamage = 0;
                        MarkNetUpdate(target);
                    }
                    break;
            }
        }

        private static void MarkNetUpdate(NPC npc) {
            if (Main.netMode == NetmodeID.Server && npc?.active == true) {
                npc.netUpdate = true;
            }
        }

        private static bool IsValidApplyPayload(EffectKind kind, int duration,
            int valueA, int valueB) {
            if (duration < 0 || duration > MaxDuration) {
                return false;
            }
            return kind switch {
                EffectKind.ChronalSlow => duration > 0,
                EffectKind.DataErosion => duration > 0 && valueA > 0
                    && valueA <= MaxTickDamage && valueB == 0,
                EffectKind.ObsidianCrack => duration > 0 && valueA > 0
                    && valueA <= MaxTickDamage && valueB == 0,
                EffectKind.Lifebloom => duration > 0 && valueA > 0
                    && valueA <= MaxTickDamage && valueB == 0,
                EffectKind.Pheromone => duration > 0 && valueA == 0
                    && valueB == 0,
                EffectKind.ObsidianBurst => duration > 0 && valueA > 0
                    && valueA <= MaxTickDamage && valueB == 0,
                _ => false,
            };
        }

        internal static void SyncProjectileFromServer(Projectile projectile) {
            if (Main.netMode == NetmodeID.Server && projectile?.active == true) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null,
                    projectile.whoAmI);
            }
        }
    }
}
