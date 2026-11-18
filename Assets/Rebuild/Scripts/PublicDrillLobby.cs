using System;
using System.Threading.Tasks;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Public waiting-room discovery; Relay remains responsible for joining
    /// and NGO remains responsible for players, capacity and round authority.</summary>
    public sealed class PublicDrillLobby : MonoBehaviour
    {
        public bool IsPublic { get; private set; } = true;
        public PublicDrillRoom[] Rooms { get; private set; } = Array.Empty<PublicDrillRoom>();
        public string BrowserStatus { get; private set; } = "Looking for upcoming drills...";
        public string HostStatus { get; private set; } = "";
        public bool Refreshing { get; private set; }
        public bool Joining { get; private set; }
        public bool IsListed => !string.IsNullOrEmpty(hostedId);
        public bool CanRefresh => !Refreshing&&!Joining&&Time.unscaledTime>=nextQuery;
        public IPublicDrillDirectory DirectoryOverride;
        public float refreshSeconds = 8, heartbeatSeconds = 15;
        StartMenu menu;
        OnlineMatch online;
        IPublicDrillDirectory cloud;
        string hostedId;
        int epoch, publishedPlayers;
        bool hostBusy;
        float nextQuery, nextHostCheck, nextHeartbeat;
        IPublicDrillDirectory Directory => DirectoryOverride ?? (cloud ??= new UnityPublicDrillDirectory());

        public void Initialize(StartMenu startMenu,OnlineMatch match){menu=startMenu;online=match;}
        bool CanAdvertise => online!=null&&online.IsHost&&online.UsesRelay&&IsPublic&&!online.RoundActive&&
            online.HumanCount<online.Capacity&&(online.RelayConnectionOverride==null||DirectoryOverride!=null);

        void Update()
        {
            if(menu==null||online==null)return;
            if(menu.IsOpen&&menu.Page==StartMenu.MenuPage.Main&&!online.Active&&!online.Busy&&
               online.RelayConfigured&&Time.unscaledTime>=nextQuery&&!Refreshing&&!Joining)_=RefreshAsync();
            if(!hostBusy&&Time.unscaledTime>=nextHostCheck)_=ReconcileHostAsync();
        }

        public void SetPublic(bool value)
        {
            if(IsPublic==value)return;
            IsPublic=value;StopAdvertising();nextHostCheck=0;
            HostStatus=value?"Publishing public room...":"Private room - share the code to invite friends.";
        }

        public async Task RefreshAsync()
        {
            // One in-flight request plus a cooldown prevents repaint/click spam and
            // respects Lobby query limits. Refresh only while on the first screen.
            if(Refreshing||Joining||Time.unscaledTime<nextQuery)return;
            Refreshing=true;nextQuery=Time.unscaledTime+Mathf.Max(3,refreshSeconds);
            try
            {
                var rooms=await Directory.QueryAsync();if(this==null)return;
                Rooms=rooms;BrowserStatus=rooms.Length==0?"No public drills waiting yet.":"";
            }
            catch{if(this!=null){Rooms=Array.Empty<PublicDrillRoom>();BrowserStatus="Could not load drills. Try Refresh.";}}
            finally{if(this!=null)Refreshing=false;}
        }

        async Task ReconcileHostAsync()
        {
            hostBusy=true;nextHostCheck=Time.unscaledTime+1;
            int version=epoch;string code=online.JoinCode;
            try
            {
                if(!CanAdvertise)
                {
                    if(IsListed)StopAdvertising();
                    if(online.IsHost&&online.UsesRelay)
                        HostStatus=!IsPublic?"Private room - share the code to invite friends.":
                            online.RoundActive?"Match in progress.":"Room full - ready to start.";
                    return;
                }
                if(!IsListed)
                {
                    HostStatus="Publishing public room...";
                    int players=online.HumanCount;
                    string id=await Directory.CreateAsync(code,online.Capacity,players);
                    // Leaving, starting, privatizing or rehosting during creation must
                    // delete the late result rather than leave a ghost public room.
                    if(this==null||epoch!=version||!CanAdvertise||online.JoinCode!=code){await DeleteSafely(id);return;}
                    hostedId=id;publishedPlayers=players;nextHeartbeat=0;
                }
                if(publishedPlayers!=online.HumanCount)
                {
                    int players=online.HumanCount;await Directory.UpdateAsync(hostedId,players);
                    if(this==null||version!=epoch)return;publishedPlayers=players;
                }
                if(Time.unscaledTime>=nextHeartbeat)
                {await Directory.HeartbeatAsync(hostedId);if(this==null||version!=epoch)return;nextHeartbeat=Time.unscaledTime+heartbeatSeconds;}
                HostStatus="Public room - listed on the start screen.";
            }
            catch
            {
                if(this!=null&&version==epoch)
                {
                    // A vanished/expired listing can be recreated on retry. The Relay
                    // room keeps working even if discovery is temporarily unavailable.
                    StopAdvertising();nextHostCheck=Time.unscaledTime+10;
                    HostStatus="Could not list room. Share its code instead.";
                }
            }
            finally{if(this!=null)hostBusy=false;}
        }

        public void StopAdvertising()
        {
            epoch++;string id=hostedId;hostedId=null;publishedPlayers=0;
            if(!string.IsNullOrEmpty(id))_=DeleteSafely(id);
        }
        async Task DeleteSafely(string id)
        {
            try{await Directory.DeleteAsync(id);}catch{/* Without heartbeats Lobby expires the abandoned listing. */}
        }

        public async Task JoinAsync(PublicDrillRoom selected)
        {
            if(selected==null||Joining||online.Active||online.Busy)return;
            Joining=true;BrowserStatus="Joining drill...";
            try
            {
                // Recheck visibility, free places and protocol immediately before
                // joining: a query result may have filled/started since it was drawn.
                var room=await Directory.GetAsync(selected.Id);
                if(this==null||!menu.IsOpen||menu.Page!=StartMenu.MenuPage.Main||online.Active||online.Busy)return;
                if(room==null){Rooms=Array.Empty<PublicDrillRoom>();BrowserStatus="That drill is no longer available.";nextQuery=Time.unscaledTime+1;return;}
                menu.OpenOnlineLobby();await online.JoinInternet(room.JoinCode);
            }
            catch{if(this!=null){BrowserStatus="That drill is unavailable. Try another.";nextQuery=Time.unscaledTime+1;}}
            finally
            {
                if(this!=null){Joining=false;if(BrowserStatus=="Joining drill...")BrowserStatus="";}
            }
        }
        void OnDestroy()=>StopAdvertising();
    }
}
