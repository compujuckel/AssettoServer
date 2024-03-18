using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssettoServer.Network.Tcp;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Network.Packets;
using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Server;

public class VoteManager
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    private readonly SessionManager _sessionManager;

    private VoteState? _state;
    
    public VoteManager(ACServerConfiguration configuration, EntryCarManager entryCarManager, SessionManager sessionManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _sessionManager = sessionManager;

        _entryCarManager.ClientDisconnected += OnClientDisconnected;
    }
    

    private void OnClientDisconnected(ACTcpClient sender, EventArgs args) => _state?.Votes.Remove(sender.SessionId);
    
    public async Task SetVote(byte sessionId, VoteType voteType, bool voteValue, byte target = 0)
    {
        Task? vote = null;
        if (_state == null)
        {
            vote = StartVote(voteType, target);
        }

        if (voteType == _state?.Type)
        {
            _state.Votes.Add(sessionId, voteValue);
            _state.LastVoter = sessionId;
            _state.LastVote = voteValue;

            var voteTime = (_state.End - DateTime.Now.AddMilliseconds(20)).TotalMilliseconds;
            
            _entryCarManager.BroadcastPacket(new VoteResponse
            {
                Protocol = (byte)_state.Type,
                Target  = _state.Target,
                Quorum = GetQuorum(_state.Type),
                VoteCount = (byte)_state.Votes.Count(v => v.Value),
                Time = (uint)voteTime,
                LastVoter = _state.LastVoter,
                LastVote = _state.LastVote
            });
        }
        if (vote != null)
            await vote;
    }

    private async Task StartVote(VoteType voteType, byte target)
    {
        _state = new VoteState
        {
            Type = voteType,
            Target = target,
            End = DateTime.Now.AddSeconds(_configuration.Server.VoteDuration)
        };
        
        await Task.Delay(_configuration.Server.VoteDuration * 1000);
        
        if (_state.Votes.Count >= GetQuorum(_state.Type))
        {
            switch (_state.Type)
            {
                case VoteType.KickPlayer:
                    var client = _entryCarManager.ConnectedCars[_state.Target].Client;
                    if (client == null) return;
                    if (client.IsAdministrator) return; 
                    await _entryCarManager.KickAsync(client, KickReason.VoteKicked, "Kicked through vote",
                            "You have been kicked through vote",
                            $"{client.Name} has been kicked from the server through vote.");
                    break;
                case VoteType.NextSession:
                    _sessionManager.NextSession();
                    break;
                case VoteType.RestartSession:
                    _sessionManager.RestartSession();
                    break;
            }
        }
        else
        {
            SendVoteQuorumNotReached();
        }

        _state = null;
    }

    private void SendVoteQuorumNotReached()
    {
        _entryCarManager.BroadcastPacket(new VoteQuorumNotReached());
    }

    private byte GetQuorum(VoteType voteType)
    {
        return voteType switch
        {
            VoteType.KickPlayer => (byte)Math.Ceiling(_configuration.Server.KickQuorum / 100f * (_entryCarManager.ConnectedCars.Count - 1)),
            _ => (byte)Math.Ceiling(_configuration.Server.VotingQuorum / 100f * _entryCarManager.ConnectedCars.Count),
        };
    }
}

public enum VoteType : byte
{
    NextSession = 0x64,
    RestartSession = 0x65,
    KickPlayer = 0x66
}

public class VoteState
{
    public byte LastVoter { get; set; }
    public bool LastVote { get; set; }
    public Dictionary<byte, bool> Votes { get; set; } = [];
    public VoteType Type { get; set; }
    public byte Target { get; set; }
    public DateTime End { get; set; }
}
