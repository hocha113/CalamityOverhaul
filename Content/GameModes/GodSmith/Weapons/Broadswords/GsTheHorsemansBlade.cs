using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【无头骑士猎首魔剑】材质：狱火淬燃的南瓜骑士黑铁。
    /// 签名：①命中唤出烈焰南瓜头弧线追撞目标（原版召瓜保留）
    /// ②对同一目标叠猎首印（上限 4），印满后终结斩命中召四骑南瓜阵列队冲锋碾过目标线
    /// </summary>
    internal class GsTheHorsemansBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TheHorsemansBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsTheHorsemansBladeHeld>();

        protected override string GsDescFallback =>
            "Reforged: striking a foe summons a flaming jack-o'-lantern to run it down; repeated strikes on one victim brand Headhunt marks, and at full marks the finishing slash calls a cavalry of four burning pumpkins to trample the line";
        internal static readonly Color JackBright = new(255, 208, 128); //灼橙刃缘
        internal static readonly Color JackMain = new(255, 128, 34);    //南瓜狱火橙
        internal static readonly Color JackHot = new(255, 70, 16);      //炽核红橙

        /// <summary>猎首印上限</summary>
        internal const int HuntMarksMax = 4;
        /// <summary>猎首印层数；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int HuntMarks;
        /// <summary>当前被烙印的目标 whoAmI（-1 无）</summary>
        internal int HuntTargetWhoAmI = -1;

        //底伤不加成：拍伤 1.0/1.0/1.3 + 南瓜头每斩首个命中 0.7x + 印满(4)骑阵 4×0.55x 约每两循环一次
        //循环 79 帧（26+26+27）单体口径 (3.3+2.1+1.1)=6.5 单位 vs 原版全套(挥1.0+南瓜1.0)/26 帧同窗 6.08 → 综合约 107%
        //南瓜自寻与骑阵穿透对群是 AoE 收益；未攒印的开局地板约 89%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 猎首魔剑手持：三拍连击。0/1 交替斩，2 猎首重劈（长举+前压+重顿帧）。
    /// 每斩首个命中放出追撞南瓜头；印满后的终结拍命中引出南瓜骑阵。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTheHorsemansBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TheHorsemansBlade;
        protected override Color EdgeBright => GsTheHorsemansBlade.JackBright;
        protected override Color BodyMain => GsTheHorsemansBlade.JackMain;
        protected override Color HotAccent => GsTheHorsemansBlade.JackHot;

        private bool jackSpawned;
        private bool cavalryFired;

        private GsTheHorsemansBlade Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsTheHorsemansBlade : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.08f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.15f,
            },
            //拍2 猎首重劈：长举燃焰、前压、重顿帧
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.25f, Follow = 1.3f, ReachScale = 1.16f, LeanAmp = 0.09f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.2f, SwingPitch = -0.3f,
            },
        };

        //==================== 猎首音效与召瓜 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //重劈：狱火轰腔 + 厚响垫底
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.55f, Pitch = -0.3f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.45f }, Owner.Center);
            }
        }

        /// <summary>命中记账：首个命中放南瓜头；同目标叠猎首印；印满的终结拍引出骑阵</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            //南瓜头一斩只出一颗（除回拍伤取底伤摊账）
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            if (!jackSpawned) {
                jackSpawned = true;
                Vector2 from = Owner.Center + new Vector2(-facingDir * 26f, -42f);
                SpawnOwnedProj(ModContent.ProjectileType<GsTheHorsemansBladeJackProj>(),
                    from, new Vector2(-facingDir * 2.2f, -6f),
                    Math.Max(1, (int)(baseDamage * 0.7f)), Projectile.knockBack * 0.6f,
                    0f, target.whoAmI);
            }

            GsTheHorsemansBlade scheme = Scheme;
            if (scheme == null) {
                return;
            }
            //猎首印：换目标重烙，同目标累印
            if (target.whoAmI != scheme.HuntTargetWhoAmI) {
                scheme.HuntTargetWhoAmI = target.whoAmI;
                scheme.HuntMarks = 1;
            }
            else if (scheme.HuntMarks < GsTheHorsemansBlade.HuntMarksMax) {
                scheme.HuntMarks++;
                if (scheme.HuntMarks == GsTheHorsemansBlade.HuntMarksMax) {
                    //印满：狱火点燃提示
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = 0.2f }, Owner.Center);
                }
            }

            //骑阵：印满 + 终结拍，一拍只发一次
            if (IsFinisher && !cavalryFired && scheme.HuntMarks >= GsTheHorsemansBlade.HuntMarksMax) {
                cavalryFired = true;
                scheme.HuntMarks = 0;
                int dir = Math.Sign(target.Center.X - Owner.Center.X);
                if (dir == 0) {
                    dir = facingDir;
                }
                float laneY = target.Center.Y;
                int rideDamage = Math.Max(1, (int)(baseDamage * 0.55f));
                for (int i = 0; i < 4; i++) {
                    Vector2 at = new(Owner.Center.X - dir * (46f + i * 36f), laneY + (i - 1.5f) * 22f);
                    SpawnOwnedProj(ModContent.ProjectileType<GsTheHorsemansBladeJackProj>(),
                        at, new Vector2(dir, 0f), rideDamage, Projectile.knockBack * 0.5f,
                        1f, i * 5f);
                }
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.55f, Pitch = -0.2f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 烈焰南瓜头：用原版无头骑士南瓜贴图（多帧，最简帧计数）。
    /// ai[0]=0 追撞（ai[1]=目标 whoAmI，弧线加速追击，命中或坠地即爆）；
    /// ai[0]=1 骑阵（ai[1]=出阵错帧，原地起阵后沿目标线冲锋，颠簸疾驰穿透碾压）。
    /// 出场/收场只走 alpha 渐显渐隐
    /// </summary>
    internal class GsTheHorsemansBladeJackProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlamingJack;

        private const int CavalryWindupBase = 12;
        private const int CavalryRideFrames = 52;

        private int Mode => (int)Projectile.ai[0];
        private bool IsCavalry => Mode == 1;
        private int Windup => (int)Projectile.ai[1] + CavalryWindupBase;
        private ref float Age => ref Projectile.localAI[0];
        private ref float LaneY => ref Projectile.localAI[1];
        private ref float RideDir => ref Projectile.localAI[2];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.FlamingJack];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            //一颗瓜对同一目标只结算一次：追撞头命中即爆，骑阵头整程碾一下
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 110;
        }

        /// <summary>确定性伪随机（identity+salt 播种，各端一致且逐帧稳定）：给骑阵颠簸相位用</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool? CanDamage() {
            if (IsCavalry) {
                return Age > Windup ? null : false;
            }
            return Age >= 2f ? null : false;
        }

        public override void AI() {
            Age++;
            if (IsCavalry) {
                CavalryAI();
            }
            else {
                ChaseAI();
            }

            //出场/收场可见度：追撞头 5 帧现身；骑阵起阵期渐显、末段渐隐（只走 alpha）
            float presence = IsCavalry
                ? (Age <= Windup
                    ? MathHelper.Clamp(Age / CavalryWindupBase, 0f, 1f)
                    : MathHelper.Clamp((Windup + CavalryRideFrames + 4 - Age) / 10f, 0f, 1f))
                : MathHelper.Clamp(Age / 5f, 0f, 1f);
            Projectile.alpha = (int)(255f * (1f - presence));

            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        /// <summary>追撞：转向率与速度随寿命抬升，弧线咬合；无标可追转坠地自爆</summary>
        private void ChaseAI() {
            if (Age == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.42f, Pitch = 0.15f }, Projectile.Center);
            }

            NPC target = null;
            int idx = (int)Projectile.ai[1];
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC n = Main.npc[idx];
                if (n.active && n.CanBeChasedBy(Projectile)) {
                    target = n;
                }
            }
            if (target == null) {
                target = FindChaseTarget(560f);
                if (target != null) {
                    Projectile.ai[1] = target.whoAmI;
                }
            }

            if (target != null) {
                float speed = MathF.Min(6f + Age * 0.5f, 17f);
                float turn = MathF.Min(0.05f + Age * 0.005f, 0.16f);
                Vector2 cur = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = Vector2.Lerp(cur, desired, turn).SafeNormalize(Vector2.UnitY) * speed;
            }
            else {
                //无标：横速衰减、重力接管、落地即爆
                Projectile.tileCollide = true;
                Projectile.velocity.X *= 0.985f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.32f, 14f);
            }
            //头身随横速前倾
            Projectile.rotation = MathHelper.Clamp(Projectile.velocity.X * 0.035f, -0.5f, 0.5f);
        }

        /// <summary>骑阵：错帧起阵，随后沿锁定线颠簸冲锋，末段收力</summary>
        private void CavalryAI() {
            if (Age == 1f) {
                LaneY = Projectile.Center.Y;
                RideDir = Projectile.velocity.X >= 0f ? 1f : -1f;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = Windup + CavalryRideFrames + 6;
            }
            if (Age <= Windup) {
                Projectile.velocity = Vector2.Zero;
                return;
            }
            if (Age == Windup + 1f && !VaultUtils.isServer) {
                //冲锋号：狱火喷腔
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = -0.25f }, Projectile.Center);
            }

            float ride = Age - Windup;
            //提速-巡航-衰力三段，全程不匀速
            float speed = MathF.Abs(Projectile.velocity.X);
            speed = ride <= 8f ? MathF.Min(19f, speed + 2.4f)
                : ride > 36f ? MathF.Max(13f, speed * 0.97f) : 19f;
            Projectile.velocity.X = RideDir * speed;
            //颠簸疾驰 + 回归锁定线
            Projectile.velocity.Y = MathF.Sin(ride * 0.5f + SegRand(3) * 6.28f) * 1.3f
                + MathHelper.Clamp((LaneY - Projectile.Center.Y) * 0.03f, -1.2f, 1.2f);
            Projectile.rotation = RideDir * 0.10f + MathF.Sin(ride * 0.5f) * 0.05f;
        }

        private NPC FindChaseTarget(float maxDist) {
            NPC best = null;
            float bestDist = maxDist;
            foreach (NPC n in Main.ActiveNPCs) {
                if (!n.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = Vector2.Distance(n.Center, Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = n;
                }
            }
            return best;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!IsCavalry) {
                //追撞头命中即爆
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //瓜体炸裂音
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.32f, Pitch = 0.35f }, Projectile.Center);
        }
    }
}
