﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using InvSee.Extensions;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace InvSee
{
	internal class Commands
	{
		public static void DoInvSee(CommandArgs args)
		{
			if (!Main.ServerSideCharacter)
			{
				args.Player.SendErrorMessage("ServerSideCharacters must be enabled.");
				return;
			}

			PlayerInfo info = args.Player.GetPlayerInfo();

			if (args.Parameters.Count < 1)
			{
				bool restored = info.Restore(args.Player);

				if (restored)
					args.Player.SendSuccessMessage("[InvSee] Restored your inventory.");
				else
				{
					args.Player.SendInfoMessage("[InvSee] You are currently not seeing anyone's inventory.");
					args.Player.SendInfoMessage("[InvSee] Use '{0}invsee <player name>' to begin.", TShockAPI.Commands.Specifier);
				}
			}
			else
			{
				Regex regex = new Regex(@"^\w+ (?:-(s(?:ave)?)|(.+))$");
				Match match = regex.Match(args.Message);
				if (!String.IsNullOrWhiteSpace(match.Groups[1].Value))
				{
					if (!args.Player.Group.HasPermission(Permissions.InvSeeSave))
					{
						args.Player.SendErrorMessage("You don't have the permission to change player inventories!");
						return;
					}

					if (info.Backup == null || String.IsNullOrWhiteSpace(info.CopyingUserName))
						args.Player.SendErrorMessage("You are not copying any user!");
					else
					{
						User user = TShock.Users.GetUserByName(info.CopyingUserName);
						TSPlayer player;
						if (user == null)
						{
							args.Player.SendErrorMessage("Invalid user!");
							return;
						}
						else if ((player = TShock.Utils.FindPlayer(info.CopyingUserName).FirstOrDefault()) != null)
							args.Player.PlayerData.RestoreCharacter(player);
						else
						{
							try
							{
								PlayerData playerData = args.Player.PlayerData;
								TShock.CharacterDB.database.Query(
									"UPDATE tsCharacter SET Health = @0, MaxHealth = @1, Mana = @2, MaxMana = @3, Inventory = @4, spawnX = @6, spawnY = @7, hair = @8, hairDye = @9, hairColor = @10, pantsColor = @11, shirtColor = @12, underShirtColor = @13, shoeColor = @14, hideVisuals = @15, skinColor = @16, eyeColor = @17, questsCompleted = @18 WHERE Account = @5;",
									playerData.health, playerData.maxHealth, playerData.mana, playerData.maxMana, String.Join("~", playerData.inventory), player.User.ID, player.TPlayer.SpawnX, player.TPlayer.SpawnY, player.TPlayer.hair, player.TPlayer.hairDye, TShock.Utils.EncodeColor(player.TPlayer.hairColor), TShock.Utils.EncodeColor(player.TPlayer.pantsColor), TShock.Utils.EncodeColor(player.TPlayer.shirtColor), TShock.Utils.EncodeColor(player.TPlayer.underShirtColor), TShock.Utils.EncodeColor(player.TPlayer.shoeColor), TShock.Utils.EncodeBoolArray(player.TPlayer.hideVisual), TShock.Utils.EncodeColor(player.TPlayer.skinColor), TShock.Utils.EncodeColor(player.TPlayer.eyeColor), player.TPlayer.anglerQuestsFinished);
							}
							catch (Exception ex)
							{
								args.Player.SendErrorMessage("Command failed. Check logs for details.");
								TShock.Log.Error(ex.ToString());
								return;
							}
						}
						args.Player.SendInfoMessage("Saved changes made to {0}'s inventory.", user.Name);
					}
				}
				else
				{
					string playerName = match.Groups[2].Value;

					int acctid = 0;
					string name = "";
					var players = TShock.Utils.FindPlayer(playerName);
					if (players.Count == 0)
					{
						if (!args.Player.Group.HasPermission(Permissions.InvSeeUser))
						{
							args.Player.SendErrorMessage("You can't copy users!");
							return;
						}

						User user = TShock.Users.GetUserByName(playerName);
						if (user == null)
						{
							args.Player.SendErrorMessage("Invalid player or account '{0}'!", playerName);
							return;
						}
						else
						{
							acctid = user.ID;
							name = user.Name;
						}
					}
					else if (players.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, players);
						return;
					}
					else
					{
						acctid = players[0].User == null ? -1 : players[0].User.ID;
						name = players[0].User == null ? "" : players[0].User.Name;
					}

					try
					{
						PlayerData data = TShock.CharacterDB.GetPlayerData(args.Player, acctid);
						if (data == null)
						{
							args.Player.SendErrorMessage("{0}'s data not found!", name);
							return;
						}

						// Setting up backup data
						if (info.Backup == null)
						{
							info.Backup = new PlayerData(args.Player);
							info.Backup.CopyCharacter(args.Player);
						}

						info.CopyingUserName = name;
						data.RestoreCharacter(args.Player);
						args.Player.SendSuccessMessage("[InvSee] Copied {0}'s inventory.", name);
					}
					catch (Exception ex)
					{
						// In case it fails, everything is restored
						if (info.Backup != null)
						{
							info.CopyingUserName = "";
							info.Backup.RestoreCharacter(args.Player);
							info.Backup = null;
						}
						TShock.Log.ConsoleError(ex.ToString());
						args.Player.SendErrorMessage("[InvSee] Something went wrong... restored your inventory.");
					}
				}
			}
		}
	}
}
