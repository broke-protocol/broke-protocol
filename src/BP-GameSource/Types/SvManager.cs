
namespace BrokeProtocol.GameSource.Types
{
    public class SvManager
    {
        [Target(typeof(API.Events.Manager), (int)API.Events.Manager.OnStarted)]
        protected void OnStarted(Managers.SvManager svManager)
        {
        }
    }
}
