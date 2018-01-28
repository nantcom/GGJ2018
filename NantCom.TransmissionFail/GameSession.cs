using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace NantCom.TransmissionFail
{
    public class GameMatch
    {
        private const int MAX_PLAYER_PER_TEAM = 2;
        private const int MAX_TEAM = 1; //multiple team support later
        private const int MAX_MATCH_TIME = 30 * 1000; // 30 seconds

        public class TimeStamped<T>
        {
            public DateTime TimeStamp { get; set; }

            public T Data { get; set; }
        }

        /// <summary>
        /// Information about a connection
        /// </summary>
        public class ConnectionInfoPack
        {
            /// <summary>
            /// Reference to the team
            /// </summary>
            public Team Team { get; set; }

            /// <summary>
            /// Player's index in the team
            /// </summary>
            public int TeamIndex { get; set; }

            /// <summary>
            /// Information about the player
            /// </summary>
            public dynamic PlayerInfo { get; set; }

            /// <summary>
            /// This player is sniper (last in the team)
            /// </summary>
            public bool IsSniperPlayer
            {
                get
                {
                    return this.TeamIndex == this.Team.MemberCount - 1;
                }
            }

            /// <summary>
            /// Draw Commands
            /// </summary>
            public List<TimeStamped<dynamic>> DrawCommands { get; set; }
        }
        
        /// <summary>
        /// Team in the match
        /// </summary>
        public class Team
        {
            private List<string> _Players = new List<string>();

            /// <summary>
            /// Join Team
            /// </summary>
            /// <param name="connectionId"></param>
            public void Join( string connectionId)
            {
                if (_Players.Count == MAX_PLAYER_PER_TEAM)
                {
                    throw new InvalidOperationException("Team Full");
                }

                if (_Players.Contains(connectionId) == false)
                {
                    _Players.Add(connectionId);
                }
            }

            /// <summary>
            /// Get Player from given index
            /// </summary>
            /// <param name="teamIndex"></param>
            /// <returns></returns>
            public string GetPlayer( int teamIndex )
            {
                return _Players[teamIndex];
            }

            /// <summary>
            /// Number of people in team
            /// </summary>
            /// <returns></returns>
            public int MemberCount
            {
                get
                {
                    return _Players.Count;
                }
            }
        }

        /// <summary>
        /// Image
        /// </summary>
        public class Image
        {
            /// <summary>
            /// Index of this image
            /// </summary>
            public int Index { get; set; }

            /// <summary>
            /// Url of the Image
            /// </summary>
            public string Url { get; set; }

            /// <summary>
            /// Votes casted on this image
            /// </summary>
            public int Votes { get; set; }
        }

        /// <summary>
        /// Uniue ID of this Game
        /// </summary>
        public string GameId { get; private set; }

        /// <summary>
        /// Whether game has started
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Whether game has ended
        /// </summary>
        public bool IsEnded { get; private set; }

        /// <summary>
        /// Whether the game still open for join
        /// </summary>
        public bool IsOpen
        {
            get
            {
                if (this.IsStarted)
                {
                    return false;
                }

                if (this.Teams == null)
                {
                    return true; // fresh instance
                }

                var enoughPlayers = this.Teams.TrueForAll(team => team.MemberCount == MAX_PLAYER_PER_TEAM);
                if (enoughPlayers) // already enough players - game may be starting
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Time that the match will start
        /// </summary>
        public DateTime MatchStart { get; private set; }
        
        /// <summary>
        /// Connection Ids of Each Teams - TODO: Change to actual object
        /// </summary>
        public List<Team> Teams { get; private set; }

        /// <summary>
        /// All players in this match, mapped by connection id
        /// </summary>
        public Dictionary<string, ConnectionInfoPack> PlayerInfo { get; private set; }
        
        /// <summary>
        /// Images that was used in this match
        /// </summary>
        public List<Image> Images { get; private set; }

        /// <summary>
        /// Image that was selected as target
        /// </summary>
        public int SelectedTarget { get; set; }

        /// <summary>
        /// Keyword to search
        /// </summary>
        public string SearchKeyword { get; private set; }

        /// <summary>
        /// Joins this game
        /// </summary>
        /// <param name="context"></param>
        /// <param name="playerInfo"></param>
        /// <returns>True if player is the last player in the team - cannot draw only vote</returns>
        public void Join( int team, dynamic playerInfo, HubCallerContext context )
        {
            if (this.Teams == null)
            {
                this.Teams = new List<Team>();
                for (int i = 0; i < MAX_TEAM; i++)
                {
                    this.Teams.Add(new Team());
                }
            }
            
            this.Teams[team].Join(context.ConnectionId);

            if (this.PlayerInfo == null)
            {
                this.PlayerInfo = new Dictionary<string, ConnectionInfoPack>();
            }

            // keeps information about player
            this.PlayerInfo.Add(context.ConnectionId, new ConnectionInfoPack()
            {
                PlayerInfo = playerInfo,
                Team = this.Teams[team],
                TeamIndex = this.Teams[team].MemberCount - 1,
                DrawCommands = new List<TimeStamped<dynamic>>()
            });

            // create group with only one member, with just connection id as group name
            // so we can directly send message to that member
            var hub = GlobalHost.ConnectionManager.GetHubContext<FailHub>();
            hub.Groups.Add(context.ConnectionId, context.ConnectionId);

            // adds connection to the game group
            hub.Groups.Add(context.ConnectionId, this.GameId);

            // adds connection to the team group
            hub.Groups.Add(context.ConnectionId, this.GameId + "-" + team);

            // notify that player joined the team
            hub.Clients.Group(this.GameId + "-" + team).playerJoin( playerInfo );

            // game will automatically begin when all team has enough players
            this.StartMatchIfPossible();

            // whether this player is last in team
            if (this.Teams[team].MemberCount == MAX_PLAYER_PER_TEAM)
            {
                hub.Clients.Group(context.ConnectionId).playerIsHandler();
            }
        }
        
        /// <summary>
        /// Starts the match if it is possible to do so
        /// </summary>
        /// <returns></returns>
        public void StartMatchIfPossible()
        {
            if (this.IsStarted == true)
            {
                return;
            }

            Task.Run( () =>
            {
                lock (this.GameId) // ensure only one thread is doing this
                {
                    if (this.IsStarted == true)
                    {
                        return;
                    }

                    var enoughPlayers = this.Teams.TrueForAll(team => team.MemberCount == MAX_PLAYER_PER_TEAM);
                    if (enoughPlayers)
                    {
                        this.IsStarted = true;
                        this.GameLogic(); // launch the game logic
                    }

                }
            });
        }

        /// <summary>
        /// Performs Google Image Search
        /// </summary>
        /// <param name="query"></param>
        public void ImageSearch( string query)
        {
            RestClient c = new RestClient("https://www.googleapis.com/customsearch");
            RestRequest req = new RestRequest("/v1", Method.GET);
            req.AddQueryParameter("q", query);
            req.AddQueryParameter("cx", "004390839007222685273:apzz4ao_ycu");
            req.AddQueryParameter("imgSize", "large");
            req.AddQueryParameter("num", "10");
            req.AddQueryParameter("searchType", "image");
            req.AddQueryParameter("safe", "high");
            req.AddQueryParameter("key", "AIzaSyD85FOfzvnhpyTXfYAe1wEyEBRWvx8qcf0");

            var result = c.Execute(req);
            dynamic response = JObject.Parse( result.Content );

            this.Images = (from item in response.items as JArray
                           let dItem = item as dynamic
                           select new Image()
                           {
                               Url = dItem.link,
                               Votes = 0
                           }).ToList();

        }

        /// <summary>
        /// The game logic
        /// </summary>
        private void GameLogic()
        {
            this.MatchStart = DateTime.Now.AddSeconds(5);

            Task.Delay(1000).Wait(); // make sure the new player can load the page

            // notify all players of this match of the time that game will start
            var hub = GlobalHost.ConnectionManager.GetHubContext<FailHub>();
            hub.Clients.Group(this.GameId).GameStartNotify(this.MatchStart);

            string keyword = "";

            var person = new string[] { "teacher", "monkey", "man", "girl", "boy", "family", "lion" };
            var places = new string[] { " in black car", " in forest", " on the beach", "" };

            keyword += person[DateTime.Now.Second % person.Length];
            keyword += places[DateTime.Now.Second % places.Length];

            ImageSearch(keyword);
            this.SearchKeyword = keyword;

            this.SelectedTarget = DateTime.Now.Second % 10;

            Task.Delay(5100).Wait(); // wait until the match start time has passed (give 100ms lag time)
            
            foreach (var team in this.Teams)
            {
                // Tell first Player that he is spy
                hub.Clients.Group(team.GetPlayer(0)).GameStart(new
                {
                    Role = "Spy",
                    ReferencePicture = this.Images[this.SelectedTarget],
                    MatchTime = MAX_MATCH_TIME,
                });

                // Others are Operator
                for (int i = 1; i < team.MemberCount - 1; i++)
                {
                    hub.Clients.Group(team.GetPlayer(i)).GameStart(new
                    {
                        Role = "Operator",
                        MatchTime = MAX_MATCH_TIME,
                    });
                }

                // Last Player is Handler
                hub.Clients.Group(team.GetPlayer( team.MemberCount - 1)).GameStart(new
                {
                    Role = "Handler",
                    ChoicePictures = this.Images,
                    MatchTime = MAX_MATCH_TIME,
                });
            }

            Task.Delay(MAX_MATCH_TIME).Wait(); // wait until match time passed

            if (this.IsEnded == true)
            {
                return; // Game already ended by vote
            }
            
            this.IsEnded = true;
            
            hub.Clients.Group(this.GameId).GameEnd( new
            {
                Correct = this.SelectedTarget,
                CorrectUrl = this.Images[this.SelectedTarget].Url,
                keyword = keyword,
                TimeOut = true

            }); // tell everyone that match has ended and the result
        }

        /// <summary>
        /// Submits a draw command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="context"></param>
        public void SubmitDrawCommand( dynamic command, HubCallerContext context)
        {
            if (this.IsStarted == false)
            {
                throw new InvalidOperationException("Game was not started.");
            }

            if (this.IsEnded == true)
            {
                throw new InvalidOperationException("Game was ended.");
            }

            var player = this.PlayerInfo[context.ConnectionId];
            if (player.IsSniperPlayer)
            {
                throw new InvalidOperationException("Witness player cannot draw");
            }

            player.DrawCommands.Add( new TimeStamped<dynamic>() {
                TimeStamp = DateTime.Now,
                Data = command
            });
            
            if (player.DrawCommands.Count == 1)
            {
                // first command is delayed by 1 second
                Task.Delay(1000).Wait();
            }

            var nextPlayer = player.Team.GetPlayer(player.TeamIndex + 1);
            var hub = GlobalHost.ConnectionManager.GetHubContext<FailHub>();

            hub.Clients.Group(nextPlayer).NewDrawCommand(command);
        }
        
        /// <summary>
        /// Submit the vote of target
        /// </summary>
        /// <param name="index"></param>
        public void SubmitVote( int index )
        {
            if (this.IsStarted == false || this.IsEnded)
            {
                throw new InvalidOperationException("Wrong Game State");
            }

            lock (this.GameId + "-vote-" + index)
            {
                this.Images[index].Votes = this.Images[index].Votes + 1;
            }

            var hub = GlobalHost.ConnectionManager.GetHubContext<FailHub>();
            if (index == this.SelectedTarget)
            {
                this.IsEnded = true;
                hub.Clients.Group(this.GameId).GameEnd(new
                {
                    Correct = this.SelectedTarget,
                    CorrectUrl = this.Images[this.SelectedTarget].Url,
                    Keyword = this.SearchKeyword,
                    Result = true,
                    TimeOut = false
                }); // tell everyone that match has ended and 
            }
            else
            {
                this.IsEnded = true;
                hub.Clients.Group(this.GameId).GameEnd(new
                {
                    Correct = this.SelectedTarget,
                    CorrectUrl = this.Images[this.SelectedTarget].Url,
                    Keyword = this.SearchKeyword,
                    Result = false,
                    TimeOut = false
                }); // tell everyone that match has ended and 
            }
        }

        #region Interfaces for game lists

        /// <summary>
        /// All Running Games
        /// </summary>
        private static Dictionary<string, GameMatch> _RunningGames = new Dictionary<string, GameMatch>();

        /// <summary>
        /// Create new Game Session
        /// </summary>
        /// <returns></returns>
        public static GameMatch NewMatch( string gameId )
        {
            GameMatch newMatch;
            if (_RunningGames.TryGetValue( gameId, out newMatch ) == true)
            {
                throw new InvalidOperationException("Game ID is not Unique");
            }
            
            newMatch = new GameMatch()
            {
                GameId = gameId,
            };

            _RunningGames.Add(gameId, newMatch);


            var hub = GlobalHost.ConnectionManager.GetHubContext<FailHub>();
            hub.Clients.All.NewMatchCreated(GameMatch.AllOpenMatch().ToList());

            return newMatch;
        }
        
        /// <summary>
        /// List all match that you can join
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<GameMatch> AllOpenMatch()
        {
            foreach (var match in _RunningGames.Values)
            {
                if (match.IsOpen)
                {
                    yield return match;
                }
            }
        }

        /// <summary>
        /// Submit the draw command from client
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="context"></param>
        public static GameMatch ById( string gameId )
        {
            GameMatch match;
            if (_RunningGames.TryGetValue(gameId, out match) == false)
            {
                throw new InvalidOperationException("Game Not Found");
            }

            return match;
        }
                
        #endregion
    }
}