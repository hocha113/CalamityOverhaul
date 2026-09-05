using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【影杀】材质：吸光的乌钢短刀，刃口一线冷白。
    /// 签名：①原版潜行保留：持刀静立积累潜行（原版逐帧机制照常生效），
    /// 命中会暴露行踪；本方案下击杀只回撤到半隐而非全暴露
    /// ②深潜行（潜行值低于 0.35）打出的第一击是背刺：大额加成
    /// ③双拍极速小弧
    /// </summary>
    internal class GsPsychoKnife : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PsychoKnife;

        protected override int HeldProjID => ModContent.ProjectileType<GsPsychoKnifeHeld>();

        protected override int ComboBeats => 2;

        //小刀连击断手很快
        protected override int ComboResetFrames => 40;

        protected override string GsDescFallback =>
            "Reforged: a two-beat flicker of blackened steel; the first strike from deep stealth is a backstab dealing heavily bonus damage, and kills fade you back to half-stealth instead of fully exposing you";
        internal static readonly Color ShadowBright = new(232, 226, 238); //冷白刃线
        internal static readonly Color ShadowMain = new(96, 88, 108);     //乌钢体色
        internal static readonly Color ShadowHot = new(255, 62, 76);      //杀意暗红

        //原版潜行数值（潜行越深近战伤害至 +300%、暴击 +30）走原版逐帧统计，
        //方案两侧同享不入预算；双拍循环 ~17 帧对原版 8 帧/斩 帧效率 ~0.94x，
        //底伤 +6% 兜底 → 稳态 DPS 约为原版 100%~106%。
        //背刺 1.8x 只吃深潜行后的第一击：蹲满一轮潜行约 1 秒零输出，
        //换单击 +80%，机会成本入账后不抬稳态包络，是身份爆发不是白送
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;
    }

    /// <summary>
    /// 影杀手持：双拍极速小弧，0/1 交替反手撩割。深潜行起手的那一挥携背刺旗；
    /// 命中暴露潜行、击杀回撤半隐。
    /// ai[0]=拍号 ai[1]=交替符号 ai[2]=背刺旗（owner 端 OnStageInit 写入随包过线）
    /// </summary>
    internal class GsPsychoKnifeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PsychoKnife;
        protected override int BeatCount => 2;
        protected override Color EdgeBright => GsPsychoKnife.ShadowBright;
        protected override Color BodyMain => GsPsychoKnife.ShadowMain;
        protected override Color HotAccent => GsPsychoKnife.ShadowHot;

        //小刀贴身：短触及、窄判定
        protected override float BaseReach => 70f;
        protected override float CollisionWidth => 30f;
        protected override float PointBlankRadius => 40f;

        //双拍无终结概念：不吃终结厚重音
        protected override bool IsFinisher => false;

        /// <summary>背刺旗：owner 在 OnStageInit 写入 ai[2]，各端读旗</summary>
        private bool IsBackstab => Projectile.ai[2] > 0.5f;
        private bool backstabConsumed;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 正手撩
            0 => new GsBroadBeat {
                Raise = 2, Hold = 1, Slash = 2, Recover = 3,
                RaiseBack = 1.35f, Follow = 0.85f, ReachScale = 1f, LeanAmp = 0.02f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.5f,
            },
            //拍1 反手割
            _ => new GsBroadBeat {
                Raise = 3, Hold = 1, Slash = 2, Recover = 3,
                RaiseBack = 1.45f, Follow = 0.9f, ReachScale = 1.02f, LeanAmp = 0.025f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.35f,
            },
        };

        /// <summary>深潜行起手判定：只在 owner 端读 stealth，写旗随包过线</summary>
        protected override void OnStageInit() {
            if (Owner.whoAmI == Main.myPlayer && Owner.stealth < 0.35f) {
                Projectile.ai[2] = 1f;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>背刺加成：深潜行后的第一击 1.8x（潜行机会成本入账，见方案预算注释）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (IsBackstab && !backstabConsumed) {
                modifiers.SourceDamage *= 1.8f;
            }
        }

        /// <summary>
        /// 命中记账（owner 端）：等效原版「攻击暴露」，命中重置潜行为 1 并发 84 号包；
        /// 击杀改为回撤到 0.55 半隐（返还潜行）。背刺首个命中放一记入肉音效弹幕
        /// </summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }

            if (IsBackstab && !backstabConsumed) {
                backstabConsumed = true;
                SpawnOwnedProj(ModContent.ProjectileType<GsPsychoKnifeShadowBurstProj>(),
                    target.Center, Vector2.Zero, 0, 0f, target.life <= 0 ? 1f : 0f);
            }

            //原版在物品命中路径里做 stealth=1 + SendData(84)，接管后在这等效；
            //击杀只回撤到 0.55：奖励处决，不奖励乱刀
            float old = Owner.stealth;
            Owner.stealth = target.life <= 0 ? MathF.Min(old, 0.55f) : 1f;
            if (Owner.stealth != old && Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.PlayerStealth, -1, -1, null, Owner.whoAmI);
            }
        }

        protected override void PlaySwingSound() {
            //背刺出手近乎无声，平砍是尖锐的短哨
            float volume = IsBackstab ? 0.3f : 0.6f;
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = volume, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsBackstab) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = -0.35f }, Owner.Center);
            }
        }

    }

    /// <summary>
    /// 背刺入肉音效弹幕（零伤、不绘制，只为全端同步播放影哨与利刃入肉声）；
    /// ai[0]=处决旗（击杀时补一记重响）
    /// </summary>
    internal class GsPsychoKnifeShadowBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private bool Lethal => Projectile.ai[0] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.5f }, Projectile.Center);
                if (Lethal) {
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
