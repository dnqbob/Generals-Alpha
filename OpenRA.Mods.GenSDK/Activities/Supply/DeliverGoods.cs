﻿#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.GenSDK.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.GenSDK.Activities
{
	class DeliverGoods : Activity
	{
		readonly SupplyCollector collector;
		readonly SupplyCollectorInfo collectorInfo;
		readonly IMove move;
		readonly Mobile mobile;
		readonly Color? targetLineColor;
		bool hasWaitInLine;

		public DeliverGoods(Actor self, Color? targetLineColor = null)
		{
			collector = self.Trait<SupplyCollector>();
			collectorInfo = self.Info.TraitInfo<SupplyCollectorInfo>();
			move = self.Trait<IMove>();
			mobile = self.TraitOrDefault<Mobile>();
			this.targetLineColor = targetLineColor;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling)
				return true;

			if (collector.DeliveryBuilding == null || !collector.DeliveryBuilding.IsInWorld || !collectorInfo.DeliveryRelationships.HasRelationship(self.Owner.RelationshipWith(collector.DeliveryBuilding.Owner)))
			{
				collector.DeliveryBuilding = collector.ClosestDeliveryBuilding(self, null);
			}

			if (collector.DeliveryBuilding == null || !collector.DeliveryBuilding.IsInWorld)
			{
				QueueChild(new Wait(collectorInfo.SearchForDeliveryBuildingDelay));
				return false;
			}

			var center = collector.DeliveryBuilding;
			var centerTrait = center.Trait<SupplyCenter>();
			CPos? cell = null;
			var dist = int.MaxValue;

			// Find the nearst cell to center
			foreach (var c in centerTrait.Info.DeliveryOffsets.Select(c => center.Location + c))
			{
				if (cell == null || (c - self.Location).LengthSquared < dist)
				{
					if (c == self.Location || mobile == null || mobile.CanEnterCell(c))
					{
						cell = c;
						dist = (c - self.Location).LengthSquared;
					}
				}
			}

			// Enter the nearst cell
			if (cell != null && cell != self.Location)
			{
				QueueChild(move.MoveTo(cell.Value, 2));
				return false;
			}

			// "cell == null" means all dockable cell is occupied, we wait for a moment
			// then try to get closer
			if (cell == null)
			{
				if (!hasWaitInLine)
				{
					hasWaitInLine = true;
					QueueChild(new Wait(collectorInfo.WaitDuration));
				}
				else
				{
					hasWaitInLine = false;
					QueueChild(move.MoveTo(center.Location, 5));
				}

				return false;
			}

			if (self.Trait<IFacing>() != null)
			{
				if (centerTrait.Info.Facing != null && self.Trait<IFacing>().Facing != centerTrait.Info.Facing.Value)
				{
					QueueChild(new Turn(self, centerTrait.Info.Facing.Value));
					return false;
				}
				else if (centerTrait.Info.Facing == null)
				{
					var facing = (center.CenterPosition - self.CenterPosition).Yaw;
					if (self.Trait<IFacing>().Facing != facing)
					{
						QueueChild(new Turn(self, facing));
						return false;
					}
				}
			}

			if (!collector.WorkingOnSupply)
			{
				collector.WorkingOnSupply = true;
				QueueChild(new Wait(collectorInfo.DeliveryDelay));
				return false;
			}

			var amount = collector.Amount;
			if (amount < 0)
			{
				self.QueueActivity(new FindGoods(self));
				return true;
			}

			if (centerTrait.CanGiveResource(amount))
			{
				var wsb = self.TraitsImplementing<WithSpriteBody>().Where(t => !t.IsTraitDisabled).FirstOrDefault();
				var wsda = self.Info.TraitInfoOrDefault<WithSupplyDeliveryAnimationInfo>();
				var rs = self.TraitOrDefault<RenderSprites>();
				if (rs != null && wsb != null && wsda != null && !collector.DeliveryAnimPlayed)
				{
					wsb.PlayCustomAnimation(self, wsda.DeliverySequence);
					collector.DeliveryAnimPlayed = true;
					QueueChild(new Wait(wsda.WaitDelay));
					return false;
				}

				var wsdo = self.TraitOrDefault<WithSupplyDeliveryOverlay>();
				if (wsb != null && wsdo != null && !collector.DeliveryAnimPlayed)
				{
					if (!wsdo.Visible)
					{
						wsdo.Visible = true;
						wsdo.Anim.PlayThen(wsdo.Info.Sequence, () => wsdo.Visible = false);
						collector.DeliveryAnimPlayed = true;
						QueueChild(new Wait(wsdo.Info.WaitDelay));
						return false;
					}
				}

				var amountToGive = Util.ApplyPercentageModifiers(amount, collector.ResourceMultipliers.Select(m => m.GetResourceValueModifier()));
				collector.WorkingOnSupply = false;
				collector.DeliveryAnimPlayed = false;
				centerTrait.GiveResource(amountToGive, self.Info.Name);

				collector.Amount = 0;
				collector.CheckConditions(self);
			}
			else
			{
				QueueChild(new Wait(collectorInfo.DeliveryDelay));
				return false;
			}

			self.QueueActivity(new FindGoods(self));
			return true;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (targetLineColor != null && collector.DeliveryBuilding != null)
				yield return new TargetLineNode(Target.FromActor(collector.DeliveryBuilding), targetLineColor.Value);
		}
	}
}
