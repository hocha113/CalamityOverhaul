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
    /// 【腐臭撕裂】材质：猩红肉瘤上拔下的腐骨拳爪，爪缝渗脓。
    /// 签名：①原版身份保留：极速贴身连抓，触及全族最短之一
    /// ②连击在目标身上叠撕裂爪印（隐形标记弹幕），叠满四印引发脓爆，
    /// 挂中毒与剧毒 ③四拍左右交替小弧，第四拍双爪撕裂
    /// </summary>
    internal class GsFetidBaghnakhs : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.FetidBaghnakhs;

        protected override int HeldProjID => ModContent.ProjectileType<GsFetidBaghnakhsHeld>();

        protected override int ComboBeats => 4;

        //贴身连抓：断手窗口收紧
        protected override int ComboResetFrames => 45;

        protected override string GsDescFallback =>
            "Reforged: a four-beat point-blank claw flurry; every hit rakes a festering mark into the target, and the fourth mark bursts into pus, poisoning and envenoming everything it splatters";
        internal static readonly Color PusBright = new(214, 232, 140); //脓黄亮缘
        internal static readonly Color PusMain = new(132, 152, 62);    //腐骨橄榄
        internal static readonly Color PusHot = new(178, 255, 64);     //剧毒炽绿

        //原版 8 帧/抓是全游戏顶级近战频率；四拍循环 ~34 帧对位 4 抓 ≈ 8.5 帧/抓，
        //拍表 1.0/1.0/1.0/1.2 均摊 ~1.05x；脓爆 0.45x 需同一目标连吃 4 记均摊 ~+11%，
        //中毒/剧毒挂尾 ~+3% → 综合单体 DPS 约为原版 108%~117%，脓爆溅射是范围收益；
        //底伤不动
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 腐臭撕裂手持：四拍贴身连抓。0~2 左右交替小弧快抓，3 双爪撕裂
    /// （小前压）。命中在目标身上记撕裂印，
    /// 四印引爆脓爆。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsFetidBaghnakhsHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.FetidBaghnakhs;
        protected override int BeatCount => 4;
        protected override Color EdgeBright => GsFetidBaghnakhs.PusBright;
        protected override Color BodyMain => GsFetidBaghnakhs.PusMain;
        protected override Color HotAccent => GsFetidBaghnakhs.PusHot;

        //拳爪贴身：触及极短、判定收窄
        protected override float BaseReach => 62f;
        protected override float CollisionWidth => 26f;
        protected override float PointBlankRadius => 42f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 3) {
                //双爪撕裂：稍长的举拍并爪，撕开时带小前压
                return new GsBroadBeat {
                    Raise = 3, Hold = 1, Slash = 2, Recover = 4,
                    RaiseBack = 1.55f, Follow = 0.95f, ReachScale = 1.1f, LeanAmp = 0.04f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 1.6f, SwingPitch = -0.05f,
                };
            }
            //极短抓挠拍：左右交替，节奏微错
            bool quick = stage % 2 == 0;
            return new GsBroadBeat {
                Raise = 2, Hold = 1, Slash = 2, Recover = 3,
                RaiseBack = 1.2f, Follow = 0.8f, ReachScale = 1f, LeanAmp = 0.02f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = quick ? 0.42f : 0.3f,
            };
        }

        /// <summary>命中记账（owner 端）：叠撕裂印，四印引爆脓爆</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer || target.life <= 0) {
                return;
            }
            int markType = ModContent.ProjectileType<GsFetidBaghnakhsMarkProj>();
            Projectile mark = null;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == Projectile.owner && proj.type == markType
                    && (int)proj.ai[0] == target.whoAmI) {
                    mark = proj;
                    break;
                }
            }
            if (mark == null) {
                SpawnOwnedProj(markType, target.Center, Vector2.Zero, 0, 0f, target.whoAmI, 1f);
                return;
            }
            mark.ai[1] += 1f;
            mark.localAI[0] = 0f; //owner 端衰减计时清零
            mark.netUpdate = true;
            if (mark.ai[1] >= 4f) {
                mark.Kill();
                int burstDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
                SpawnOwnedProj(ModContent.ProjectileType<GsFetidBaghnakhsBurstProj>(),
                    target.Center, Vector2.Zero, burstDamage, Projectile.knockBack * 0.5f);
            }
        }

        protected override void PlaySwingSound() {
            //抓挠比刀砍碎而湿
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.35f, Pitch = 0.25f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 撕裂爪印：钉在目标身上的隐形标记弹幕（零伤、不绘制）。ai[0]=目标 NPC 序号
    /// ai[1]=印数（1~4，owner 递增后 netUpdate 过线）；owner 端 4 秒未续印自灭
    /// </summary>
    internal class GsFetidBaghnakhsMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int DecayFrames = 240;
        private int TargetIndex => (int)Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }
            NPC npc = Main.npc[TargetIndex];
            if (!npc.active) {
                Projectile.Kill();
                return;
            }
            //钉在目标身上随行
            Projectile.Center = npc.Center;

            //印的衰减只由 owner 裁决（远端 localAI 不重置，不许自灭）
            if (Projectile.owner == Main.myPlayer && ++Projectile.localAI[0] > DecayFrames) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// 脓爆：四印引爆的小范围爆裂。8 帧过冲撑到满径，伤害只在扩张期结算一次，
    /// 命中挂中毒与剧毒；用原版毒云贴图按半径缩放画一笔作范围提示
    /// </summary>
    internal class GsFetidBaghnakhsBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ToxicCloud;

        private const int TotalLife = 22;
        private const float MaxRadius = 88f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 8% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.08f * (p / 0.7f) : MathHelper.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.ToxicCloud];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                //脓爆：湿裂声
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.85f, Pitch = -0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath9 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
            }
        }

        //伤害只在扩张期结算一次
        public override bool? CanDamage() => Life <= 8f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);

        /// <summary>脓液蚀身：中毒 + 剧毒（AddBuff 自动同步）</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 300);
            target.AddBuff(BuffID.Venom, 180);
        }

        /// <summary>范围提示：原版毒云贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            float scale = Radius * 2f / MathF.Max(frame.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * (1f - Life01),
                0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
