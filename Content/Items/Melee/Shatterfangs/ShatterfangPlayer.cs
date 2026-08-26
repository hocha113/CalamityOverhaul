using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 崩牙獠刃的剑身状态。稳固度挥砍消耗、闲置缓慢愈合，触顶复原并叮一声<br/>
    /// 状态只在持有者本机有意义，远端渲染一律走弹幕 ai 快照
    /// </summary>
    internal class ShatterfangPlayer : ModPlayer
    {
        /// <summary>剑身稳固度 0~1</summary>
        public float Stability = 1f;
        /// <summary>半刃崩坏态</summary>
        public bool Broken;
        /// <summary>连击拍计数</summary>
        public int ComboCounter;
        /// <summary>断手回第一拍的倒计时</summary>
        public int ComboResetTimer;
        /// <summary>距离自然愈合恢复的等待帧</summary>
        public int RegenDelay;

        /// <summary>每记常规挥砍的稳固度消耗</summary>
        public const float SwingCost = 0.09f;
        /// <summary>终结震撼斩的稳固度消耗，一轮合计 0.50，满稳固两轮后第二记终结斩崩坏</summary>
        public const float FinisherCost = 0.23f;
        /// <summary>闲置自然愈合速率(每帧)</summary>
        public const float RegenRate = 1f / 600f;
        /// <summary>右键修补速率(每帧)</summary>
        public const float RepairRate = 1f / 80f;

        public override void Initialize() {
            Stability = 1f;
            Broken = false;
        }

        public override void PostUpdateMiscEffects() {
            if (ComboResetTimer > 0 && --ComboResetTimer == 0) {
                ComboCounter = 0;
            }
            if (RegenDelay > 0) {
                RegenDelay--;
                return;
            }
            if (Stability < 1f) {
                AddStability(RegenRate, silent: false);
            }
        }

        /// <summary>常规挥砍记账，返回 true 表示这一挥收势时剑身疲劳碎裂</summary>
        public bool ConsumeSwing() {
            ComboResetTimer = 75;
            RegenDelay = 55;
            if (Broken) {
                return false;
            }
            Stability = MathHelper.Max(0f, Stability - SwingCost);
            return Stability <= 0f;
        }

        /// <summary>终结斩记账，返回 true 表示剑身被这一斩耗干、当场崩坏</summary>
        public bool ConsumeFinisher() {
            ComboResetTimer = 75;
            RegenDelay = 55;
            Stability = MathHelper.Max(0f, Stability - FinisherCost);
            return Stability <= 0.005f;
        }

        /// <summary>终结斩主动崩坏与疲劳碎裂共用的落态</summary>
        public void BreakBlade() {
            Broken = true;
            Stability = 0f;
            RegenDelay = 90;
        }

        /// <summary>修补完成落态，叮声由修补持械代播</summary>
        public void CompleteRepair() {
            Stability = 1f;
            Broken = false;
        }

        /// <summary>加稳固度，触顶复原；silent=true 时不叮(修补持械自带完成演出)</summary>
        public void AddStability(float amount, bool silent) {
            if (Stability >= 1f && !Broken) {
                return;
            }
            Stability += amount;
            if (Stability < 1f) {
                return;
            }
            Stability = 1f;
            Broken = false;
            if (silent || Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //最高耐久的叮声，只在手持本刃时可闻
            if (Player.HeldItem?.type == ModContent.ItemType<Shatterfang>()) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.1f }, Player.Center);
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(16f, 22f)
                        , DustID.Bone, Main.rand.NextVector2Circular(1f, 1f) - new Vector2(0f, 1f), 60, default, 0.9f);
                    d.noGravity = true;
                }
            }
        }
    }
}
