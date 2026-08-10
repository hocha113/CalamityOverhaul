using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares.Skills;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.OmniElectricFoots
{
    /// <summary>
    /// 全向电动义足，足部槽，空中二段跳、地面蓄力跳、免坠落伤
    /// <br/>蓄力满 60 帧，冲量倍率 1.55~2.6x，进度见 <see cref="OmniElectricFootHUD"/>
    /// <br/>二段跳走 tML 官方 <see cref="OmniElectricFootJump"/>，状态与推进窗口在 <see cref="OmniElectricFootPlayer"/>
    /// </summary>
    internal class OmniElectricFoot : BaseCyberware
    {
        /// <summary>满蓄帧数</summary>
        public const int FullChargeTicks = 60;

        /// <summary>
        /// 最低蓄力跳倍率。这是蹬地冲量倍率，不是高度倍率：原版起跳有 15 帧维持，
        /// 一次纯冲量拿不到，所以轻点这档要 1.55 才够持平普通跳跃
        /// </summary>
        public const float MinChargeJumpMul = 1.55f;

        /// <summary>满蓄冲量倍率；叠上推进窗口后总高度约为普通跳跃 3 倍</summary>
        public const float MaxChargeJumpMul = 2.6f;

        /// <summary>二段跳初速度</summary>
        public const float DoubleJumpSpeed = 8.6f;

        /// <summary>二段跳横向蹬力，按方向键给</summary>
        public const float DoubleJumpKick = 3.4f;

        /// <summary>基础起跳加成，参照原版蛙腿 +2.4 取保守值</summary>
        public const float GroundJumpBoost = 0.6f;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.Feet;

        public override int CapacityCost => 3;

        public override CyberwareSkillBase ActiveSkill => OmniElectricFootSkill.Instance;

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.sellPrice(0, 7, 0, 0);
        }

        /// <summary>
        /// 每帧续期二段跳额度：<see cref="Terraria.DataStructures.ExtraJumpState.Enabled"/>
        /// 会在 ResetEffects 清零，原版在贴地帧调 RefreshDoubleJumps 补上 Available
        /// </summary>
        public override void PostUpdateEquipped(Player player) {
            player.GetJumpState<OmniElectricFootJump>().Enable();
            //基础跳也带电，否则二段跳比第一段高、读起来是反的。
            //本钩子早于 UpdateJumpHeight，加成会在本帧被并入 jumpSpeed
            player.jumpSpeedBoost += GroundJumpBoost;
        }

        /// <summary>未装备返回 null</summary>
        public static OmniElectricFoot GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is OmniElectricFoot foot) {
                    return foot;
                }
            }
            return null;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            //嵌入绑定键，未绑定时可读提示
            string keyHint = CWRKeySystem.CyberwareSkill_Key?.GetAssignedKeys() is { Count: > 0 } keys
                ? $"[{keys[0]}]"
                : CWRKeySystem.Notbound.Value + $"[{CWRKeySystem.CyberwareSkill_Key?.DisplayName}]";
            tooltips.Add(new TooltipLine(Mod, "CyberwareSkillHint",
                Language.GetTextValue("Mods.CalamityOverhaul.Items.OmniElectricFoot.SkillHint", keyHint)));
        }
    }

    /// <summary>
    /// 义足二段跳。必须走官方 ExtraJump：原版空中起跳分支要求 AnyExtraJumpUsable，
    /// 且 JumpMovement 会在 PostUpdate 之前把 releaseJump 清零，自绘的按键沿判定永不成立
    /// </summary>
    internal class OmniElectricFootJump : ExtraJump
    {
        public override Position GetDefaultPosition() => AfterBottleJumps;

        //必须为 0：>0 时原版每帧把 velocity.Y 钉回静态 jumpSpeed(≈5.01)，压掉 7.6 的蹬地冲量；
        //持续上升改走 OmniElectricFootPlayer 的推进窗口
        public override float GetDurationMultiplier(Player player) => 0f;

        public override bool CanStart(Player player)
            => OmniElectricFoot.GetEquipped(player) != null
                && !player.GetModPlayer<OmniElectricFootPlayer>().ChargeLive;

        public override void OnStarted(Player player, ref bool playSound) {
            playSound = false;
            player.GetModPlayer<OmniElectricFootPlayer>().OnAirJumpStarted();
        }
    }
}
