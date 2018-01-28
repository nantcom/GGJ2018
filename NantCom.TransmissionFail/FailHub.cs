using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace NantCom.TransmissionFail
{
    public class FailHub : Hub
    {
        public string CreateMatch( string gameName)
        {
            var newMatch = GameMatch.NewMatch(gameName);
            return newMatch.GameId;
        }

        /// <summary>
        /// Join Game
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="playername"></param>
        /// <returns>True if player is the last player in the team - cannot draw only vote</returns>
        public void JoinMatch( string gameId, int team, dynamic playerInfo )
        {
            GameMatch.ById(gameId).Join( team, playerInfo, this.Context );
        }
        
        /// <summary>
        /// List all match that user can join
        /// </summary>
        /// <returns></returns>
        public List<GameMatch> ListMatch()
        {
            return GameMatch.AllOpenMatch().ToList();
        }
        
        /// <summary>
        /// Submid Draw Command
        /// </summary>
        /// <param name="gameId"></param>
        public void SubmitDrawCommand( string gameId, dynamic command )
        {
            GameMatch.ById(gameId).SubmitDrawCommand( command, this.Context );
        }

        /// <summary>
        /// Submid Draw Command
        /// </summary>
        /// <param name="gameId"></param>
        public void SubmitVote(string gameId, int index )
        {
            GameMatch.ById(gameId).SubmitVote(index);
        }
    }
}