using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 远程回收：把掉落物瞬移到施法者脚下，剩下的交给原版拾取。<br/>
    /// 刻意不直接写背包，非 ServerSideCharacter 的联机里背包归客户端管，
    /// 服务端写下去会被原版丢弃（<c>MessageBuffer</c> case 5）
    /// </summary>
    internal class ItemRecall : QuickHackDef
    {
        private static readonly Color Beam = new(120, 255, 200);

        public override void SetDefaults() {
            UploadTime = 40;
            RamCost = 2;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Item;
            UnlockedByDefault = false;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryItem(target, out Item item, out int itemIndex)
                || caster == null) {
                return false;
            }
            Vector2 from = item.Center;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                item.Center = caster.Center;
                item.velocity = Vector2.Zero;
                //归零抓取延迟，落地即被原版拾取逻辑捞走
                item.noGrabDelay = 0;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncItem, number: itemIndex);
                    HackTimeNetSync.BroadcastPointCue(HackPointCue.ItemRecall,
                        caster.whoAmI, from);
                }
            }

            if (Main.netMode != NetmodeID.Server) EmitTrail(from, caster.Center);
            return true;
        }

        /// <summary>
        /// 远端轨迹只能走专用表现包：EffectApply 到达时物品已经被 <c>SyncItem</c>
        /// 挪到施法者身上，起点丢了；施法者也必须按包里的索引取，
        /// 读本机玩家会让每个旁观者都看到物品朝自己飞
        /// </summary>
        internal static void PlayRecallTrail(int casterIndex, Vector2 from) {
            if (casterIndex < 0 || casterIndex >= Main.maxPlayers) return;
            Player caster = Main.player[casterIndex];
            if (caster?.active != true) return;
            EmitTrail(from, caster.Center);
        }

        //沿回收路径铺一串火花，读作被拽过来而不是凭空消失
        private static void EmitTrail(Vector2 from, Vector2 to) {
            Vector2 delta = to - from;
            int steps = (int)MathHelper.Clamp(delta.Length() / 24f, 3f, 26f);
            for (int i = 0; i <= steps; i++) {
                Vector2 pos = from + delta * (i / (float)steps);
                Vector2 vel = delta.SafeNormalize(Vector2.UnitY) * 1.4f;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Beam, 0.7f)
                    ?.Configure(false, 14);
            }
        }
    }
}
