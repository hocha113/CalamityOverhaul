using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public struct HitAttributeStruct
    {
        /// <summary>
        /// 设置为<see langword="true"/>必定暴击
        /// </summary>
        public bool CertainCrit;
        /// <summary>
        /// 设置为<see langword="true"/>必定不暴击，如果启用，会覆盖<see cref="CertainCrit"/>的设置
        /// </summary>
        public bool NeverCrit;
        /// <summary>
        /// 是否无视护甲
        /// </summary>
        public bool OnHitBlindArmor;
        /// <summary>
        /// 是否是一次超级攻击
        /// </summary>
        public bool SuperAttack;
        /// <summary>
        /// 蠕虫抗性衰减系数，默认为0.0f，即对不启用，如果设置为大于0的数则会换算成百分比进行伤害缩放，比如0.15f，则只造成15%伤害
        /// </summary>
        public float WormResistance = 0f;

        public HitAttributeStruct() { }
    }

    public class CWRProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        /// <summary>
        /// 是否不受特殊效果影响（如脉冲箭等）
        /// </summary>
        public bool NotSubjectToSpecialEffects;
        /// <summary>
        /// 是否具有粘滞效果
        /// </summary>
        public bool Viscosity;
        /// <summary>
        /// 是否具有穿甲抗性
        /// </summary>
        public bool PierceResist;
        /// <summary>
        /// 弹幕的发射源类型
        /// </summary>
        public byte SpanTypes;
        /// <summary>
        /// 如果大于0，将停止该实体的大部分活动以模拟冻结效果
        /// </summary>
        public int TimeFrozenTick;
        /// <summary>
        /// 弹幕的命中属性
        /// </summary>
        public HitAttributeStruct HitAttribute;
        /// <summary>
        /// 弹幕的发射源
        /// </summary>
        public IEntitySource Source;
        /// <summary>
        /// 弹幕所属的CWR物品
        /// </summary>
        private CWRItem cwrItem;
        private NPC hitNPC;
        private Vector2 offsetHitPos;
        private float offsetHitRot;
        private float oldNPCRot;
        private float npcRotUpdateSengs;
        internal int DyeItemID;
        internal bool SendDyeItemID;
        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            Source = source;

            if (source != null) {
                if (source.Context == "CWRGunShoot") {
                    Item heldItem = Main.player[projectile.owner].GetItem();
                    if (heldItem.type != ItemID.None) {
                        cwrItem = heldItem.CWR();
                    }
                }

                if (source is EntitySource_Parent parent) {
                    if (parent.Entity is Item item) {
                        if (item.Alives()) {
                            DyeItemID = item.CWR().DyeItemID;
                        }
                    }
                    else if (parent.Entity is Player player) {
                        Item heldItem = player.GetItem();
                        if (heldItem.Alives()) {
                            DyeItemID = heldItem.CWR().DyeItemID;
                        }
                    }
                    else if (parent.Entity is Projectile monProj) {
                        if (monProj.Alives()) {
                            DyeItemID = monProj.CWR().DyeItemID;
                        }
                    }
                    else if (parent.Entity is NPC npc) {
                        if (npc.Alives()) {
                            DyeItemID = npc.CWR().DyeItemID;
                        }
                    }
                }

                if (source is EntitySource_ItemUse_WithAmmo shootSource) {
                    if (shootSource.Item.Alives()) {
                        DyeItemID = shootSource.Item.CWR().DyeItemID;
                    }
                    if (DyeItemID == ItemID.None && shootSource.Player != null) {
                        Item heldItem = shootSource.Player.GetItem();
                        if (heldItem.Alives()) {
                            DyeItemID = heldItem.CWR().DyeItemID;
                        }
                        if (DyeItemID == ItemID.None) {
                            Item ammo = shootSource.Player.ChooseAmmo(shootSource.Player.GetItem());
                            if (ammo.Alives() && ammo.type == shootSource.AmmoItemIdUsed) {
                                DyeItemID = ammo.CWR().DyeItemID;
                            }
                        }
                    }
                }
            }
        }

        public void SendProjectileDyeItemID(Projectile projectile) {
            if (VaultUtils.isSinglePlayer) {
                return;//单人模式不需要发包
            }
            if (DyeItemID <= ItemID.None) {
                return;//没有染色的也不需要发包
            }
            if (!projectile.IsOwnedByLocalPlayer()) {
                return;//只让主人端发包
            }
            if (SendDyeItemID) {
                return;//已经发过包的不要再发包
            }

            SendDyeItemID = true;
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ProjectileDyeItemID);
            //这几个数都不太可能超过60000，所以转化成ushort发送节省性能
            modPacket.Write((ushort)projectile.identity);
            modPacket.Write((ushort)projectile.type);
            modPacket.Write((ushort)DyeItemID);
            modPacket.Send();
        }

        public static void HandleProjectileDyeItemID(BinaryReader reader, int whoAmI) {
            ushort identity = reader.ReadUInt16();
            ushort projID = reader.ReadUInt16();
            ushort dyeItemID = reader.ReadUInt16();
            Projectile projectile = Main.projectile.FirstOrDefault(p => p.identity == identity);
            if (projectile == null || projectile.type <= ProjectileID.None || projectile.type != projID) {
                return;
            }
            projectile.CWR().DyeItemID = dyeItemID;
            if (!VaultUtils.isServer) {
                return;
            }
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ProjectileDyeItemID);
            modPacket.Write(identity);
            modPacket.Write(projID);
            modPacket.Write(dyeItemID);
            modPacket.Send(-1, whoAmI);
        }

        public override bool PreAI(Projectile projectile) {
            SendProjectileDyeItemID(projectile);//在AI中发送一次染色数据，在这里identity等数据已经分配好了

            if (Viscosity && projectile.numHits > 0) {
                if (!hitNPC.Alives()) {
                    projectile.Kill();
                    return false;
                }
                npcRotUpdateSengs = oldNPCRot - hitNPC.rotation;
                oldNPCRot = hitNPC.rotation;
                offsetHitRot -= npcRotUpdateSengs;
                projectile.rotation = offsetHitRot;
                offsetHitPos = offsetHitPos.RotatedBy(npcRotUpdateSengs);
                projectile.Center = hitNPC.Center + offsetHitPos;
                return false;
            }

            return base.PreAI(projectile);
        }

        public override void PostAI(Projectile projectile) {
        }

        public override bool PreKill(Projectile projectile, int timeLeft) {
            DyeEffectHandle.IsDyeDustEffectActive = true;
            int dyeItemID = projectile.CWR().DyeItemID;
            if (DyeItemID > 0) {
                DyeEffectHandle.DyeShaderData = GameShaders.Armor.GetShaderFromItemId(dyeItemID);
            }
            return true;
        }

        public override void OnKill(Projectile projectile, int timeLeft) {
            cwrItem = null;
            hitNPC = null;

            DyeEffectHandle.IsDyeDustEffectActive = false;
            DyeEffectHandle.DyeShaderData = null;
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            if (HitAttribute.CertainCrit) {
                modifiers.SetCrit();
            }
            if (HitAttribute.NeverCrit) {
                modifiers.DisableCrit();
            }
            if (HitAttribute.OnHitBlindArmor) {
                if (modifiers.SuperArmor || target.defense > 999) {
                    return;
                }
                modifiers.DefenseEffectiveness *= 0f;
            }
            if (projectile.type == CWRID.Proj_ArcZap && target.IsWormBody()) {
                modifiers.FinalDamage /= 2;
            }

            if (HitAttribute.WormResistance > 0f && target.IsWormBody()) {
                modifiers.FinalDamage *= HitAttribute.WormResistance;
            }

            ModifyProjectileHitNPC(projectile, target, ref modifiers);
        }

        internal static void ModifyProjectileHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            if (projectile.type == ProjectileID.FinalFractal) {
                if (target.IsWormBody()) {
                    modifiers.FinalDamage *= 0.75f;
                }
                if (target.type == CWRID.NPC_AresLaserCannon || target.type == CWRID.NPC_AresPlasmaFlamethrower
                    || target.type == CWRID.NPC_AresTeslaCannon || target.type == CWRID.NPC_AresGaussNuke) {
                    modifiers.FinalDamage *= 0.7f;
                }
                if (target.type == CWRID.NPC_DevourerofGodsBody || target.type == CWRID.NPC_DevourerofGodsHead) {
                    modifiers.FinalDamage *= 0.7f;
                }
                if (target.type == CWRID.NPC_Polterghast) {
                    modifiers.FinalDamage *= 0.8f;
                }
            }
            else if (projectile.type == CWRID.Proj_CosmicIceBurst) {
                if (target.type == CWRID.NPC_Yharon) {
                    modifiers.FinalDamage *= 0.8f;
                }
            }
        }

        internal void SuperAttackOnHitNPC(Projectile projectile, NPC target) {
            if (projectile.type <= ProjectileID.None || !HitAttribute.SuperAttack) {
                return;
            }

            if (projectile.type == ProjectileID.DeerclopsIceSpike) {
                if (!target.boss && !target.IsWormBody() && !target.CWR().IceParclose) {
                    int type = ModContent.ProjectileType<IceParclose>();
                    Projectile.NewProjectile(projectile.FromObjectGetParent(), target.Center, Vector2.Zero
                        , type, 0, 0, projectile.owner, target.whoAmI, target.type, target.rotation);
                }
            }
            else if (projectile.type == ProjectileID.SnowBallFriendly) {
                if (projectile.numHits == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 spanPos = projectile.Center + VaultUtils.RandVr(1160, 1290);
                        Vector2 vr = spanPos.To(target.Center).UnitVector() * 15;
                        Projectile proj = Projectile.NewProjectileDirect(projectile.GetSource_FromThis(), spanPos, vr
                        , ProjectileID.FrostBeam, projectile.damage / 2, 0, projectile.owner, 1);
                        proj.penetrate = -1;
                        proj.extraUpdates = 6;
                        proj.hostile = false;
                        proj.friendly = true;
                        proj.timeLeft /= 2;
                        proj.usesLocalNPCImmunity = true;
                        proj.localNPCHitCooldown = -1;
                        proj.ArmorPenetration = 15;
                        proj.CWR().HitAttribute.NeverCrit = true;
                    }
                }
            }
        }

        private void ViscositySD(Projectile projectile, NPC target) {
            if (!Viscosity || projectile.numHits != 0) {
                return;
            }

            hitNPC = target;
            offsetHitPos = target.Center.To(projectile.Center);
            offsetHitRot = projectile.rotation;
            oldNPCRot = target.rotation;
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (!projectile.owner.TryGetPlayer(out var owner)) {
                return;//不是本地玩家发出的弹幕不处理
            }
            ViscositySD(projectile, target);
            SuperAttackOnHitNPC(projectile, target);
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor) {
            projectile.BeginDyeEffectForWorld(DyeItemID);
            return true;
        }

        public override void PostDraw(Projectile projectile, Color lightColor) {
            projectile.EndDyeEffectForWorld();
        }
    }
}
