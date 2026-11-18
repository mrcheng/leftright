using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

namespace LeftRight.Rebuild
{
    [Serializable]
    public sealed class PublicDrillRoom
    {
        public string Id, Name, JoinCode;
        public int Players, Capacity;
    }

    // Keep cloud discovery separate from the actual Relay/NGO match. Lobby has
    // only the host as a member; the authoritative NGO human count is published
    // explicitly, so code joins and public-list joins report the same occupancy.
    public interface IPublicDrillDirectory
    {
        Task<PublicDrillRoom[]> QueryAsync();
        Task<PublicDrillRoom> GetAsync(string id);
        Task<string> CreateAsync(string code, int capacity, int players);
        Task UpdateAsync(string id, int players);
        Task HeartbeatAsync(string id);
        Task DeleteAsync(string id);
    }

    public sealed class UnityPublicDrillDirectory : IPublicDrillDirectory
    {
        // Change with incompatible match protocols; never advertise rooms from
        // unrelated games or join an older wire format by accident.
        const string Game = "leftright-ngo4-public1";
        static DataObject Public(string value) => new DataObject(DataObject.VisibilityOptions.Public, value);
        static Dictionary<string, DataObject> Data(string code, int players) => new Dictionary<string, DataObject>
        {
            {"game", new DataObject(DataObject.VisibilityOptions.Public, Game, DataObject.IndexOptions.S1)},
            {"state", new DataObject(DataObject.VisibilityOptions.Public, "waiting", DataObject.IndexOptions.S2)},
            {"relay", Public(code)}, {"humans", Public(players.ToString())}
        };

        static PublicDrillRoom Read(Lobby lobby)
        {
            if(lobby==null||lobby.IsPrivate||lobby.IsLocked||lobby.Data==null)return null;
            string Value(string key) => lobby.Data.TryGetValue(key,out var data)?data.Value:null;
            if(Value("game")!=Game||Value("state")!="waiting"||!int.TryParse(Value("humans"),out int players)||
               players<1||players>=lobby.MaxPlayers||lobby.MaxPlayers<2||lobby.MaxPlayers>4)return null;
            string code=Value("relay");
            if(string.IsNullOrEmpty(code)||code.Length<4||code.Length>12||code.Any(c=>!char.IsLetterOrDigit(c)))return null;
            return new PublicDrillRoom{Id=lobby.Id,Name=lobby.Name,JoinCode=code,Players=players,Capacity=lobby.MaxPlayers};
        }

        public async Task<PublicDrillRoom[]> QueryAsync()
        {
            await UnityRelayConnection.Ready();
            var result=await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions{Count=20,Filters=new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.S1,Game,QueryFilter.OpOptions.EQ),
                new QueryFilter(QueryFilter.FieldOptions.S2,"waiting",QueryFilter.OpOptions.EQ)
            }});
            // Lobby queries exclude private/inactive rooms. Validate metadata too:
            // a full or malformed listing must never produce a usable Join button.
            return result.Results.Select(Read).Where(room=>room!=null).OrderBy(room=>room.Name).ToArray();
        }
        public async Task<PublicDrillRoom> GetAsync(string id)
        {await UnityRelayConnection.Ready();return Read(await LobbyService.Instance.GetLobbyAsync(id));}
        public async Task<string> CreateAsync(string code,int capacity,int players)
        {
            await UnityRelayConnection.Ready();
            var room=await LobbyService.Instance.CreateLobbyAsync("Drill "+code,capacity,new CreateLobbyOptions{IsPrivate=false,Data=Data(code,players)});
            return room.Id;
        }
        public async Task UpdateAsync(string id,int players)
        {await LobbyService.Instance.UpdateLobbyAsync(id,new UpdateLobbyOptions{Data=new Dictionary<string,DataObject>{{"humans",Public(players.ToString())}}});}
        public Task HeartbeatAsync(string id)=>LobbyService.Instance.SendHeartbeatPingAsync(id);
        public Task DeleteAsync(string id)=>LobbyService.Instance.DeleteLobbyAsync(id);
    }
}
