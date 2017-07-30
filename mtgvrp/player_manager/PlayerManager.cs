﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using GrandTheftMultiplayer.Server;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Server.Managers;
using GrandTheftMultiplayer.Shared;
using mtgvrp.weapon_manager;
using mtgvrp.inventory;
using mtgvrp.core;
using mtgvrp.core.Help;
using mtgvrp.group_manager;

namespace mtgvrp.player_manager
{
    class PlayerManager : Script
    {
        private static Dictionary<int, Character> _players = new Dictionary<int, Character>();

        public static List<Character> Players => _players.Values.ToList();

        public Timer PlayerSaveTimer = new Timer();

        public static void AddPlayer(Character c)
        {
            int id = -1;
            for (var i = 0; i < API.shared.getMaxPlayers(); i++)
            {
                if (_players.ContainsKey(i) == false)
                {
                    id = i;
                    break;
                }
            }

            if(id == -1) return;

            _players.Add(id, c);
        }

        public static void RemovePlayer(Character c)
        {
            _players.Remove(GetPlayerId(c));
        }


        public PlayerManager()
        {
            DebugManager.DebugMessage("[PlayerM] Initalizing player manager...");

            API.onPlayerConnected += OnPlayerConnected;
            API.onPlayerDisconnected += OnPlayerDisconnected;
            API.onClientEventTrigger += API_onClientEventTrigger;
            API.onPlayerRespawn += API_onPlayerRespawn;
            API.onPlayerHealthChange += API_onPlayerHealthChange;

            //Setup respawn timer.
            PlayerSaveTimer.Interval = 900000;
            PlayerSaveTimer.Elapsed += PlayerSaveTimer_Elapsed;
            PlayerSaveTimer.Start();

            DebugManager.DebugMessage("[PlayerM] Player Manager initalized.");
        }

        private void PlayerSaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            foreach (var player in API.shared.getAllPlayers())
            {
                if (player == null)
                    continue;

                var character = player.GetCharacter();
                if (character == null)
                    continue;

                character.Save();

                player.sendChatMessage("Character saved.");
            }
        }

        private void API_onPlayerHealthChange(Client player, int oldValue)
        {
            Account account = API.getEntityData(player, "Account");
            if (account == null)
                return;

            if (API.getPlayerHealth(player) < oldValue && account.AdminDuty)
            {
                API.setPlayerHealth(player, 100); 
            }
        }


        private void API_onPlayerRespawn(Client player)
        {
            var character = player.GetCharacter();

            player.sendChatMessage("You were revived by the ~b~Los Santos Medical Department ~w~ and were charged $500 for hospital fees.");
            WeaponManager.RemoveAllPlayerWeapons(player);
            int amount = -500;

            if (Money.GetCharacterMoney(character) < 500)
            {
                character.BankBalance += amount;
            }
            else
            {
                InventoryManager.DeleteInventoryItem(player.GetCharacter(), typeof(Money), 500);
            }
            LogManager.Log(LogManager.LogTypes.Death, $"{character.CharacterName}[{player.socialClubName}] has died.");
        }

        public static int basepaycheck = Properties.Settings.Default.basepaycheck;
        public static int taxationAmount = Properties.Settings.Default.taxationamount;

        private void API_onClientEventTrigger(Client sender, string eventName, params object[] arguments)
        {
            if(eventName == "update_ped_for_client")
            {
                var player = (NetHandle)arguments[0];
                Character c = API.getEntityData(player, "Character");
                c?.update_ped();
            }
        }

        public void OnPlayerConnected(Client player)
        {
            var account = new Account();
            account.AccountName = player.socialClubName;

            API.setEntityData(player.handle, "Account", account);
        }

        public void OnPlayerDisconnected(Client player, string reason)
        {
            //Save data
            Character character = API.getEntityData(player.handle, "Character");

            if (character != null)
            {
                if (character.Group != Group.None)
                {
                    GroupManager.SendGroupMessage(player,
                        character.CharacterName + " from your group has left the server. (" + reason + ")");
                }

                var account = player.GetAccount();
                account.Save();
                character.Health = API.getPlayerHealth(player);
                character.LastPos = player.position;
                character.LastRot = player.rotation;
                character.GetTimePlayed(); //Update time played before save.
                character.Save();
                RemovePlayer(character);
                LogManager.Log(LogManager.LogTypes.Connection, $"{character.CharacterName}[{player.socialClubName}] has left the server.");
            }
            else
                LogManager.Log(LogManager.LogTypes.Connection, $"{player.socialClubName} has left the server. (Not logged into a character)");
        }

        public static void UpdatePlayerNametags()
        {
            foreach(var c in Players)
            {
                c.update_nametag();
            }
        }

        public static Client GetPlayerByName(string name)
        {
            if (name == null)
                return null;

            foreach (var c in Players)
            {
                if (c.Client.GetAccount().AdminName == null)
                    c.Client.GetAccount().AdminName = "";

                if (c.CharacterName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    c.Client.GetAccount().AdminName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    c.CharacterName.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
                    c.Client.GetAccount().AdminName.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    return c.Client;
                }
            }

            return null;
        }

