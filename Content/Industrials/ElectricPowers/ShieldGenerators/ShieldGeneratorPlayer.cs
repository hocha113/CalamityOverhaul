using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShieldGenerators
{
    /// <summary>
    /// 护盾发生器的玩家侧结算:护盾池充放与受击吸收。
    /// 池子是本地态,吸收只在受击玩家自己的客户端结算(owner-local),
    /// 各端为范围内玩家本地挂buff,全程零自定义网络包
    /// </summary>
    internal class ShieldGeneratorPlayer : ModPlayer
    {
        /// <summary>护盾池上限</summary>
        internal const float ShieldMax = 60f;
        /// <summary>光环内每帧充能</summary>
        internal const float ChargePerTick = 0.4f;
        /// <summary>离开光环后每帧衰减</summary>
        internal const float DecayPerTick = 1f;
        /// <summary>单次受击最多吸收伤害的比例</summary>
        internal const float AbsorbRatio = 0.6f;

        /// <summary>本帧是否处于护盾光环内,由buff每帧置位</summary>
        internal bool ShieldAuraActive;
        /// <summary>当前护盾池</summary>
        internal float ShieldCharge;

        public override void ResetEffects() {
            ShieldAuraActive = false;
        }

        public override void PostUpdateBuffs() {
            if (ShieldAuraActive) {
                ShieldCharge = Math.Min(ShieldCharge + ChargePerTick, ShieldMax);
            }
            else {
                ShieldCharge = Math.Max(0f, ShieldCharge - DecayPerTick);
            }
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            //吸收结算owner-local:只在被击玩家自己的端上扣池减伤,
            //其余端沿用owner广播的最终伤害,池子漂移无碍
            if (Player.whoAmI != Main.myPlayer || ShieldCharge < 1f) {
                return;
            }
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) => {
                int absorb = (int)Math.Min(ShieldCharge, info.Damage * AbsorbRatio);
                if (absorb <= 0) {
                    return;
                }
                info.Damage -= absorb;
                ShieldCharge -= absorb;

                //吸收反馈:护盾碎光+闷响,仅本地
                SoundEngine.PlaySound(SoundID.NPCHit53 with { Volume = 0.4f, Pitch = 0.4f }, Player.Center);
                for (int i = 0; i < 10; i++) {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                        DustID.PurpleTorch, 0f, 0f, 120, default, 1.2f);
                    dust.noGravity = true;
                    dust.velocity = VaultUtils.RandVr(3f);
                }
            };
        }

        public override void UpdateDead() {
            //死亡清池,复活不带残盾
            ShieldCharge = 0f;
            ShieldAuraActive = false;
        }
    }
}
