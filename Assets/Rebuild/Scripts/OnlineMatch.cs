using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Host-owned rules, timestamped round starts and presentation snapshots.
    /// Internet sessions use Relay; direct transport remains available to test the same
    /// input, roster and match-state protocol without cloud dependencies.</summary>
    [DefaultExecutionOrder(-30)]
    public sealed class OnlineMatch : MonoBehaviour
    {
        const string StateMessage = "leftright.state.v1", InputMessage = "leftright.input.v1";
        public StartMenu menu;
        public ushort port = 7777;
        public float snapshotInterval = .05f;
        public bool Active { get; private set; }
        public bool Busy { get; private set; }
        public bool UsesRelay { get; private set; }
        public string JoinCode { get; private set; } = "";
        public bool RelayConfigured => !string.IsNullOrWhiteSpace(Application.cloudProjectId);
        public IRelayConnection RelayConnectionOverride { get; set; }
        public bool IsHost => Active && manager != null && manager.IsHost;
        public bool RoundActive { get; private set; }
        public int LocalSlot { get; private set; }
        public int Capacity { get; private set; } = 4;
        public int HumanCount { get; private set; }
        public string Status { get; private set; } = "Host a game or enter your friend's join code.";
        public bool CanStart => IsHost && HumanCount >= 2;
        public double StartTime { get; private set; }
        // NGO's ServerTime is intentionally buffered for interpolation. Music needs the
        // estimated current server time, so remove the local input buffer instead.
        public double Now => manager == null || !manager.IsListening ? 0 : manager.IsServer
            ? manager.ServerTime.Time : manager.LocalTime.Time - manager.NetworkTimeSystem.LocalBufferSec;
        public int RoundNumber { get; private set; }
        public float? TestInput { get; set; }
        public bool TestJumpRequested { get; set; }
        public int ReceivedSnapshots { get; private set; }
        NetworkManager manager;
        GameObject networkRoot, remoteRoot;
        SpikeCourse course;
        HealthCrateSpawner spawner;
        VictoryDirector victory;
        readonly Dictionary<ulong,int> slots = new Dictionary<ulong,int>();
        readonly Dictionary<ulong,float> inputSeen = new Dictionary<ulong,float>();
        readonly Dictionary<ulong,int> inputSequence = new Dictionary<ulong,int>();
        readonly Dictionary<ulong,int> jumpSequence = new Dictionary<ulong,int>();
        readonly Dictionary<int,HealthCratePickup> crates = new Dictionary<int,HealthCratePickup>();
        readonly Dictionary<HealthCratePickup,int> hostCrateIds = new Dictionary<HealthCratePickup,int>();
        int nextCrateId;
        readonly int[] recoveryVersions = new int[4];
        int outgoingInput, cueVersion, remoteCue = -1, remoteRound, stateSerial, lastState;
        int outgoingJump;
        float nextSnapshot, nextInput, connectDeadline;
        double victoryAt;
        bool resultReceived, stopping;
        int connectionAttempt;

        public async void HostInternetFromMenu(int capacity) => await HostInternet(capacity);
        public async void JoinInternetFromMenu(string code) => await JoinInternet(code);

        public Task<bool> HostInternet(int capacity) => ConnectRelay(null,Mathf.Clamp(capacity,2,4));
        public Task<bool> JoinInternet(string code)
        {
            code=(code??"").Trim().ToUpperInvariant();
            if(code.Length<4||code.Length>12||code.Any(c=>!char.IsLetterOrDigit(c)))
            {Status="Enter the join code shared by your friend.";return Task.FromResult(false);}
            return ConnectRelay(code,0);
        }

        async Task<bool> ConnectRelay(string code,int capacity)
        {
            if(Active||Busy)return false;
            if(!RelayConfigured&&RelayConnectionOverride==null)
            {Status="Online setup needed: link this game to Unity Services.";return false;}
            Busy=true;int attempt=++connectionAttempt;
            Status=code==null?"Creating an online game...":"Joining your friend's game...";
            try
            {
                var connection=RelayConnectionOverride??new UnityRelayConnection();
                var request=code==null?connection.HostAsync(capacity-1):connection.JoinAsync(code);
                // Cloud retries must not leave the menu disabled indefinitely. Late
                // responses are ignored; faulted background requests are observed.
                if(await Task.WhenAny(request,ConnectionDeadline(attempt))!=request)
                {
                    _=ObserveLateRequest(request);
                    throw new TimeoutException();
                }
                var ticket=await request;
                // Back/cancel invalidates this attempt. A late cloud response must never
                // start a hidden connection after the player returns to local play.
                if(attempt!=connectionAttempt||this==null)return false;
                if(code==null)Capacity=capacity;
                Setup();UsesRelay=true;JoinCode=ticket.JoinCode;
                ticket.Configure(networkRoot.GetComponent<UnityTransport>());
                if(code==null)
                {
                    manager.ConnectionApprovalCallback=Approve;
                    if(!manager.StartHost())throw new InvalidOperationException("Host start failed.");
                    RegisterHandlers();slots[manager.LocalClientId]=0;HumanCount=1;LocalSlot=0;
                    Status="Share the join code with your friend.";
                }
                else
                {
                    if(!manager.StartClient())throw new InvalidOperationException("Client start failed.");
                    RegisterHandlers();connectDeadline=Time.unscaledTime+20;Status="Connecting to your friend's game...";
                }
                return true;
            }
            catch(Exception error)
            {
                if(attempt!=connectionAttempt||this==null)return false;
                string message=UnityRelayConnection.FriendlyError(error);
                if(Active){Leave(message);menu.OpenOnlineLobby();}else Status=message;
                return false;
            }
            finally {if(attempt==connectionAttempt)Busy=false;}
        }

        async Task ConnectionDeadline(int attempt)
        {
            // Browser builds have no managed timer threads. A Unity frame wait works
            // on every platform and stops as soon as this connection completes/cancels.
            double until=Time.realtimeSinceStartupAsDouble+30;
            while(this!=null&&Busy&&attempt==connectionAttempt&&Time.realtimeSinceStartupAsDouble<until)
                await Awaitable.NextFrameAsync();
        }
        static async Task ObserveLateRequest(Task request)
        {try{await request;}catch{/* The menu has already reported timeout/cancellation. */}}

        void Awake()
        {
            course = menu.course; spawner = GetComponentInChildren<HealthCrateSpawner>(true); victory = GetComponent<VictoryDirector>();
            course.round.NetworkStartRequest = RequestRound;
            course.round.officer.DirectionChanged += OnCue;
            course.Completed += OnCompleted;
        }

        public bool Host(int capacity)
        {
            if (Active||Busy) return false;
            Capacity = Mathf.Clamp(capacity,2,4); Setup();
            manager.ConnectionApprovalCallback = Approve;
            if (!manager.StartHost()) { Leave("Could not start host. Is the port already in use?"); return false; }
            RegisterHandlers(); slots[manager.LocalClientId] = 0; HumanCount = 1; LocalSlot = 0;
            Status = "Hosting on port " + port + ". Waiting for another player.";
            return true;
        }

        public bool Join(string address)
        {
            if (Active || Busy || string.IsNullOrWhiteSpace(address)) return false;
            address=address.Trim();if(address.Equals("localhost",StringComparison.OrdinalIgnoreCase))address="127.0.0.1";
            if(!System.Net.IPAddress.TryParse(address,out _)){Status="Enter the host's IP address.";return false;}
            Setup(); networkRoot.GetComponent<UnityTransport>().SetConnectionData(address.Trim(), port);
            if (!manager.StartClient()) { Leave("Could not connect."); return false; }
            RegisterHandlers(); connectDeadline = Time.unscaledTime + 12;
            Status = "Connecting to " + address.Trim() + "..."; return true;
        }

        void Setup()
        {
            Active = true; RoundActive = false; RoundNumber = remoteRound = stateSerial = lastState = 0;
            UsesRelay=false;JoinCode="";
            ReceivedSnapshots = 0; resultReceived = false; outgoingInput = 0; HumanCount = 0;
            menu.OpenOnlineLobby();
            networkRoot = new GameObject("Online network transport");
            var transport = networkRoot.AddComponent<UnityTransport>();
            transport.DisconnectTimeoutMS = 5000;
            manager = networkRoot.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ConnectionApproval = true, TickRate = 30, ProtocolVersion = 4 };
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
            remoteRoot = new GameObject("Online remote effects");
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = !RoundActive && slots.Count < Capacity;
            response.CreatePlayerObject = false; response.Pending = false;
            response.Reason = RoundActive ? "Match already started." : "Game is full.";
            if(response.Approved && !slots.ContainsKey(request.ClientNetworkId))
                slots[request.ClientNetworkId]=Enumerable.Range(0,Capacity).First(i=>!slots.ContainsValue(i));
        }

        void RegisterHandlers()
        {
            manager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveState);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(InputMessage, ReceiveInput);
        }

        void Connected(ulong client)
        {
            if (!IsHost) return;
            if (!slots.ContainsKey(client))
            {
                int slot = Enumerable.Range(0,Capacity).First(i => !slots.ContainsValue(i));
                slots[client] = slot;
            }
            HumanCount = manager.ConnectedClientsIds.Count;
            Status = HumanCount + " players connected. Host can start.";
        }

        void Disconnected(ulong client)
        {
            if (!Active || stopping) return;
            if (!IsHost || RoundActive)
            {
                string reason=manager!=null?manager.DisconnectReason:null;
                string message = reason != null && reason.IndexOf("already started",StringComparison.OrdinalIgnoreCase)>=0 ? "Match already started."
                    : reason != null && reason.IndexOf("full",StringComparison.OrdinalIgnoreCase)>=0 ? "Game is full."
                    : "Connection lost. Reconnect or host another game.";
                Leave(message);return;
            }
            slots.Remove(client); inputSeen.Remove(client); inputSequence.Remove(client); HumanCount = slots.Count;
            Status = "Player left. Waiting for another player.";
        }
        void TransportFailed() => Leave("Network transport failed.");

        public bool RequestRound()
        {
            if (!Active) return false;
            if (!CanStart) { Status = IsHost ? "Wait for a second player." : "Waiting for the host to start the next round."; return true; }
            RoundNumber++; cueVersion = 0; victoryAt = 0; resultReceived = false;
            hostCrateIds.Clear();nextCrateId=0;
            StartTime = Now + Math.Max(3,course.round.leadInSeconds);
            menu.PrepareOnlineMatch(Capacity,0);
            for (int i = 0; i < course.players.Length; i++)
            {
                var player = course.players[i]; player.NetworkReplica = false;
                player.isLocallyControlled = i == 0; player.computerControlled = !slots.ContainsValue(i);
                player.InputOverride = slots.ContainsValue(i) && i != 0 ? 0 : (float?)null;
            }
            course.NetworkReplica = false; spawner.enabled = true;
            RoundActive = true;
            course.round.BeginScheduled(AudioSettings.dspTime + StartTime - Now, false);
            course.round.masterBeat.NetworkElapsedSeconds = () => Now - StartTime;
            Status = "Online round " + RoundNumber; nextSnapshot = 0;
            return true;
        }

        void OnCue(bool right) { if (IsHost) cueVersion++; }
        void OnCompleted(RecruitController winner)
        {
            if (IsHost) { victoryAt = Now + victory.victoryDelaySeconds; nextSnapshot = 0; }
        }

        void Update()
        {
            if (!Active || manager == null) return;
            if (!manager.IsHost && connectDeadline > 0 && Time.unscaledTime > connectDeadline && ReceivedSnapshots == 0)
            { Leave(UsesRelay?"Connection timed out. Ask your friend for a new code.":"Connection timed out. Check the host address and firewall."); return; }
            if (!RoundActive) return;
            bool jumpPressed=TestJumpRequested||(!IsHost&&course.players[LocalSlot].ReadJumpPressed());
            TestJumpRequested=false;
            if(jumpPressed){if(IsHost)course.players[0].TryJump();else outgoingJump++;}
            if (!IsHost && Time.unscaledTime >= nextInput && manager.IsConnectedClient)
            {
                nextInput = Time.unscaledTime + 1f/30;
                float input = TestInput ?? course.players[LocalSlot].ReadNetworkInput();
                using var writer = new FastBufferWriter(16,Allocator.Temp);
                writer.WriteValueSafe(++outgoingInput); writer.WriteValueSafe(Mathf.Clamp(input,-1,1));
                // Repeat the press counter in subsequent input packets: packet loss
                // cannot lose the button edge or turn a held key into auto-jumping.
                writer.WriteValueSafe(outgoingJump);
                manager.CustomMessagingManager.SendNamedMessage(InputMessage,NetworkManager.ServerClientId,writer,NetworkDelivery.UnreliableSequenced);
            }
            if (IsHost)
            {
                course.players[0].InputOverride=TestInput;
                foreach (var entry in slots)
                    if (entry.Value != 0 && (!inputSeen.TryGetValue(entry.Key,out float seen) || Time.unscaledTime-seen>.5f))
                        course.players[entry.Value].InputOverride = 0;
            }
        }

        void LateUpdate()
        {
            if (!Active || !IsHost || Time.unscaledTime < nextSnapshot) return;
            nextSnapshot = Time.unscaledTime + snapshotInterval;
            var state = Capture();
            foreach (var entry in slots)
            {
                if (entry.Key == manager.LocalClientId) continue;
                if (!manager.ConnectedClients.ContainsKey(entry.Key)) continue;
                state.yourSlot = entry.Value;
                string json = JsonUtility.ToJson(state);
                using var writer = new FastBufferWriter(16384,Allocator.Temp,32768);
                writer.WriteValueSafe(json);
                manager.CustomMessagingManager.SendNamedMessage(StateMessage,entry.Key,writer,NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        void ReceiveInput(ulong sender, FastBufferReader reader)
        {
            // The sender's connection owns a slot. A packet cannot name another recruit,
            // send transforms, choose the cue, heal, or declare itself the winner.
            if (!IsHost || !RoundActive || !slots.TryGetValue(sender,out int slot) || slot == 0 || reader.Length < 12 || reader.Length > 32) return;
            reader.ReadValueSafe(out int sequence); reader.ReadValueSafe(out float input);
            reader.ReadValueSafe(out int jump);
            if (float.IsNaN(input) || float.IsInfinity(input) || (inputSequence.TryGetValue(sender,out int last) && sequence <= last)) return;
            inputSequence[sender] = sequence; inputSeen[sender] = Time.unscaledTime;
            course.players[slot].InputOverride = Mathf.Clamp(input,-1,1);
            if(jump>0&&(!jumpSequence.TryGetValue(sender,out int previousJump)||jump>previousJump))
            {jumpSequence[sender]=jump;course.players[slot].TryJump();}
        }

        OnlineState Capture()
        {
            var state = new OnlineState { serial = ++stateSerial, round = RoundActive ? RoundNumber : 0,
                capacity = Capacity, humans = HumanCount, startsAt = StartTime, cue = cueVersion,
                right = course.round.officer.DirectionRight, over = course.IsGameOver, victoryAt = victoryAt,
                winner = Array.IndexOf(course.players,course.Winner), sequence = Array.IndexOf(victory.sequences,victory.SelectedSequence) };
            state.survivalSeconds=menu.Scores.ElapsedSeconds;state.survivalTimes=menu.Scores.Times;
            state.players = course.players.Select((p,i) => new OnlinePlayerState { x = p.transform.position.x, horizontal = p.Horizontal,
                jumping=p.IsJumping,jumpPhase=p.JumpPhase,
                blasted=p.WasBlasted,
                pinned = p.IsPinned, eliminated = p.IsEliminated, fromLeft = p.PinnedFromLeft,
                travel = course.traps[i].CurrentTravel, target = course.traps[i].TargetTravel,
                pending = course.traps[i].PendingRecoverySteps, recovery = course.traps[i].RecoveryVersion,
                crushed = course.traps[i].HasFinishedSqueeze, returned = course.traps[i].HasReturned }).ToArray();
            var activeCrates=spawner.GetComponentsInChildren<HealthCratePickup>().Where(c=>!c.IsCollected).ToArray();
            foreach(var crate in activeCrates)if(!hostCrateIds.ContainsKey(crate))hostCrateIds[crate]=++nextCrateId;
            foreach(var old in hostCrateIds.Keys.Where(c=>c==null||!activeCrates.Contains(c)).ToArray())hostCrateIds.Remove(old);
            state.crates = activeCrates.Select(c=>new OnlineCrateState { id=hostCrateIds[c],lane=c.LaneIndex,x=c.transform.position.x }).ToArray();
            state.mines=menu.Obstacles.ActiveMines.Where(m=>m!=null&&!m.Detonated).Select(m=>new OnlineMineState
                {id=m.Id,lane=m.LaneIndex,x=m.transform.position.x,arms=m.ArmsAtBeat,expires=m.ExpiresAtBeat}).ToArray();
            return state;
        }

        void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (!Active || manager == null || IsHost || sender != NetworkManager.ServerClientId || reader.Length > 32768) return;
            reader.ReadValueSafe(out string json);
            var state = JsonUtility.FromJson<OnlineState>(json);
            if (state == null || state.serial <= lastState || state.capacity < 2 || state.capacity > 4 || state.yourSlot < 0 || state.yourSlot >= state.capacity) return;
            lastState = state.serial; ReceivedSnapshots++; Capacity=state.capacity; HumanCount=state.humans; LocalSlot=state.yourSlot;
            Status = HumanCount + " players connected. Waiting for host.";
            if (state.round == 0) return;
            if (state.players == null || state.players.Length != Capacity) return;
            if (state.round != remoteRound)
            {
                ClearRemoteCrates(); remoteRound = state.round; RoundNumber = state.round;
                StartTime = state.startsAt; RoundActive = true; remoteCue = -1; resultReceived=false;
                Array.Clear(recoveryVersions,0,4);
                menu.PrepareOnlineMatch(Capacity,LocalSlot);
                course.NetworkReplica = true; spawner.enabled = false;
                foreach(var player in course.players) { player.NetworkReplica=true; player.computerControlled=false; player.InputOverride=null; }
                course.round.BeginScheduled(AudioSettings.dspTime + Math.Max(.05,StartTime-Now),true);
                menu.ArrangeOnlineFront(LocalSlot);
                course.round.masterBeat.NetworkElapsedSeconds = () => Now - StartTime;
            }
            if (state.cue > 0 && state.cue != remoteCue && !state.over)
            { remoteCue=state.cue; course.round.officer.ApplyNetworkCue(state.right); }
            for (int i=0;i<Capacity;i++)
            {
                var value=state.players[i]; var player=course.players[i];var trap=course.traps[i];
                player.ApplyNetworkMovement(value.x,value.horizontal);
                player.ApplyNetworkJump(value.jumping,value.jumpPhase);
                // Set the banks first so pinning uses the same visible geometry as host.
                trap.ApplyNetworkPose(value.travel,value.target,value.pending,value.recovery);
                if(value.blasted&&!player.WasBlasted)player.Explode(menu.Obstacles.BloodMaterial,menu.Obstacles.ExplosionClip);
                if (!value.blasted&&(value.pinned || value.eliminated) && !player.IsPinned && !player.IsEliminated)
                    player.PinTo(value.fromLeft?trap.leftBank:trap.rightBank,value.fromLeft,trap.spikeReach);
                if(value.eliminated && !player.IsEliminated)
                { player.Eliminate();player.SqueezeBetween(trap.leftBank,trap.rightBank,trap.pillarThickness); }
                trap.ApplyNetworkState(value.travel,value.target,value.pending,value.recovery,value.crushed,value.returned,player);
                if(value.recovery>recoveryVersions[i])
                {
                    course.round.audioOutput.PlayHealthPickup();
                    if(spawner.cratePrefab.burstPrefab!=null)Instantiate(spawner.cratePrefab.burstPrefab,
                        new Vector3(player.transform.position.x,.65f,player.transform.position.z),Quaternion.identity,remoteRoot.transform);
                }
                recoveryVersions[i]=value.recovery;
            }
            ApplyCrates(state.crates);
            menu.Obstacles.ApplyNetwork(state.mines);
            menu.Scores.ApplyNetwork(state.survivalSeconds,state.survivalTimes,state.over);
            if(state.over && !resultReceived)
            {
                resultReceived=true; course.ApplyNetworkResult(state.winner);
                victory.BeginNetworkVictory(course.Winner,state.sequence,(float)(state.victoryAt-Now));
            }
        }

        void ApplyCrates(OnlineCrateState[] values)
        {
            var present = new HashSet<int>();
            foreach(var value in values ?? Array.Empty<OnlineCrateState>())
            {
                if(value.lane<0||value.lane>=course.players.Length)continue;present.Add(value.id);
                if(!crates.TryGetValue(value.id,out var crate))
                {
                    crate=Instantiate(spawner.cratePrefab,remoteRoot.transform);crate.enabled=false;crates[value.id]=crate;
                }
                crate.transform.position=new Vector3(value.x,.65f,course.players[value.lane].transform.position.z);
            }
            foreach(int id in crates.Keys.Where(id=>!present.Contains(id)).ToArray())
            { if(crates[id]!=null)Destroy(crates[id].gameObject);crates.Remove(id); }
        }

        void ClearRemoteCrates()
        {
            foreach(var crate in crates.Values)if(crate!=null)Destroy(crate.gameObject);crates.Clear();
            if(remoteRoot!=null)foreach(Transform child in remoteRoot.transform)Destroy(child.gameObject);
        }

        public void Leave(string reason = "Disconnected.")
        {
            if(stopping)return; stopping=true; Active=false;RoundActive=false;
            connectionAttempt++;Busy=false;UsesRelay=false;JoinCode="";
            course.NetworkReplica=false;course.round.masterBeat.NetworkElapsedSeconds=null;
            foreach(var player in GetComponentsInChildren<RecruitController>(true))
            {player.NetworkReplica=false;player.InputOverride=null;}
            ClearRemoteCrates(); if(remoteRoot!=null)Destroy(remoteRoot);
            if(manager!=null)
            {
                manager.OnClientConnectedCallback-=Connected;manager.OnClientDisconnectCallback-=Disconnected;manager.OnTransportFailure-=TransportFailed;
                manager.Shutdown();Destroy(networkRoot);
            }
            manager=null;slots.Clear();inputSeen.Clear();inputSequence.Clear();HumanCount=0;
            jumpSequence.Clear();outgoingJump=0;
            TestJumpRequested=false;
            spawner.enabled=true;Status=reason;menu.RestoreLaneOrder();menu.Open();stopping=false;
        }

        void OnDestroy()
        {
            connectionAttempt++;
            if(manager!=null){manager.OnClientDisconnectCallback-=Disconnected;manager.Shutdown();Destroy(networkRoot);}
            if(course!=null){course.Completed-=OnCompleted;course.round.officer.DirectionChanged-=OnCue;course.round.NetworkStartRequest=null;course.round.masterBeat.NetworkElapsedSeconds=null;}
            if(remoteRoot!=null)Destroy(remoteRoot);
        }
    }

    [Serializable] public sealed class OnlineState
    {
        public int serial,round,capacity,humans,yourSlot,cue,winner,sequence;
        public double startsAt,victoryAt;
        public float survivalSeconds;
        public float[] survivalTimes;
        public bool right,over;
        public OnlinePlayerState[] players;
        public OnlineCrateState[] crates;
        public OnlineMineState[] mines;
    }
    [Serializable] public sealed class OnlinePlayerState
    {
        public float x,horizontal,travel,target;
        public float jumpPhase;
        public bool jumping;
        public bool blasted;
        public int pending,recovery;
        public bool pinned,eliminated,fromLeft,crushed,returned;
    }
    [Serializable] public sealed class OnlineCrateState { public int id,lane; public float x; }
    [Serializable] public sealed class OnlineMineState { public int id,lane;public float x;public double arms,expires; }
}