        public static Client GetPlayerById(int id)
        {
            if (!_players.ContainsKey(id))
            {
                return null;
            }

            var c = _players[id];

            return c.Client ?? null;
        }

        public static int GetPlayerId(Character c)
        {
            if (_players.ContainsValue(c))
                return _players.Single(x => x.Value == c).Key;
            else
            {
                Console.WriteLine("NEGATIVE ONE ID RETURNED");
                return -1;
            }
        }

        public static Client ParseClient(string input)
        {
            var c = GetPlayerByName(input);

            if (c != null) return c;

            var id = -1;
            if(int.TryParse(input, out id))
            {
                c = GetPlayerById(id);
            }

            return c;
        }

        public static string GetName(Client player)
        {
            Character c = API.shared.getEntityData(player.handle, "Character");
            return c.CharacterName;
        }

        public static string GetAdminName(Client player)
        {
            Account account = API.shared.getEntityData(player.handle, "Account");
            return account.AdminName;
        }

        public static int getVIPPaycheckBonus(Client player)
        {
            Account account = API.shared.getEntityData(player.handle, "Account");

            if (account.VipLevel == 1) { return Properties.Settings.Default.vipbonuslevelone; }
            if (account.VipLevel == 2) { return Properties.Settings.Default.vipbonusleveltwo; }
            if (account.VipLevel == 3) { return Properties.Settings.Default.vipbonuslevelthree; }
            else { return 0; }
        }

        public static int getFactionBonus(Client player)
        {
            Character character = API.shared.getEntityData(player.handle, "Character");

            if (character.Group == Group.None) { return 0; }

            if (Properties.Settings.Default.governmentbalance * character.Group.FundingPercentage / 100 - character.Group.FactionPaycheckBonus < 0 && character.Group.FundingPercentage != -1)
            {
                return 0;
            }

            return character.Group.FactionPaycheckBonus;

        }

        public static int CalculatePaycheck(Client player)
        {
            Character character = API.shared.getEntityData(player.handle, "Character");
            return basepaycheck - (Properties.Settings.Default.basepaycheck * Properties.Settings.Default.taxationamount/100) + /*(Properties.Settings.Default.basepaycheck * getVIPPaycheckBonus(player)/100) +*/ getFactionBonus(player) + character.BankBalance/1000;
        }

        public static void SendPaycheckToPlayer(Client player)
        {
            Account account = API.shared.getEntityData(player.handle, "Account");
            Character character = API.shared.getEntityData(player.handle, "Character");
            if(character != null)
                if (character.GetTimePlayed() % 3600 == 0)
                {
                    int paycheckAmount = CalculatePaycheck(player);
                    character.BankBalance += paycheckAmount;
                    Properties.Settings.Default.governmentbalance += paycheckAmount * taxationAmount / 100;
                    player.sendChatMessage("--------------PAYCHECK RECEIVED!--------------");
                    player.sendChatMessage("Base paycheck: $" + basepaycheck + ".");
                    player.sendChatMessage("Interest: $" + character.BankBalance / 1000 + ".");
                    player.sendChatMessage("You were taxed at " + taxationAmount + "%.");
                    //player.sendChatMessage("VIP bonus: " + getVIPPaycheckBonus(player) + "%.");
                    player.sendChatMessage("Faction bonus: $" + getFactionBonus(player) + ".");
                    player.sendChatMessage("----------------------------------------------");
                    player.sendChatMessage("Total: ~g~$" + paycheckAmount + "~w~.");

                player.sendPictureNotificationToPlayer("Your paycheck for ~g~$" + paycheckAmount + " ~w~has been added to your balance.", "CHAR_BANK_MAZE", 0, 0, "Maze Bank", "Paycheck Received!");
                if (account.VipLevel > 0 && account.AdminLevel < 1)
                    {
                        int result = DateTime.Compare(DateTime.Now, account.VipExpirationDate);
                        if (result == 1)
                        {
                            player.sendChatMessage(
                                "Your ~y~VIP~w~ subscription has ran out. Visit www.mt-gaming.com to renew your subscription.");
                            account.VipLevel = 0;
                        }
                    }

                    account.TotalPlayingHours++;
                    account.Save();
                    character.Save();
                }
        }

        [Command("getid", GreedyArg = true, Alias = "id"), Help(HelpManager.CommandGroups.General, "Used to find the ID of specific player name.", new [] {"Name of the target character. (Partial name accepted)"})]
        public void getid_cmd(Client sender, string playerName)
        {
            API.sendChatMessageToPlayer(sender, Color.White, "----------- Searching for: " + playerName + " -----------");
            foreach(var c in Players)
            {
                if(c.CharacterName.StartsWith(playerName, StringComparison.OrdinalIgnoreCase))
                {
                    API.sendChatMessageToPlayer(sender, Color.Grey, c.CharacterName + " - ID " + GetPlayerId(c));
                }
            }
            API.sendChatMessageToPlayer(sender, Color.White, "------------------------------------------------------------");
        }

