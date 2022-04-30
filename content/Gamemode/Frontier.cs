namespace TC2.Frontier
{
	public static partial class Frontier
	{
		[IGamemode.Data("Frontier", "")]
		public partial struct Gamemode: IGamemode
		{
			[Flags]
			public enum Flags: uint
			{
				None = 0,
			
				
			}

			/// <summary>
			/// Match duration in seconds.
			/// </summary>
			public float match_duration = 60.00f * 60.00f * 16.00f;
			public float elapsed = default;

			[Save.Ignore] public bool finished = default;

			public Gamemode()
			{

			}

			public static void Configure()
			{

			}

			public static void Init()
			{
				App.WriteLine("Frontier Init!", App.Color.Magenta);
			}
		}

#if SERVER
		[ChatCommand.Region("nextmap", "", creative: true)]
		public static void NextMapCommand(ref ChatCommand.Context context, string map)
		{
			ref var region = ref context.GetRegion();
			if (!region.IsNull())
			{
				var map_handle = new Map.Handle(map);
				if (map_handle.id != 0)
				{
					Frontier.ChangeMap(ref region, map_handle);
				}
				else
				{
					ref var frontier = ref region.GetSingletonComponent<Frontier.Gamemode>();
					if (!frontier.IsNull())
					{
						frontier.elapsed = frontier.match_duration - 0.10f; // TODO: hack
					}
				}
			}
		}

		public static void ChangeMap(ref Region.Data region, Map.Handle map)
		{
			ref var world = ref Server.GetWorld();

			//ref var region = ref world.GetAnyRegion();
			if (!region.IsNull())
			{
				var region_id_old = region.GetID();

				if (world.TryGetFirstAvailableRegionID(out var region_id_new))
				{
					world.Save().ContinueWith(() =>
					{
						ref var world = ref Server.GetWorld();
						world.UnloadRegion(region_id_old).ContinueWith(() =>
						{
							ref var world = ref Server.GetWorld();

							ref var region_new = ref world.ImportRegion(region_id_new, map);
							if (!region_new.IsNull())
							{
								world.SetContinueRegionID(region_id_new);

								region_new.Wait().ContinueWith(() =>
								{
									Net.SetActiveRegionForAllPlayers(region_id_new);
								});
							}
						});
					});	
				}
			}
		}
#endif

#if SERVER
		[ISystem.AddFirst(ISystem.Mode.Single)]
		public static void OnAdd(ISystem.Info info, [Source.Owned] ref MapCycle.Global mapcycle)
		{
			ref var region = ref info.GetRegion();
			mapcycle.AddMaps(ref region, "frontier");
		}
#endif

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

					var top_index = 0;
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

						Frontier.ChangeMap(ref info.GetRegion(), map_name.ToString());
					}
				}
#endif
			}
		}

