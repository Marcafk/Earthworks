﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Earthworks
{
    class BlockSmeltingContainerFix : BlockSmeltingContainer
    {
        public override void DoSmelt(IWorldAccessor world, ISlotProvider cookingSlotsProvider, ItemSlot inputSlot, ItemSlot outputSlot)
        {
            ItemStack[] stacks = GetIngredients(world, cookingSlotsProvider);

            AlloyRecipe alloy = GetMatchingAlloy(world, stacks);

            Block block = world.GetBlock(new AssetLocation(CodeWithoutParts(1) + "-smelted"));
            ItemStack outputStack = new ItemStack(block);

            if (alloy != null)
            {
                ItemStack smeltedStack = alloy.Output.ResolvedItemstack.Clone();
                int units = (int)(alloy.GetTotalOutputQuantity(stacks) * 100);

                ((BlockSmeltedContainerFix)block).SetContents(outputStack, smeltedStack, units);
                outputStack.Collectible.SetTemperature(world, outputStack, GetIngredientsTemperature(world, stacks));
                outputSlot.Itemstack = outputStack;
                inputSlot.Itemstack = null;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }


                return;
            }


            MatchedSmeltableStack match = GetSingleSmeltableStack(stacks);

            if (match != null)
            {
                ((BlockSmeltedContainerFix)block).SetContents(outputStack, match.output, (int)(match.stackSize * 100));
                outputStack.Collectible.SetTemperature(world, outputStack, GetIngredientsTemperature(world, stacks));
                outputSlot.Itemstack = outputStack;
                inputSlot.Itemstack = null;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                {
                    cookingSlotsProvider.Slots[i].Itemstack = null;
                }
            }

        }
    }

    class BlockSmeltedContainerFix : BlockSmeltedContainer
    {
        internal void SetContents(ItemStack stack, ItemStack output, int units)
        {
            stack.Attributes.SetItemstack("output", output);
            stack.Attributes.SetInt("units", units);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;

            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink;

            if (be != null)
            {
                handling = EnumHandHandling.PreventDefault;
            }

            if (be != null && be.CanReceiveAny)
            {
                KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);

                if (contents.Key == null)
                {
                    slot.Itemstack = new ItemStack(byEntity.World.GetBlock(new AssetLocation(CodeWithoutParts(1) + "-burned")));
                    slot.MarkDirty();
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }


                if (HasSolidifed(slot.Itemstack, contents.Key, byEntity.World))
                {
                    return;
                }

                if (contents.Value <= 0) return;
                if (!be.CanReceive(contents.Key)) return;
                be.BeginFill(blockSel.HitPosition);

                byEntity.World.RegisterCallback((world, pos, dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        IPlayer byPlayer = null;
                        if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                        world.PlaySoundAt(new AssetLocation("sounds/hotmetal"), byEntity, byPlayer);
                    }
                }, blockSel.Position, 666);

                handling = EnumHandHandling.PreventDefault;
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            ILiquidMetalSink be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as ILiquidMetalSink;
            if (be == null) return false;

            if (!be.CanReceiveAny) return false;
            KeyValuePair<ItemStack, int> contents = GetContents(byEntity.World, slot.Itemstack);
            if (!be.CanReceive(contents.Key)) return false;

            float speed = 1.5f;
            float temp = GetTemperature(byEntity.World, slot.Itemstack);

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                tf.Origin.Set(0.5f, 0.2f, 0.5f);
                tf.Translation.Set(0, 0, -Math.Min(0.25f, speed * secondsUsed / 4));
                tf.Scale = 1f + Math.Min(0.25f, speed * secondsUsed / 4);
                tf.Rotation.X = Math.Max(-110, -secondsUsed * 90 * speed);
                byEntity.Controls.UsingHeldItemTransformBefore = tf;
            }

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);


            if (secondsUsed > 1 / speed)
            {
                if ((int)(30 * secondsUsed) % 3 == 1)
                {
                    Vec3d pos =
                        byEntity.Pos.XYZ
                        .Ahead(0.1f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                        .Ahead(1.0f, byEntity.Pos.Pitch, byEntity.Pos.Yaw - GameMath.PIHALF)
                    ;
                    pos.Y += byEntity.EyeHeight - 0.4f;

                    smokePouring.minPos = pos.AddCopy(-0.15, -0.15, -0.15);

                    Vec3d blockpos = blockSel.Position.ToVec3d().Add(0.5, 0.2, 0.5);

                    bigMetalSparks.minQuantity = Math.Max(0.2f, 1 - (secondsUsed - 1) / 4);

                    if ((int)(30 * secondsUsed) % 7 == 1)
                    {
                        bigMetalSparks.minPos = pos;
                        bigMetalSparks.minVelocity.Set(-2, -1, -2);
                        bigMetalSparks.addVelocity.Set(4, 1, 4);
                        byEntity.World.SpawnParticles(bigMetalSparks, byPlayer);

                        byEntity.World.SpawnParticles(smokePouring, byPlayer);
                    }

                    float y2 = 0;
                    Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                    Cuboidf[] collboxs = block.GetCollisionBoxes(byEntity.World.BlockAccessor, blockSel.Position);
                    for (int i = 0; collboxs != null && i < collboxs.Length; i++)
                    {
                        y2 = Math.Max(y2, collboxs[i].Y2);
                    }

                    // Metal Spark on the mold
                    bigMetalSparks.minVelocity.Set(-2, 1, -2);
                    bigMetalSparks.addVelocity.Set(4, 5, 4);
                    bigMetalSparks.minPos = blockpos.AddCopy(-0.25, y2 - 2 / 16f, -0.25);
                    bigMetalSparks.addPos.Set(0.5, 0, 0.5);
                    bigMetalSparks.glowLevel = (byte)GameMath.Clamp((int)temp - 770, 48, 128);
                    byEntity.World.SpawnParticles(bigMetalSparks, byPlayer);

                    // Smoke on the mold
                    byEntity.World.SpawnParticles(
                        Math.Max(1, 12 - (secondsUsed - 1) * 6),
                        ColorUtil.ToRgba(50, 220, 220, 220),
                        blockpos.AddCopy(-0.5, y2 - 2 / 16f, -0.5),
                        blockpos.Add(0.5, y2 - 2 / 16f + 0.15, 0.5),
                        new Vec3f(-0.5f, 0f, -0.5f),
                        new Vec3f(0.5f, 0f, 0.5f),
                        1.5f,
                        -0.05f,
                        0.75f,
                        EnumParticleModel.Quad,
                        byPlayer
                    );

                }

                int transferedAmount = Math.Min(2, contents.Value);


                be.ReceiveLiquidMetal(contents.Key, ref transferedAmount, temp);

                int newAmount = Math.Max(0, contents.Value - (2 - transferedAmount));
                slot.Itemstack.Attributes.SetInt("units", newAmount);


                if (newAmount <= 0 && byEntity.World is IServerWorldAccessor)
                {
                    slot.Itemstack = new ItemStack(byEntity.World.GetBlock(new AssetLocation(CodeWithoutParts(1) + "-burned")));
                    slot.MarkDirty();
                    // Since we change the item stack we have to call this ourselves
                    OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
                    return false;
                }

                return true;
            }

            return true;
        }

        KeyValuePair<ItemStack, int> GetContents(IWorldAccessor world, ItemStack stack)
        {
            ItemStack outstack = stack.Attributes.GetItemstack("output");
            if (outstack != null)
            {
                outstack.ResolveBlockOrItem(world);
            }
            return new KeyValuePair<ItemStack, int>(
                outstack,
                stack.Attributes.GetInt("units")
            );
        }
    }
}
