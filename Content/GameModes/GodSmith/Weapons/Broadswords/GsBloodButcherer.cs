using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【血铁屠刀】材质：猩红血铁锻的剁骨屠刀。签名：①每记重剁给目标叠一层「放血」
    /// ②终结剖割引爆全部放血层，每层炸出一跳小范围血爆
    /// ③满三层引爆时持刀人吮血回 2 生命
    /// </summary>
    internal class GsBloodButcherer : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BloodButcherer;

        protected override int HeldProjID => ModContent.ProjectileType<GsBloodButchererHeld>();

        protected override string GsDescFallback =>
            "Reforged: heavy chops stack Exsanguination on the victim; the carving finisher detonates every stack into a blood burst, and a full three-stack burst feeds you life";
        internal static readonly Color GoreBright = new(255, 96, 96);   //鲜血亮红
        internal static readonly Color GoreMain = new(168, 32, 40);     //血铁暗红
        internal static readonly Color GoreHot = new(255, 40, 24);      //迸血炽红

        //底伤 +2%：普通拍 1.0x，终结 1.25x，引爆期望每循环 0.5~0.7x（2 层常态、3 层要跨循环经营），
        //按三拍摊算综合 DPS 约为原版 106%~116%，回血 2 点是操作奖励不进伤害账
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 血铁屠刀手持：三拍剁割连击。0/1 交替重剁（长滞帧短挥程，剁而非扫），
    /// 2 剖割终结（大弧贯穿+前压+引爆放血层）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBloodButchererHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BloodButcherer;
        protected override Color EdgeBright => GsBloodButcherer.GoreBright;
        protected override Color BodyMain => GsBloodButcherer.GoreMain;
        protected override Color HotAccent => GsBloodButcherer.GoreHot;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //剖割终结：大弧贯穿到底，引爆放血层
                return new GsBroadBeat {
                    Raise = 9, Hold = 4, Slash = 5, Recover = 12,
                    RaiseBack = 2.3f, Follow = 1.35f, ReachScale = 1.16f, LeanAmp = 0.09f,
                    DamageMult = 1.25f, Hitstop = 3, LungeSpeed = 2.6f, SwingPitch = -0.38f,
                };
            }
            //重剁：长滞帧蓄劲、短挥程急落，剁而非扫
            return new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 3, Recover = 10,
                RaiseBack = 2.0f, Follow = 0.75f, ReachScale = 1f, LeanAmp = 0.06f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 0f,
                SwingPitch = stage == 0 ? -0.2f : -0.26f,
            };
        }

        protected override void PlaySwingSound() {
            //剁击厚重，终结补一记撕裂声
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.3f }, Owner.Center);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            GsBloodButchererGlobalNPC bleed = target.GetGlobalNPC<GsBloodButchererGlobalNPC>();
            if (!IsFinisher) {
                //重剁叠放血层（攻击方端计数，可见结果经弹幕过线）
                bleed.AddStack();
                return;
            }
            int stacks = bleed.Stacks;
            if (stacks <= 0) {
                return;
            }
            bleed.ClearStacks();
            //引爆：每层 35% 底伤并入一跳血爆（除回 DamageMult 取底伤，账目见方案注释）
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            int burstDamage = Math.Max(1, (int)(baseDamage * 0.35f * stacks));
            SpawnOwnedProj(ModContent.ProjectileType<GsBloodButchererBurstProj>(),
                target.Center, Vector2.Zero, burstDamage, 2f, stacks);
            //满三层吮血：owner 端守门回 2 生命（Heal 自带回血演出并同步）
            if (stacks >= 3 && Owner.whoAmI == Main.myPlayer) {
                Owner.Heal(2);
            }
        }
    }

    /// <summary>
    /// 放血层记录（攻击方本地量：命中钩子只在攻击方端执行，引爆经弹幕生成包过线）。
    /// 层数上限 3，5 秒不续层即衰减清零
    /// </summary>
    internal class GsBloodButchererGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private const int MaxStacks = 3;
        private const uint DecayTicks = 300;

        private int stacks;
        private uint staleAt;

        /// <summary>当前有效层数（过期自动读 0）</summary>
        internal int Stacks {
            get {
                if (stacks > 0 && Main.GameUpdateCount >= staleAt) {
                    stacks = 0;
                }
                return stacks;
            }
        }

        internal void AddStack() {
            stacks = Math.Min(MaxStacks, Stacks + 1);
            staleAt = Main.GameUpdateCount + DecayTicks;
        }

        internal void ClearStacks() {
            stacks = 0;
            staleAt = 0;
        }
    }

    /// <summary>
    /// 血爆：引爆放血层的一跳小范围血浪。ai[0]=引爆层数（定半径）。
    /// 用原版血雨云贴图按当前半径缩放画一笔作范围提示
    /// </summary>
    internal class GsBloodButchererBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodCloudRaining;

        private const int LifeTicks = 26;
        private const int DamageWindow = 12;

        private int BurstStacks => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private float MaxRadius => 62f + BurstStacks * 16f;
        private float Age => LifeTicks - Projectile.timeLeft;

        /// <summary>血浪半径：前 8 帧猛涨后驻定，尾段随消散回缩</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp(Age / 8f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
                return MaxRadius * (1f - (1f - grow) * (1f - grow)) * (0.4f + 0.6f * fade);
            }
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.BloodCloudRaining];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//一跳 AoE，同目标只结算一次
            Projectile.timeLeft = LifeTicks;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Age <= DamageWindow ? null : false;

        public override void AI() {
            if (Age == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath21 with { Volume = 0.55f, Pitch = 0.25f }, Projectile.Center);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = RadiusNow;
            if (r < 8f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).LengthSquared() <= r * r;
        }

        /// <summary>范围提示：原版血雨云贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)LifeTicks, 0f, 1f);
            float scale = r * 2f / MathF.Max(frame.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * fade,
                0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
