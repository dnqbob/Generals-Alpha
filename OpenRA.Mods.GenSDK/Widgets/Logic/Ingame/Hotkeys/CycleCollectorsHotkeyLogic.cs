#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.GenSDK.Traits;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.GenSDK.Widgets.Logic.Ingame
{
	[ChromeLogicArgsHotkeys("CycleCollectorsKey")]
	public class CycleCollectorsHotkeyLogic : SingleHotkeyBaseLogic
	{
		readonly Viewport viewport;
		readonly ISelection selection;
		readonly World world;

		[ObjectCreator.UseCtor]
		public CycleCollectorsHotkeyLogic(Widget widget, ModData modData, WorldRenderer worldRenderer, World world, Dictionary<string, MiniYaml> logicArgs)
			: base(widget, modData, "CycleCollectorsKey", "WORLD_KEYHANDLER", logicArgs)
		{
			viewport = worldRenderer.Viewport;
			selection = world.Selection;
			this.world = world;
		}

		protected override bool OnHotkeyActivated(KeyInput e)
		{
			var player = world.RenderPlayer ?? world.LocalPlayer;

			var collectors = world.ActorsHavingTrait<SupplyCollector>()
				.Where(a => a.IsInWorld && a.Owner == player)
				.ToList();

			if (!collectors.Any())
				return true;

			var next = collectors
				.SkipWhile(b => !selection.Contains(b))
				.Skip(1)
				.FirstOrDefault();

			if (next == null)
				next = collectors.First();

			selection.Combine(world, new Actor[] { next }, false, true);
			viewport.Center(selection.Actors);

			return true;
		}
	}
}
