using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【染匠异国弯刀】材质：浸饱染料的异国弯刃。签名：①「染彩」——涂抹带颜色
    /// 每一挥沿七彩预表轮换 ②弯刀长弧几何：小后摆大跟进的流畅弧线
    /// ③终结拍七彩闪：三层错相彩虹残影 + 命中迸溅彩斑
    /// </summary>
    internal class GsDyeTradersScimitar : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.DyeTradersScimitar;

        protected override int HeldProjID => ModContent.ProjectileType<GsDyeTradersScimitarHeld>();

        protected override string GsDescFallback =>
            "Reforged: each sweep dyes its arc a new color of the rainbow; " +
            "the third stroke flares all seven at once and splatters the target with dye";

        //暖金+孔雀蓝基色板
        internal static readonly Color DyeBright = new(250, 222, 158); //暖金刃缘
        internal static readonly Color DyeMain = new(46, 132, 150);    //孔雀蓝身
        internal static readonly Color DyeHot = new(255, 196, 96);     //鎏金亮
        internal static readonly Color DyeDeep = new(26, 22, 34);      //暗染影

        /// <summary>七彩预表：涂抹带轮换与彩斑取色都从这取，禁掷 rand</summary>
        internal static readonly Color[] Rainbow = [
            new(255, 76, 76),   //红
            new(255, 156, 56),  //橙
            new(255, 232, 84),  //黄
            new(96, 224, 108),  //绿
            new(72, 214, 228),  //青
            new(84, 128, 255),  //蓝
            new(196, 96, 244),  //紫
        ];

        /// <summary>七彩轮换指针，只在 myPlayer 路径消费（方案单例跨玩家共享）</summary>
        private int hueCounter;

        //底伤 +15%：商店弯刀本就偏弱，染彩为演出向、终结彩闪已含 1.3x 拍伤，
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 112%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.15f;

        /// <summary>
        /// 重实现基类出手：连段记账不变，追加 ai[2]=七彩指针随生成包过线，
        /// 各端看到同一挥的同一染色
        /// </summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[HeldProjID] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % ComboBeats;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                ModifyLocalSwing(item, player, ref beat, ref swingSign);
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                int hue = hueCounter++ % Rainbow.Length;
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    HeldProjID, player.GetWeaponDamage(item), item.knockBack, player.whoAmI,
                    beat, swingSign, hue);
            }
            return false;
        }
    }

    /// <summary>
    /// 染匠异国弯刀手持：三拍长弧。0/1 流畅弯斩（小后摆大跟进），2 七彩终结闪。
    /// ai[0]=拍号 ai[1]=交替符号 ai[2]=七彩指针
    /// </summary>
    internal class GsDyeTradersScimitarHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.DyeTradersScimitar;
        protected override Color EdgeBright => GsDyeTradersScimitar.DyeBright;
        protected override Color BodyMain => GsDyeTradersScimitar.DyeMain;
        protected override Color HotAccent => GsDyeTradersScimitar.DyeHot;
        protected override Color DeepShadow => GsDyeTradersScimitar.DyeDeep;

        /// <summary>本挥染色指针（ai[2] 随生成包过线，各端一致）</summary>
        private int HueIndex => Math.Clamp((int)Projectile.ai[2], 0, GsDyeTradersScimitar.Rainbow.Length - 1);

        protected override GsBroadBeat GetBeat(int stage) {
            return stage switch {
                //弯斩一：小后摆大跟进的流畅长弧
                0 => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 5, Recover = 9,
                    RaiseBack = 1.1f, Follow = 1.7f, ReachScale = 1f, LeanAmp = 0.045f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0f,
                },
                //弯斩二：弧更长
                1 => new GsBroadBeat {
                    Raise = 5, Hold = 2, Slash = 5, Recover = 9,
                    RaiseBack = 1f, Follow = 1.8f, ReachScale = 1f, LeanAmp = 0.045f,
                    DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
                },
                //七彩终结：最长的满弧闪
                _ => new GsBroadBeat {
                    Raise = 7, Hold = 3, Slash = 6, Recover = 11,
                    RaiseBack = 1.35f, Follow = 2.1f, ReachScale = 1.1f, LeanAmp = 0.075f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.12f,
                },
            };
        }

        /// <summary>染彩：涂抹带外层取本挥的七彩色</summary>
        protected override Color SmearOuterColor
            => Color.Lerp(GsDyeTradersScimitar.Rainbow[HueIndex], Color.White, 0.2f);

        /// <summary>内层取下一格彩色向体色回融，弧里能读出两种染料在淌</summary>
        protected override Color SmearInnerColor
            => Color.Lerp(GsDyeTradersScimitar.Rainbow[(HueIndex + 1) % GsDyeTradersScimitar.Rainbow.Length],
                GsDyeTradersScimitar.DyeMain, 0.35f);

        protected override Color GlowColor => IsFinisher
            ? GsDyeTradersScimitar.Rainbow[HueIndex]
            : GsDyeTradersScimitar.DyeHot;

        /// <summary>终结七彩闪：三层错相彩虹刀身加色残影，彩色沿七彩表连号取</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase < PhaseSlash || fanFade <= 0.1f) {
                return;
            }
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 hand = Hand;

            for (int i = 0; i < 3; i++) {
                float phaseAngle = mainAngle - (swingDir * 0.14f * (i + 1));
                Color c = GsDyeTradersScimitar.Rainbow[(HueIndex + i + 1) % GsDyeTradersScimitar.Rainbow.Length]
                    * (fanFade * (0.3f - i * 0.07f));
                c.A = 0;
                Vector2 at = hand + (phaseAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                sb.Draw(tex, at, null, c, phaseAngle + rotOffset, tex.Size() / 2f, scale, effect, 0);
            }
        }

        /// <summary>命中彩斑：RainbowMk2 尘经 newColor 染色（尘 267 的着色即取 newColor，已查证）</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int spots = IsFinisher ? 12 : 6;
            for (int i = 0; i < spots; i++) {
                //彩斑沿七彩表连号取色，不掷色相
                Color c = GsDyeTradersScimitar.Rainbow[(HueIndex + i) % GsDyeTradersScimitar.Rainbow.Length];
                Dust d = Dust.NewDustPerfect(target.Center, DustID.RainbowMk2,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 0, c,
                    Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }
    }
}
