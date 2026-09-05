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
    /// 【常青灯树圣剑】材质：缀满彩灯与饰品的节日松木魔剑。
    /// 签名：①连段轮抛三种饰品：红球落地爆裂、
    /// 金星旋转穿刺、礼盒炸成碎片扇（原版饰品星保留并升级为轮换）
    /// ②终结礼盒落点立起小圣诞树，周期洒星屑伤害 ③命中铃音
    /// </summary>
    internal class GsChristmasTreeSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ChristmasTreeSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsChristmasTreeSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: each slash hurls a festive ornament in turn: a bursting bauble, a piercing gold star, then a gift box that cracks into shards and raises a little tree of blinking lights that sprinkles stinging stardust";
        internal static readonly Color FestSnow = new(255, 248, 232);  //暖白灯
        internal static readonly Color FestPine = new(88, 176, 104);   //松绿体色
        internal static readonly Color FestRed = new(232, 62, 70);     //饰品红

        //底伤不加成：拍伤 1.0/1.0/1.3 + 饰品轮换 红球0.7x/金星0.7x/礼盒0.6x + 礼盒碎片 4×0.2x + 光树 0.25x×4跳
        //循环 71 帧（23+23+25）全中口径 ≈7.1 单位 vs 原版全套(挥1.0+饰品星1.0)/23 帧同窗 6.17 → 综合约 105%~113%
        //碎片扇与光树洒屑对群是 AoE 收益，单体典型吃不满全中口径
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 灯树圣剑手持：三拍轻快连击，铃音逐拍上行。0 红球拍 / 1 金星拍 / 2 礼盒终结
    /// （重敲+前压，礼盒落点立树）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsChristmasTreeSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.ChristmasTreeSword;
        protected override Color EdgeBright => GsChristmasTreeSword.FestSnow;
        protected override Color BodyMain => GsChristmasTreeSword.FestPine;
        protected override Color HotAccent => GsChristmasTreeSword.FestRed;

        private bool ornamentThrown;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 红球斩：轻快高音
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.8f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.12f,
            },
            //拍1 金星斩：回手更高一格
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.26f,
            },
            //拍2 礼盒重敲：前压抛盒
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 5, Recover = 10,
                RaiseBack = 2.15f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.1f,
            },
        };

        //==================== 节日演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            //铃音节拍：连段逐拍上行，终结补一记高音和声
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.4f, Pitch = 0.08f + 0.18f * ComboStage }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = 0.65f }, Owner.Center);
            }
        }

        /// <summary>圣诞轮换：每拍斩切爆发抛出本拍饰品（除回拍伤取底伤摊账）</summary>
        protected override void OnSlashBegin() {
            if (ornamentThrown) {
                return;
            }
            ornamentThrown = true;
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            int mode = ComboStage;
            float frac = mode == 2 ? 0.6f : 0.7f;
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 vel = mode switch {
                0 => dir * 8.5f + new Vector2(0f, -2.6f),  //红球：饱满抛物线
                1 => dir * 12.5f + new Vector2(0f, -1.2f), //金星：快而平
                _ => dir * 7.5f + new Vector2(0f, -3.4f),  //礼盒：高抛落点开树
            };
            SpawnOwnedProj(ModContent.ProjectileType<GsChristmasTreeSwordOrnamentProj>(),
                Hand + dir * (FullReach * 0.55f), vel,
                Math.Max(1, (int)(baseDamage * frac)), Projectile.knockBack * 0.5f, mode);
        }

        /// <summary>命中碎铃音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Volume = 0.26f,
                    Pitch = Main.rand.NextFloat(0.4f, 0.7f),
                    MaxInstances = 3
                }, target.Center);
            }
        }
    }

    /// <summary>
    /// 节日饰品：ai[0]=模式（0 红球：重力抛物+弹跳一次+爆裂；1 金星：平抛自旋穿 3；
    /// 2 礼盒：高抛落地开箱，炸 4 碎片扇并立起圣诞树；3 碎片：礼盒抛出的小彩屑）。
    /// 全程重力弧线禁匀速；用原版灯树剑饰品贴图，按模式取帧
    /// </summary>
    internal class GsChristmasTreeSwordOrnamentProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.OrnamentFriendly;

        private int Mode => (int)Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];
        private ref float Bounces => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.OrnamentFriendly];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            Age++;
            if (Age == 1f) {
                //按模式定弹跳数与穿透：金星穿 3 且可弹两次，红球弹一次，礼盒/碎片落地即碎
                Bounces = Mode switch { 0 => 1f, 1 => 2f, _ => 0f };
                if (Mode == 1) {
                    Projectile.penetrate = 3;
                }
                if (Mode == 3) {
                    Projectile.timeLeft = 44;
                }
                //原版饰品贴图多帧即按模式取帧，单帧则恒 0
                Projectile.frame = Mode % Math.Max(1, Main.projFrames[Type]);
            }

            //重力弧线：金星平缓、其余饱满
            float gravity = Mode switch { 1 => 0.22f, 3 => 0.30f, _ => 0.34f };
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + gravity, 15f);

            //姿态：球/盒滚转，星快旋，碎片翻飞（翻飞方向按 identity 奇偶定，各端一致）
            Projectile.rotation += Mode switch {
                1 => 0.30f,
                3 => 0.22f * ((Projectile.identity & 1) == 0 ? 1f : -1f),
                _ => Projectile.velocity.X * 0.045f,
            };
        }

        /// <summary>落地：还有弹跳数就叮一声弹起，否则碎裂（礼盒即开箱）</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Bounces > 0f && MathF.Abs(oldVelocity.Y) > 1.4f) {
                Bounces--;
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * 0.6f;
                }
                Projectile.velocity.Y = -MathF.Abs(oldVelocity.Y) * 0.55f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.22f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                }
                return false;
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.2f, Pitch = 0.6f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //礼盒开箱：碎片扇 + 落点立光树（owner 端生成，随包同步）
            if (Mode == 2 && Projectile.owner == Main.myPlayer) {
                int shardDamage = Math.Max(1, (int)(Projectile.damage / 3f));
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = (-MathHelper.PiOver2 + (i - 1.5f) * 0.42f).ToRotationVector2()
                        * Main.rand.NextFloat(4.5f, 6.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        Projectile.type, shardDamage, Projectile.knockBack * 0.4f, Projectile.owner, 3f, i);
                }
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsChristmasTreeSwordTreeProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.42f)), 2f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }

            //碎裂相：按模式配音
            if (Mode == 0) {
                //红球爆裂：玻璃脆响
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.38f, Pitch = 0.35f }, Projectile.Center);
            }
            else if (Mode == 1) {
                //金星散芒
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = 0.7f }, Projectile.Center);
            }
            else if (Mode == 2) {
                //礼盒炸开：低铃 + 脆响
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.35f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.25f, Pitch = 0.55f }, Projectile.Center);
            }
        }
    }

    /// <summary>
    /// 小圣诞树：礼盒落点竖起的驻场树。坠地扎根 → 12 帧长成 → 每 22 帧洒一轮星屑并对树冠半径结算一跳
    /// → 末 12 帧折叠收场。用原版灯树剑饰品贴图按树冠半径缩放画一笔作范围提示
    /// </summary>
    internal class GsChristmasTreeSwordTreeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.OrnamentFriendly;

        private const int RootLife = 100;   //扎根后的总寿命
        private const int GrowFrames = 12;
        private const int CollapseFrames = 12;
        private const int TickInterval = 22;
        private const int FirstTick = 14;
        private const float CanopyRadius = 104f;

        private ref float Age => ref Projectile.localAI[0];
        private ref float RootMark => ref Projectile.localAI[1];
        private bool Rooted => RootMark > 0f;
        private float RootAge => Rooted ? Age - (RootMark - 1f) : 0f;
        private Vector2 CanopyCenter => Projectile.Center + new Vector2(0f, -30f);

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.OrnamentFriendly];
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            Age++;
            if (!Rooted) {
                //落苗：慢旋下坠，最多 24 帧后原地扎根
                Projectile.velocity.X *= 0.9f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.42f, 12f);
                if (Age >= 24f) {
                    Root();
                }
                return;
            }

            float ra = RootAge;
            //洒屑节拍：进入新一轮 tick 时鸣铃
            if (ra >= FirstTick && ra <= RootLife - CollapseFrames && (ra - FirstTick) % TickInterval == 0
                && !VaultUtils.isServer) {
                int tickIdx = (int)((ra - FirstTick) / TickInterval);
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Volume = 0.3f,
                    Pitch = 0.2f + 0.15f * (tickIdx % 3),
                    MaxInstances = 3
                }, Projectile.Center);
            }
        }

        private void Root() {
            RootMark = Age;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.timeLeft = RootLife;
            if (VaultUtils.isServer) {
                return;
            }
            //落成和弦：双铃
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.4f, Pitch = 0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = 0.55f }, Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!Rooted) {
                Root();
            }
            return false;
        }

        /// <summary>只在洒屑节拍的头 3 帧结算，冠内一跳</summary>
        public override bool? CanDamage() {
            if (!Rooted) {
                return false;
            }
            float ra = RootAge;
            return ra >= FirstTick && ra <= RootLife - CollapseFrames
                && (ra - FirstTick) % TickInterval < 3 ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(CanopyCenter) <= CanopyRadius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //收树铃音
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = -0.15f }, Projectile.Center);
        }

        /// <summary>范围提示：原版饰品贴图按树冠半径缩放画一笔在冠心，长成 12 帧撑开、末 12 帧折叠淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            float grow = 0.4f;
            float alpha = 1f;
            if (Rooted) {
                grow = MathHelper.Clamp(RootAge / GrowFrames, 0f, 1f);
                if (Projectile.timeLeft < CollapseFrames) {
                    alpha = Projectile.timeLeft / (float)CollapseFrames;
                    grow *= 0.5f + 0.5f * alpha;
                }
            }
            float scale = CanopyRadius * 2f * grow / MathF.Max(frame.Width, 1);
            Vector2 at = (Rooted ? CanopyCenter : Projectile.Center) - Main.screenPosition;
            Main.EntitySpriteDraw(tex, at, frame, lightColor * (0.7f * alpha), 0f, frame.Size() * 0.5f,
                scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