        [Command("stats"), Help(HelpManager.CommandGroups.General, "Used to find your character statistics", new []{"ID of target character. <strong>[ADMIN ONLY]</strong>"})]          //Stats command
        public void GetStatistics(Client sender, string id = null)
        {
            var receiver = PlayerManager.ParseClient(id);
            Character character = API.getEntityData(sender.handle, "Character");
            Account account = API.shared.getEntityData(sender.handle, "Account");

            if (receiver == null)
            {
                receiver = sender;
            }

            if (account.AdminLevel < 2 && receiver != sender)
            {
                if (receiver != sender)
                {
                    return;
                }
                ShowStats(sender);
            }
            ShowStats(sender, receiver);
        }

        //Show time and time until paycheck.
        [Command("time"), Help(HelpManager.CommandGroups.General, "Used to find the server time, in-game time, various cooldowns, etc.", null)]
        public void CheckTime(Client player)
        {
            Character character = API.getEntityData(player.handle, "Character");

            API.sendChatMessageToPlayer(player, Color.White, "__________________ TIME __________________");
            API.sendChatMessageToPlayer(player, Color.Grey, "The current server time is: " + DateTime.Now.ToString("h:mm:ss tt"));
            API.sendChatMessageToPlayer(player, Color.Grey, "The current in-game time is: " + TimeWeatherManager.CurrentTime.ToString("h:mm:ss tt"));
            API.sendChatMessageToPlayer(player, Color.Grey,
                $"Time until next paycheck: { (int)(3600 - (character.GetTimePlayed() % 3600)) / 60}" + " minutes.");
            API.sendChatMessageToPlayer(player, Color.White, "__________________ TIME __________________");
        }

        [Command("attempt", GreedyArg = true), Help(HelpManager.CommandGroups.Roleplay, "Attempt to do something with a 50% chance of either success or fail.", "The attempt message")]
        public void attempt_cmd(Client player, string message)
        {
            Character character = API.getEntityData(player.handle, "Character");

            Random ran = new Random();
            var chance = ran.Next(100);

            string val = (chance <= 50) ? "succeeded" : "failed";

            ChatManager.RoleplayMessage(character, $"attempted to {message} and {val}.", ChatManager.RoleplayMe);
        }

        //Show player stats (admins can show stats of other players).
        public void ShowStats(Client sender)
        {
            ShowStats(sender, sender);
        }
 
        public void ShowStats(Client sender, Client receiver)
        {
            Character character = API.getEntityData(receiver.handle, "Character");
            Account account = API.shared.getEntityData(receiver.handle, "Account");
            Account senderAccount = API.shared.getEntityData(receiver.handle, "Account");

            API.sendChatMessageToPlayer(sender, "==============================================");
            API.sendChatMessageToPlayer(sender, "Player statistics for " + character.CharacterName);
            API.sendChatMessageToPlayer(sender, "==============================================");
            API.sendChatMessageToPlayer(sender, "~g~General:~g~");
            API.sendChatMessageToPlayer(sender,
                $"~h~Character name:~h~ {character.CharacterName} | ~h~ID:~h~ {character.Id} | ~h~Money:~h~ {Money.GetCharacterMoney(character)} | ~h~Bank balance:~h~ {character.BankBalance} | ~h~Playing hours:~h~ {character.GetPlayingHours()}  | ~h~Total hours:~h~ {account.TotalPlayingHours}");

            API.sendChatMessageToPlayer(sender,
                $"~h~Age:~h~ {character.Age} ~h~Birthplace:~h~ {character.Birthplace} ~h~Birthday:~h~ {character.Birthday} ~h~VIP level:~h~ {account.VipLevel} ~h~VIP expires:~h~ {account.VipExpirationDate}");

            API.sendChatMessageToPlayer(sender, "~b~Faction/Jobs:~b~");
            API.sendChatMessageToPlayer(sender,
                $"~h~Faction ID:~h~ {character.GroupId} ~h~Rank:~h~ {character.GroupRank} ~h~Group name:~h~ {character.Group.Name} ~h~Job 1:~h~ {character.JobOne.Name}");

            API.sendChatMessageToPlayer(sender, "~r~Property:~r~");
            API.sendChatMessageToPlayer(sender, $"~h~Owned vehicles:~h~ {character.OwnedVehicles.Count()}");

            if (senderAccount.AdminLevel > 0)
            {
                API.sendChatMessageToPlayer(sender, "~y~Admin:~y~");
                API.sendChatMessageToPlayer(sender,
                    $"~h~Admin level:~h~ {account.AdminLevel} ~h~Admin name:~h~ {account.AdminName} ~h~Last vehicle:~h~ {character?.LastVehicle?.Id} ~h~Dimension:~h~ {character?.LastDimension} ~h~Last IP:~h~ {account.LastIp}");
                API.sendChatMessageToPlayer(sender,
                    $"~h~Social Club Name:~h~ {account.AccountName}");
            }
        }
    }
}