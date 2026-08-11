using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 品质重掷：重roll 地上这件装备的前缀。<br/>
    /// 掉落物是世界实体，权威端改完 <c>SyncItem</c> 就够，不牵扯背包所有权
    /// </summary>
    internal class Reappraise : QuickHackDef
    {
        private static readonly Color Appraise = new(255, 210, 120);

        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 5;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Item;
            UnlockedByDefault = false;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            //只有能带前缀的东西才谈得上重掷；一堆矿石没有品质可言
            return HackTargets.TryItem(target, out Item item)
                && item.maxStack == 1 && item.Prefix(-3);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryItem(target, out Item item, out int itemIndex)) return false;
            Vector2 center = item.Center;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                //必须先清前缀再重掷：Item.Prefix 乘的是当前数值而不是基础值，
                //少了这一步反复重掷就是连乘，伤害与售价一起滚成天文数字
                item.ResetPrefix();
                item.Prefix(-2);
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncItem, number: itemIndex);
                }
            }
            if (Main.netMode != NetmodeID.Server) EmitAppraise(center);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryItem(target, out Item item)) {
                EmitAppraise(item.Center);
            }
        }

        private static void EmitAppraise(Vector2 center) {
            //环绕上升，读作"被过了一遍检定"
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 offset = angle.ToRotationVector2() * 20f;
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    new Vector2(offset.Y, -offset.X) * 0.08f
                        + new Vector2(0f, -0.8f), Appraise, 0.8f)
                    ?.Configure(false, 22);
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.4f }, center);
        }
    }
}
