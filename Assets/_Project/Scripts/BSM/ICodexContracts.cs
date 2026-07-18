using Desk42.Core;

namespace Desk42.BSM
{
    public interface ITellVisualIndicator
    {
        void ShowTell(TellDefinition tell);
    }

    public interface IClientFidgetDriver
    {
        void SetState(ClientStateID state);
        void OnTellReceived(TellDefinition tell);
    }

    public interface IDeskItemReactor
    {
        void OnClientStateChanged(ClientStateID prev, ClientStateID newState);
    }
}
