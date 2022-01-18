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
			public float match_duration = 60.00f * 60.00f * 8.00f;
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

							world.SetNextMap(map_name);
							world.Save();
							//world.SaveRegion(ref info.GetRegion());

							App.WriteLine($"finished ({map_name})");
							App.Quit();
							//App.Restart();
						}
					}
#endif
				}
			}
		}

#if SERVER
		[ISystem.Add(ISystem.Mode.Single)]
		public static void OnAdd(ISystem.Info info, [Source.Owned] ref MapCycle.Global mapcycle)
		{
			ref var region = ref info.GetRegion();
			mapcycle.AddMaps(ref region, "frontier");
		}
#endif

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
			}
		}

		[ISystem.GUI(ISystem.Mode.Single)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void OnGUI([Source.Global] in Frontier.Gamemode frontier, [Source.Global] in MapCycle.Global mapcycle, [Source.Global] in MapCycle.Voting voting)
		{
			//var gui = new Frontier.HUD()
			//{
			//	frontier = frontier,
			//	mapcycle = mapcycle,
			//	voting = voting
			//};
			//gui.Submit();
		}
#endif

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

				var window_pos = (GUI.CanvasSize * new Vector2(0.50f, 0.00f)) + new Vector2(100, 100);
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

						using (GUI.Group.New(size: new Vector2(GUI.GetAvailableWidth(), 0), padding: new(14, 12)))
						{
							//GUI.Title($"Scoreboard - {world.name} - Deathmatch", size: 32);
							using (GUI.Group.New(size: new Vector2(GUI.GetRemainingWidth(), 32)))
							{
								GUI.Title($"{game_info.name}", size: 32);
								//GUI.OffsetLine(GUI.GetRemainingWidth() - 260);
								GUI.SameLine();
								GUI.TitleCentered($"Next map in: {GUI.FormatTime(MathF.Max(0.00f, this.gamemode.match_duration - this.gamemode.elapsed))}", size: 24, pivot: new Vector2(1, 1));
							}

							GUI.SeparatorThick();

							using (GUI.Group.New(padding: new Vector2(4, 4)))
							{
								//GUI.DrawFillBackground("ui_window", new Vector4(4, 4, 4, 4));
								//GUI.Text("<some text here>");


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

							using (GUI.Group.New(padding: new Vector2(4, 4)))
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

								using (var table = GUI.Table.New("Players", 5, size: new Vector2(0, 80)))
								{
									if (table.show)
									{
										table.SetupColumnFlex(1);
										table.SetupColumnFlex(1);
										table.SetupColumnFixed(64);
										table.SetupColumnFixed(64);
										table.SetupColumnFixed(64);

										using (var row = GUI.Table.Row.New(size: new(GUI.GetRemainingWidth(), 16), header: true))
										{
											using (row.Column(0)) GUI.Title("Name", size: 20);
											using (row.Column(1)) GUI.Title("Faction", size: 20);
											using (row.Column(2)) GUI.Title("Money", size: 20);
											using (row.Column(3)) GUI.Title("Status", size: 20);
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

														ref var money = ref player.GetMoneyReadOnly().Value;
														if (!money.IsNull())
														{
															using (row.Column(2))
															{
																GUI.Text($"{money.amount:0}", color: GUI.font_color_default.WithAlphaMult(alpha));
															}
														}

														using (row.Column(3))
														{
															GUI.Text(is_online ? "Online" : "Offline", color: GUI.font_color_default.WithAlphaMult(alpha));
														}



														//ref var score = ref entity.GetComponent<Score.Data>();
														//if (!score.IsNull())
														//{
														//	using (row.Column(3))
														//	{
														//		GUI.Text($"{score.kills}");
														//	}

														//	using (row.Column(4))
														//	{
														//		GUI.Text($"{score.deaths}");
														//	}
														//}

														GUI.SameLine();
														//GUI.Selectable("", ref selected, play_sound: false, enabled: false, size: new Vector2(0, 0));
														GUI.Selectable2(false, play_sound: false, enabled: false, size: new Vector2(0, 0), is_readonly: true);
													}
												}
											}
										}
									}
								}



								//GUI.NewLine(16);

								//using (GUI.Group.New(padding: new Vector2(0, 0)))
								//{
								//	DrawFaction(ref region, ref this.player, ref faction_b, ref this);
								//}
							}
						}
					}
				}
			}

			private static void DrawFaction(ref Region.Data region, ref Player.Data player, ref Faction.Data faction, ref ScoreboardGUI gui)
			{
				using (GUI.ID.Push(faction.ent_faction))
				{
					var color = Color32BGRA.Lerp(faction.color_a, 0xffffffff, 0.20f);

					GUI.Title(faction.name, size: 32, color: color);
					GUI.OffsetLine(GUI.GetRemainingWidth() - 80);
					if (GUI.DrawButton("Join", new Vector2(80, 32), enabled: faction.id != gui.player.faction_id && !player.flags.HasAny(Player.Flags.Alive)))
					{

					}

					GUI.NewLine(2);
					GUI.Separator();
					GUI.NewLine(2);

					using (var table = GUI.Table.New(faction.name, 5, size: new Vector2(0, 80)))
					{
						if (table.show)
						{
							table.SetupColumnFlex(1);
							table.SetupColumnFixed(64);
							table.SetupColumnFixed(64);
							table.SetupColumnFixed(64);
							table.SetupColumnFixed(64);

							using (var row = GUI.Table.Row.New(size: new(GUI.GetRemainingWidth(), 16), header: true))
							{
								using (row.Column(0)) GUI.Title("Name");
								using (row.Column(1)) GUI.Title("Money");
								using (row.Column(2)) GUI.Title("Status");
								using (row.Column(3)) GUI.Title("Kills");
								using (row.Column(4)) GUI.Title("Deaths");
							}

							region.Query<Region.GetPlayersQuery>(Func).Execute(ref faction);
							static void Func(ISystem.Info info, Entity entity, in Player.Data player, in Faction.Data faction)
							{
								ref var arg = ref info.GetParameter<Faction.Data>();
								if (!arg.IsNull() && player.faction_id == arg.id)
								{
									using (var row = GUI.Table.Row.New(size: new(GUI.GetRemainingWidth(), 20)))
									{
										using (GUI.ID.Push(entity))
										{
											using (row.Column(0))
											{
												GUI.Text(player.GetName());
											}

											ref var money = ref player.GetMoneyReadOnly().Value;
											if (!money.IsNull())
											{
												using (row.Column(1))
												{
													GUI.Text($"{money.amount:0}");
												}
											}

											using (row.Column(2))
											{
												GUI.Text(player.flags.HasAny(Player.Flags.Alive) ? "Alive" : "Dead");
											}

											//ref var score = ref entity.GetComponent<Score.Data>();
											//if (!score.IsNull())
											//{
											//	using (row.Column(3))
											//	{
											//		GUI.Text($"{score.kills}");
											//	}

											//	using (row.Column(4))
											//	{
											//		GUI.Text($"{score.deaths}");
											//	}
											//}

											var selected = false;
											GUI.SameLine();
											GUI.Selectable("", ref selected, play_sound: false, size: new Vector2(0, 0));
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

				Spawn.RespawnGUI.window_offset = new Vector2(100, 120);
				Spawn.RespawnGUI.window_pivot = new Vector2(0, 0);

				if (ScoreboardGUI.show || !player.flags.HasAll(Player.Flags.Alive))
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