#if CLIENT
		public struct ScoreboardGUI: IGUICommand
		{
			public Player.Data player;
			public Frontier.Gamemode gamemode;
			public MapCycle.Global mapcycle;
			public MapCycle.Voting voting;

			public static bool show;

			public void Draw()
			{
				var alive = this.player.flags.HasAny(Player.Flags.Alive);
				var lh = 32;
				//App.WriteLine(alive);

				var window_pos = (GUI.CanvasSize * new Vector2(0.50f, 0.00f)) + new Vector2(100, 48);
				using (var window = GUI.Window.Standalone("Scoreboard", position: alive ? null : window_pos, size: new Vector2(700, 400), pivot: alive ? new Vector2(0.50f, 0.00f) : new(1.00f, 0.00f)))
				{
					this.StoreCurrentWindowTypeID();
					if (window.show)
					{
						ref var region = ref Client.GetRegion();
						ref var world = ref Client.GetWorld();
						ref var game_info = ref Client.GetGameInfo();

						if (alive)
						{
							GUI.DrawWindowBackground("ui_scoreboard_bg", new Vector4(8, 8, 8, 8));
						}

						using (GUI.Group.New(size: GUI.GetAvailableSize(), padding: new(14, 12)))
						{
							using (GUI.Group.New(size: new Vector2(GUI.GetRemainingWidth(), 32)))
							{
								GUI.Title($"{game_info.name}", size: 32);
								GUI.SameLine();
								GUI.TitleCentered($"Next map in: {GUI.FormatTime(MathF.Max(0.00f, this.gamemode.match_duration - this.gamemode.elapsed))}", size: 24, pivot: new Vector2(1, 1));
							}

							GUI.SeparatorThick();

							using (GUI.Group.New(padding: new Vector2(4, 4)))
							{
								using (GUI.Group.New(size: new(GUI.GetRemainingWidth() * 0.50f, 0), padding: new Vector2(8, 4)))
								{
									GUI.Label("Players:", $"{game_info.player_count}/{game_info.player_count_max}", font: GUI.Font.Superstar, size: 16);
									GUI.Label("Map:", game_info.map, font: GUI.Font.Superstar, size: 16);
									GUI.Label("Gamemode:", $"{game_info.gamemode}", font: GUI.Font.Superstar, size: 16);
								}

								GUI.SameLine();

								using (GUI.Group.New(size: new(GUI.GetRemainingWidth(), 0), padding: new Vector2(8, 4)))
								{
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

															using (row.Column(2, padding: new(0, 4)))
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

							GUI.NewLine(4);

							GUI.SeparatorThick();

							GUI.NewLine(4);

							using (GUI.Group.New(size: GUI.GetRemainingSpace(), padding: new Vector2(4, 4)))
							{
								//region.Query<Region.GetFactionsQuery>(Func).Execute(ref this);
								//static void Func(ISystem.Info info, Entity entity, in Faction.Data faction)
								//{
								//	ref var arg = ref info.GetParameter<ScoreboardGUI>();
								//	if (!arg.IsNull())
								//	{
								//		ref var region = ref info.GetRegion();
								//		ref var player = ref Client.GetPlayer();

								//		using (GUI.Group.New(padding: new Vector2(0, 0)))
								//		{
								//			var faction_tmp = faction;
								//			DrawFaction(ref region, ref player, ref faction_tmp, ref arg);
								//		}
								//	}
								//}

								using (var table = GUI.Table.New("Players", 3, size: new Vector2(0, GUI.GetRemainingHeight())))
								{
									if (table.show)
									{
										table.SetupColumnFlex(1);
										table.SetupColumnFixed(128);
										table.SetupColumnFixed(64);
										//table.SetupColumnFixed(64);
										//table.SetupColumnFixed(64);

										using (var row = GUI.Table.Row.New(size: new(GUI.GetRemainingWidth(), 16), header: true))
										{
											using (row.Column(0)) GUI.Title("Name", size: 20);
											using (row.Column(1)) GUI.Title("Faction", size: 20);
											//using (row.Column(2)) GUI.Title("Money", size: 20);
											using (row.Column(2)) GUI.Title("Status", size: 20);
											//using (row.Column(4)) GUI.Title("Deaths");
										}

										region.Query<Region.GetPlayersQuery>(Func).Execute(ref this);
										static void Func(ISystem.Info info, Entity entity, in Player.Data player, in Faction.Data faction)
										{
											ref var arg = ref info.GetParameter<ScoreboardGUI>();
											if (!arg.IsNull())
											{
												using (var row = GUI.Table.Row.New(size: new(GUI.GetRemainingWidth(), 16)))
												{
													using (GUI.ID.Push(entity))
													{
														var is_online = player.flags.HasAny(Player.Flags.Online);

														var alpha = is_online ? 1.00f : 0.50f;

														using (row.Column(0))
														{
															GUI.Text(player.GetName(), color: GUI.font_color_default.WithAlphaMult(alpha));
														}

														using (row.Column(1))
														{
															GUI.Title(faction.name, color: faction.color_a.WithAlphaMult(alpha));
														}

														//ref var money = ref player.GetMoneyReadOnly().Value;
														//if (!money.IsNull())
														//{
														//	using (row.Column(2))
														//	{
														//		GUI.Text($"{money.amount:0}", color: GUI.font_color_default.WithAlphaMult(alpha));
														//	}
														//}

														using (row.Column(2))
														{
															GUI.Text(is_online ? "Online" : "Offline", color: GUI.font_color_default.WithAlphaMult(alpha));
														}

														GUI.SameLine();
														GUI.Selectable2(false, play_sound: false, enabled: false, size: new Vector2(0, 0), is_readonly: true);
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
		}

		[ISystem.EarlyGUI(ISystem.Mode.Single)]
		public static void OnEarlyGUI(Entity entity, [Source.Owned] in Player.Data player, [Source.Global] in Frontier.Gamemode gamemode, [Source.Global] in MapCycle.Global mapcycle, [Source.Global] in MapCycle.Voting voting)
		{
			if (player.IsLocal())
			{
				ref readonly var kb = ref Control.GetKeyboard();
				if (kb.GetKeyDown(Keyboard.Key.Tab))
				{
					ScoreboardGUI.show = !ScoreboardGUI.show;
				}

				Spawn.RespawnGUI.window_offset = new Vector2(100, 90);
				Spawn.RespawnGUI.window_pivot = new Vector2(0, 0);

				if (ScoreboardGUI.show || (!player.flags.HasAny(Player.Flags.Alive | Player.Flags.Editor)))
				{
					var gui = new ScoreboardGUI()
					{
						player = player,
						gamemode = gamemode,
						mapcycle = mapcycle,
						voting = voting
					};
					gui.Submit();
				}
			}
		}
#endif
	}
}

