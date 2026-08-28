using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 鱼叉枪「绞盘叉」：船用铁叉·麻链绞盘。<br/>
    /// ①链叉去而复返：收链回手即装填，没有计时装填这回事；
    /// ②叉中重敌成锚：再扣一次扳机，绞盘把你拽飞过去（位移技）；
    /// ③叉中轻敌反拽：回程链把敌人拖回你面前（回程命中一律把人往你怀里撞）。<br/>
    /// 链在外时点击=绞盘/催链。后坐 2.5px 掷叉沉手。<br/>
    /// 账目：去回双判对原版单发（×1.7 弹著上限），周期约 1.4 倍原版，
    /// 伤害行 ×1.1（原版偏弱）→ 约 118%（绞盘为机动收益，待游戏内标定）
    /// </summary>
    internal class GsHarpoon : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Harpoon;

        protected override string GsDescFallback =>
            "Reforged: the harpoon flies out and winds back; the reload IS the chain coming home.\n" +
            "Spear a heavy foe to set an anchor, then pull the trigger again to winch yourself to it.\n" +
            "Light foes get dragged back to you on the return pass";

        public override int MagSize => 1;
        public override int ReloadTicks => 1;
        public override GsReloadStyle Style => GsReloadStyle.Chain;
        public override bool UsesTimedReload => false;
        public override int PerfectWindow => 0;
        protected override bool EjectsShell => false;
        protected override float GetRecoil(bool lastRound) => 2.5f;

        /// <summary>伤害行 ×1.1：去回双判折算后余量，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.1f;

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //压掉原版鱼叉，改射自家链叉
            Projectile.NewProjectile(source, position, velocity * 1.2f,
                ModContent.ProjectileType<GsHarpoonSpearProj>(), damage, knockback, player.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.8f, Pitch = -0.25f }, position);
            }
            return false;
        }

        /// <summary>
        /// 链在外时点击=绞盘：锚定重敌则拽己，否则催链快收。
        /// 链已丢失（叉体不在场）时重新上叉解死锁
        /// </summary>
        protected override void OnBlockedUse(Item item, Player player, GsGunsEarlyPlayer mp) {
            int spearType = ModContent.ProjectileType<GsHarpoonSpearProj>();
            if (player.ownedProjectileCounts[spearType] <= 0) {
                //链丢了：重新上叉
                mp.magLeft = MagSize;
                mp.pullArmed = false;
                mp.pullTimer = 0;
                return;
            }
            if (mp.pullArmed && mp.pullTimer <= 0) {
                NPC anchor = mp.pullNpc >= 0 && mp.pullNpc < Main.maxNPCs ? Main.npc[mp.pullNpc] : null;
                if (anchor != null && anchor.active) {
                    //绞盘拽己
                    mp.pullTimer = 30;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.7f, Pitch = -0.3f }, player.Center);
                    }
                    return;
                }
                mp.pullArmed = false;
            }
            //催链快收：给在场链叉下回收令
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == spearType && proj.ai[0] != 2f) {
                    proj.ai[0] = 2f;
                    proj.ai[2] = 1f;    //快收旗标
                    proj.netUpdate = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = 0.3f }, player.Center);
                    }
                    break;
                }
            }
        }

        /// <summary>绞盘拽己：owner 权威改自身速度，抵近或超时松劲</summary>
        protected override void HoldTick(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (mp.pullTimer <= 0) {
                return;
            }
            NPC anchor = mp.pullNpc >= 0 && mp.pullNpc < Main.maxNPCs ? Main.npc[mp.pullNpc] : null;
            if (anchor == null || !anchor.active) {
                mp.pullTimer = 0;
                mp.pullArmed = false;
                return;
            }
            mp.pullTimer--;
            Vector2 toAnchor = anchor.Center - player.Center;
            float dist = toAnchor.Length();
            if (dist < 70f || mp.pullTimer <= 0) {
                //到位：松劲留冲量，链叉转入回收
                mp.pullTimer = 0;
                mp.pullArmed = false;
                player.velocity *= 0.6f;
                CommandReturn(player);
                return;
            }
            player.velocity = Vector2.Lerp(player.velocity, toAnchor.SafeNormalize(Vector2.UnitX) * 15f, 0.3f);
            //拽飞尾流（个人反馈）
            if (!VaultUtils.isServer && Main.GameUpdateCount % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center, -player.velocity * 0.15f,
                    new Color(200, 210, 220), Main.rand.NextFloat(0.2f, 0.35f))?.Configure(false, 8);
            }
        }

        /// <summary>叫回在场链叉（owner 路径）</summary>
        private void CommandReturn(Player player) {
            int spearType = ModContent.ProjectileType<GsHarpoonSpearProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == spearType && proj.ai[0] != 2f) {
                    proj.ai[0] = 2f;
                    proj.netUpdate = true;
                    break;
                }
            }
        }

        //==================== 后坐姿态：掷叉沉手 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction, 0f) * (2.5f * progress);
            player.itemRotation -= player.direction * 0.1f * progress;
        }
    }

    /// <summary>
    /// 绞盘链叉：ai[0]=状态（0 掷出 / 1 锚定 / 2 回收），ai[1]=锚定 NPC，ai[2]=快收旗标。<br/>
    /// 掷出触敌：重敌锚定（owner 写 pullArmed 供绞盘消费）、轻敌即转回收；
    /// 回收路上命中一律把敌人往玩家方向撞。收链回手＝弹匣归位。
    /// 链体逐节自绘（麻链暗节 + 铁环亮节），叉头借原版鱼叉贴图垫底 + 寒光自绘
    /// </summary>
    internal class GsHarpoonSpearProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color ChainDark = new(74, 66, 54);
        private static readonly Color ChainLight = new(148, 138, 116);
        private static readonly Color SteelGlint = new(208, 222, 230);

        private const float MaxRange = 420f;

        private Player Owner => Main.player[Projectile.owner];
        private int State {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private int AnchorNpc => (int)Projectile.ai[1];
        private bool FastReturn => Projectile.ai[2] > 0f;
        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 600;
        }

        /// <summary>锚定期不再判定；掷出与回收都咬人</summary>
        public override bool? CanDamage() => State == 1 ? false : null;

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            switch (State) {
                case 0: //掷出：铁叉带坠，超程收链
                    Projectile.localAI[0]++;
                    if (Projectile.localAI[0] > 16f) {
                        Projectile.velocity.Y += 0.1f;
                    }
                    Projectile.rotation = Projectile.velocity.ToRotation();
                    if (Vector2.Distance(owner.MountedCenter, Projectile.Center) > MaxRange
                        && Projectile.IsOwnedByLocalPlayer()) {
                        BeginReturn();
                    }
                    break;

                case 1: //锚定：钉在重敌身上随行
                    NPC anchor = AnchorNpc >= 0 && AnchorNpc < Main.maxNPCs ? Main.npc[AnchorNpc] : null;
                    if (anchor == null || !anchor.active) {
                        if (Projectile.IsOwnedByLocalPlayer()) {
                            ClearPull(owner);
                            BeginReturn();
                        }
                        break;
                    }
                    Projectile.Center = anchor.Center + (Projectile.rotation.ToRotationVector2() * -anchor.width * 0.3f);
                    Projectile.velocity = Vector2.Zero;
                    Projectile.localAI[1]++;
                    //锚定最多 2.5 秒，超时自行拔叉（owner 判）
                    if (Projectile.localAI[1] > 150f && Projectile.IsOwnedByLocalPlayer()) {
                        ClearPull(owner);
                        BeginReturn();
                    }
                    //链上张力火花（个人反馈层）
                    if (!VaultUtils.isServer && Main.GameUpdateCount % 9 == Projectile.identity % 9) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            Main.rand.NextVector2Circular(1.5f, 1.5f), SteelGlint,
                            Main.rand.NextFloat(0.2f, 0.3f))?.Configure(false, 8);
                    }
                    break;

                case 2: //回收：加速归手，回手即装填
                    Vector2 toOwner = owner.MountedCenter - Projectile.Center;
                    float dist = toOwner.Length();
                    float speed = MathHelper.Clamp(10f + Projectile.localAI[0] * 0.3f, 10f, FastReturn ? 30f : 22f);
                    Projectile.localAI[0]++;
                    Projectile.velocity = toOwner.SafeNormalize(Vector2.UnitX) * speed;
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
                    Projectile.tileCollide = false;
                    if (dist < 32f) {
                        Projectile.Kill();
                        return;
                    }
                    break;
            }
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 2);
        }

        /// <summary>转入回收（owner 路径，随包同步）</summary>
        private void BeginReturn() {
            State = 2;
            Projectile.localAI[0] = 0f;
            Projectile.netUpdate = true;
        }

        /// <summary>解除绞盘待命（owner 本地态）</summary>
        private void ClearPull(Player owner) {
            GsGunsEarlyPlayer mp = owner.GetModPlayer<GsGunsEarlyPlayer>();
            mp.pullArmed = false;
            mp.pullTimer = 0;
        }

        /// <summary>回收路上命中：一律把人往玩家怀里撞</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (State == 2) {
                modifiers.HitDirectionOverride = Owner.Center.X < target.Center.X ? -1 : 1;
                modifiers.Knockback *= 2.2f;
                modifiers.FinalDamage *= 0.7f;  //回程判按 0.7 计
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (State == 2) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.5f, Pitch = -0.1f }, target.Center);
                }
                return;
            }
            if (State != 0) {
                return;
            }
            //重敌判据：完全抗击退或 Boss，可下锚
            bool heavy = target.boss || target.knockBackResist <= 0.05f;
            if (heavy) {
                State = 1;
                Projectile.ai[1] = target.whoAmI;
                Projectile.localAI[1] = 0f;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    GsGunsEarlyPlayer mp = Owner.GetModPlayer<GsGunsEarlyPlayer>();
                    mp.pullArmed = true;
                    mp.pullNpc = target.whoAmI;
                    //咬合重响：族内共享咬合爆（径 50）
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsGunsEarlyBurstProj>(),
                        Math.Max(1, Projectile.damage / 3), 2f, Projectile.owner, 50f, 1f);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.8f, Pitch = -0.4f }, target.Center);
                }
            }
            else if (Projectile.IsOwnedByLocalPlayer()) {
                //轻敌：即刻反拽回收
                BeginReturn();
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.6f, Pitch = 0.2f }, target.Center);
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == 0) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    BeginReturn();
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_ProcSpark>(Projectile.Center,
                            (-oldVelocity.SafeNormalize(Vector2.UnitX)).RotatedByRandom(0.7) * Main.rand.NextFloat(1.5f, 3.5f),
                            SteelGlint, Main.rand.NextFloat(0.25f, 0.4f));
                    }
                }
            }
            return false;
        }

        /// <summary>收链回手＝弹匣归位（owner 端结算，兼收线音）</summary>
        public override void OnKill(int timeLeft) {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            Player owner = Owner;
            GsGunsEarlyPlayer mp = owner.GetModPlayer<GsGunsEarlyPlayer>();
            if (mp.heldType == ItemID.Harpoon) {
                mp.magLeft = 1;
                mp.barLinger = 8;
            }
            mp.pullArmed = false;
            mp.pullTimer = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = 0.35f }, owner.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Player owner = Owner;
            Vector2 from = owner.MountedCenter;
            Vector2 to = Projectile.Center;
            Vector2 delta = to - from;
            float dist = delta.Length();
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle px = new(0, 0, 1, 1);

            //麻链逐节：暗节铁环交替，回收时链身轻微抖浪
            if (dist > 8f) {
                Vector2 dir = delta / dist;
                Vector2 normal = new(-dir.Y, dir.X);
                float rot = dir.ToRotation();
                int links = (int)(dist / 9f);
                for (int i = 0; i < links; i++) {
                    float t = (i + 0.5f) / links;
                    float sag = MathF.Sin(t * MathHelper.Pi) * (State == 1 ? 2f : 7f);
                    float wave = State == 2
                        ? MathF.Sin(t * 14f + Main.GlobalTimeWrappedHourly * 18f + Seed * 6f) * 2.2f
                        : 0f;
                    Vector2 pos = from + dir * (dist * t) + normal * (sag + wave);
                    bool ring = i % 2 == 0;
                    Color c = (ring ? ChainLight : ChainDark)
                        * Lighting.Brightness((int)(pos.X / 16f), (int)(pos.Y / 16f));
                    Main.EntitySpriteDraw(pixel, pos - Main.screenPosition, px, c, rot,
                        new Vector2(0.5f, 0.5f), new Vector2(ring ? 5f : 7f, ring ? 3.4f : 2.2f),
                        SpriteEffects.None, 0);
                }
            }

            //叉头：原版鱼叉贴图垫底 + 叉尖寒光
            Main.instance.LoadProjectile(ProjectileID.Harpoon);
            Texture2D head = TextureAssets.Projectile[ProjectileID.Harpoon].Value;
            Main.EntitySpriteDraw(head, to - Main.screenPosition, null, lightColor,
                Projectile.rotation + MathHelper.PiOver2, head.Size() / 2f, 1f, SpriteEffects.None, 0);
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star != null && State != 1) {
                float glint = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Seed * 9f);
                Color c = SteelGlint * (0.55f * glint);
                c.A = 0;
                Main.EntitySpriteDraw(star, to - Main.screenPosition + Projectile.rotation.ToRotationVector2() * 6f,
                    null, c, Projectile.rotation, star.Size() / 2f, 0.14f * glint, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
