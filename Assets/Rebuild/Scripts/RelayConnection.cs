using System;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Cloud allocation/authentication boundary. The match protocol and rules
    /// are identical over Relay and direct transport. Allocation keys stay in memory;
    /// only the friend-facing join code is shown in the menu.</summary>
    public interface IRelayConnection
    {
        Task<RelayTransportTicket> HostAsync(int guestCapacity);
        Task<RelayTransportTicket> JoinAsync(string code);
    }

    public sealed class RelayTransportTicket
    {
        public string JoinCode { get; }
        readonly Action<UnityTransport> configure;
        public RelayTransportTicket(string code, Action<UnityTransport> configure)
        { JoinCode=code;this.configure=configure; }
        public void Configure(UnityTransport transport) => configure(transport);
    }

    public sealed class UnityRelayConnection : IRelayConnection
    {
#if UNITY_WEBGL
        // The SDK/transport also restrict the Editor to WebSockets when WebGL is
        // the active target. Native standalone builds continue to use DTLS.
        const string ConnectionType="wss";
#else
        const string ConnectionType="dtls";
#endif
        static Task initialize;
        static async Task Initialize()
        {
            if(string.IsNullOrWhiteSpace(Application.cloudProjectId))
                throw new InvalidOperationException("Unity Cloud project is not linked.");
            if(UnityServices.State!=ServicesInitializationState.Initialized)
            {
                // Separate anonymous profiles let two copies on the same PC test Relay
                // without sharing an authentication identity. No player account UI yet.
                var options=new InitializationOptions().SetProfile("lr"+Guid.NewGuid().ToString("N").Substring(0,20));
                await UnityServices.InitializeAsync(options);
            }
            if(!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        public static async Task Ready()
        {
            if(initialize==null||initialize.IsFaulted||initialize.IsCanceled)initialize=Initialize();
            await initialize;
        }
        public async Task<RelayTransportTicket> HostAsync(int guestCapacity)
        {
            await Ready();
            var allocation=await RelayService.Instance.CreateAllocationAsync(guestCapacity);
            string code=await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            var data=AllocationUtils.ToRelayServerData(allocation,ConnectionType);
            return new RelayTransportTicket(code,transport=>{
                transport.UseWebSockets=ConnectionType=="wss";
                transport.SetRelayServerData(data);
            });
        }
        public async Task<RelayTransportTicket> JoinAsync(string code)
        {
            await Ready();
            var allocation=await RelayService.Instance.JoinAllocationAsync(code);
            var data=AllocationUtils.ToRelayServerData(allocation,ConnectionType);
            return new RelayTransportTicket(code,transport=>{
                transport.UseWebSockets=ConnectionType=="wss";
                transport.SetRelayServerData(data);
            });
        }
        public static string FriendlyError(Exception error)
        {
            if(error is TimeoutException)return "Online services took too long. Please try again.";
            if(error is RelayServiceException relay)
            {
                switch(relay.Reason)
                {
                    case RelayExceptionReason.JoinCodeNotFound:
                    case RelayExceptionReason.AllocationNotFound:
                    case RelayExceptionReason.EntityNotFound:return "Code not found. Ask your friend for a fresh code.";
                    case RelayExceptionReason.InactiveProject:
                    case RelayExceptionReason.Forbidden:return "Online setup needed: enable Relay for this Unity project.";
                    case RelayExceptionReason.PaymentRequired:return "Unity's free service allowance is unavailable for this project.";
                    case RelayExceptionReason.RateLimited:return "Too many attempts. Please try again shortly.";
                }
            }
            return "Could not reach online services. Check your connection and try again.";
        }
    }

}
