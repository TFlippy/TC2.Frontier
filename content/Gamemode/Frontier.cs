namespace TC2.Frontier
{
	public static partial class Frontier
	{
		[IGamemode.Data("Frontier", "")]
		public partial struct Gamemode: IGamemode
		{
			/// <summary>
			/// Match duration in seconds.
			/// </summary>
			public float match_duration = 60.00f * 30.00f;
			public float elapsed;

			[Save.Ignore] public bool finished;

			public static void Configure()
			{

			}

			public static void Init()
			{
				App.WriteLine("Gamemode Init!", App.Color.Magenta);
			}

			[ISystem.VeryLateUpdate(ISystem.Mode.Single)]
			public static void OnUpdate(ISystem.Info info, [Source.Global] ref Frontier.Gamemode frontier, [Source.Global] in MapCycle.Global mapcycle, [Source.Global] ref MapCycle.Voting voting)
			{
				if (frontier.elapsed < frontier.match_duration)
				{
					frontier.elapsed += info.DeltaTime;

#if SERVER
					frontier.finished = frontier.elapsed >= frontier.match_duration;
					if (frontier.finished)
					{
						ref var world = ref Server.GetWorld();

						var weights = new FixedArray16<float>();

						ref var votes = ref voting.votes;
						for (int i = 0; i < votes.Length; i++)
						{
							ref var vote = ref votes[i];
							if (vote.player_id != 0)
							{
								weights[vote.map_index] += vote.weight;
							}
						}

						var top_index = -1;
						var top_weight = 0.00f;

						for (int i = 0; i < weights.Length; i++)
						{
							var weight = weights[i];
							if (weight > top_weight)
							{
								top_weight = weight;
								top_index = i;
							}
						}

						if (top_index != -1)
						{
							var map_name = mapcycle.maps[top_index];

							voting.votes = default;
							frontier.elapsed = 0.00f;

							world.SetNextMap(map_name);

							world.Save();
							world.SaveRegion(ref info.GetRegion());


							App.WriteLine($"finished ({map_name})");
							App.Quit();
							//App.Restart();

						}
					}
#endif
				}
			}
		}

#if CLIENT
		public struct HUD: IGUICommand
		{
			public Frontier.Gamemode frontier;
			public MapCycle.Global mapcycle;
			public MapCycle.Voting voting;

			public void Draw()
			{
				var position = new Vector2(GUI.CanvasSize.X - 16, 16);

				var lh = 32;

				using (var window = GUI.Window.Standalone("MapCycle", position: position, pivot: new(1.00f, 0.00f), size: new(400, 0)))
				{
					this.StoreCurrentWindowTypeID();
					if (window.show)
					{
						GUI.Title($"Match ends in: {GUI.FormatTime(this.frontier.match_duration - this.frontier.elapsed)}", size: 24);

						GUI.Separator();
						GUI.NewLine();

						var weights = new FixedArray16<float>();

						ref var votes = ref this.voting.votes;
						for (int i = 0; i < votes.Length; i++)
						{
							ref var vote = ref votes[i];
							if (vote.player_id != 0)
							{
								weights[vote.map_index] += vote.weight;
							}
						}

						using (var table = GUI.Table.New("MapCycle.Table", 3))
						{
							if (table.show)
							{
								table.SetupColumnFixed(64);
								table.SetupColumnFixed(lh);
								table.SetupColumnFlex(1);

								ref var maps = ref this.mapcycle.maps;
								for (int i = 0; i < maps.Length; i++)
								{
									ref var map = ref maps[i];
									if (!map.IsEmpty())
									{
										using (GUI.ID.Push(i))
										{
											using (var row = table.NextRow(lh))
											{
												using (row.Column(0))
												{
													if (GUI.DrawButton("Vote", new Vector2(64, lh)))
													{
														var rpc = new MapCycle.VoteRPC()
														{
															map_index = i
														};
														rpc.Send();
													}
													if (GUI.IsItemHovered()) using (GUI.Tooltip.New()) GUI.Text("Vote for this map to be played next.");
												}

												using (row.Column(1))
												{
													GUI.TextShadedCentered($"{weights[i]:0}", pivot: new(0.50f, 0.50f), color: weights[i] > 0 ? 0xff00ff00 : default, font: GUI.Font.Superstar, size: 20, shadow_offset: new(2, 2));								
												}

												using (row.Column(2, padding: new(4, 4)))
												{
													GUI.TextShadedCentered(map, pivot: new(0.00f, 0.50f), font: GUI.Font.Superstar, size: 20, shadow_offset: new(2, 2));
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
		}

		[ISystem.GUI(ISystem.Mode.Single)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void OnGUI([Source.Global] in Frontier.Gamemode frontier, [Source.Global] in MapCycle.Global mapcycle, [Source.Global] in MapCycle.Voting voting)
		{
			var gui = new Frontier.HUD()
			{
				frontier = frontier,
				mapcycle = mapcycle,
				voting = voting
			};
			gui.Submit();
		}
#endif
	}
}

